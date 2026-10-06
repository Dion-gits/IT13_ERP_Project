using CoreErp.WinForms.Theme;
using System.Net.Http.Json;

namespace CoreErp.WinForms.Views.POS;

public class PosView : UserControl
{
    private const int DefaultCompanyId = 1;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5067/") };

    private DataGridView _productGrid = null!;
    private DataGridView _cartGrid = null!;
    private TextBox _txtSearch = null!;
    private Label _lblSubtotal = null!;
    private Label _lblVat = null!;
    private Label _lblDiscount = null!;
    private Label _lblDiscountInfo = null!;
    private Label _lblTotal = null!;
    private Label _lblChange = null!;
    private Label _lblPaymentInfo = null!;
    private Label _lblStatus = null!;
    private TextBox _txtAmountPaid = null!;
    private Button _btnNoDiscount = null!;
    private Button _btnSenior = null!;
    private Button _btnPwd = null!;
    private Button _btnCash = null!;
    private Button _btnGcash = null!;
    private Button _btnPlaceOrder = null!;

    private List<ProductRow> _allProducts = new();
    private List<CartLine> _cart = new();
    private string _discountType = "None";
    private string _paymentMethod = "Cash";

    private string? _customerName;
    private string? _customerIdNumber;
    private string? _gcashReference;

    private const decimal SENIOR_PWD_RATE = 0.20m;
    private const decimal VAT_RATE = 0.12m;

    public PosView()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;
        BuildUI();
        _ = LoadProductsWithRetryAsync();
    }

    private void BuildUI()
    {
        // ═══════════════════════════════════════════════════════════════════
        // HEADER
        // ═══════════════════════════════════════════════════════════════════
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            BackColor = AppTheme.AppBackground
        };
        pnlHeader.Controls.Add(new Label
        {
            Text = "Point of Sale",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        });
        pnlHeader.Controls.Add(new Label
        {
            Text = "Create and process customer orders",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(2, 32),
            AutoSize = true
        });

        // ═══════════════════════════════════════════════════════════════════
        // STATUS BAR
        // ═══════════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════════
        // MAIN LAYOUT
        // ═══════════════════════════════════════════════════════════════════
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = AppTheme.AppBackground
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));

        // ═══════════════════════════════════════════════════════════════════
        // LEFT — PRODUCT CATALOG
        // ═══════════════════════════════════════════════════════════════════
        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = AppTheme.AppBackground,
            Padding = new Padding(0, 0, 10, 0)
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var pnlSearch = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(10)
        };
        pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 82));
        pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));

        _txtSearch = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11),
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "🔍  Scan or type product code / name — press Enter",
            Margin = new Padding(0, 0, 8, 0)
        };
        _txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                TryAddFirstMatch();
            }
        };

        var btnSearch = new Button
        {
            Text = "🔍  Search",
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = AppTheme.FontButton,
            Cursor = Cursors.Hand
        };
        btnSearch.FlatAppearance.BorderSize = 0;
        btnSearch.Click += (s, e) => ApplyFilter();

        pnlSearch.Controls.Add(_txtSearch, 0, 0);
        pnlSearch.Controls.Add(btnSearch, 1, 0);

        _productGrid = new DataGridView
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
            Cursor = Cursors.Hand,
            RowTemplate = { Height = 38 }
        };
        StyleGrid(_productGrid);
        _productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Code", DataPropertyName = "ProductCode", FillWeight = 18 });
        _productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "ProductName", FillWeight = 44 });
        _productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Price", DataPropertyName = "UnitPrice", FillWeight = 18, DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Stock", DataPropertyName = "QuantityOnHand", FillWeight = 20, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

        _productGrid.CellDoubleClick += (s, e) =>
        {
            if (_productGrid.CurrentRow?.DataBoundItem is ProductRow p) AddToCart(p, 1);
        };

        leftLayout.Controls.Add(pnlSearch, 0, 0);
        leftLayout.Controls.Add(_productGrid, 0, 1);

        // ═══════════════════════════════════════════════════════════════════
        // RIGHT — CART / CHECKOUT
        // ═══════════════════════════════════════════════════════════════════
        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 13,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(20)
        };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // 0 title
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 1 cart
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));   // 2 hint + remove
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));   // 3 discount toggles
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));   // 4 discount detail
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));   // 5 subtotal
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));   // 6 VAT
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));   // 7 discount amount
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // 8 total
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));   // 9 payment toggles
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));   // 10 payment detail
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));   // 11 amount paid + change
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));   // 12 place order

        // 0 — Title
        rightLayout.Controls.Add(new Label
        {
            Text = "🧾   Current Order",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        // 1 — Cart grid
        _cartGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = false,
            AllowUserToAddRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextPrimary,
            GridColor = AppTheme.BorderLight,
            BorderStyle = BorderStyle.None,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
            RowTemplate = { Height = 36 }
        };
        StyleGrid(_cartGrid);

        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Item",
            DataPropertyName = "ProductName",
            ReadOnly = true,
            FillWeight = 42
        });
        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Qty",
            DataPropertyName = "Quantity",
            ReadOnly = false,
            FillWeight = 14,
            DefaultCellStyle =
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                BackColor = AppTheme.InputBg,
                SelectionBackColor = AppTheme.SidebarActive,
                SelectionForeColor = AppTheme.Primary,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            }
        });
        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Price",
            DataPropertyName = "UnitPrice",
            ReadOnly = true,
            FillWeight = 20,
            DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Total",
            DataPropertyName = "Subtotal",
            ReadOnly = true,
            FillWeight = 26,
            DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight }
        });

        var cartMenu = new ContextMenuStrip();
        cartMenu.Items.Add("Remove item").Click += (s, e) =>
        {
            var i = _cartGrid.CurrentCell?.RowIndex ?? -1;
            if (i < 0 || i >= _cart.Count) return;
            _cart.RemoveAt(i);
            RefreshCart();
        };
        _cartGrid.ContextMenuStrip = cartMenu;

        _cartGrid.CellEndEdit += (s, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _cart.Count) return;
            if (_cartGrid.Columns[e.ColumnIndex].DataPropertyName != "Quantity") return;

            var raw = _cartGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString()?.Trim();

            if (!int.TryParse(raw, out int newQty) || newQty < 0)
            {
                SetStatus("Invalid quantity — enter a whole number ≥ 0.", true);
                RefreshCart();
                return;
            }

            if (newQty == 0)
            {
                _cart.RemoveAt(e.RowIndex);
                SetStatus("Item removed (qty set to 0).");
            }
            else
            {
                _cart[e.RowIndex].Quantity = newQty;
                SetStatus($"Quantity updated to {newQty}.");
            }

            RefreshCart();
        };

        rightLayout.Controls.Add(_cartGrid, 0, 1);

        // 2 — Hint + Remove
        var pnlCartActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppTheme.CardBackground
        };
        var lblHint = new Label
        {
            Text = "💡 Type qty in the Qty cell — set to 0 to remove",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            AutoSize = true,
            Margin = new Padding(0, 8, 12, 0)
        };
        var btnRemove = SmallButton("Remove Selected", 140, AppTheme.Danger);
        btnRemove.Click += (s, e) =>
        {
            var i = _cartGrid.CurrentCell?.RowIndex ?? -1;
            if (i < 0 || i >= _cart.Count) return;
            _cart.RemoveAt(i);
            RefreshCart();
        };
        pnlCartActions.Controls.AddRange(new Control[] { lblHint, btnRemove });
        rightLayout.Controls.Add(pnlCartActions, 0, 2);

        // 3 — Discount toggles
        var pnlDiscount = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppTheme.CardBackground
        };
        _btnNoDiscount = ToggleButton("No Discount", 118);
        _btnSenior = ToggleButton("Senior (20%)", 118);
        _btnPwd = ToggleButton("PWD (20%)", 118);
        _btnNoDiscount.Click += (s, e) => ClearDiscount();
        _btnSenior.Click += (s, e) => PromptDiscount("Senior");
        _btnPwd.Click += (s, e) => PromptDiscount("PWD");
        pnlDiscount.Controls.AddRange(new Control[] { _btnNoDiscount, _btnSenior, _btnPwd });
        rightLayout.Controls.Add(pnlDiscount, 0, 3);

        // 4 — Discount detail
        _lblDiscountInfo = new Label
        {
            Text = "",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };
        rightLayout.Controls.Add(_lblDiscountInfo, 0, 4);

        // 5 — Subtotal
        _lblSubtotal = new Label
        {
            Text = "Subtotal (VAT incl.):  ₱0.00",
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontBody,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        rightLayout.Controls.Add(_lblSubtotal, 0, 5);

        // 6 — VAT
        _lblVat = new Label
        {
            Text = "VAT (12% incl.):  ₱0.00",
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontBody,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        rightLayout.Controls.Add(_lblVat, 0, 6);

        // 7 — Discount amount
        _lblDiscount = new Label
        {
            Text = "Discount:  ₱0.00",
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontBody,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        rightLayout.Controls.Add(_lblDiscount, 0, 7);

        // 8 — Total
        _lblTotal = new Label
        {
            Text = "₱0.00",
            ForeColor = AppTheme.Success,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        rightLayout.Controls.Add(_lblTotal, 0, 8);

        // 9 — Payment toggles
        var pnlPayment = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppTheme.CardBackground
        };
        _btnCash = ToggleButton("💵  Cash", 100);
        _btnGcash = ToggleButton("📱  GCash", 100);
        _btnCash.Click += (s, e) => SetCashPayment();
        _btnGcash.Click += (s, e) => PromptGcashReference();
        pnlPayment.Controls.AddRange(new Control[] { _btnCash, _btnGcash });
        rightLayout.Controls.Add(pnlPayment, 0, 9);

        // 10 — Payment detail
        _lblPaymentInfo = new Label
        {
            Text = "",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };
        rightLayout.Controls.Add(_lblPaymentInfo, 0, 10);

        // 11 — Amount paid + change
        var pnlPay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = AppTheme.CardBackground
        };
        pnlPay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pnlPay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pnlPay.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        pnlPay.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        pnlPay.Controls.Add(new Label
        {
            Text = "Amount Received",
            ForeColor = AppTheme.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Dock = DockStyle.Fill
        }, 0, 0);
        pnlPay.Controls.Add(new Label
        {
            Text = "Change",
            ForeColor = AppTheme.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Dock = DockStyle.Fill
        }, 1, 0);

        _txtAmountPaid = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Right,
            Margin = new Padding(0, 0, 6, 0)
        };
        _txtAmountPaid.TextChanged += (s, e) => RecomputeTotals();
        pnlPay.Controls.Add(_txtAmountPaid, 0, 1);

        _lblChange = new Label
        {
            Text = "₱0.00",
            ForeColor = AppTheme.Success,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        pnlPay.Controls.Add(_lblChange, 1, 1);

        rightLayout.Controls.Add(pnlPay, 0, 11);

        // 12 — Place Order
        _btnPlaceOrder = new Button
        {
            Text = "✓   PLACE ORDER",
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Success,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnPlaceOrder.FlatAppearance.BorderSize = 0;
        _btnPlaceOrder.Click += async (s, e) => await PlaceOrderAsync();
        rightLayout.Controls.Add(_btnPlaceOrder, 0, 12);

        // Assemble
        mainLayout.Controls.Add(leftLayout, 0, 0);
        mainLayout.Controls.Add(rightLayout, 1, 0);

        Controls.Add(mainLayout);
        Controls.Add(_lblStatus);
        Controls.Add(pnlHeader);

        SetDiscount("None");
        SetPayment("Cash");
    }

    // ═══════════════════════════════════════════════════════════════════
    // DISCOUNT PROMPT
    // ═══════════════════════════════════════════════════════════════════
    private void PromptDiscount(string type)
    {
        using var dlg = new Form
        {
            Text = $"{type} Discount — Customer Details",
            Size = new Size(440, 340),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = AppTheme.CardBackground,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };

        var lblHead = new Label
        {
            Text = $"{type} Discount",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(28, 20),
            AutoSize = true
        };
        var lblSub = new Label
        {
            Text = "Please enter the customer's details to apply the 20% discount.",
            Font = AppTheme.FontSmall,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(30, 50),
            AutoSize = true
        };

        var lblName = new Label
        {
            Text = "Customer Name",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(30, 90),
            AutoSize = true
        };
        var txtName = new TextBox
        {
            Location = new Point(30, 112),
            Width = 380,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblId = new Label
        {
            Text = "ID Number (Senior / PWD)",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(30, 150),
            AutoSize = true
        };
        var txtId = new TextBox
        {
            Location = new Point(30, 172),
            Width = 380,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblErr = new Label
        {
            Text = "",
            ForeColor = AppTheme.Danger,
            Font = AppTheme.FontSmall,
            Location = new Point(30, 205),
            Size = new Size(380, 20)
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(30, 240),
            Size = new Size(140, 40),
            BackColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextSecondary,
            FlatStyle = FlatStyle.Flat,
            Font = AppTheme.FontButton,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = AppTheme.Border;
        btnCancel.Click += (s, e) => dlg.Close();

        var btnApply = new Button
        {
            Text = "Apply Discount",
            Location = new Point(180, 240),
            Size = new Size(230, 40),
            BackColor = AppTheme.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = AppTheme.FontButton,
            Cursor = Cursors.Hand
        };
        btnApply.FlatAppearance.BorderSize = 0;
        btnApply.Click += (s, e) =>
        {
            var name = txtName.Text.Trim();
            var id = txtId.Text.Trim();

            if (name.Length == 0) { lblErr.Text = "Customer name is required."; txtName.Focus(); return; }
            if (id.Length == 0) { lblErr.Text = "ID number is required."; txtId.Focus(); return; }

            _customerName = name;
            _customerIdNumber = id;
            SetDiscount(type);

            _lblDiscountInfo.Text = $"🎫 {type}: {name}  ·  ID {id}";
            _lblDiscountInfo.ForeColor = AppTheme.Primary;

            dlg.DialogResult = DialogResult.OK;
            dlg.Close();
        };

        txtName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; btnApply.PerformClick(); } };
        txtId.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; btnApply.PerformClick(); } };

        dlg.Controls.AddRange(new Control[] { lblHead, lblSub, lblName, txtName, lblId, txtId, lblErr, btnCancel, btnApply });
        dlg.Shown += (s, e) => txtName.Focus();

        if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
        {
            if (_discountType is "Senior" or "PWD")
                return;
            SetDiscount("None");
            _lblDiscountInfo.Text = "";
        }
    }

    private void ClearDiscount()
    {
        _customerName = null;
        _customerIdNumber = null;
        _lblDiscountInfo.Text = "";
        SetDiscount("None");
    }

    // ═══════════════════════════════════════════════════════════════════
    // GCASH REFERENCE PROMPT
    // ═══════════════════════════════════════════════════════════════════
    private void PromptGcashReference()
    {
        using var dlg = new Form
        {
            Text = "GCash Payment — Reference Number",
            Size = new Size(440, 300),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = AppTheme.CardBackground,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };

        var lblHead = new Label
        {
            Text = "📱  GCash Payment",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(28, 20),
            AutoSize = true
        };
        var lblSub = new Label
        {
            Text = "Enter the reference number from the customer's GCash receipt.",
            Font = AppTheme.FontSmall,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(30, 50),
            AutoSize = true
        };

        var lblRef = new Label
        {
            Text = "Reference Number",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(30, 90),
            AutoSize = true
        };
        var txtRef = new TextBox
        {
            Location = new Point(30, 112),
            Width = 380,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            CharacterCasing = CharacterCasing.Upper
        };

        var lblErr = new Label
        {
            Text = "",
            ForeColor = AppTheme.Danger,
            Font = AppTheme.FontSmall,
            Location = new Point(30, 148),
            Size = new Size(380, 20)
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(30, 190),
            Size = new Size(140, 40),
            BackColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextSecondary,
            FlatStyle = FlatStyle.Flat,
            Font = AppTheme.FontButton,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = AppTheme.Border;
        btnCancel.Click += (s, e) => dlg.Close();

        var btnOk = new Button
        {
            Text = "Confirm GCash",
            Location = new Point(180, 190),
            Size = new Size(230, 40),
            BackColor = AppTheme.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = AppTheme.FontButton,
            Cursor = Cursors.Hand
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (s, e) =>
        {
            var refNum = txtRef.Text.Trim();
            if (refNum.Length < 4) { lblErr.Text = "Enter a valid reference number (4+ characters)."; txtRef.Focus(); return; }

            _gcashReference = refNum;
            SetPayment("GCash");

            _lblPaymentInfo.Text = $"📱 GCash Ref: {refNum}";
            _lblPaymentInfo.ForeColor = AppTheme.Primary;

            dlg.DialogResult = DialogResult.OK;
            dlg.Close();
        };

        txtRef.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; btnOk.PerformClick(); } };

        dlg.Controls.AddRange(new Control[] { lblHead, lblSub, lblRef, txtRef, lblErr, btnCancel, btnOk });
        dlg.Shown += (s, e) => txtRef.Focus();

        if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
        {
            if (_paymentMethod == "GCash") return;
            SetPayment("Cash");
            _lblPaymentInfo.Text = "";
        }
    }

    private void SetCashPayment()
    {
        _gcashReference = null;
        _lblPaymentInfo.Text = "";
        SetPayment("Cash");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Toggle logic
    // ═══════════════════════════════════════════════════════════════════
    private void SetDiscount(string type)
    {
        _discountType = type;
        if (_btnNoDiscount == null || _btnSenior == null || _btnPwd == null) return;

        HighlightToggle(_btnNoDiscount, type == "None");
        HighlightToggle(_btnSenior, type == "Senior");
        HighlightToggle(_btnPwd, type == "PWD");
        RecomputeTotals();
    }

    private void SetPayment(string method)
    {
        _paymentMethod = method;
        if (_btnCash == null || _btnGcash == null || _txtAmountPaid == null) return;

        HighlightToggle(_btnCash, method == "Cash");
        HighlightToggle(_btnGcash, method == "GCash");

        _txtAmountPaid.Enabled = method == "Cash";
        if (method == "GCash") _txtAmountPaid.Text = "";

        RecomputeTotals();
    }

    private static void HighlightToggle(Button b, bool active)
    {
        b.BackColor = active ? AppTheme.Primary : AppTheme.AppBackground;
        b.ForeColor = active ? Color.White : AppTheme.TextSecondary;
        b.Tag = active;
    }

    private void SetStatus(string msg, bool error = false)
    {
        if (_lblStatus == null) return;
        _lblStatus.Text = msg;
        _lblStatus.ForeColor = error ? AppTheme.Danger : AppTheme.TextMuted;
    }

    private async Task LoadProductsWithRetryAsync()
    {
        const int maxAttempts = 10;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                SetStatus($"Connecting to API... ({attempt}/{maxAttempts})");
                var list = await _http.GetFromJsonAsync<List<ProductRow>>(
                    $"tenant/{DefaultCompanyId}/products") ?? new();
                _allProducts = list;
                ApplyFilter();
                SetStatus($"Ready. {list.Count} product(s) available.");
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
        SetStatus("API not reachable after 10 attempts. Is the API running?", true);
    }

    private void ApplyFilter()
    {
        var term = _txtSearch.Text.Trim().ToLower();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allProducts
            : _allProducts.Where(p =>
                p.ProductCode.ToLower().Contains(term) ||
                p.ProductName.ToLower().Contains(term)).ToList();

        _productGrid.DataSource = null;
        _productGrid.DataSource = filtered;
    }

    private void TryAddFirstMatch()
    {
        if (_productGrid.CurrentRow?.DataBoundItem is ProductRow p)
            AddToCart(p, 1);
        else if (_productGrid.Rows.Count > 0)
        {
            _productGrid.Rows[0].Selected = true;
            if (_productGrid.Rows[0].DataBoundItem is ProductRow first)
                AddToCart(first, 1);
        }
    }

    private void AddToCart(ProductRow product, decimal qty)
    {
        var existing = _cart.FirstOrDefault(c => c.ProductId == product.ProductId);
        if (existing != null) existing.Quantity += qty;
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
        RefreshCart();
    }

    private void RefreshCart()
    {
        _cartGrid.DataSource = null;
        _cartGrid.DataSource = _cart.ToList();
        RecomputeTotals();
    }

    private readonly record struct Totals(decimal Subtotal, decimal Vat, decimal VatRemoved, decimal Discount, decimal Total, bool VatExempt);

    private Totals CalcTotals()
    {
        decimal subtotal = _cart.Sum(c => c.Quantity * c.UnitPrice);

        if (_discountType is "Senior" or "PWD")
        {
            decimal net = Math.Round(subtotal / (1 + VAT_RATE), 2);
            decimal discount = Math.Round(net * SENIOR_PWD_RATE, 2);
            return new Totals(subtotal, 0m, subtotal - net, discount, net - discount, true);
        }

        decimal vatable = Math.Round(subtotal / (1 + VAT_RATE), 2);
        return new Totals(subtotal, subtotal - vatable, 0m, 0m, subtotal, false);
    }

    private void RecomputeTotals()
    {
        if (_lblSubtotal == null || _lblVat == null || _lblDiscount == null || _lblTotal == null || _lblChange == null) return;

        var calc = CalcTotals();
        decimal subtotal = calc.Subtotal;
        decimal discount = calc.Discount;
        decimal total = calc.Total;

        _lblSubtotal.Text = $"Subtotal (VAT incl.):  ₱{subtotal:N2}";
        _lblVat.Text = calc.VatExempt
            ? $"Less VAT (exempt):  -₱{calc.VatRemoved:N2}"
            : $"VAT (12% incl.):  ₱{calc.Vat:N2}";
        _lblDiscount.Text = discount > 0 ? $"Discount (20%):  -₱{discount:N2}" : "Discount:  ₱0.00";
        _lblTotal.Text = $"₱{total:N2}";

        if (_paymentMethod == "GCash")
        {
            _lblChange.Text = "—";
            _lblChange.ForeColor = AppTheme.TextMuted;
        }
        else
        {
            decimal paid = decimal.TryParse(_txtAmountPaid.Text, out var p) ? p : 0;
            decimal change = paid - total;
            if (paid < total)
            {
                _lblChange.Text = "Insufficient";
                _lblChange.ForeColor = AppTheme.Danger;
            }
            else
            {
                _lblChange.Text = $"₱{change:N2}";
                _lblChange.ForeColor = AppTheme.Success;
            }
        }
    }

    private async Task PlaceOrderAsync()
    {
        if (_cart.Count == 0) { SetStatus("Cart is empty.", true); return; }

        if (_discountType is "Senior" or "PWD" && (string.IsNullOrWhiteSpace(_customerName) || string.IsNullOrWhiteSpace(_customerIdNumber)))
        {
            SetStatus("Customer name and ID are required for Senior/PWD discount.", true);
            return;
        }

        if (_paymentMethod == "GCash" && string.IsNullOrWhiteSpace(_gcashReference))
        {
            SetStatus("GCash reference number is required.", true);
            return;
        }

        var calc = CalcTotals();
        decimal total = calc.Total;

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
        _btnPlaceOrder.Text = "Processing...";

        try
        {
            var payload = new
            {
                cashierId = 1,
                cashierName = "Cashier",
                discountType = _discountType,
                paymentMethod = _paymentMethod,
                amountPaid,
                customerName = _customerName,
                customerIdNumber = _customerIdNumber,
                paymentReference = _gcashReference,
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
                ShowReceipt(receipt);
                SetStatus($"✓ Sale completed — {receipt.InvoiceNumber}");
            }

            _cart.Clear();
            _txtAmountPaid.Clear();
            _customerName = null;
            _customerIdNumber = null;
            _gcashReference = null;
            _lblDiscountInfo.Text = "";
            _lblPaymentInfo.Text = "";
            SetDiscount("None");
            SetPayment("Cash");
            RefreshCart();
            await LoadProductsWithRetryAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", true);
        }
        finally
        {
            _btnPlaceOrder.Enabled = true;
            _btnPlaceOrder.Text = "✓   PLACE ORDER";
        }
    }

    private void ShowReceipt(SaleReceipt r)
    {
        using var dlg = new Form
        {
            Text = "Customer Receipt",
            Size = new Size(420, 720),
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
            ForeColor = Color.Black,
            Font = new Font("Consolas", 10),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Padding = new Padding(20)
        };

        rtb.AppendText($"        CORE ERP\n");
        rtb.AppendText($"    OFFICIAL RECEIPT\n");
        rtb.AppendText($"================================\n");
        rtb.AppendText($"Invoice : {r.InvoiceNumber}\n");
        rtb.AppendText($"Date    : {r.SaleDate:yyyy-MM-dd HH:mm}\n");
        rtb.AppendText($"Cashier : {r.CashierName}\n");

        if (!string.IsNullOrWhiteSpace(r.CustomerName))
            rtb.AppendText($"Name    : {r.CustomerName}\n");
        if (!string.IsNullOrWhiteSpace(r.CustomerIdNumber))
            rtb.AppendText($"ID No.  : {r.CustomerIdNumber}\n");

        rtb.AppendText($"Payment : {r.PaymentMethod}\n");
        if (!string.IsNullOrWhiteSpace(r.PaymentReference))
            rtb.AppendText($"Ref No. : {r.PaymentReference}\n");

        rtb.AppendText($"--------------------------------\n");
        rtb.AppendText(string.Format("{0,-16} {1,4} {2,10}\n", "Item", "Qty", "Amount"));
        rtb.AppendText($"--------------------------------\n");

        foreach (var line in r.Lines)
        {
            var name = line.ProductName.Length > 16 ? line.ProductName.Substring(0, 16) : line.ProductName;
            rtb.AppendText(string.Format("{0,-16} {1,4} {2,10:N2}\n", name, line.Quantity, line.Subtotal));
        }

        rtb.AppendText($"--------------------------------\n");
        rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "Subtotal", r.Subtotal));
        if (r.VatExemptSales > 0)
            rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "Less: VAT (12%)", -(r.Subtotal - r.VatExemptSales)));
        if (r.DiscountAmount > 0)
            rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", $"Discount ({r.DiscountType})", -r.DiscountAmount));
        rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "TOTAL", r.TotalAmount));

        if (r.PaymentMethod == "Cash")
        {
            rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "Cash", r.AmountPaid));
            rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "Change", r.ChangeDue));
        }

        rtb.AppendText($"--------------------------------\n");
        rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "VATable Sales", r.VatableSales));
        rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "VAT (12%)", r.VatAmount));
        rtb.AppendText(string.Format("{0,-20} {1,10:N2}\n", "VAT-Exempt Sales", r.VatExemptSales));

        rtb.AppendText($"================================\n\n");
        rtb.AppendText($"     Thank you for your purchase!\n");

        var btnClose = new Button
        {
            Text = "Close Receipt",
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = AppTheme.Success,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => dlg.Close();

        dlg.Controls.Add(rtb);
        dlg.Controls.Add(btnClose);
        dlg.ShowDialog();
    }

    // ─── Helpers ───
    private static void StyleGrid(DataGridView g)
    {
        g.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.AppBackground;
        g.ColumnHeadersDefaultCellStyle.ForeColor = AppTheme.TextSecondary;
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        g.ColumnHeadersHeight = 38;
        g.DefaultCellStyle.BackColor = AppTheme.CardBackground;
        g.DefaultCellStyle.ForeColor = AppTheme.TextPrimary;
        g.DefaultCellStyle.SelectionBackColor = AppTheme.SidebarActive;
        g.DefaultCellStyle.SelectionForeColor = AppTheme.Primary;
        g.DefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
    }

    private static Button SmallButton(string text, int width, Color bg)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(width, 32),
            BackColor = bg,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 6, 0)
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private static Button ToggleButton(string text, int width)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(width, 34),
            BackColor = AppTheme.AppBackground,
            ForeColor = AppTheme.TextSecondary,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 6, 0),
            Tag = false
        };
        b.FlatAppearance.BorderColor = AppTheme.Border;
        return b;
    }

    // ─── DTOs ───
    public class ProductRow
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal QuantityOnHand { get; set; }
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
        public string? CustomerName { get; set; }
        public string? CustomerIdNumber { get; set; }
        public string? PaymentReference { get; set; }
        public decimal VatableSales { get; set; }
        public decimal VatAmount { get; set; }
        public decimal VatExemptSales { get; set; }
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