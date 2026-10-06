using CoreErp.WinForms.Theme;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;
using System.Text.Json;

namespace CoreErp.WinForms.Views.Inventory;

public class InventoryListView : UserControl
{
    // TODO: will come from login later
    private const int DefaultCompanyId = 1;

    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5067/") };

    private TextBox _txtSearch = null!;
    private CheckBox _chkArchived = null!;
    private Label _lblStatus = null!;
    private Label _lblCount = null!;
    private FlowLayoutPanel _list = null!;
    private Panel _newPanel = null!;
    private ProductEditor _newEditor = null!;
    private Label? _emptyLabel;

    private List<ProductRow> _allProducts = new();
    private readonly List<ProductItem> _items = new();

    public InventoryListView()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;
        BuildUI();
        _ = LoadProductsAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // UI
    // ═══════════════════════════════════════════════════════════
    private void BuildUI()
    {
        // ── Page header ──
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = AppTheme.AppBackground };
        pnlHeader.Controls.Add(new Label
        {
            Text = "Inventory",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        });
        pnlHeader.Controls.Add(new Label
        {
            Text = "Manage products, stock levels and reorder points",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(2, 36),
            AutoSize = true
        });

        // ── Toolbar ──
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = AppTheme.AppBackground };

        _txtSearch = new TextBox
        {
            Location = new Point(0, 12),
            Width = 340,
            Font = new Font("Segoe UI", 10.5f),
            BackColor = Color.White,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Search by code or name"
        };
        _txtSearch.TextChanged += (s, e) => RebuildList();

        _chkArchived = new CheckBox
        {
            Text = "Show archived",
            AutoSize = true,
            Font = AppTheme.FontBody,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(360, 16),
            Cursor = Cursors.Hand
        };
        _chkArchived.CheckedChanged += async (s, e) => await LoadProductsAsync();

        var btnRefresh = OutlineButton("Refresh", 96);
        btnRefresh.Location = new Point(500, 10);
        btnRefresh.Click += async (s, e) => await LoadProductsAsync();

        _lblCount = new Label
        {
            Text = "",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            AutoSize = true,
            Location = new Point(612, 19)
        };

        var btnAdd = SolidButton("+  Add product", 150, AppTheme.Primary);
        btnAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAdd.Location = new Point(Math.Max(0, toolbar.Width - btnAdd.Width), 10);
        btnAdd.Click += (s, e) => ToggleNewPanel();
        toolbar.Resize += (s, e) => btnAdd.Location = new Point(toolbar.Width - btnAdd.Width, 10);

        toolbar.Controls.AddRange(new Control[] { _txtSearch, _chkArchived, btnRefresh, _lblCount, btnAdd });

        // ── "New product" panel (hidden until Add product is clicked) ──
        _newEditor = new ProductEditor("Add product", isNew: true) { Dock = DockStyle.Fill };
        _newEditor.SaveClicked += async (s, e) => await CreateProductAsync();
        _newEditor.CancelClicked += (s, e) => _newPanel.Visible = false;

        var newCard = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.CardBackground };
        newCard.Paint += (s, e) => DrawBorder(e.Graphics, newCard);
        newCard.Padding = new Padding(1);
        newCard.Controls.Add(_newEditor);

        _newPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = _newEditor.PreferredHeight + 14,
            Padding = new Padding(0, 0, 0, 14),
            BackColor = AppTheme.AppBackground,
            Visible = false
        };
        _newPanel.Controls.Add(newCard);

        // ── Table card: header strip + scrolling rows ──
        var card = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.CardBackground, Padding = new Padding(1) };
        card.Paint += (s, e) => DrawBorder(e.Graphics, card);

        _list = new BufferedFlow
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = AppTheme.CardBackground
        };
        _list.ClientSizeChanged += (s, e) => FitWidths();

        card.Controls.Add(_list);                       // Fill first
        card.Controls.Add(new GridHeader { Dock = DockStyle.Top });

        // ── Status line ──
        _lblStatus = new Label
        {
            Text = "Ready.",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Dock = DockStyle.Bottom,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Dock order: Fill first, then Bottom, then Tops (last added = closest to the top)
        Controls.Add(card);
        Controls.Add(_lblStatus);
        Controls.Add(_newPanel);
        Controls.Add(toolbar);
        Controls.Add(pnlHeader);
    }

    private static void DrawBorder(Graphics g, Control c)
    {
        using var pen = new Pen(AppTheme.Border, 1);
        g.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
    }

    private static Button SolidButton(string text, int width, Color bg)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(width, 36),
            BackColor = bg,
            ForeColor = Color.White,
            Font = AppTheme.FontButton,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private static Button OutlineButton(string text, int width)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(width, 36),
            BackColor = Color.White,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.FontButton,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = AppTheme.Border;
        return b;
    }

    // ═══════════════════════════════════════════════════════════
    // List handling
    // ═══════════════════════════════════════════════════════════
    private void RebuildList()
    {
        var term = _txtSearch.Text.Trim();
        IEnumerable<ProductRow> query = _allProducts;
        if (term.Length > 0)
        {
            query = query.Where(p =>
                p.ProductCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        var rows = query.ToList();

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
            var item = new ProductItem(row);
            item.EditToggled += (s, e) => ToggleEdit(item);
            item.SaveClicked += async (s, e) => await SaveEditAsync(item);
            item.ArchiveClicked += async (s, e) => await ArchiveAsync(item.Data);
            item.RestoreClicked += async (s, e) => await SetArchivedAsync(item.Data, "restore", $"Restored {item.Data.ProductName}.");
            _items.Add(item);
            _list.Controls.Add(item);
        }

        if (rows.Count == 0)
        {
            _emptyLabel = new Label
            {
                Text = _allProducts.Count == 0
                    ? "No products yet. Use “Add product” to create the first one."
                    : "No products match your search.",
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
        _lblCount.Text = $"{rows.Count} product(s)";
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

    private void ToggleEdit(ProductItem item)
    {
        bool open = !item.Expanded;

        _newPanel.Visible = false;
        foreach (var other in _items)
            if (other != item) other.SetExpanded(false);

        item.SetExpanded(open);
        if (open) _list.ScrollControlIntoView(item);
    }

    private void ToggleNewPanel()
    {
        if (_newPanel.Visible)
        {
            _newPanel.Visible = false;
            return;
        }

        foreach (var item in _items) item.SetExpanded(false);
        _newEditor.Load(null);
        _newPanel.Visible = true;
        _newEditor.FocusFirst();
    }

    private void SetStatus(string message, bool isError = false)
    {
        _lblStatus.Text = message;
        _lblStatus.ForeColor = isError ? AppTheme.Danger : AppTheme.TextMuted;
    }

    // ═══════════════════════════════════════════════════════════
    // API calls
    // ═══════════════════════════════════════════════════════════
    private async Task LoadProductsAsync()
    {
        const int maxAttempts = 10;
        var url = $"tenant/{DefaultCompanyId}/products?includeArchived={(_chkArchived.Checked ? "true" : "false")}";

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                SetStatus($"Connecting to API... ({attempt}/{maxAttempts})");
                var list = await _http.GetFromJsonAsync<List<ProductRow>>(url) ?? new();

                _allProducts = list;
                RebuildList();
                SetStatus($"Loaded {list.Count} product(s).");
                return;
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                SetStatus($"API not ready yet, retrying... ({attempt}/{maxAttempts})");
                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load: {ex.Message}", true);
                return;
            }
        }

        SetStatus($"API not reachable after {maxAttempts} attempts. Is the API running?", true);
    }

    private async Task CreateProductAsync()
    {
        if (!_newEditor.TryRead(out var v)) return;

        _newEditor.SetBusy(true);
        try
        {
            var payload = new
            {
                productName = v.Name,
                unitPrice = v.Price,
                unitsPerBox = 1,
                unitOfMeasure = "Piece",
                initialStock = v.Stock,
                reorderLevel = v.Reorder
            };

            var resp = await _http.PostAsJsonAsync($"tenant/{DefaultCompanyId}/products", payload);
            if (!resp.IsSuccessStatusCode)
            {
                _newEditor.ShowError(await ReadError(resp));
                return;
            }

            _newPanel.Visible = false;
            SetStatus($"Added {v.Name}.");
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            _newEditor.ShowError(ex.Message);
        }
        finally
        {
            _newEditor.SetBusy(false);
        }
    }

    private async Task SaveEditAsync(ProductItem item)
    {
        var editor = item.Editor;
        if (!editor.TryRead(out var v)) return;

        var row = item.Data;
        editor.SetBusy(true);
        try
        {
            var payload = new
            {
                productCode = row.ProductCode,   // code stays the same
                productName = v.Name,
                unitPrice = v.Price,
                unitsPerBox = row.UnitsPerBox,
                unitOfMeasure = row.UnitOfMeasure,
                reorderLevel = v.Reorder
            };

            var resp = await _http.PutAsJsonAsync($"tenant/{DefaultCompanyId}/products/{row.ProductId}", payload);
            if (!resp.IsSuccessStatusCode)
            {
                editor.ShowError(await ReadError(resp));
                return;
            }

            var delta = v.Stock - row.QuantityOnHand;
            if (delta != 0)
            {
                var adj = await _http.PostAsJsonAsync($"tenant/{DefaultCompanyId}/inventory/adjust", new
                {
                    productId = row.ProductId,
                    quantityDelta = delta,
                    reorderLevel = v.Reorder
                });
                if (!adj.IsSuccessStatusCode)
                {
                    editor.ShowError("Details saved, but the stock update failed.");
                    return;
                }
            }

            SetStatus($"Saved {v.Name}.");
            await LoadProductsAsync();      // rebuilds the list, which collapses the row
        }
        catch (Exception ex)
        {
            editor.ShowError(ex.Message);
        }
        finally
        {
            if (!editor.IsDisposed) editor.SetBusy(false);
        }
    }

    private async Task ArchiveAsync(ProductRow row)
    {
        var confirm = MessageBox.Show(
            $"Archive “{row.ProductName}”?\n\nIt will no longer appear in Point of Sale. You can bring it back any time with “Show archived”.",
            "Archive product", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        await SetArchivedAsync(row, "archive", $"Archived {row.ProductName}.");
    }

    private async Task SetArchivedAsync(ProductRow row, string action, string okMessage)
    {
        try
        {
            var resp = await _http.PostAsync($"tenant/{DefaultCompanyId}/products/{row.ProductId}/{action}", null);
            if (!resp.IsSuccessStatusCode)
            {
                SetStatus(await ReadError(resp), true);
                return;
            }

            SetStatus(okMessage);
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", true);
        }
    }

    private static async Task<string> ReadError(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m) && m.GetString() is { Length: > 0 } text)
                return text;
        }
        catch { /* fall through */ }
        return $"Request failed ({(int)resp.StatusCode}).";
    }

    // ═══════════════════════════════════════════════════════════
    // DTO
    // ═══════════════════════════════════════════════════════════
    public class ProductRow
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public int UnitsPerBox { get; set; }
        public string UnitOfMeasure { get; set; } = "";
        public decimal QuantityOnHand { get; set; }
        public decimal ReorderLevel { get; set; }
        public bool IsLowStock { get; set; }
        public bool IsActive { get; set; } = true;
    }

    private sealed record EditorValues(string Name, decimal Price, decimal Reorder, decimal Stock);

    // ═══════════════════════════════════════════════════════════
    // Flow panel with double buffering (no flicker on expand/collapse)
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
        private static readonly string[] Titles = { "Code", "Product", "Price", "Stock", "Reorder", "Status" };
        private static readonly Font TitleFont = new("Segoe UI Semibold", 8.5f);

        public GridHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 40;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.FromArgb(251, 251, 250));

            var cols = SummaryBar.ColumnRects(Width, Height);
            for (int i = 0; i < Titles.Length; i++)
            {
                bool right = SummaryBar.IsRightAligned(i);
                TextRenderer.DrawText(g, Titles[i], TitleFont, SummaryBar.TextRect(cols[i], right),
                    AppTheme.TextSecondary, SummaryBar.Flags(right));
            }

            TextRenderer.DrawText(g, "Actions", TitleFont,
                new Rectangle(Width - SummaryBar.ActionsWidth, 0, SummaryBar.ActionsWidth - 24, Height),
                AppTheme.TextSecondary, SummaryBar.Flags(true));

            using var pen = new Pen(AppTheme.Border, 1);
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // One collapsed row: values + Edit / Archive actions
    // ═══════════════════════════════════════════════════════════
    private sealed class SummaryBar : Control
    {
        public const int ActionsWidth = 190;

        private static readonly float[] Weights = { 14, 34, 12, 10, 10, 12 };
        private static readonly Font NameFont = new("Segoe UI Semibold", 10f);
        private static readonly Font CellFont = AppTheme.FontBody;
        private static readonly Font StockBoldFont = new("Segoe UI Semibold", 9.5f);
        private static readonly Font PillFont = new("Segoe UI Semibold", 8.5f);
        private static readonly Font ActionFont = new("Segoe UI Semibold", 9.5f);
        private static readonly Font ActionHoverFont = new("Segoe UI Semibold", 9.5f, FontStyle.Underline);

        private enum Act { None, Edit, Archive, Restore }
        private readonly record struct ActionHit(Act Kind, string Text, Rectangle Rect);

        private readonly ProductRow _data;
        private bool _rowHover;
        private Act _hoverAct = Act.None;
        private bool _expanded;

        public event EventHandler? EditClicked;
        public event EventHandler? ArchiveClicked;
        public event EventHandler? RestoreClicked;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Expanded
        {
            get => _expanded;
            set { _expanded = value; Invalidate(); }
        }

        public SummaryBar(ProductRow data)
        {
            _data = data;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        // ── shared column geometry (also used by GridHeader) ──
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

        public static bool IsRightAligned(int col) => col is 2 or 3 or 4;

        public static Rectangle TextRect(Rectangle col, bool right) =>
            new(col.X, col.Y, col.Width - (right ? 24 : 8), col.Height);

        public static TextFormatFlags Flags(bool right) =>
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
            TextFormatFlags.NoPadding | (right ? TextFormatFlags.Right : TextFormatFlags.Left);

        // ── actions ──
        private ActionHit[] Actions()
        {
            var labels = _data.IsActive
                ? new[] { (Act.Edit, _expanded ? "Close" : "Edit"), (Act.Archive, "Archive") }
                : new[] { (Act.Restore, "Restore") };

            var hits = new ActionHit[labels.Length];
            int x = Width - 24;
            for (int i = labels.Length - 1; i >= 0; i--)
            {
                var size = TextRenderer.MeasureText(labels[i].Item2, ActionFont, Size.Empty, TextFormatFlags.NoPadding);
                x -= size.Width;
                hits[i] = new ActionHit(labels[i].Item1, labels[i].Item2,
                    new Rectangle(x - 4, (Height - 30) / 2, size.Width + 8, 30));
                x -= 22;
            }
            return hits;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bg = _expanded ? AppTheme.AccentSoft
                   : _rowHover ? Color.FromArgb(250, 250, 249)
                   : AppTheme.CardBackground;
            g.Clear(bg);

            if (_expanded)
            {
                using var bar = new SolidBrush(AppTheme.Accent);
                g.FillRectangle(bar, 0, 0, 3, Height);
            }

            var cols = ColumnRects(Width, Height);
            bool archived = !_data.IsActive;
            var main = archived ? AppTheme.TextMuted : AppTheme.TextPrimary;
            var sub = archived ? AppTheme.TextMuted : AppTheme.TextSecondary;

            TextRenderer.DrawText(g, _data.ProductCode, AppTheme.FontMono, TextRect(cols[0], false), sub, Flags(false));
            TextRenderer.DrawText(g, _data.ProductName, NameFont, TextRect(cols[1], false), main, Flags(false));
            TextRenderer.DrawText(g, $"₱{_data.UnitPrice:N2}", CellFont, TextRect(cols[2], true), main, Flags(true));

            bool low = _data.IsLowStock && !archived;
            TextRenderer.DrawText(g, _data.QuantityOnHand.ToString("0.##"), low ? StockBoldFont : CellFont,
                TextRect(cols[3], true), low ? AppTheme.Danger : main, Flags(true));
            TextRenderer.DrawText(g, _data.ReorderLevel.ToString("0.##"), CellFont, TextRect(cols[4], true), sub, Flags(true));

            DrawStatusPill(g, cols[5], archived, low);

            foreach (var hit in Actions())
            {
                bool hot = hit.Kind == _hoverAct;
                var color = hit.Kind == Act.Archive && hot ? AppTheme.Danger
                          : hit.Kind == Act.Archive ? AppTheme.TextSecondary
                          : AppTheme.TextPrimary;
                TextRenderer.DrawText(g, hit.Text, hot ? ActionHoverFont : ActionFont, hit.Rect, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        private static void DrawStatusPill(Graphics g, Rectangle col, bool archived, bool low)
        {
            var (text, fg, fill) = archived ? ("Archived", AppTheme.TextSecondary, Color.FromArgb(244, 244, 245))
                                 : low ? ("Low stock", AppTheme.Danger, Color.FromArgb(254, 236, 236))
                                 : ("In stock", AppTheme.TextSecondary, Color.FromArgb(244, 244, 245));

            var size = TextRenderer.MeasureText(text, PillFont, Size.Empty, TextFormatFlags.NoPadding);
            var pill = new Rectangle(col.X, (col.Height - 24) / 2, Math.Min(size.Width + 22, col.Width - 8), 24);

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

        // ── mouse ──
        private Act HitTest(Point p)
        {
            foreach (var hit in Actions())
                if (hit.Rect.Contains(p)) return hit.Kind;
            return Act.None;
        }

        protected override void OnMouseEnter(EventArgs e) { _rowHover = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e)
        {
            _rowHover = false;
            _hoverAct = Act.None;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var act = HitTest(e.Location);
            if (act != _hoverAct)
            {
                _hoverAct = act;
                Cursor = act == Act.None ? Cursors.Default : Cursors.Hand;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;

            switch (HitTest(e.Location))
            {
                case Act.Edit: EditClicked?.Invoke(this, EventArgs.Empty); break;
                case Act.Archive: ArchiveClicked?.Invoke(this, EventArgs.Empty); break;
                case Act.Restore: RestoreClicked?.Invoke(this, EventArgs.Empty); break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Row = summary + expandable editor underneath
    // ═══════════════════════════════════════════════════════════
    private sealed class ProductItem : Panel
    {
        private const int SummaryHeight = 56;

        private readonly SummaryBar _summary;
        private readonly ProductEditor _editor;

        public event EventHandler? EditToggled;
        public event EventHandler? SaveClicked;
        public event EventHandler? ArchiveClicked;
        public event EventHandler? RestoreClicked;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ProductRow Data { get; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Expanded { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ProductEditor Editor => _editor;

        public ProductItem(ProductRow data)
        {
            Data = data;
            Margin = Padding.Empty;
            Padding = new Padding(0, 0, 0, 1);          // 1px for the divider line
            Height = SummaryHeight + 1;
            BackColor = AppTheme.CardBackground;
            DoubleBuffered = true;

            _editor = new ProductEditor("Save changes", isNew: false)
            {
                Dock = DockStyle.Bottom,
                Visible = false
            };
            _editor.Height = _editor.PreferredHeight;
            _editor.Load(data);
            _editor.CancelClicked += (s, e) => SetExpanded(false);
            _editor.SaveClicked += (s, e) => SaveClicked?.Invoke(this, EventArgs.Empty);

            _summary = new SummaryBar(data) { Dock = DockStyle.Top, Height = SummaryHeight };
            _summary.EditClicked += (s, e) => EditToggled?.Invoke(this, EventArgs.Empty);
            _summary.ArchiveClicked += (s, e) => ArchiveClicked?.Invoke(this, EventArgs.Empty);
            _summary.RestoreClicked += (s, e) => RestoreClicked?.Invoke(this, EventArgs.Empty);

            Controls.Add(_editor);
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
            _editor.Visible = expanded;
            Height = SummaryHeight + 1 + (expanded ? _editor.PreferredHeight : 0);
            if (expanded) _editor.Load(Data);           // always start from the saved values
            ResumeLayout(true);

            if (expanded) _editor.FocusFirst();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Details form used by both "Edit" (inline) and "Add product"
    // ═══════════════════════════════════════════════════════════
    private sealed class ProductEditor : Panel
    {
        private readonly TextBox _name, _price, _reorder, _stock;
        private readonly Label _error;
        private readonly Button _save, _cancel;
        private readonly string _saveText;

        public event EventHandler? SaveClicked;
        public event EventHandler? CancelClicked;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int PreferredHeight { get; }

        public ProductEditor(string saveText, bool isNew)
        {
            _saveText = saveText;
            BackColor = isNew ? AppTheme.CardBackground : Color.FromArgb(251, 251, 250);

            int left = isNew ? 24 : 28;
            int top = isNew ? 56 : 24;
            PreferredHeight = top + 112 + 14;

            if (isNew)
            {
                Controls.Add(new Label
                {
                    Text = "New product",
                    Font = new Font("Segoe UI Semibold", 12f),
                    ForeColor = AppTheme.TextPrimary,
                    Location = new Point(left, 18),
                    AutoSize = true
                });
            }

            _name = AddField("Product name", left, top, 360, numeric: false);
            _price = AddField("Unit price (₱)", left + 380, top, 140, numeric: true);
            _reorder = AddField("Reorder level", left + 540, top, 140, numeric: true);
            _stock = AddField(isNew ? "Initial stock" : "Stock on hand", left + 700, top, 140, numeric: true);

            _save = SolidButton(saveText, 140, AppTheme.Primary);
            _save.Location = new Point(left, top + 72);
            _save.Click += (s, e) => SaveClicked?.Invoke(this, EventArgs.Empty);

            _cancel = OutlineButton("Cancel", 100);
            _cancel.Location = new Point(left + 152, top + 72);
            _cancel.Click += (s, e) => CancelClicked?.Invoke(this, EventArgs.Empty);

            _error = new Label
            {
                Text = "",
                ForeColor = AppTheme.Danger,
                Font = AppTheme.FontBody,
                AutoSize = true,
                Location = new Point(left + 272, top + 81)
            };

            Controls.AddRange(new Control[] { _save, _cancel, _error });
        }

        private TextBox AddField(string label, int x, int y, int width, bool numeric)
        {
            Controls.Add(new Label
            {
                Text = label,
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.FontSmall,
                Location = new Point(x, y),
                AutoSize = true
            });

            var tb = new TextBox
            {
                Location = new Point(x, y + 22),
                Width = width,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.White,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };

            if (numeric)
            {
                tb.KeyPress += (s, e) =>
                {
                    if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar)) return;
                    if (e.KeyChar == '.' && !tb.Text.Contains('.')) return;
                    e.Handled = true;
                };
            }

            tb.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    SaveClicked?.Invoke(this, EventArgs.Empty);
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    CancelClicked?.Invoke(this, EventArgs.Empty);
                }
            };

            Controls.Add(tb);
            return tb;
        }

        public void Load(ProductRow? row)
        {
            _error.Text = "";
            if (row == null)
            {
                _name.Text = "";
                _price.Text = "";
                _reorder.Text = "5";
                _stock.Text = "0";
                return;
            }

            _name.Text = row.ProductName;
            _price.Text = row.UnitPrice.ToString("0.00");
            _reorder.Text = row.ReorderLevel.ToString("0.##");
            _stock.Text = row.QuantityOnHand.ToString("0.##");
        }

        public void FocusFirst()
        {
            _name.Focus();
            _name.SelectAll();
        }

        public bool TryRead(out EditorValues values)
        {
            values = null!;

            var name = _name.Text.Trim();
            if (name.Length == 0) return Fail("Enter a product name.", _name);
            if (!decimal.TryParse(_price.Text, out var price) || price < 0) return Fail("Enter a valid unit price.", _price);
            if (!decimal.TryParse(_reorder.Text, out var reorder) || reorder < 0) return Fail("Reorder level must be 0 or more.", _reorder);
            if (!decimal.TryParse(_stock.Text, out var stock) || stock < 0) return Fail("Stock must be 0 or more.", _stock);

            _error.Text = "";
            values = new EditorValues(name, price, reorder, stock);
            return true;

            bool Fail(string message, Control focus)
            {
                _error.Text = message;
                focus.Focus();
                return false;
            }
        }

        public void ShowError(string message) => _error.Text = message;

        public void SetBusy(bool busy)
        {
            _save.Enabled = !busy;
            _cancel.Enabled = !busy;
            _save.Text = busy ? "Saving..." : _saveText;
        }
    }
}