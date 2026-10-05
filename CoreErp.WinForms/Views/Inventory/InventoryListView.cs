using CoreErp.WinForms.Theme;
using System.Net.Http.Json;

namespace CoreErp.WinForms.Views.Inventory;

public class InventoryListView : UserControl
{
    // TODO: will come from login later
    private const int DefaultCompanyId = 1;

    // TODO: change to your API URL if different
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5067/") };

    private DataGridView _grid = null!;
    private TextBox _txtName = null!;
    private TextBox _txtPrice = null!;
    private TextBox _txtReorder = null!;
    private TextBox _txtInitialStock = null!;
    private TextBox _txtSearch = null!;
    private Label _lblStatus = null!;

    private List<ProductRow> _allProducts = new();

    public InventoryListView()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;
        BuildUI();
        _ = LoadProductsAsync();
    }

    private void BuildUI()
    {
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = AppTheme.AppBackground };
        pnlHeader.Controls.Add(new Label
        {
            Text = "Inventory Management",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        });
        pnlHeader.Controls.Add(new Label
        {
            Text = "View and manage products, stock levels, and reorder points",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(2, 36),
            AutoSize = true
        });

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.AppBackground
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        // Search
        var pnlSearch = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.AppBackground };
        _txtSearch = new TextBox
        {
            Location = new Point(0, 8),
            Width = 350,
            Height = 32,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "🔍  Search by code or name..."
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();
        pnlSearch.Controls.Add(_txtSearch);

        var btnRefresh = new Button
        {
            Text = "↻  Refresh",
            Location = new Point(365, 6),
            Size = new Size(120, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.CardBackground,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.FontButton,
            Cursor = Cursors.Hand
        };
        btnRefresh.FlatAppearance.BorderColor = AppTheme.Border;
        btnRefresh.Click += async (s, e) => await LoadProductsAsync();
        pnlSearch.Controls.Add(btnRefresh);

        // Grid
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
            RowTemplate = { Height = 36 }
        };
        StyleGrid(_grid);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = "ProductId", FillWeight = 6 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Code", DataPropertyName = "ProductCode", FillWeight = 14 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "ProductName", FillWeight = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Price", DataPropertyName = "UnitPrice", FillWeight = 12, DefaultCellStyle = { Format = "C2" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Stock", DataPropertyName = "QuantityOnHand", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reorder", DataPropertyName = "ReorderLevel", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "StockStatus", FillWeight = 14 });

        _grid.CellClick += (s, e) => LoadSelectedIntoForm();
        _grid.CellFormatting += Grid_CellFormatting;

        // Form
        var pnlForm = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(24)
        };
        pnlForm.Paint += (s, e) =>
        {
            using var p = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawRectangle(p, 0, 0, pnlForm.Width - 1, pnlForm.Height - 1);
        };

        int x = 24, y = 24;
        (_txtName, _) = AddField(pnlForm, "Product Name", x, y, 400);
        (_txtPrice, _) = AddField(pnlForm, "Unit Price (₱)", x + 420, y, 140);
        (_txtReorder, _) = AddField(pnlForm, "Reorder Level", x + 580, y, 140);
        (_txtInitialStock, _) = AddField(pnlForm, "Stock (or set new target)", x, y + 80, 200);

        int btnY = y + 155;
        var btnAdd = CreateButton("➕  Add Product", x, btnY, AppTheme.Success);
        btnAdd.Click += async (s, e) => await CreateProductAsync();

        var btnUpdate = CreateButton("✏  Update", x + 170, btnY, AppTheme.Primary);
        btnUpdate.Click += async (s, e) => await UpdateProductAsync();

        var btnDelete = CreateButton("🗑  Delete", x + 340, btnY, AppTheme.Danger);
        btnDelete.Click += async (s, e) => await DeleteProductAsync();

        var btnClear = CreateButton("✖  Clear Form", x + 510, btnY, AppTheme.TextSecondary);
        btnClear.Click += (s, e) => ClearForm();

        var btnAdjust = CreateButton("📦  Set Stock", x + 680, btnY, AppTheme.Warning);
        btnAdjust.Click += async (s, e) => await AdjustStockAsync();

        _lblStatus = new Label
        {
            Text = "",
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontSmall,
            Location = new Point(x, btnY + 55),
            AutoSize = true
        };

        pnlForm.Controls.AddRange(new Control[] { btnAdd, btnUpdate, btnDelete, btnClear, btnAdjust, _lblStatus });

        layout.Controls.Add(pnlSearch, 0, 0);
        layout.Controls.Add(_grid, 0, 1);
        layout.Controls.Add(pnlForm, 0, 2);

        Controls.Add(layout);
        Controls.Add(pnlHeader);
    }

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

    private static (TextBox, int) AddField(Control parent, string label, int x, int y, int width)
    {
        var lbl = new Label
        {
            Text = label,
            ForeColor = AppTheme.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(x, y),
            AutoSize = true
        };
        var txt = new TextBox
        {
            Location = new Point(x, y + 22),
            Width = width,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };
        parent.Controls.Add(lbl);
        parent.Controls.Add(txt);
        return (txt, x + width);
    }

    private static Button CreateButton(string text, int x, int y, Color bg)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(160, 40),
            BackColor = bg,
            ForeColor = Color.White,
            Font = AppTheme.FontButton,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Rows[e.RowIndex].DataBoundItem is not ProductRow row) return;

        if (_grid.Columns[e.ColumnIndex].DataPropertyName == "QuantityOnHand")
        {
            if (row.IsLowStock)
            {
                e.CellStyle.ForeColor = AppTheme.Danger;
                e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            }
        }
    }

    private void ApplyFilter()
    {
        var term = _txtSearch.Text.Trim().ToLower();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allProducts
            : _allProducts.Where(p =>
                p.ProductCode.ToLower().Contains(term) ||
                p.ProductName.ToLower().Contains(term)).ToList();

        _grid.DataSource = null;
        _grid.DataSource = filtered;
    }

    private void LoadSelectedIntoForm()
    {
        if (_grid.CurrentRow?.DataBoundItem is not ProductRow row) return;

        _txtName.Text = row.ProductName;
        _txtPrice.Text = row.UnitPrice.ToString("F2");
        _txtReorder.Text = row.ReorderLevel.ToString("F2");
        _txtInitialStock.Text = row.QuantityOnHand.ToString("F2");
    }

    private void ClearForm()
{
    _txtName.Clear();
    _txtPrice.Clear();
    _txtReorder.Text = "5";
    _txtInitialStock.Text = "0";
    SetStatus("");
}

    private void SetStatus(string message, bool isError = false)
    {
        _lblStatus.Text = message;
        _lblStatus.ForeColor = isError ? AppTheme.Danger : AppTheme.Success;
    }

    private async Task LoadProductsAsync()
    {
        const int maxAttempts = 10;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                SetStatus($"Connecting to API... (attempt {attempt}/{maxAttempts})");

                var list = await _http.GetFromJsonAsync<List<ProductRow>>(
                    $"tenant/{DefaultCompanyId}/products") ?? new();

                _allProducts = list;
                ApplyFilter();
                SetStatus($"Loaded {list.Count} product(s).");
                return;
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                SetStatus($"API not ready yet, retrying in 1.5s... ({attempt}/{maxAttempts})");
                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load: {ex.Message}", true);
                return;
            }
        }

        SetStatus($"Failed to load: API not reachable after {maxAttempts} attempts. Is the API running?", true);
    }

    private async Task CreateProductAsync()
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            SetStatus("Product name is required.", true);
            return;
        }
        if (!decimal.TryParse(_txtPrice.Text, out var price))
        {
            SetStatus("Invalid price.", true);
            return;
        }
        decimal.TryParse(_txtReorder.Text, out var reorder);
        decimal.TryParse(_txtInitialStock.Text, out var initialStock);

        try
        {
            var payload = new
            {
                productName = _txtName.Text.Trim(),
                unitPrice = price,
                unitsPerBox = 1,
                unitOfMeasure = "Piece",
                initialStock,
                reorderLevel = reorder
            };

            var resp = await _http.PostAsJsonAsync($"tenant/{DefaultCompanyId}/products", payload);
            if (!resp.IsSuccessStatusCode)
            {
                SetStatus("Create failed: " + await resp.Content.ReadAsStringAsync(), true);
                return;
            }

            SetStatus("✓ Product added.");
            ClearForm();
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", true);
        }
    }

    private async Task UpdateProductAsync()
    {
        if (_grid.CurrentRow?.DataBoundItem is not ProductRow row)
        {
            SetStatus("Select a product first.", true);
            return;
        }
        if (!decimal.TryParse(_txtPrice.Text, out var price)) return;
        decimal.TryParse(_txtReorder.Text, out var reorder);

        try
        {
            var payload = new
            {
                productCode = row.ProductCode,  // preserve existing code
                productName = _txtName.Text.Trim(),
                unitPrice = price,
                unitsPerBox = row.UnitsPerBox,
                unitOfMeasure = row.UnitOfMeasure,
                reorderLevel = reorder
            };

            var resp = await _http.PutAsJsonAsync($"tenant/{DefaultCompanyId}/products/{row.ProductId}", payload);
            if (!resp.IsSuccessStatusCode)
            {
                SetStatus("Update failed: " + await resp.Content.ReadAsStringAsync(), true);
                return;
            }

            SetStatus("✓ Product updated.");
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", true);
        }
    }

    private async Task DeleteProductAsync()
    {
        if (_grid.CurrentRow?.DataBoundItem is not ProductRow row) return;
        if (MessageBox.Show($"Delete '{row.ProductName}'?", "Confirm",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            var resp = await _http.DeleteAsync($"tenant/{DefaultCompanyId}/products/{row.ProductId}");
            if (!resp.IsSuccessStatusCode)
            {
                SetStatus("Delete failed.", true);
                return;
            }
            SetStatus("✓ Product deleted.");
            ClearForm();
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", true);
        }
    }

    private async Task AdjustStockAsync()
    {
        if (_grid.CurrentRow?.DataBoundItem is not ProductRow row)
        {
            SetStatus("Select a product first.", true);
            return;
        }
        if (!decimal.TryParse(_txtInitialStock.Text, out var newStock))
        {
            SetStatus("Enter target stock in 'Stock' field.", true);
            return;
        }
        decimal.TryParse(_txtReorder.Text, out var reorder);

        var delta = newStock - row.QuantityOnHand;

        try
        {
            var payload = new
            {
                productId = row.ProductId,
                quantityDelta = delta,
                reorderLevel = reorder
            };

            var resp = await _http.PostAsJsonAsync($"tenant/{DefaultCompanyId}/inventory/adjust", payload);
            if (!resp.IsSuccessStatusCode)
            {
                SetStatus("Adjust failed.", true);
                return;
            }
            SetStatus($"✓ Stock adjusted by {delta:+0;-0;0}.");
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", true);
        }
    }

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
        public string StockStatus => IsLowStock ? "⚠ Low" : "✓ OK";
    }
}