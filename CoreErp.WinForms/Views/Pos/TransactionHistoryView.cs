using CoreErp.WinForms.Theme;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;

namespace CoreErp.WinForms.Views.POS;

public class TransactionHistoryView : UserControl
{
    private const int DefaultCompanyId = 1;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5067/") };

    private TextBox _txtSearch = null!;
    private ComboBox _cbPayment = null!;
    private ComboBox _cbDateRange = null!;
    private FlowLayoutPanel _list = null!;
    private Label _lblStatus = null!;
    private Label _lblResultCount = null!;

    private StatStrip _statStrip = null!;

    private List<TransactionRow> _allTransactions = new();
    private readonly List<TransactionItem> _items = new();
    private Label? _emptyLabel;

    public TransactionHistoryView()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;
        BuildUI();
        _ = LoadTransactionsWithRetryAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // UI
    // ═══════════════════════════════════════════════════════════
    private void BuildUI()
    {
        // ── Header ──
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = AppTheme.AppBackground
        };
        pnlHeader.Controls.Add(new Label
        {
            Text = "Transaction History",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        });
        pnlHeader.Controls.Add(new Label
        {
            Text = "View and filter completed sales transactions",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(2, 34),
            AutoSize = true
        });

        // ── Layout ──
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.AppBackground
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));   // strip
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));    // filters
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // list

        // ── Stat strip (single card, divided columns) ──
        _statStrip = new StatStrip(
            "Sales Overview",
            "Today's Transactions",
            "Today's Revenue",
            "Total Transactions",
            "Total Revenue");

        var pnlCards = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.AppBackground,
            Padding = new Padding(0, 8, 0, 8)
        };
        _statStrip.Dock = DockStyle.Fill;
        pnlCards.Controls.Add(_statStrip);

        // ── Filters ──
        var pnlFilters = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.AppBackground
        };

        _txtSearch = new TextBox
        {
            Location = new Point(0, 12),
            Width = 320,
            Height = 34,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "🔍  Search by invoice # or cashier..."
        };
        _txtSearch.TextChanged += (s, e) => RebuildList();
        _txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RebuildList(); }
        };

        _cbPayment = new ComboBox
        {
            Location = new Point(330, 12),
            Width = 140,
            Height = 34,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextPrimary,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cbPayment.Items.AddRange(new object[] { "All Payments", "Cash", "GCash" });
        _cbPayment.SelectedIndex = 0;
        _cbPayment.SelectedIndexChanged += (s, e) => RebuildList();

        _cbDateRange = new ComboBox
        {
            Location = new Point(480, 12),
            Width = 140,
            Height = 34,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextPrimary,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cbDateRange.Items.AddRange(new object[] { "All Time", "Today", "This Week", "This Month", "This Year" });
        _cbDateRange.SelectedIndex = 0;
        _cbDateRange.SelectedIndexChanged += (s, e) => RebuildList();

        var btnReset = new Button
        {
            Text = "Reset",
            Location = new Point(630, 11),
            Size = new Size(90, 36),
            BackColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontButton,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnReset.FlatAppearance.BorderColor = AppTheme.Border;
        btnReset.Click += (s, e) =>
        {
            _txtSearch.Clear();
            _cbPayment.SelectedIndex = 0;
            _cbDateRange.SelectedIndex = 0;
            RebuildList();
        };

        var btnRefresh = new Button
        {
            Text = "↻  Refresh",
            Location = new Point(728, 11),
            Size = new Size(110, 36),
            BackColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.FontButton,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnRefresh.FlatAppearance.BorderColor = AppTheme.Border;
        btnRefresh.Click += async (s, e) => await LoadTransactionsWithRetryAsync();

        _lblResultCount = new Label
        {
            Text = "0 results",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight
        };
        pnlFilters.Controls.AddRange(new Control[]
        {
            _txtSearch, _cbPayment, _cbDateRange, btnReset, btnRefresh, _lblResultCount
        });
        pnlFilters.Resize += (s, e) => _lblResultCount.Location = new Point(pnlFilters.Width - 90, 22);

        // ── List card ──
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(1)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        _list = new BufferedFlow
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = AppTheme.CardBackground
        };
        _list.ClientSizeChanged += (s, e) => FitWidths();

        card.Controls.Add(_list);
        card.Controls.Add(new GridHeader { Dock = DockStyle.Top });

        // ── Status ──
        _lblStatus = new Label
        {
            Text = "Ready.",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Dock = DockStyle.Bottom,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(28, 0, 0, 0)
        };

        // Assemble
        layout.Controls.Add(pnlCards, 0, 0);
        layout.Controls.Add(pnlFilters, 0, 1);
        layout.Controls.Add(card, 0, 2);

        Controls.Add(layout);
        Controls.Add(_lblStatus);
        Controls.Add(pnlHeader);
    }

    private static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void SetStatus(string msg, bool error = false)
    {
        if (_lblStatus == null) return;
        _lblStatus.Text = msg;
        _lblStatus.ForeColor = error ? AppTheme.Danger : AppTheme.TextMuted;
    }

    // ═══════════════════════════════════════════════════════════
    // List building
    // ═══════════════════════════════════════════════════════════
    private void RebuildList()
    {
        var term = _txtSearch.Text.Trim().ToLower();
        var payment = _cbPayment.SelectedItem?.ToString() ?? "All Payments";
        var range = _cbDateRange.SelectedItem?.ToString() ?? "All Time";

        var query = _allTransactions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(t =>
                t.InvoiceNumber.ToLower().Contains(term) ||
                t.CashierName.ToLower().Contains(term));
        }

        if (payment != "All Payments")
            query = query.Where(t => t.PaymentMethod == payment);

        var now = DateTime.UtcNow;
        query = range switch
        {
            "Today" => query.Where(t => t.SaleDate.Date == now.Date),
            "This Week" => query.Where(t => t.SaleDate >= now.AddDays(-7)),
            "This Month" => query.Where(t => t.SaleDate >= now.AddDays(-30)),
            "This Year" => query.Where(t => t.SaleDate >= now.AddDays(-365)),
            _ => query
        };

        var rows = query.OrderByDescending(t => t.SaleDate).ToList();

        _list.SuspendLayout();

        foreach (var old in _items)
        {
            _list.Controls.Remove(old);
            old.Dispose();
        }
        _items.Clear();

        if (_emptyLabel != null)
        {
            _list.Controls.Remove(_emptyLabel);
            _emptyLabel.Dispose();
            _emptyLabel = null;
        }

        foreach (var row in rows)
        {
            var item = new TransactionItem(row);
            item.ToggleRequested += (s, e) => ToggleItem(item);
            _items.Add(item);
            _list.Controls.Add(item);
        }

        if (rows.Count == 0)
        {
            _emptyLabel = new Label
            {
                Text = _allTransactions.Count == 0
                    ? "No transactions yet."
                    : "No transactions match your filters.",
                ForeColor = AppTheme.TextMuted,
                Font = AppTheme.FontBody,
                Height = 90,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty
            };
            _list.Controls.Add(_emptyLabel);
        }

        FitWidths();
        _list.ResumeLayout(true);
        _lblResultCount.Text = $"{rows.Count} result(s)";
    }

    private bool _fitting;
    private void FitWidths()
    {
        if (_fitting || _list == null) return;
        _fitting = true;
        try
        {
            int w = _list.ClientSize.Width;
            foreach (Control c in _list.Controls)
                if (c.Width != w) c.Width = w;
        }
        finally { _fitting = false; }
    }

    private void ToggleItem(TransactionItem item)
    {
        bool open = !item.Expanded;
        foreach (var other in _items)
            if (other != item) other.SetExpanded(false);

        item.SetExpanded(open);
        if (open) _list.ScrollControlIntoView(item);
    }

    // ═══════════════════════════════════════════════════════════
    // API
    // ═══════════════════════════════════════════════════════════
    private async Task LoadTransactionsWithRetryAsync()
    {
        const int maxAttempts = 10;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                SetStatus($"Loading transactions... ({attempt}/{maxAttempts})");
                var list = await _http.GetFromJsonAsync<List<TransactionRow>>(
                    $"tenant/{DefaultCompanyId}/sales") ?? new();

                _allTransactions = list;
                RebuildList();
                UpdateSummaryCards();
                SetStatus($"Loaded {list.Count} transaction(s).");
                return;
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                SetStatus($"API not ready, retrying... ({attempt}/{maxAttempts})");
                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load: {ex.Message}", true);
                return;
            }
        }
        SetStatus("API not reachable after 10 attempts.", true);
    }

    private void UpdateSummaryCards()
    {
        var today = DateTime.UtcNow.Date;
        var todayTx = _allTransactions.Where(t => t.SaleDate.Date == today).ToList();

        _statStrip.SetValues(
            todayTx.Count.ToString("N0"),
            $"₱{todayTx.Sum(t => t.TotalAmount):N2}",
            _allTransactions.Count.ToString("N0"),
            $"₱{_allTransactions.Sum(t => t.TotalAmount):N2}");
    }

    // ═══════════════════════════════════════════════════════════
    // Flow panel with double buffering
    // ═══════════════════════════════════════════════════════════
    private sealed class BufferedFlow : FlowLayoutPanel
    {
        public BufferedFlow() { DoubleBuffered = true; }
    }

    // ═══════════════════════════════════════════════════════════
    // Column header strip
    // ═══════════════════════════════════════════════════════════
    private sealed class GridHeader : Control
    {
        private static readonly string[] Titles =
        {
            "Invoice #", "Date", "Cashier", "Items", "Subtotal", "Discount", "Total", "Payment", "Status"
        };
        private static readonly Font TitleFont = new("Segoe UI Semibold", 8.5f);

        public GridHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 42;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(AppTheme.AppBackground);

            var cols = SummaryBar.ColumnRects(Width, Height);
            for (int i = 0; i < Titles.Length; i++)
            {
                bool right = SummaryBar.IsRightAligned(i);
                TextRenderer.DrawText(g, Titles[i], TitleFont, SummaryBar.TextRect(cols[i], right),
                    AppTheme.TextSecondary, SummaryBar.Flags(right));
            }

            // Actions column header
            TextRenderer.DrawText(g, "Actions", TitleFont,
                new Rectangle(Width - SummaryBar.ActionsWidth, 0, SummaryBar.ActionsWidth - 24, Height),
                AppTheme.TextSecondary, SummaryBar.Flags(true));

            using var pen = new Pen(AppTheme.Border, 1);
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Summary bar — one row
    // ═══════════════════════════════════════════════════════════
    private sealed class SummaryBar : Control
    {
        public const int ActionsWidth = 150;

        // Invoice, Date, Cashier, Items, Subtotal, Discount, Total, Payment, Status
        private static readonly float[] Weights = { 15, 15, 13, 6, 11, 11, 12, 9, 9 };
        private static readonly Font CellFont = AppTheme.FontBody;
        private static readonly Font MonoCellFont = AppTheme.FontMono;
        private static readonly Font TotalFont = new("Segoe UI Semibold", 9.5f);
        private static readonly Font PillFont = new("Segoe UI Semibold", 8.5f);
        private static readonly Font ActionFont = new("Segoe UI Semibold", 9.5f);
        private static readonly Font ActionHoverFont = new("Segoe UI Semibold", 9.5f, FontStyle.Underline);

        private readonly TransactionRow _data;
        private bool _rowHover;
        private bool _actionHover;
        private bool _expanded;

        public event EventHandler? ToggleClicked;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Expanded
        {
            get => _expanded;
            set { _expanded = value; Invalidate(); }
        }

        public SummaryBar(TransactionRow data)
        {
            _data = data;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public static Rectangle[] ColumnRects(int width, int height)
        {
            const int left = 24;
            int usable = Math.Max(120, width - left - ActionsWidth);
            float total = Weights.Sum();
            var rects = new Rectangle[Weights.Length];
            int x = left;
            for (int i = 0; i < Weights.Length; i++)
            {
                int w = (int)(usable * Weights[i] / total);
                rects[i] = new Rectangle(x, 0, w, height);
                x += w;
            }
            return rects;
        }

        public static bool IsRightAligned(int col) => col is 3 or 4 or 5 or 6;
        public static Rectangle TextRect(Rectangle col, bool right)
            => new(col.X, col.Y, col.Width - (right ? 20 : 8), col.Height);

        public static TextFormatFlags Flags(bool right)
            => TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
               TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
               (right ? TextFormatFlags.Right : TextFormatFlags.Left);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bg = _expanded ? AppTheme.SidebarActive
                   : _rowHover ? Color.FromArgb(250, 250, 249)
                   : AppTheme.CardBackground;
            g.Clear(bg);

            if (_expanded)
            {
                using var bar = new SolidBrush(AppTheme.Accent);
                g.FillRectangle(bar, 0, 0, 3, Height);
            }

            var cols = ColumnRects(Width, Height);

            TextRenderer.DrawText(g, _data.InvoiceNumber, MonoCellFont,
                TextRect(cols[0], false), AppTheme.TextPrimary, Flags(false));

            TextRenderer.DrawText(g, _data.SaleDate.ToString("MMM dd, HH:mm"),
                CellFont, TextRect(cols[1], false), AppTheme.TextSecondary, Flags(false));

            TextRenderer.DrawText(g, _data.CashierName,
                CellFont, TextRect(cols[2], false), AppTheme.TextPrimary, Flags(false));

            TextRenderer.DrawText(g, _data.ItemCount.ToString(),
                CellFont, TextRect(cols[3], true), AppTheme.TextSecondary, Flags(true));

            TextRenderer.DrawText(g, $"₱{_data.Subtotal:N2}",
                CellFont, TextRect(cols[4], true), AppTheme.TextSecondary, Flags(true));

            TextRenderer.DrawText(g,
                _data.DiscountAmount > 0 ? $"-₱{_data.DiscountAmount:N2}" : "₱0.00",
                CellFont, TextRect(cols[5], true),
                _data.DiscountAmount > 0 ? AppTheme.Danger : AppTheme.TextMuted, Flags(true));

            TextRenderer.DrawText(g, $"₱{_data.TotalAmount:N2}",
                TotalFont, TextRect(cols[6], true), AppTheme.TextPrimary, Flags(true));

            // Payment pill
            DrawPill(g, cols[7], _data.PaymentMethod,
                _data.PaymentMethod == "Cash" ? AppTheme.Success : AppTheme.Primary,
                _data.PaymentMethod == "Cash" ? Color.FromArgb(220, 245, 235) : Color.FromArgb(230, 240, 255));

            // Status pill
            DrawPill(g, cols[8], "Completed", AppTheme.TextSecondary, Color.FromArgb(244, 244, 245));

            // Actions
            var actionText = _expanded ? "Close" : "View Details";
            var size = TextRenderer.MeasureText(actionText, ActionFont, Size.Empty, TextFormatFlags.NoPadding);
            int ax = Width - 24 - size.Width;
            int ay = (Height - 30) / 2;
            var actionRect = new Rectangle(ax - 4, ay, size.Width + 8, 30);

            TextRenderer.DrawText(g, actionText, _actionHover ? ActionHoverFont : ActionFont,
                actionRect, AppTheme.Primary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static void DrawPill(Graphics g, Rectangle col, string text, Color fg, Color fill)
        {
            var size = TextRenderer.MeasureText(text, PillFont, Size.Empty, TextFormatFlags.NoPadding);
            var pill = new Rectangle(col.X, (col.Height - 24) / 2,
                Math.Min(size.Width + 22, col.Width - 8), 24);

            using var path = RoundedRect(pill, 12);
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);

            TextRenderer.DrawText(g, text, PillFont, pill, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Mouse ──
        private bool ActionHitTest(Point p)
        {
            var actionText = _expanded ? "Close" : "View Details";
            var size = TextRenderer.MeasureText(actionText, ActionFont, Size.Empty, TextFormatFlags.NoPadding);
            int ax = Width - 24 - size.Width;
            int ay = (Height - 30) / 2;
            var rect = new Rectangle(ax - 4, ay, size.Width + 8, 30);
            return rect.Contains(p);
        }

        protected override void OnMouseEnter(EventArgs e) { _rowHover = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e)
        {
            _rowHover = false;
            _actionHover = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hov = ActionHitTest(e.Location);
            if (hov != _actionHover)
            {
                _actionHover = hov;
                Cursor = hov ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (ActionHitTest(e.Location)) ToggleClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Row = summary + expandable details
    // ═══════════════════════════════════════════════════════════
    private sealed class TransactionItem : Panel
    {
        private const int SummaryHeight = 56;

        private readonly SummaryBar _summary;
        private readonly SaleDetailsPanel _details;

        public event EventHandler? ToggleRequested;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TransactionRow Data { get; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Expanded { get; private set; }

        public TransactionItem(TransactionRow data)
        {
            Data = data;
            Margin = Padding.Empty;
            Padding = new Padding(0, 0, 0, 1);
            Height = SummaryHeight + 1;
            BackColor = AppTheme.CardBackground;
            DoubleBuffered = true;

            _details = new SaleDetailsPanel(data)
            {
                Dock = DockStyle.Bottom,
                Visible = false,
                Height = 0
            };

            _summary = new SummaryBar(data) { Dock = DockStyle.Top, Height = SummaryHeight };
            _summary.ToggleClicked += (s, e) => ToggleRequested?.Invoke(this, EventArgs.Empty);

            Controls.Add(_details);
            Controls.Add(_summary);

            Paint += (s, e) =>
            {
                using var pen = new Pen(AppTheme.BorderLight, 1);
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            };
        }

        public void SetExpanded(bool expanded)
        {
            if (Expanded == expanded) return;
            Expanded = expanded;

            SuspendLayout();
            _summary.Expanded = expanded;
            _details.Visible = expanded;

            if (expanded)
            {
                _details.Height = _details.PreferredHeight;
                Height = SummaryHeight + 1 + _details.PreferredHeight;
            }
            else
            {
                Height = SummaryHeight + 1;
            }
            ResumeLayout(true);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Details panel — one aligned 4-column grid (label above value)
    // ═══════════════════════════════════════════════════════════
    private sealed class SaleDetailsPanel : Control
    {
        private const int Columns = 4;
        private const int RowHeight = 54;
        private const int SidePadding = 28;
        private const int VerticalPadding = 12;

        private static readonly Font LabelFont = new("Segoe UI", 8.5f);
        private static readonly Font ValueFont = new("Segoe UI Semibold", 10.5f);

        private readonly record struct Cell(string Label, string Value, Color Color);

        private readonly List<Cell?[]> _rows = new();

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int PreferredHeight { get; }

        public SaleDetailsPanel(TransactionRow d)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(251, 251, 250);

            var normal = AppTheme.TextPrimary;
            string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!;

            // Row 1 — payment + total
            _rows.Add(new Cell?[]
            {
                new Cell("Payment Method", d.PaymentMethod, normal),
                new Cell("Reference No.", Dash(d.PaymentReference), normal),
                new Cell("Items Sold", d.ItemCount.ToString(), normal),
                new Cell("Total", $"₱{d.TotalAmount:N2}", AppTheme.Success)
            });

            // Row 2 — VAT breakdown
            _rows.Add(new Cell?[]
            {
                new Cell("Subtotal (VAT incl.)", $"₱{d.Subtotal:N2}", normal),
                new Cell("VATable Sales", $"₱{d.VatableSales:N2}", normal),
                new Cell("VAT (12%)", $"₱{d.VatAmount:N2}", normal),
                new Cell("VAT-Exempt Sales", $"₱{d.VatExemptSales:N2}", normal)
            });

            // Row 3 — cash handling (+ discount when there is one)
            _rows.Add(new Cell?[]
            {
                new Cell("Amount Paid", $"₱{d.AmountPaid:N2}", normal),
                new Cell("Change", $"₱{d.ChangeDue:N2}", normal),
                d.DiscountAmount > 0
                    ? new Cell($"{d.DiscountType} Discount", $"-₱{d.DiscountAmount:N2}", AppTheme.Danger)
                    : null,
                null
            });

            // Row 4 — discount customer details (only when applicable)
            if (d.DiscountAmount > 0)
            {
                _rows.Add(new Cell?[]
                {
                    new Cell("Customer Name", Dash(d.CustomerName), normal),
                    new Cell("ID Number", Dash(d.CustomerIdNumber), normal),
                    null,
                    null
                });
            }

            PreferredHeight = _rows.Count * RowHeight + VerticalPadding * 2;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            int colWidth = Math.Max(1, (Width - SidePadding * 2) / Columns);
            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                          TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                                          TextFormatFlags.NoPadding;

            for (int r = 0; r < _rows.Count; r++)
            {
                int y = VerticalPadding + r * RowHeight;

                for (int c = 0; c < Columns; c++)
                {
                    if (_rows[r][c] is not { } cell) continue;

                    int x = SidePadding + c * colWidth;
                    int w = colWidth - 16;

                    TextRenderer.DrawText(g, cell.Label, LabelFont, new Rectangle(x, y, w, 18),
                        AppTheme.TextMuted, flags);
                    TextRenderer.DrawText(g, cell.Value, ValueFont, new Rectangle(x, y + 20, w, 24),
                        cell.Color, flags);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Stat strip — one card, title on top, metrics in divided columns
    // ═══════════════════════════════════════════════════════════
    private sealed class StatStrip : Control
    {
        private static readonly Font TitleFont = new("Segoe UI", 12f);
        private static readonly Font LabelFont = new("Segoe UI", 9f);
        private static readonly Font ValueFont = new("Segoe UI Semibold", 18f); // Slightly adjusted down to prevent vertical clipping

        private const TextFormatFlags Flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                             TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                                             TextFormatFlags.NoPadding;

        private readonly string _title;
        private readonly string[] _labels;
        private readonly string[] _values;

        public StatStrip(string title, params string[] labels)
        {
            _title = title;
            _labels = labels;
            _values = labels.Select(_ => "0").ToArray();

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetValues(params string[] values)
        {
            for (int i = 0; i < Math.Min(values.Length, _values.Length); i++)
                _values[i] = values[i];
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(AppTheme.CardBackground);

            using (var border = new Pen(AppTheme.Border, 1))
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            TextRenderer.DrawText(g, _title, TitleFont, new Rectangle(24, 10, Width - 48, 24),
                AppTheme.TextPrimary, Flags);

            const int top = 44; // Adjusted starting Y coordinate for columns
            int count = _labels.Length;
            int colWidth = (Width - 48) / count;

            using var divider = new Pen(AppTheme.Border, 1);

            for (int i = 0; i < count; i++)
            {
                int x = 24 + i * colWidth;
                int textX = i == 0 ? x : x + 20;
                int textW = colWidth - (i == 0 ? 12 : 32);

                if (i > 0)
                    g.DrawLine(divider, x, top + 2, x, Height - 14);

                TextRenderer.DrawText(g, _labels[i], LabelFont, new Rectangle(textX, top, textW, 18),
                    AppTheme.TextSecondary, Flags);
                TextRenderer.DrawText(g, _values[i], ValueFont, new Rectangle(textX, top + 18, textW, 40),
                    AppTheme.TextPrimary, Flags);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // DTO
    // ═══════════════════════════════════════════════════════════
    public class TransactionRow
    {
        public int SaleId { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public string CashierName { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
        public string DiscountType { get; set; } = "";

        public string? CustomerName { get; set; }
        public string? CustomerIdNumber { get; set; }
        public string? PaymentReference { get; set; }

        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal VatableSales { get; set; }
        public decimal VatAmount { get; set; }
        public decimal VatExemptSales { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal ChangeDue { get; set; }
        public DateTime SaleDate { get; set; }
        public int ItemCount { get; set; }
    }
}