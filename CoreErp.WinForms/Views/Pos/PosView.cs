using CoreErp.WinForms.Theme;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;

namespace CoreErp.WinForms.Views.POS;

/// <summary>
/// Card-based POS matching the D.CC design language:
/// light surfaces, product cards with photo placeholder + stepper + Add to cart,
/// category tabs, cart details panel with totals and orange Proceed payment.
/// </summary>
public class PosView : UserControl
{
    private const int DefaultCompanyId = 1;
    private const decimal SENIOR_PWD_RATE = 0.20m;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5067/") };

    private TextBox _txtSearch = null!;
    private FlowLayoutPanel _categoryBar = null!;
    private FlowLayoutPanel _productCards = null!;
    private FlowLayoutPanel _cartItems = null!;
    private Label _lblSubtotal = null!;
    private Label _lblDiscount = null!;
    private Label _lblTotal = null!;
    private Label _lblChange = null!;
    private Label _lblCartCount = null!;
    private Label _lblStatus = null!;
    private TextBox _txtAmountPaid = null!;
    private Button _btnPlaceOrder = null!;
    private Button _btnNone = null!, _btnSenior = null!, _btnPwd = null!;
    private Button _btnCash = null!, _btnGCash = null!;
    private TableLayoutPanel _pnlCashFields = null!;
    private readonly List<Button> _categoryButtons = new();
    private readonly Dictionary<int, decimal> _pendingQty = new(); // productId → qty on card before add

    private List<ProductRow> _products = new();
    private readonly List<CartLine> _cart = new();
    private string _discountType = "None";
    private string _paymentMethod = "Cash";
    private string _activeFilter = "All";

    public PosView()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;
        DoubleBuffered = true;
        BuildUI();
        _ = LoadProductsWithRetryAsync();
    }

    private void BuildUI()
    {
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = AppTheme.AppBackground
        };
        pnlHeader.Controls.Add(new Label
        {
            Text = "Point of Sale",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 8),
            AutoSize = true
        });

        _lblStatus = new Label
        {
            Text = "Ready.",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = AppTheme.AppBackground
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));

        // ── LEFT: Product Lists ─────────────────────────────────────────
        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.AppBackground,
            Padding = new Padding(0, 0, 16, 0)
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));   // title + search
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));   // category tabs
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // product cards

        // ── Title row + search — 2-column grid so nothing collides on resize ──
        var topRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

        topRow.Controls.Add(new Label
        {
            Text = "Product Lists",
            Font = AppTheme.FontHeading,
            ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _txtSearch = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "🔍  Search for product…",
            Margin = new Padding(8, 6, 0, 6)
        };
        _txtSearch.TextChanged += (s, e) => RebuildCards();
        topRow.Controls.Add(_txtSearch, 1, 0);

        left.Controls.Add(topRow, 0, 0);

        // ── Category tabs ────────────────────────────────────────────────
        _categoryBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 0)
        };
        AddCategoryChip("All", true);
        AddCategoryChip("In Stock", false);
        AddCategoryChip("Low Stock", false);
        AddCategoryChip("Out of Stock", false);
        left.Controls.Add(_categoryBar, 0, 1);

        // ── Product cards host ───────────────────────────────────────────
        var cardsHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
        _productCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 8)
        };
        cardsHost.Controls.Add(_productCards);
        left.Controls.Add(cardsHost, 0, 2);

        // ── RIGHT: Cart Details ─────────────────────────────────────────
        var cartPanel = BuildCartPanel();

        main.Controls.Add(left, 0, 0);
        main.Controls.Add(cartPanel, 1, 0);

        Controls.Add(main);
        Controls.Add(_lblStatus);
        Controls.Add(pnlHeader);
    }

    private void AddCategoryChip(string key, bool active)
    {
        var btn = new Button
        {
            Text = key,
            AutoSize = true,
            MinimumSize = new Size(80, 34),
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            Font = AppTheme.FontOf(9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(14, 0, 14, 0),
            Tag = key
        };
        btn.FlatAppearance.BorderSize = 0;
        ApplyChip(btn, active);
        btn.Click += (s, e) =>
        {
            _activeFilter = key;
            foreach (var b in _categoryButtons)
                ApplyChip(b, (string)b.Tag! == key);
            RebuildCards();
        };
        _categoryButtons.Add(btn);
        _categoryBar.Controls.Add(btn);
    }

    private void ApplyChip(Button btn, bool active)
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

    // ── Product cards (D.CC style) ──────────────────────────────────────
    private void RebuildCards()
    {
        _productCards.SuspendLayout();
        _productCards.Controls.Clear();

        var term = _txtSearch.Text.Trim().ToLowerInvariant();
        IEnumerable<ProductRow> q = _products;

        if (!string.IsNullOrWhiteSpace(term))
            q = q.Where(p =>
                p.ProductName.ToLowerInvariant().Contains(term) ||
                p.ProductCode.ToLowerInvariant().Contains(term));

        q = _activeFilter switch
        {
            "In Stock" => q.Where(p => p.QuantityOnHand > p.ReorderLevel),
            "Low Stock" => q.Where(p => p.IsLowStock && p.QuantityOnHand > 0),
            "Out of Stock" => q.Where(p => p.QuantityOnHand <= 0),
            _ => q
        };

        foreach (var p in q.OrderBy(p => p.ProductName))
            _productCards.Controls.Add(CreateProductCard(p));

        _productCards.ResumeLayout();
    }

    private Panel CreateProductCard(ProductRow p)
    {
        var card = new Panel
        {
            Width = 210,
            Height = 280,
            Margin = new Padding(0, 0, 14, 14),
            BackColor = AppTheme.CardBackground
        };
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 14);
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawPath(pen, path);
        };

        // Photo area
        var img = new Panel
        {
            Location = new Point(12, 12),
            Size = new Size(186, 120),
            BackColor = AppTheme.SurfaceRaised
        };
        img.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(new Rectangle(0, 0, img.Width - 1, img.Height - 1), 10);
            using var brush = new SolidBrush(AppTheme.SurfaceRaised);
            e.Graphics.FillPath(brush, path);

            // Gradient-ish circle with initial
            using var circle = new SolidBrush(Color.FromArgb(40, AppTheme.Primary));
            e.Graphics.FillEllipse(circle, 53, 25, 80, 80);

            var letter = string.IsNullOrEmpty(p.ProductName) ? "?" : p.ProductName[0].ToString().ToUpper();
            using var font = AppTheme.FontOf(26, FontStyle.Bold);
            using var textBrush = new SolidBrush(AppTheme.Primary);
            var size = e.Graphics.MeasureString(letter, font);
            e.Graphics.DrawString(letter, font, textBrush,
                (img.Width - size.Width) / 2,
                (img.Height - size.Height) / 2);
        };

        // Name
        var lblName = new Label
        {
            Text = p.ProductName,
            Location = new Point(12, 140),
            Size = new Size(186, 36),
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.FontOf(10, FontStyle.Bold),
            BackColor = Color.Transparent
        };

        // Price
        var lblPrice = new Label
        {
            Text = $"₱{p.UnitPrice:N2}",
            Location = new Point(12, 176),
            Size = new Size(120, 20),
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.FontOf(11, FontStyle.Bold),
            BackColor = Color.Transparent
        };

        // Category / stock tag
        string tagText;
        Color tagBg, tagFg;
        if (p.QuantityOnHand <= 0)
        {
            tagText = "Out of stock";
            tagBg = AppTheme.DangerBg;
            tagFg = AppTheme.Danger;
        }
        else if (p.IsLowStock)
        {
            tagText = "Low stock";
            tagBg = AppTheme.WarningBg;
            tagFg = AppTheme.Warning;
        }
        else
        {
            tagText = $"{p.QuantityOnHand:N0} left";
            tagBg = AppTheme.SuccessBg;
            tagFg = AppTheme.Success;
        }
        var tag = new Label
        {
            Text = tagText,
            Location = new Point(12, 198),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            BackColor = tagBg,
            ForeColor = tagFg,
            Font = AppTheme.FontOf(8, FontStyle.Bold)
        };

        // Qty stepper + Add to cart (bottom)
        if (!_pendingQty.ContainsKey(p.ProductId))
            _pendingQty[p.ProductId] = 0;

        var stepperRow = new Panel
        {
            Location = new Point(12, 228),
            Size = new Size(186, 36),
            BackColor = Color.Transparent
        };

        var btnMinus = new Button
        {
            Text = "−",
            Location = new Point(0, 2),
            Size = new Size(32, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SurfaceRaised,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.FontOf(12, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnMinus.FlatAppearance.BorderSize = 0;
        btnMinus.Click += (s, e) =>
        {
            if (_pendingQty[p.ProductId] > 0)
            {
                _pendingQty[p.ProductId]--;
                RebuildCards();
            }
        };

        var lblQty = new Label
        {
            Text = _pendingQty[p.ProductId].ToString("0"),
            Location = new Point(32, 2),
            Size = new Size(28, 32),
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.FontOf(11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };

        var btnPlus = new Button
        {
            Text = "+",
            Location = new Point(60, 2),
            Size = new Size(32, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SurfaceRaised,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.FontOf(12, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnPlus.FlatAppearance.BorderSize = 0;
        btnPlus.Click += (s, e) =>
        {
            if (_pendingQty[p.ProductId] < p.QuantityOnHand)
            {
                _pendingQty[p.ProductId]++;
                RebuildCards();
            }
            else
                SetStatus($"Only {p.QuantityOnHand} available.", true);
        };

        var btnAdd = new Button
        {
            Text = "Add to cart",
            Location = new Point(100, 2),
            Size = new Size(86, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = p.QuantityOnHand <= 0 ? AppTheme.SurfaceRaised : AppTheme.Primary,
            ForeColor = p.QuantityOnHand <= 0 ? AppTheme.TextMuted : Color.White,
            Font = AppTheme.FontOf(8.5f, FontStyle.Bold),
            Cursor = p.QuantityOnHand <= 0 ? Cursors.Default : Cursors.Hand,
            Enabled = p.QuantityOnHand > 0
        };
        btnAdd.FlatAppearance.BorderSize = 0;
        if (p.QuantityOnHand > 0)
        {
            btnAdd.Click += (s, e) =>
            {
                var qty = _pendingQty.GetValueOrDefault(p.ProductId, 0);
                if (qty <= 0) qty = 1;
                AddToCart(p, qty);
                _pendingQty[p.ProductId] = 0;
                RebuildCards();
            };
        }

        stepperRow.Controls.Add(btnMinus);
        stepperRow.Controls.Add(lblQty);
        stepperRow.Controls.Add(btnPlus);
        stepperRow.Controls.Add(btnAdd);

        card.Controls.Add(img);
        card.Controls.Add(lblName);
        card.Controls.Add(lblPrice);
        card.Controls.Add(tag);
        card.Controls.Add(stepperRow);
        return card;
    }

    // ── Cart Details panel ──────────────────────────────────────────────
    private Panel BuildCartPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(18)
        };
        panel.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 14);
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawPath(pen, path);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));    // title row
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // items (fill remaining)
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));    // clear-all row
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));   // summary
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));    // proceed button

        // ── Title row: Cart Details | item count ─────────────────────
        var titleRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        titleRow.Controls.Add(new Label
        {
            Text = "Cart Details",
            Font = AppTheme.FontHeading,
            ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _lblCartCount = new Label
        {
            Text = "0 items",
            Font = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        titleRow.Controls.Add(_lblCartCount, 1, 0);

        layout.Controls.Add(titleRow, 0, 0);

        // ── Items ────────────────────────────────────────────────────
        var cartHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
        _cartItems = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        cartHost.Controls.Add(_cartItems);
        layout.Controls.Add(cartHost, 0, 1);

        // ── Clear all items ──────────────────────────────────────────
        var btnClear = new LinkLabel
        {
            Text = "Clear all items",
            LinkColor = AppTheme.Primary,
            ActiveLinkColor = AppTheme.PrimaryHover,
            Font = AppTheme.FontSmall,
            AutoSize = true,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 6, 0, 0)
        };
        btnClear.Click += (s, e) =>
        {
            _cart.Clear();
            RefreshCartUI();
        };
        var clearWrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        clearWrap.Controls.Add(btnClear);
        layout.Controls.Add(clearWrap, 0, 2);

        // ── Summary ──────────────────────────────────────────────────
        layout.Controls.Add(BuildSummaryPanel(), 0, 3);

        // ── Proceed payment button ───────────────────────────────────
        _btnPlaceOrder = AppTheme.MakePrimaryButton("Proceed payment", 0, 46);
        _btnPlaceOrder.Dock = DockStyle.Fill;
        _btnPlaceOrder.Font = AppTheme.FontOf(11, FontStyle.Bold);
        _btnPlaceOrder.Click += async (s, e) => await PlaceOrderAsync();
        layout.Controls.Add(_btnPlaceOrder, 0, 4);

        panel.Controls.Add(layout);
        return panel;
    }

    private void RefreshCartUI()
    {
        _cartItems.SuspendLayout();
        _cartItems.Controls.Clear();

        foreach (var line in _cart)
        {
            var row = new Panel
            {
                Width = Math.Max(260, 300),
                Height = 64,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = AppTheme.InputBg
            };
            row.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, row.Width - 1, row.Height - 1), 8);
                using var pen = new Pen(AppTheme.BorderLight, 1);
                e.Graphics.DrawPath(pen, path);
            };

            // Mini avatar
            var av = new Panel
            {
                Location = new Point(8, 12),
                Size = new Size(40, 40),
                BackColor = Color.Transparent
            };
            var letter = string.IsNullOrEmpty(line.ProductName) ? "?" : line.ProductName[0].ToString().ToUpper();
            av.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(AppTheme.SidebarActive);
                e.Graphics.FillEllipse(brush, 0, 0, 38, 38);
                using var font = AppTheme.FontOf(12, FontStyle.Bold);
                using var tb = new SolidBrush(AppTheme.Primary);
                var sz = e.Graphics.MeasureString(letter, font);
                e.Graphics.DrawString(letter, font, tb, (38 - sz.Width) / 2, (38 - sz.Height) / 2);
            };

            var name = new Label
            {
                Text = line.ProductName.Length > 20 ? line.ProductName[..20] + "…" : line.ProductName,
                Location = new Point(56, 8),
                Size = new Size(140, 20),
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.FontOf(9, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            var price = new Label
            {
                Text = $"₱{line.UnitPrice:N2}",
                Location = new Point(56, 30),
                Size = new Size(80, 18),
                ForeColor = AppTheme.TextMuted,
                Font = AppTheme.FontSmall,
                BackColor = Color.Transparent
            };

            // Inline stepper
            var btnM = new Button
            {
                Text = "−",
                Location = new Point(200, 16),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.SurfaceRaised,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.FontOf(10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnM.FlatAppearance.BorderSize = 0;
            var pid = line.ProductId;
            btnM.Click += (s, e) => ChangeQty(pid, -1);

            var qLbl = new Label
            {
                Text = line.Quantity.ToString("0"),
                Location = new Point(226, 16),
                Size = new Size(24, 26),
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.FontOf(10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var btnP = new Button
            {
                Text = "+",
                Location = new Point(250, 16),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.SurfaceRaised,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.FontOf(10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnP.FlatAppearance.BorderSize = 0;
            btnP.Click += (s, e) => ChangeQty(pid, 1);

            row.Controls.Add(av);
            row.Controls.Add(name);
            row.Controls.Add(price);
            row.Controls.Add(btnM);
            row.Controls.Add(qLbl);
            row.Controls.Add(btnP);
            _cartItems.Controls.Add(row);
        }

        _cartItems.ResumeLayout();
        _lblCartCount.Text = $"{_cart.Sum(c => c.Quantity):0} items";
        RecomputeTotals();
    }

    private Panel BuildSummaryPanel()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));    // subtotal
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));    // discount
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));    // discount chips
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));    // total
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));    // payment chips
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // amount received + change

        // ── Helper: 2-column money row ──
        TableLayoutPanel MoneyRow(string label, out Label valueLabel, bool bold = false)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            row.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                ForeColor = bold ? AppTheme.TextPrimary : AppTheme.TextSecondary,
                Font = bold ? AppTheme.FontOf(12, FontStyle.Bold) : AppTheme.FontBody,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            valueLabel = new Label
            {
                Text = "₱0.00",
                Dock = DockStyle.Fill,
                ForeColor = AppTheme.TextPrimary,
                Font = bold ? AppTheme.FontOf(15, FontStyle.Bold) : AppTheme.FontBody,
                TextAlign = ContentAlignment.MiddleRight
            };
            row.Controls.Add(valueLabel, 1, 0);
            return row;
        }

        // ── Row 0 & 1: subtotal + discount ──
        root.Controls.Add(MoneyRow("Sub total", out _lblSubtotal), 0, 0);
        root.Controls.Add(MoneyRow("Discount", out _lblDiscount), 0, 1);

        // ── Row 2: discount chips ──
        var discRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 6, 0, 6)
        };
        _btnNone = MakeToggle("None", true);
        _btnSenior = MakeToggle("Senior", false);
        _btnPwd = MakeToggle("PWD", false);
        _btnNone.Click += (s, e) => SetDiscount("None");
        _btnSenior.Click += (s, e) => SetDiscount("Senior");
        _btnPwd.Click += (s, e) => SetDiscount("PWD");
        discRow.Controls.AddRange(new Control[] { _btnNone, _btnSenior, _btnPwd });
        root.Controls.Add(discRow, 0, 2);

        // ── Row 3: total amount (big) ──
        root.Controls.Add(MoneyRow("Total amount", out _lblTotal, bold: true), 0, 3);

        // ── Row 4: payment chips ──
        var payChips = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 0, 2)
        };
        _btnCash = MakeToggle("Cash", true);
        _btnGCash = MakeToggle("GCash", false);
        _btnCash.Click += (s, e) => SetPayment("Cash");
        _btnGCash.Click += (s, e) => SetPayment("GCash");
        payChips.Controls.AddRange(new Control[] { _btnCash, _btnGCash });
        root.Controls.Add(payChips, 0, 4);

        // ── Row 5: amount received + change (hidden on GCash) ──
        _pnlCashFields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        _pnlCashFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _pnlCashFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _pnlCashFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        _pnlCashFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Row 0: labels
        _pnlCashFields.Controls.Add(new Label
        {
            Text = "Amount received",
            Dock = DockStyle.Fill,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontSmall,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _pnlCashFields.Controls.Add(new Label
        {
            Text = "Change",
            Dock = DockStyle.Fill,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontSmall,
            TextAlign = ContentAlignment.MiddleRight
        }, 1, 0);

        // Row 1: input + change value
        _txtAmountPaid = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = AppTheme.FontOf(11),
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 2, 8, 4)
        };
        _txtAmountPaid.TextChanged += (s, e) => RecomputeTotals();
        _pnlCashFields.Controls.Add(_txtAmountPaid, 0, 1);

        _lblChange = new Label
        {
            Text = "₱0.00",
            Dock = DockStyle.Fill,
            ForeColor = AppTheme.Success,
            Font = AppTheme.FontOf(13, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        _pnlCashFields.Controls.Add(_lblChange, 1, 1);

        root.Controls.Add(_pnlCashFields, 0, 5);

        return root;
    }

    private Button MakeToggle(string text, bool active)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(88, 34),
            FlatStyle = FlatStyle.Flat,
            Font = AppTheme.FontOf(9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        ApplyToggle(btn, active);
        return btn;
    }

    private void ApplyToggle(Button btn, bool active)
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

    private void SetDiscount(string type)
    {
        _discountType = type;
        ApplyToggle(_btnNone, type == "None");
        ApplyToggle(_btnSenior, type == "Senior");
        ApplyToggle(_btnPwd, type == "PWD");
        RecomputeTotals();
    }

    private void SetPayment(string method)
    {
        _paymentMethod = method;
        ApplyToggle(_btnCash, method == "Cash");
        ApplyToggle(_btnGCash, method == "GCash");

        // Hide the entire "Amount received + Change" section for GCash
        _pnlCashFields.Visible = method == "Cash";

        RecomputeTotals();
    }

    // ── Cart logic ──────────────────────────────────────────────────────
    private void AddToCart(ProductRow product, decimal qty)
    {
        if (product.QuantityOnHand <= 0)
        {
            SetStatus($"Out of stock: {product.ProductName}", true);
            return;
        }
        var existing = _cart.FirstOrDefault(c => c.ProductId == product.ProductId);
        if (existing != null)
        {
            if (existing.Quantity + qty > product.QuantityOnHand)
            {
                SetStatus($"Only {product.QuantityOnHand} available.", true);
                return;
            }
            existing.Quantity += qty;
        }
        else
        {
            _cart.Add(new CartLine
            {
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                UnitPrice = product.UnitPrice,
                Quantity = qty
            });
        }
        RefreshCartUI();
        SetStatus($"Added {product.ProductName}");
    }

    private void ChangeQty(int productId, int delta)
    {
        var line = _cart.FirstOrDefault(c => c.ProductId == productId);
        if (line == null) return;

        var product = _products.FirstOrDefault(p => p.ProductId == productId);
        var newQty = line.Quantity + delta;

        if (newQty <= 0)
            _cart.Remove(line);
        else if (product != null && newQty > product.QuantityOnHand)
        {
            SetStatus($"Only {product.QuantityOnHand} available.", true);
            return;
        }
        else
            line.Quantity = newQty;

        RefreshCartUI();
    }

    private void RecomputeTotals()
    {
        if (_lblSubtotal == null) return;

        decimal subtotal = _cart.Sum(c => c.Quantity * c.UnitPrice);
        decimal discount = _discountType is "Senior" or "PWD"
            ? Math.Round(subtotal * SENIOR_PWD_RATE, 2) : 0m;
        decimal total = subtotal - discount;

        _lblSubtotal.Text = $"₱{subtotal:N2}";
        _lblDiscount.Text = discount > 0 ? $"-₱{discount:N2}" : "₱0.00";
        _lblTotal.Text = $"₱{total:N2}";

        if (_paymentMethod == "GCash")
        {
            _lblChange.Text = "—";
            _lblChange.ForeColor = AppTheme.TextMuted;
        }
        else
        {
            decimal paid = decimal.TryParse(_txtAmountPaid.Text, out var p) ? p : 0;
            if (paid < total)
            {
                _lblChange.Text = "Insufficient";
                _lblChange.ForeColor = AppTheme.Danger;
            }
            else
            {
                _lblChange.Text = $"₱{(paid - total):N2}";
                _lblChange.ForeColor = AppTheme.Success;
            }
        }
    }

    private async Task PlaceOrderAsync()
    {
        if (_cart.Count == 0)
        {
            SetStatus("Cart is empty.", true);
            return;
        }

        decimal subtotal = _cart.Sum(c => c.Quantity * c.UnitPrice);
        decimal discount = _discountType is "Senior" or "PWD"
            ? Math.Round(subtotal * SENIOR_PWD_RATE, 2) : 0m;
        decimal total = subtotal - discount;

        decimal amountPaid = total;
        if (_paymentMethod == "Cash")
        {
            if (!decimal.TryParse(_txtAmountPaid.Text, out amountPaid) || amountPaid < total)
            {
                SetStatus("Amount received is insufficient.", true);
                return;
            }
        }

        _btnPlaceOrder.Enabled = false;
        _btnPlaceOrder.Text = "Processing…";

        try
        {
            var payload = new
            {
                cashierId = 1,
                cashierName = "Cashier",
                discountType = _discountType,
                paymentMethod = _paymentMethod,
                amountPaid,
                items = _cart.Select(c => new { productId = c.ProductId, quantity = c.Quantity }).ToList()
            };

            var resp = await _http.PostAsJsonAsync($"tenant/{DefaultCompanyId}/sales", payload);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                SetStatus($"Sale failed: {body}", true);
                return;
            }

            var receipt = System.Text.Json.JsonSerializer.Deserialize<SaleReceipt>(
                body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (receipt != null)
            {
                ShowPaymentSuccess(receipt);
                SetStatus($"✓ Sale completed — {receipt.InvoiceNumber}");
            }

            _cart.Clear();
            _txtAmountPaid.Clear();
            SetDiscount("None");
            SetPayment("Cash");
            RefreshCartUI();
            await LoadProductsWithRetryAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", true);
        }
        finally
        {
            _btnPlaceOrder.Enabled = true;
            _btnPlaceOrder.Text = "Proceed payment";
        }
    }

    private void ShowPaymentSuccess(SaleReceipt r)
    {
        var metaRows = new List<(string Label, string Value)>
        {
            ("Order ID",       r.InvoiceNumber),
            ("Payment Method", r.PaymentMethod),
            ("Payment Time",   r.SaleDate.ToLocalTime().ToString("MM/dd/yyyy hh:mm tt"))
        };
        if (r.DiscountAmount > 0)
            metaRows.Add(("Discount", $"₱{r.DiscountAmount:N2} ({r.DiscountType})"));

        using var dlg = new Form
        {
            Text = "Payment Success",
            Size = new Size(460, 640),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = AppTheme.CardBackground,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Font,
            Padding = new Padding(0)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
            Padding = new Padding(32, 24, 32, 24)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));   // icon
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));    // title
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));    // amount
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // meta
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));   // buttons

        // ── 1) Big green check ──────────────────────────────────────
        var iconHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var iconCircle = new Panel { Size = new Size(80, 80), BackColor = Color.Transparent };
        iconHost.Controls.Add(iconCircle);
        iconHost.Resize += (s, e) =>
        {
            iconCircle.Left = (iconHost.Width - iconCircle.Width) / 2;
            iconCircle.Top = (iconHost.Height - iconCircle.Height) / 2;
        };
        iconCircle.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(AppTheme.Success);
            e.Graphics.FillEllipse(brush, 0, 0, 79, 79);
            using var font = AppTheme.FontOf(30, FontStyle.Bold);
            using var white = new SolidBrush(Color.White);
            var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            e.Graphics.DrawString("✓", font, white, new RectangleF(0, 0, 80, 80), fmt);
        };
        root.Controls.Add(iconHost, 0, 0);

        // ── 2) Title ────────────────────────────────────────────────
        root.Controls.Add(new Label
        {
            Text = "Payment Success!",
            Dock = DockStyle.Fill,
            Font = AppTheme.FontOf(18, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 1);

        // ── 3) Amount ───────────────────────────────────────────────
        root.Controls.Add(new Label
        {
            Text = $"₱{r.TotalAmount:N2}",
            Dock = DockStyle.Fill,
            Font = AppTheme.FontOf(28, FontStyle.Bold),
            ForeColor = AppTheme.Primary,
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 2);

        // ── 4) Meta rows ────────────────────────────────────────────
        var metaTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = metaRows.Count,
            BackColor = Color.Transparent,
            Padding = new Padding(8, 8, 8, 8)
        };
        metaTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        metaTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        for (int i = 0; i < metaRows.Count; i++)
        {
            metaTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            var (label, value) = metaRows[i];

            metaTable.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                ForeColor = AppTheme.TextMuted,
                Font = AppTheme.FontOf(10),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, i);

            metaTable.Controls.Add(new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.FontOf(10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true
            }, 1, i);
        }
        root.Controls.Add(metaTable, 0, 3);

        // ── 5) Buttons ──────────────────────────────────────────────
        var btnHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        btnHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        btnHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var btnNew = AppTheme.MakePrimaryButton("New Order", 0, 46);
        btnNew.Dock = DockStyle.Fill;
        btnNew.Click += (s, e) => dlg.Close();
        btnHost.Controls.Add(btnNew, 0, 0);

        var btnPrint = AppTheme.MakeGhostButton("🖨  Print Receipt", 0, 42);
        btnPrint.Dock = DockStyle.Fill;
        btnPrint.Click += (s, e) =>
        {
            dlg.Hide();
            ShowReceipt(r);
            dlg.Close();
        };
        btnHost.Controls.Add(btnPrint, 0, 1);
        root.Controls.Add(btnHost, 0, 4);

        dlg.Controls.Add(root);
        dlg.ShowDialog(FindForm());
    }

    private void ShowReceipt(SaleReceipt r)
    {
        using var dlg = new Form
        {
            Text = "Customer Receipt",
            Size = new Size(420, 620),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = AppTheme.CardBackground,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.FontMono,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Padding = new Padding(16)
        };

        rtb.AppendText("        ☀  CORE ERP\n");
        rtb.AppendText("      OFFICIAL RECEIPT\n");
        rtb.AppendText("================================\n");
        rtb.AppendText($"Invoice : {r.InvoiceNumber}\n");
        rtb.AppendText($"Date    : {r.SaleDate:yyyy-MM-dd HH:mm}\n");
        rtb.AppendText($"Cashier : {r.CashierName}\n");
        rtb.AppendText($"Payment : {r.PaymentMethod}\n");
        rtb.AppendText("--------------------------------\n");
        rtb.AppendText(string.Format("{0,-16} {1,4} {2,10}\n", "Item", "Qty", "Amount"));
        rtb.AppendText("--------------------------------\n");
        foreach (var line in r.Lines)
        {
            var name = line.ProductName.Length > 16 ? line.ProductName[..16] : line.ProductName;
            rtb.AppendText(string.Format("{0,-16} {1,4} {2,10:N2}\n", name, line.Quantity, line.Subtotal));
        }
        rtb.AppendText("--------------------------------\n");
        rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "Subtotal", r.Subtotal));
        if (r.DiscountAmount > 0)
            rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", $"Discount ({r.DiscountType})", -r.DiscountAmount));
        rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "TOTAL", r.TotalAmount));
        if (r.PaymentMethod == "Cash")
        {
            rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "Cash", r.AmountPaid));
            rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "Change", r.ChangeDue));
        }
        rtb.AppendText("================================\n\n");
        rtb.AppendText("     Thank you for your purchase!\n");

        var btnClose = AppTheme.MakePrimaryButton("Close Receipt", 0, 48);
        btnClose.Dock = DockStyle.Bottom;
        btnClose.Click += (s, e) => dlg.Close();

        dlg.Controls.Add(rtb);
        dlg.Controls.Add(btnClose);
        dlg.ShowDialog(FindForm());
    }

    private async Task LoadProductsWithRetryAsync()
    {
        const int maxAttempts = 10;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                SetStatus($"Loading products… ({attempt}/{maxAttempts})");
                var list = await _http.GetFromJsonAsync<List<ProductRow>>(
                    $"tenant/{DefaultCompanyId}/products") ?? new();
                _products = list;
                RebuildCards();
                SetStatus($"Catalog ready — {_products.Count} product(s).");
                return;
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                SetStatus($"API not ready, retrying… ({attempt}/{maxAttempts})");
                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed: {ex.Message}", true);
                return;
            }
        }
        SetStatus("API not reachable.", true);
    }

    private void SetStatus(string msg, bool error = false)
    {
        _lblStatus.Text = msg;
        _lblStatus.ForeColor = error ? AppTheme.Danger : AppTheme.TextMuted;
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
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

    public class ProductRow
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal ReorderLevel { get; set; }
        public bool IsLowStock { get; set; }
    }

    public class CartLine
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Subtotal => Quantity * UnitPrice;
    }

    public class SaleReceipt
    {
        public int SaleId { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public string CashierName { get; set; } = "";
        public string DiscountType { get; set; } = "";
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal ChangeDue { get; set; }
        public string PaymentMethod { get; set; } = "";
        public DateTime SaleDate { get; set; }
        public List<SaleReceiptLine> Lines { get; set; } = new();
    }

    public class SaleReceiptLine
    {
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }
}