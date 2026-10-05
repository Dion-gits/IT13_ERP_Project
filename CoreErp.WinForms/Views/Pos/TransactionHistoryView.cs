using CoreErp.WinForms.Theme;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;

namespace CoreErp.WinForms.Views.POS;

/// <summary>
/// Transaction history matching the D.CC Orders list design:
/// stat cards, status filter pills, clean table with colored status badges.
/// </summary>
public class TransactionHistoryView : UserControl
{
    private const int DefaultCompanyId = 1;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5067/") };

    private TextBox _txtSearch = null!;
    private DataGridView _grid = null!;
    private Label _lblStatus = null!;
    private Label _lblResultCount = null!;
    private readonly List<Button> _statusPills = new();

    private Label _lblTotalOrders = null!;
    private Label _lblTodayOrders = null!;
    private Label _lblCashTotal = null!;
    private Label _lblGCashTotal = null!;
    private Label _lblRevenue = null!;

    private List<TransactionRow> _allTransactions = new();
    private string _activeStatus = "All";

    public TransactionHistoryView()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;
        BuildUI();
        _ = LoadTransactionsWithRetryAsync();
    }

    private void BuildUI()
    {
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = AppTheme.AppBackground
        };
        pnlHeader.Controls.Add(new Label
        {
            Text = "Orders",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 4),
            AutoSize = true
        });
        pnlHeader.Controls.Add(new Label
        {
            Text = "Manage all sales transactions",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(2, 38),
            AutoSize = true
        });

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.AppBackground
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Stat cards
        var pnlCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 8, 0, 8)
        };
        pnlCards.Controls.Add(CreateStatCard("Total Orders", out _lblTotalOrders));
        pnlCards.Controls.Add(CreateStatCard("Today", out _lblTodayOrders));
        pnlCards.Controls.Add(CreateStatCard("Cash Sales", out _lblCashTotal));
        pnlCards.Controls.Add(CreateStatCard("GCash Sales", out _lblGCashTotal));
        pnlCards.Controls.Add(CreateStatCard("Revenue", out _lblRevenue));

        // Filters
        var pnlFilters = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var pills = new FlowLayoutPanel
        {
            Location = new Point(0, 10),
            Size = new Size(520, 36),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        AddStatusPill(pills, "All", true);
        AddStatusPill(pills, "Cash", false);
        AddStatusPill(pills, "GCash", false);
        AddStatusPill(pills, "Senior/PWD", false);

        _txtSearch = new TextBox
        {
            Location = new Point(540, 12),
            Width = 240,
            Height = 32,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "🔍  Search invoice or cashier…"
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        var btnRefresh = AppTheme.MakeGhostButton("↻  Refresh", 100, 32);
        btnRefresh.Location = new Point(792, 12);
        btnRefresh.Click += async (s, e) => await LoadTransactionsWithRetryAsync();

        _lblResultCount = new Label
        {
            Text = "0 results",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        pnlFilters.Controls.Add(pills);
        pnlFilters.Controls.Add(_txtSearch);
        pnlFilters.Controls.Add(btnRefresh);
        pnlFilters.Controls.Add(_lblResultCount);
        pnlFilters.Resize += (s, e) => _lblResultCount.Location = new Point(pnlFilters.Width - 90, 18);

        // Grid
        _grid = new DataGridView();
        AppTheme.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Order ID", DataPropertyName = "InvoiceNumber", FillWeight = 14 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cashier", DataPropertyName = "CashierName", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Items", DataPropertyName = "ItemCount", FillWeight = 7, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Amount", DataPropertyName = "TotalAmount", FillWeight = 12, DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Discount", DataPropertyName = "DiscountAmount", FillWeight = 10, DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "StatusLabel", FillWeight = 12, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date & Time", DataPropertyName = "SaleDate", FillWeight = 16, DefaultCellStyle = { Format = "MMM dd, yyyy hh:mm tt" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Payment", DataPropertyName = "PaymentMethod", FillWeight = 10, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellDoubleClick += (s, e) => ShowDetails();

        _lblStatus = new Label
        {
            Text = "Ready.",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft
        };

        layout.Controls.Add(pnlCards, 0, 0);
        layout.Controls.Add(pnlFilters, 0, 1);
        layout.Controls.Add(_grid, 0, 2);

        Controls.Add(layout);
        Controls.Add(_lblStatus);
        Controls.Add(pnlHeader);
    }

    private void AddStatusPill(FlowLayoutPanel host, string key, bool active)
    {
        var btn = new Button
        {
            Text = key,
            AutoSize = true,
            MinimumSize = new Size(70, 32),
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(12, 0, 12, 0),
            Tag = key
        };
        btn.FlatAppearance.BorderSize = 0;
        ApplyPill(btn, active);
        btn.Click += (s, e) =>
        {
            _activeStatus = key;
            foreach (var b in _statusPills)
                ApplyPill(b, (string)b.Tag! == key);
            ApplyFilter();
        };
        _statusPills.Add(btn);
        host.Controls.Add(btn);
    }

    private void ApplyPill(Button btn, bool active)
    {
        if (active)
        {
            btn.BackColor = AppTheme.Primary;
            btn.ForeColor = Color.White;
        }
        else
        {
            btn.BackColor = AppTheme.SurfaceRaised;
            btn.ForeColor = AppTheme.TextSecondary;
        }
    }

    private static Panel CreateStatCard(string title, out Label valueLabel)
    {
        var card = new Panel
        {
            Width = 200,
            Height = 80,
            BackColor = AppTheme.CardBackground,
            Margin = new Padding(0, 0, 12, 0)
        };
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = GetRoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 12);
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawPath(pen, path);
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 12, 16, 10),
            BackColor = Color.Transparent
        };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 60));

        var lblTitle = new Label
        {
            Text = title,
            ForeColor = AppTheme.TextSecondary,
            Font = new Font("Segoe UI", 9f),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };
        valueLabel = new Label
        {
            Text = "0",
            ForeColor = AppTheme.TextPrimary,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        };
        table.Controls.Add(lblTitle, 0, 0);
        table.Controls.Add(valueLabel, 0, 1);
        card.Controls.Add(table);
        return card;
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

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Rows[e.RowIndex].DataBoundItem is not TransactionRow row) return;

        var col = _grid.Columns[e.ColumnIndex].DataPropertyName;
        if (col == "StatusLabel")
        {
            e.CellStyle.BackColor = AppTheme.SuccessBg;
            e.CellStyle.ForeColor = AppTheme.Success;
            e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        else if (col == "PaymentMethod")
        {
            e.CellStyle.ForeColor = row.PaymentMethod == "Cash" ? AppTheme.Success : AppTheme.Primary;
            e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        }
        else if (col == "DiscountAmount" && row.DiscountAmount > 0)
        {
            e.CellStyle.ForeColor = AppTheme.Danger;
        }
    }

    private async Task LoadTransactionsWithRetryAsync()
    {
        const int maxAttempts = 10;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                SetStatus($"Loading transactions… ({attempt}/{maxAttempts})");
                var list = await _http.GetFromJsonAsync<List<TransactionRow>>(
                    $"tenant/{DefaultCompanyId}/sales") ?? new();

                _allTransactions = list;
                ApplyFilter();
                UpdateSummaryCards();
                SetStatus($"Loaded {list.Count} transaction(s).");
                return;
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                SetStatus($"API not ready, retrying… ({attempt}/{maxAttempts})");
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

        _lblTotalOrders.Text = _allTransactions.Count.ToString("N0");
        _lblTodayOrders.Text = todayTx.Count.ToString("N0");
        _lblCashTotal.Text = _allTransactions.Count(t => t.PaymentMethod == "Cash").ToString("N0");
        _lblGCashTotal.Text = _allTransactions.Count(t => t.PaymentMethod == "GCash").ToString("N0");
        _lblRevenue.Text = $"₱{_allTransactions.Sum(t => t.TotalAmount):N0}";
    }

    private void ApplyFilter()
    {
        var term = _txtSearch.Text.Trim().ToLowerInvariant();
        var filtered = _allTransactions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            filtered = filtered.Where(t =>
                t.InvoiceNumber.ToLowerInvariant().Contains(term) ||
                t.CashierName.ToLowerInvariant().Contains(term));
        }

        filtered = _activeStatus switch
        {
            "Cash" => filtered.Where(t => t.PaymentMethod == "Cash"),
            "GCash" => filtered.Where(t => t.PaymentMethod == "GCash"),
            "Senior/PWD" => filtered.Where(t => t.DiscountType is "Senior" or "PWD"),
            _ => filtered
        };

        var result = filtered.OrderByDescending(t => t.SaleDate).ToList();
        _grid.DataSource = null;
        _grid.DataSource = result;
        _lblResultCount.Text = $"{result.Count} result(s)";
    }

    private void ShowDetails()
    {
        if (_grid.CurrentRow?.DataBoundItem is not TransactionRow t) return;

        using var dlg = new Form
        {
            Text = $"Order {t.InvoiceNumber}",
            Size = new Size(480, 440),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = AppTheme.CardBackground,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        dlg.Controls.Add(new Label
        {
            Text = $"Order ID: {t.InvoiceNumber}",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(24, 20),
            AutoSize = true
        });

        var lines = new List<(string Label, string Value)>
        {
            ("Date",        t.SaleDate.ToString("MMMM dd, yyyy HH:mm:ss")),
            ("Cashier",     t.CashierName),
            ("Payment",     t.PaymentMethod),
            ("Items",       t.ItemCount.ToString()),
            ("Subtotal",    $"₱{t.Subtotal:N2}"),
            ("Discount",    t.DiscountAmount > 0 ? $"₱{t.DiscountAmount:N2} ({t.DiscountType})" : "None"),
            ("Total",       $"₱{t.TotalAmount:N2}"),
            ("Amount Paid", $"₱{t.AmountPaid:N2}"),
            ("Change",      $"₱{t.ChangeDue:N2}"),
        };

        int y = 66;
        foreach (var (label, value) in lines)
        {
            dlg.Controls.Add(new Label
            {
                Text = label,
                ForeColor = AppTheme.TextSecondary,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(24, y),
                Size = new Size(140, 24)
            });
            dlg.Controls.Add(new Label
            {
                Text = value,
                ForeColor = label == "Total" ? AppTheme.Primary : AppTheme.TextPrimary,
                Font = label == "Total"
                    ? new Font("Segoe UI", 12, FontStyle.Bold)
                    : new Font("Segoe UI", 9.5f),
                Location = new Point(170, y),
                Size = new Size(280, 24),
                TextAlign = ContentAlignment.MiddleLeft
            });
            y += 30;
        }

        var btnClose = AppTheme.MakePrimaryButton("Close", 426, 40);
        btnClose.Location = new Point(24, 360);
        btnClose.Click += (s, e) => dlg.Close();
        dlg.Controls.Add(btnClose);
        dlg.ShowDialog(FindForm());
    }

    public class TransactionRow
    {
        public int SaleId { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public string CashierName { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
        public string DiscountType { get; set; } = "";
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal ChangeDue { get; set; }
        public DateTime SaleDate { get; set; }
        public int ItemCount { get; set; }
        public string StatusLabel => "Completed";
    }
}
