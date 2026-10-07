using CoreErp.WinForms.Shell;
using CoreErp.WinForms.Theme;
using CoreErp.WinForms.Views.Auth;
using System.Net.Http.Json;

namespace CoreErp.WinForms.Views.Roles.SuperAdmin;

public class SuperAdminHomeView : UserControl
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5067/") };
    private readonly LoginResult _user;

    private Label _lblTenants = null!;
    private Label _lblActive = null!;
    private Label _lblDevices = null!;
    private Label _lblAdmins = null!;
    private FlowLayoutPanel _tenantList = null!;
    private Label _lblStatus = null!;

    public SuperAdminHomeView(LoginResult user)
    {
        _user = user;
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;
        BuildUI();
        _ = LoadAllAsync();
    }

    // ═══════════════════════════════════════════════════════════
    private void BuildUI()
    {
        // Header
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = AppTheme.AppBackground };
        pnlHeader.Controls.Add(new Label
        {
            Text = "Platform Overview",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        });
        pnlHeader.Controls.Add(new Label
        {
            Text = $"Master database · signed in as {_user.Email}",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(2, 36),
            AutoSize = true
        });

        // Stat strip
        var strip = new Panel
        {
            Dock = DockStyle.Top,
            Height = 96,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(1)
        };
        strip.Paint += (s, e) =>
        {
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, strip.Width - 1, strip.Height - 1);
        };
        strip.Resize += (s, e) => LayoutStats(strip);

        _lblTenants = StatValue();
        _lblActive = StatValue();
        _lblDevices = StatValue();
        _lblAdmins = StatValue();

        strip.Controls.AddRange(new Control[]
        {
            StatLabel("TOTAL TENANTS"),     _lblTenants,
            StatLabel("ACTIVE TENANTS"),    _lblActive,
            StatLabel("DEVICES"),           _lblDevices,
            StatLabel("PLATFORM ADMINS"),   _lblAdmins
        });

        // Spacer
        var spacer = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = AppTheme.AppBackground };

        // Tenant list card
        var card = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.CardBackground, Padding = new Padding(1) };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        card.Controls.Add(new Label
        {
            Text = "Tenants",
            Font = new Font("Segoe UI Semibold", 12f),
            ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(20, 10, 0, 0)
        });

        var header = new TenantTableHeader { Dock = DockStyle.Top, Height = 38 };

        _tenantList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(0)
        };
        _tenantList.ClientSizeChanged += (s, e) => FitRows();

        card.Controls.Add(_tenantList);
        card.Controls.Add(header);

        // Status bar
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

        Controls.Add(card);
        Controls.Add(spacer);
        Controls.Add(strip);
        Controls.Add(_lblStatus);
        Controls.Add(pnlHeader);
    }

    private void LayoutStats(Control strip)
    {
        // 4 columns, evenly split
        int cols = 4;
        int w = strip.ClientSize.Width / cols;
        int y = 20;

        for (int i = 0; i < cols; i++)
        {
            int x = i * w + 24;
            foreach (Control c in strip.Controls)
            {
                if (c.Tag is string tag && tag == $"label{i}")
                    c.Location = new Point(x, y);
                if (c.Tag is string tag2 && tag2 == $"value{i}")
                    c.Location = new Point(x, y + 28);
            }
        }
    }

    private Label StatLabel(string text) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        ForeColor = AppTheme.TextSecondary,
        AutoSize = true,
        Tag = "pending"
    };

    private Label StatValue() => new()
    {
        Text = "—",
        Font = new Font("Segoe UI Semibold", 22f),
        ForeColor = AppTheme.TextPrimary,
        AutoSize = true,
        Tag = "pending"
    };

    // Re-tag labels with column indexes
    private void TagStatColumns()
    {
        var controls = new List<Control>();
        controls.AddRange(new Control[] { _lblTenants, _lblActive, _lblDevices, _lblAdmins });
    }

    private bool _fitting;
    private void FitRows()
    {
        if (_fitting || _tenantList == null) return;
        _fitting = true;
        try
        {
            int w = _tenantList.ClientSize.Width;
            foreach (Control c in _tenantList.Controls)
                if (c.Width != w) c.Width = w;
        }
        finally { _fitting = false; }
    }

    // ═══════════════════════════════════════════════════════════
    private async Task LoadAllAsync()
    {
        try
        {
            _lblStatus.Text = "Loading master DB…";

            var overview = await _http.GetFromJsonAsync<OverviewDto>("superadmin/overview");
            if (overview != null)
            {
                _lblTenants.Text = overview.TotalTenants.ToString("N0");
                _lblActive.Text = overview.ActiveTenants.ToString("N0");
                _lblDevices.Text = overview.TotalDevices.ToString("N0");
                _lblAdmins.Text = overview.PlatformAdmins.ToString("N0");
            }

            var companies = await _http.GetFromJsonAsync<List<CompanyDto>>("superadmin/companies") ?? new();

            _tenantList.SuspendLayout();
            _tenantList.Controls.Clear();

            foreach (var c in companies)
            {
                _tenantList.Controls.Add(new TenantRow(c));
            }

            FitRows();
            _tenantList.ResumeLayout();

            _lblStatus.Text = $"{companies.Count} tenant(s) loaded from master DB.";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Failed: {ex.Message}";
            _lblStatus.ForeColor = AppTheme.Danger;
        }
    }

    // ═══════════════════════════════════════════════════════════
    private sealed class TenantTableHeader : Control
    {
        private static readonly string[] Titles = { "ID", "CODE", "NAME", "DATABASE", "SERVER", "DEVICES", "STATUS" };
        private static readonly float[] Weights = { 5, 12, 24, 20, 20, 9, 10 };
        private static readonly Font F = new("Segoe UI Semibold", 8.5f);

        public TenantTableHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(251, 251, 250);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            int left = 20;
            int usable = Width - left - 20;
            float total = Weights.Sum();
            int x = left;

            for (int i = 0; i < Titles.Length; i++)
            {
                int w = (int)(usable * Weights[i] / total);
                var rect = new Rectangle(x, 0, w - 6, Height);
                TextRenderer.DrawText(g, Titles[i], F, rect, AppTheme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                x += w;
            }

            using var pen = new Pen(AppTheme.Border, 1);
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    private sealed class TenantRow : Control
    {
        private static readonly float[] Weights = { 5, 12, 24, 20, 20, 9, 10 };
        private static readonly Font F = AppTheme.FontBody;
        private static readonly Font Mono = AppTheme.FontMono;
        private static readonly Font Bold = new("Segoe UI Semibold", 9.5f);
        private static readonly Font Pill = new("Segoe UI Semibold", 8.5f);

        private readonly CompanyDto _c;
        private bool _hover;

        public TenantRow(CompanyDto c)
        {
            _c = c;
            Height = 56;
            Margin = Padding.Empty;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = AppTheme.CardBackground;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_hover ? Color.FromArgb(250, 250, 249) : AppTheme.CardBackground);

            int left = 20;
            int usable = Width - left - 20;
            float total = Weights.Sum();
            int x = left;

            var db = _c.Databases.FirstOrDefault();
            string dbName = db?.DatabaseName ?? "—";
            string server = db?.ServerName ?? "—";

            DrawCell(g, x, Weights[0], total, usable, _c.CompanyId.ToString(), Mono, AppTheme.TextSecondary);
            x += (int)(usable * Weights[0] / total);
            DrawCell(g, x, Weights[1], total, usable, _c.CompanyCode, Mono, AppTheme.TextSecondary);
            x += (int)(usable * Weights[1] / total);
            DrawCell(g, x, Weights[2], total, usable, _c.CompanyName, Bold, AppTheme.TextPrimary);
            x += (int)(usable * Weights[2] / total);
            DrawCell(g, x, Weights[3], total, usable, dbName, F, AppTheme.TextPrimary);
            x += (int)(usable * Weights[3] / total);
            DrawCell(g, x, Weights[4], total, usable, server, F, AppTheme.TextSecondary);
            x += (int)(usable * Weights[4] / total);
            DrawCell(g, x, Weights[5], total, usable, _c.DeviceCount.ToString(), F, AppTheme.TextSecondary);
            x += (int)(usable * Weights[5] / total);

            // Status pill
            int w = (int)(usable * Weights[6] / total);
            DrawPill(g, new Rectangle(x, (Height - 22) / 2, Math.Min(76, w - 6), 22),
                _c.IsActive ? "Active" : "Inactive",
                _c.IsActive ? AppTheme.TextSecondary : AppTheme.Danger,
                _c.IsActive ? Color.FromArgb(244, 244, 245) : Color.FromArgb(254, 236, 236));

            using var pen = new Pen(AppTheme.BorderLight, 1);
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }

        private static void DrawCell(Graphics g, int x, float weight, float total, int usable,
            string text, Font font, Color color)
        {
            int w = (int)(usable * weight / total);
            var rect = new Rectangle(x, 0, w - 6, 56);
            TextRenderer.DrawText(g, text, font, rect, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        private static void DrawPill(Graphics g, Rectangle r, string text, Color fg, Color fill)
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);

            TextRenderer.DrawText(g, text, Pill, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    // ═══════════════════════════════════════════════════════════
    private class OverviewDto
    {
        public int TotalTenants { get; set; }
        public int ActiveTenants { get; set; }
        public int TotalDevices { get; set; }
        public int PlatformAdmins { get; set; }
    }

    private class CompanyDto
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string? ContactEmail { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<DatabaseDto> Databases { get; set; } = new();
        public int DeviceCount { get; set; }
        public int ActiveDeviceCount { get; set; }
    }

    private class DatabaseDto
    {
        public int CompanyDatabaseId { get; set; }
        public string ServerName { get; set; } = "";
        public string DatabaseName { get; set; } = "";
        public string CredentialKey { get; set; } = "";
        public bool IsActive { get; set; }
    }
}