using CoreErp.WinForms.Theme;
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
    private DataGridView _grid = null!;
    private Label _lblStatus = null!;
    private Label _lblResultCount = null!;

    // Summary card labels
    private Label _lblTodayCount = null!;
    private Label _lblTodayRevenue = null!;
    private Label _lblTotalCount = null!;
    private Label _lblTotalRevenue = null!;

    private List<TransactionRow> _allTransactions = new();

    public TransactionHistoryView()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;
        BuildUI();
        _ = LoadTransactionsWithRetryAsync();
    }

    private void BuildUI()
    {
        // ═══════════════════════════════════════════════════════════
        // HEADER
        // ═══════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════
        // LAYOUT (stats → filters → grid)
        // ═══════════════════════════════════════════════════════════
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.AppBackground
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));   // cards
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));    // filters
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // grid

        // ═══════════════════════════════════════════════════════════
        // SUMMARY CARDS
        // ═══════════════════════════════════════════════════════════
        var pnlCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppTheme.AppBackground,
            Padding = new Padding(0, 8, 0, 8)
        };

        pnlCards.Controls.Add(CreateStatCard("Today's Transactions", out _lblTodayCount));
        pnlCards.Controls.Add(CreateStatCard("Today's Revenue", out _lblTodayRevenue));
        pnlCards.Controls.Add(CreateStatCard("Total Transactions", out _lblTotalCount));
        pnlCards.Controls.Add(CreateStatCard("Total Revenue", out _lblTotalRevenue));

        // ═══════════════════════════════════════════════════════════
        // FILTERS
        // ═══════════════════════════════════════════════════════════
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
        _txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ApplyFilter(); }
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

        var btnFilter = new Button
        {
            Text = "Filter",
            Location = new Point(630, 11),
            Size = new Size(90, 36),
            BackColor = AppTheme.Primary,
            ForeColor = Color.White,
            Font = AppTheme.FontButton,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnFilter.FlatAppearance.BorderSize = 0;
        btnFilter.Click += (s, e) => ApplyFilter();

        var btnReset = new Button
        {
            Text = "Reset",
            Location = new Point(728, 11),
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
            ApplyFilter();
        };

        var btnRefresh = new Button
        {
            Text = "↻  Refresh",
            Location = new Point(826, 11),
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
            _txtSearch, _cbPayment, _cbDateRange, btnFilter, btnReset, btnRefresh, _lblResultCount
        });
        pnlFilters.Resize += (s, e) => _lblResultCount.Location = new Point(pnlFilters.Width - 90, 22);

        // ═══════════════════════════════════════════════════════════
        // GRID
        // ═══════════════════════════════════════════════════════════
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextPrimary,
            GridColor = AppTheme.BorderLight,
            BorderStyle = BorderStyle.FixedSingle,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            RowTemplate = { Height = 40 }
        };
        StyleGrid(_grid);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Invoice #", DataPropertyName = "InvoiceNumber", FillWeight = 14 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "SaleDate", FillWeight = 15, DefaultCellStyle = { Format = "MMM dd, yyyy hh:mm tt" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cashier", DataPropertyName = "CashierName", FillWeight = 14 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Items", DataPropertyName = "ItemCount", FillWeight = 7, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Subtotal", DataPropertyName = "Subtotal", FillWeight = 11, DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Discount", DataPropertyName = "DiscountAmount", FillWeight = 11, DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total", DataPropertyName = "TotalAmount", FillWeight = 12, DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Payment", DataPropertyName = "PaymentMethod", FillWeight = 10, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "StatusLabel", FillWeight = 8, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellDoubleClick += (s, e) => ShowDetails();

        // ═══════════════════════════════════════════════════════════
        // STATUS BAR
        // ═══════════════════════════════════════════════════════════
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
        layout.Controls.Add(_grid, 0, 2);

        Controls.Add(layout);
        Controls.Add(_lblStatus);
        Controls.Add(pnlHeader);
    }

    // ─── Stat card ───
    private static Panel CreateStatCard(string title, out Label valueLabel)
    {
        var card = new Panel
        {
            Width = 280,
            Height = 76,
            BackColor = AppTheme.CardBackground,
            Margin = new Padding(0, 0, 14, 0)
        };
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = GetRoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10);
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawPath(pen, path);
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(20, 0, 20, 0),
            BackColor = Color.Transparent
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));

        var lblTitle = new Label
        {
            Text = title,
            ForeColor = AppTheme.TextSecondary,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        valueLabel = new Label
        {
            Text = "0",
            ForeColor = AppTheme.TextPrimary,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        table.Controls.Add(lblTitle, 0, 0);
        table.Controls.Add(valueLabel, 1, 0);
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

    private static void StyleGrid(DataGridView g)
    {
        g.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.AppBackground;
        g.ColumnHeadersDefaultCellStyle.ForeColor = AppTheme.TextSecondary;
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        g.ColumnHeadersHeight = 42;
        g.DefaultCellStyle.BackColor = AppTheme.CardBackground;
        g.DefaultCellStyle.ForeColor = AppTheme.TextPrimary;
        g.DefaultCellStyle.SelectionBackColor = AppTheme.SidebarActive;
        g.DefaultCellStyle.SelectionForeColor = AppTheme.Primary;
        g.DefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
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
            e.CellStyle.ForeColor = AppTheme.Success;
            e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        }
        else if (col == "DiscountAmount" && row.DiscountAmount > 0)
        {
            e.CellStyle.ForeColor = AppTheme.Danger;
        }
        else if (col == "PaymentMethod")
        {
            e.CellStyle.ForeColor = row.PaymentMethod == "Cash" ? AppTheme.Success : AppTheme.Primary;
            e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        }
    }

    // ─── Data loading ───
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
                ApplyFilter();
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

        _lblTodayCount.Text = todayTx.Count.ToString("N0");
        _lblTodayRevenue.Text = $"₱{todayTx.Sum(t => t.TotalAmount):N2}";
        _lblTotalCount.Text = _allTransactions.Count.ToString("N0");
        _lblTotalRevenue.Text = $"₱{_allTransactions.Sum(t => t.TotalAmount):N2}";
    }

    private void ApplyFilter()
    {
        var term = _txtSearch.Text.Trim().ToLower();
        var payment = _cbPayment.SelectedItem?.ToString() ?? "All Payments";
        var range = _cbDateRange.SelectedItem?.ToString() ?? "All Time";

        var filtered = _allTransactions.AsEnumerable();

        // Search
        if (!string.IsNullOrWhiteSpace(term))
        {
            filtered = filtered.Where(t =>
                t.InvoiceNumber.ToLower().Contains(term) ||
                t.CashierName.ToLower().Contains(term));
        }

        // Payment filter
        if (payment != "All Payments")
            filtered = filtered.Where(t => t.PaymentMethod == payment);

        // Date filter
        var now = DateTime.UtcNow;
        filtered = range switch
        {
            "Today" => filtered.Where(t => t.SaleDate.Date == now.Date),
            "This Week" => filtered.Where(t => t.SaleDate >= now.AddDays(-7)),
            "This Month" => filtered.Where(t => t.SaleDate >= now.AddDays(-30)),
            "This Year" => filtered.Where(t => t.SaleDate >= now.AddDays(-365)),
            _ => filtered
        };

        var result = filtered.OrderByDescending(t => t.SaleDate).ToList();

        _grid.DataSource = null;
        _grid.DataSource = result;
        _lblResultCount.Text = $"{result.Count} result(s)";
    }

    // ─── Detail dialog ───
    private void ShowDetails()
    {
        if (_grid.CurrentRow?.DataBoundItem is not TransactionRow t) return;

        using var dlg = new Form
        {
            Text = $"Transaction {t.InvoiceNumber}",
            Size = new Size(480, 460),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = AppTheme.CardBackground,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lblHeader = new Label
        {
            Text = $"Invoice: {t.InvoiceNumber}",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(24, 20),
            AutoSize = true
        };

        var lines = new List<(string Label, string Value)>
        {
            ("Date",        t.SaleDate.ToString("MMMM dd, yyyy HH:mm:ss")),
            ("Cashier",     t.CashierName),
            ("Payment",     t.PaymentMethod),
            ("Items",       t.ItemCount.ToString()),
            ("Subtotal",    $"₱{t.Subtotal:N2}"),
            ("Discount",    t.DiscountAmount > 0 ? $"₱{t.DiscountAmount:N2} ({t.DiscountType})" : "None"),
            ("VAT (12%)",   t.VatExemptSales > 0 ? "Exempt" : $"₱{t.VatAmount:N2}"),
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
                ForeColor = label == "Total" ? AppTheme.Success : AppTheme.TextPrimary,
                Font = label == "Total"
                    ? new Font("Segoe UI", 11, FontStyle.Bold)
                    : new Font("Segoe UI", 9.5f),
                Location = new Point(170, y),
                Size = new Size(280, 24),
                TextAlign = ContentAlignment.MiddleLeft
            });
            y += 30;
        }

        var btnClose = new Button
        {
            Text = "Close",
            Location = new Point(24, 372),
            Size = new Size(426, 40),
            BackColor = AppTheme.Primary,
            ForeColor = Color.White,
            Font = AppTheme.FontButton,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => dlg.Close();

        dlg.Controls.Add(lblHeader);
        dlg.Controls.Add(btnClose);
        dlg.ShowDialog();
    }

    // ─── DTO ───
    public class TransactionRow
    {
        public int SaleId { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public string CashierName { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
        public string DiscountType { get; set; } = "";
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal VatExemptSales { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal ChangeDue { get; set; }
        public DateTime SaleDate { get; set; }
        public int ItemCount { get; set; }
        public string StatusLabel => "✓ Completed";
    }
}