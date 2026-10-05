using CoreErp.WinForms.Theme;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;

namespace CoreErp.WinForms.Views.Inventory;

/// <summary>
/// Inventory list with add / edit / archive / stock-adjust flows
/// styled after the logo (dark + orange) and modern card dialogs.
/// </summary>
public class InventoryListView : UserControl
{
    private const int DefaultCompanyId = 1;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5067/") };

    private DataGridView _grid = null!;
    private TextBox _txtSearch = null!;
    private Label _lblStatus = null!;
    private Label _lblCount = null!;
    private Label _lblLowStock = null!;
    private Label _lblTotalSku = null!;
    private Label _lblTotalValue = null!;

    private List<ProductRow> _allProducts = new();
    private ProductRow? _selected;

    public InventoryListView()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;
        BuildUI();
        _ = LoadProductsWithRetryAsync();
    }

    private void BuildUI()
    {
        // ── Header ──────────────────────────────────────────────────────
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = AppTheme.AppBackground
        };
        pnlHeader.Controls.Add(new Label
        {
            Text = "Inventory",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 4),
            AutoSize = true
        });
        pnlHeader.Controls.Add(new Label
        {
            Text = "Manage products, stock levels, reorder points · Add · Edit · Archive · Adjust",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(2, 40),
            AutoSize = true
        });

        // ── Layout: stats → toolbar → grid ──────────────────────────────
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.AppBackground
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ── Stat cards ──────────────────────────────────────────────────
        var pnlCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppTheme.AppBackground,
            Padding = new Padding(0, 8, 0, 4)
        };
        pnlCards.Controls.Add(CreateStatCard("SKUs", out _lblTotalSku));
        pnlCards.Controls.Add(CreateStatCard("Low Stock", out _lblLowStock));
        pnlCards.Controls.Add(CreateStatCard("Est. Value", out _lblTotalValue));

        // ── Toolbar ─────────────────────────────────────────────────────
        // ── Toolbar — FlowLayoutPanel so it never clips on narrow windows ──
        var pnlToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            BackColor = AppTheme.AppBackground,
            Padding = new Padding(0, 8, 0, 8)
        };

        _txtSearch = new TextBox
        {
            Width = 280,
            Height = 34,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "🔍  Search by code or name...",
            Margin = new Padding(0, 0, 12, 0)
        };
        _txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ApplyFilter(); }
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        var btnAdd = AppTheme.MakePrimaryButton("＋  Add Product", 140, 36);
        btnAdd.Margin = new Padding(0, 0, 8, 0);
        btnAdd.Click += (s, e) => OpenProductDialog(null);

        var btnEdit = AppTheme.MakeGhostButton("✏  Edit", 100, 36);
        btnEdit.Margin = new Padding(0, 0, 8, 0);
        btnEdit.Click += (s, e) =>
        {
            if (_selected == null) { SetStatus("Select a product first.", true); return; }
            OpenProductDialog(_selected);
        };

        var btnArchive = AppTheme.MakeGhostButton("🗄  Archive", 110, 36);
        btnArchive.ForeColor = AppTheme.Danger;
        btnArchive.Margin = new Padding(0, 0, 8, 0);
        btnArchive.Click += async (s, e) => await ArchiveProductAsync();

        var btnAdjust = AppTheme.MakeGhostButton("📦  Adjust Stock", 130, 36);
        btnAdjust.Margin = new Padding(0, 0, 8, 0);
        btnAdjust.Click += (s, e) =>
        {
            if (_selected == null) { SetStatus("Select a product first.", true); return; }
            OpenAdjustDialog(_selected);
        };

        var btnRefresh = AppTheme.MakeGhostButton("↻  Refresh", 100, 36);
        btnRefresh.Margin = new Padding(0, 0, 12, 0);
        btnRefresh.Click += async (s, e) => await LoadProductsWithRetryAsync();

        _lblCount = new Label
        {
            Text = "0 items",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };

        pnlToolbar.Controls.AddRange(new Control[]
        {
            _txtSearch, btnAdd, btnEdit, btnArchive, btnAdjust, btnRefresh, _lblCount
        });

        // ── Grid ────────────────────────────────────────────────────────
        _grid = new DataGridView();
        AppTheme.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Code", DataPropertyName = "ProductCode", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Product", DataPropertyName = "ProductName", FillWeight = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Unit Price", DataPropertyName = "UnitPrice", FillWeight = 12, DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "On Hand", DataPropertyName = "QuantityOnHand", FillWeight = 10, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reorder", DataPropertyName = "ReorderLevel", FillWeight = 10, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "UoM", DataPropertyName = "UnitOfMeasure", FillWeight = 8, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "StockStatus", FillWeight = 18, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

        _grid.CellFormatting += Grid_CellFormatting;
        _grid.SelectionChanged += (s, e) =>
        {
            _selected = _grid.CurrentRow?.DataBoundItem as ProductRow;
        };
        _grid.CellDoubleClick += (s, e) =>
        {
            if (_selected != null) OpenProductDialog(_selected);
        };

        // ── Status ──────────────────────────────────────────────────────
        _lblStatus = new Label
        {
            Text = "Ready.",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Dock = DockStyle.Bottom,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };

        layout.Controls.Add(pnlCards, 0, 0);
        layout.Controls.Add(pnlToolbar, 0, 1);
        layout.Controls.Add(_grid, 0, 2);

        Controls.Add(layout);
        Controls.Add(_lblStatus);
        Controls.Add(pnlHeader);
    }

    // ── Stat card ───────────────────────────────────────────────────────
    private static Panel CreateStatCard(string title, out Label valueLabel)
    {
        var card = new Panel
        {
            Width = 220,
            Height = 72,
            BackColor = AppTheme.CardBackground,
            Margin = new Padding(0, 0, 14, 0)
        };
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10);
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawPath(pen, path);
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 10, 16, 10),
            BackColor = Color.Transparent
        };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 55));

        var lblTitle = new Label
        {
            Text = title,
            ForeColor = AppTheme.TextSecondary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };
        valueLabel = new Label
        {
            Text = "—",
            ForeColor = AppTheme.Primary,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        };
        table.Controls.Add(lblTitle, 0, 0);
        table.Controls.Add(valueLabel, 0, 1);
        card.Controls.Add(table);
        return card;
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

    private void SetStatus(string msg, bool error = false)
    {
        if (_lblStatus == null) return;
        _lblStatus.Text = msg;
        _lblStatus.ForeColor = error ? AppTheme.Danger : AppTheme.TextMuted;
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Rows[e.RowIndex].DataBoundItem is not ProductRow row) return;

        var col = _grid.Columns[e.ColumnIndex].DataPropertyName;
        if (col == "StockStatus")
        {
            if (row.IsLowStock)
            {
                e.CellStyle.ForeColor = AppTheme.Danger;
                e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            }
            else
            {
                e.CellStyle.ForeColor = AppTheme.Success;
            }
        }
        else if (col == "QuantityOnHand" && row.IsLowStock)
        {
            e.CellStyle.ForeColor = AppTheme.Warning;
            e.CellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        }
    }

    // ── Data ────────────────────────────────────────────────────────────
    private async Task LoadProductsWithRetryAsync()
    {
        const int maxAttempts = 10;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                SetStatus($"Loading inventory… ({attempt}/{maxAttempts})");
                var list = await _http.GetFromJsonAsync<List<ProductRow>>(
                    $"tenant/{DefaultCompanyId}/products") ?? new();

                _allProducts = list;
                ApplyFilter();
                UpdateSummary();
                SetStatus($"Loaded {_allProducts.Count} product(s).");
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

    private void UpdateSummary()
    {
        _lblTotalSku.Text = _allProducts.Count.ToString("N0");
        var low = _allProducts.Count(p => p.IsLowStock);
        _lblLowStock.Text = low.ToString("N0");
        _lblLowStock.ForeColor = low > 0 ? AppTheme.Danger : AppTheme.Primary;
        var value = _allProducts.Sum(p => p.UnitPrice * p.QuantityOnHand);
        _lblTotalValue.Text = $"₱{value:N0}";
    }

    private void ApplyFilter()
    {
        var term = _txtSearch.Text.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allProducts
            : _allProducts.Where(p =>
                p.ProductCode.ToLowerInvariant().Contains(term) ||
                p.ProductName.ToLowerInvariant().Contains(term)).ToList();

        _grid.DataSource = null;
        _grid.DataSource = filtered;
        _lblCount.Text = $"{filtered.Count} item(s)";
        _selected = null;
    }

    // ── Add / Edit dialog ───────────────────────────────────────────────
    private void OpenProductDialog(ProductRow? existing)
    {
        bool isEdit = existing != null;

        using var dlg = new Form
        {
            Text = isEdit ? "Edit Product" : "Add Product",
            Size = new Size(460, isEdit ? 420 : 480),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = AppTheme.CardBackground,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };

        int y = 24;
        Label L(string t)
        {
            var lbl = new Label
            {
                Text = t,
                ForeColor = AppTheme.TextSecondary,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(28, y),
                AutoSize = true
            };
            dlg.Controls.Add(lbl);
            y += 22;
            return lbl;
        }
        TextBox T(string value, bool readOnly = false)
        {
            var tb = new TextBox
            {
                Location = new Point(28, y),
                Width = 380,
                Height = 32,
                Font = AppTheme.FontBody,
                BackColor = readOnly ? AppTheme.AppBackground : AppTheme.InputBg,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text = value,
                ReadOnly = readOnly
            };
            dlg.Controls.Add(tb);
            y += 44;
            return tb;
        }

        L(isEdit ? "Product code" : "Product name *");
        var txtCodeOrName = T(isEdit ? existing!.ProductCode : "", readOnly: isEdit);

        if (isEdit)
        {
            L("Product name *");
            var txtName = T(existing!.ProductName);
            L("Unit price (₱) *");
            var txtPrice = T(existing.UnitPrice.ToString("0.##"));
            L("Reorder level");
            var txtReorder = T(existing.ReorderLevel.ToString("0.##"));
            L("Unit of measure");
            var txtUom = T(existing.UnitOfMeasure);

            var btnSave = AppTheme.MakePrimaryButton("Save Changes", 380, 42);
            btnSave.Location = new Point(28, y + 8);
            btnSave.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Product name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!decimal.TryParse(txtPrice.Text, out var price) || price < 0)
                {
                    MessageBox.Show("Enter a valid unit price.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                decimal.TryParse(txtReorder.Text, out var reorder);

                try
                {
                    var payload = new
                    {
                        productCode = existing.ProductCode,
                        productName = txtName.Text.Trim(),
                        unitPrice = price,
                        unitsPerBox = existing.UnitsPerBox,
                        unitOfMeasure = string.IsNullOrWhiteSpace(txtUom.Text) ? "Piece" : txtUom.Text.Trim(),
                        reorderLevel = reorder
                    };
                    var resp = await _http.PutAsJsonAsync($"tenant/{DefaultCompanyId}/products/{existing.ProductId}", payload);
                    if (!resp.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Update failed: " + await resp.Content.ReadAsStringAsync(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    SetStatus("✓ Product updated.");
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                    await LoadProductsWithRetryAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            dlg.Controls.Add(btnSave);
        }
        else
        {
            // Add mode: name is first field already
            L("Unit price (₱) *");
            var txtPrice = T("");
            L("Initial stock");
            var txtStock = T("0");
            L("Reorder level");
            var txtReorder = T("5");
            L("Unit of measure");
            var txtUom = T("Piece");

            var btnCreate = AppTheme.MakePrimaryButton("＋  Create Product", 380, 42);
            btnCreate.Location = new Point(28, y + 8);
            btnCreate.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtCodeOrName.Text))
                {
                    MessageBox.Show("Product name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!decimal.TryParse(txtPrice.Text, out var price) || price < 0)
                {
                    MessageBox.Show("Enter a valid unit price.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                decimal.TryParse(txtStock.Text, out var initial);
                decimal.TryParse(txtReorder.Text, out var reorder);

                try
                {
                    var payload = new
                    {
                        productName = txtCodeOrName.Text.Trim(),
                        unitPrice = price,
                        unitsPerBox = 1,
                        unitOfMeasure = string.IsNullOrWhiteSpace(txtUom.Text) ? "Piece" : txtUom.Text.Trim(),
                        initialStock = initial,
                        reorderLevel = reorder
                    };
                    var resp = await _http.PostAsJsonAsync($"tenant/{DefaultCompanyId}/products", payload);
                    if (!resp.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Create failed: " + await resp.Content.ReadAsStringAsync(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    SetStatus("✓ Product added.");
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                    await LoadProductsWithRetryAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            dlg.Controls.Add(btnCreate);
        }

        dlg.ShowDialog(FindForm());
    }

    // ── Adjust stock dialog ─────────────────────────────────────────────
    private void OpenAdjustDialog(ProductRow row)
    {
        using var dlg = new Form
        {
            Text = "Adjust Stock",
            Size = new Size(420, 340),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = AppTheme.CardBackground,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        dlg.Controls.Add(new Label
        {
            Text = $"{row.ProductCode}  ·  {row.ProductName}",
            Font = AppTheme.FontHeading,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(24, 20),
            AutoSize = true
        });
        dlg.Controls.Add(new Label
        {
            Text = $"Current on hand:  {row.QuantityOnHand:N2}",
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontBody,
            Location = new Point(24, 50),
            AutoSize = true
        });

        dlg.Controls.Add(new Label
        {
            Text = "Quantity delta (+ receive / − issue)",
            ForeColor = AppTheme.TextSecondary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(24, 90),
            AutoSize = true
        });
        var txtDelta = new TextBox
        {
            Location = new Point(24, 114),
            Width = 360,
            Height = 32,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Text = "0"
        };
        dlg.Controls.Add(txtDelta);

        dlg.Controls.Add(new Label
        {
            Text = "Reorder level",
            ForeColor = AppTheme.TextSecondary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(24, 160),
            AutoSize = true
        });
        var txtReorder = new TextBox
        {
            Location = new Point(24, 184),
            Width = 360,
            Height = 32,
            Font = AppTheme.FontBody,
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Text = row.ReorderLevel.ToString("0.##")
        };
        dlg.Controls.Add(txtReorder);

        var btnApply = AppTheme.MakePrimaryButton("Apply Adjustment", 360, 42);
        btnApply.Location = new Point(24, 240);
        btnApply.Click += async (s, e) =>
        {
            if (!decimal.TryParse(txtDelta.Text, out var delta))
            {
                MessageBox.Show("Enter a valid quantity delta.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            decimal.TryParse(txtReorder.Text, out var reorder);

            try
            {
                var payload = new { productId = row.ProductId, quantityDelta = delta, reorderLevel = reorder };
                var resp = await _http.PostAsJsonAsync($"tenant/{DefaultCompanyId}/inventory/adjust", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Adjust failed: " + await resp.Content.ReadAsStringAsync(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                SetStatus("✓ Stock adjusted.");
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
                await LoadProductsWithRetryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        dlg.Controls.Add(btnApply);
        dlg.ShowDialog(FindForm());
    }

    // ── Archive (delete) ────────────────────────────────────────────────
    private async Task ArchiveProductAsync()
    {
        if (_selected == null)
        {
            SetStatus("Select a product first.", true);
            return;
        }

        var confirm = MessageBox.Show(
            $"Archive \"{_selected.ProductName}\" ({_selected.ProductCode})?\n\nThis removes the product and its inventory record.",
            "Archive Product",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            var resp = await _http.DeleteAsync($"tenant/{DefaultCompanyId}/products/{_selected.ProductId}");
            if (!resp.IsSuccessStatusCode)
            {
                SetStatus("Archive failed: " + await resp.Content.ReadAsStringAsync(), true);
                return;
            }
            SetStatus($"✓ Archived {_selected.ProductCode}.");
            _selected = null;
            await LoadProductsWithRetryAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", true);
        }
    }

    // ── DTO ─────────────────────────────────────────────────────────────
    public class ProductRow
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public int UnitsPerBox { get; set; } = 1;
        public string UnitOfMeasure { get; set; } = "Piece";
        public decimal QuantityOnHand { get; set; }
        public decimal ReorderLevel { get; set; }
        public bool IsLowStock { get; set; }
        public string StockStatus => IsLowStock ? "● Low stock" : "● In stock";
    }
}
