using CoreErp.WinForms.Theme;

namespace CoreErp.WinForms.Shell;

public class ErpShell : Form
{
    public class NavItem
    {
        public string Key { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Label { get; set; } = "";
        public Func<Control> ViewFactory { get; set; } = () => new Panel();
    }

    private readonly List<NavItem> _navItems;
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Panel _contentPanel;
    private readonly Panel _sidebarPanel;

    public ErpShell(string roleName, string userEmail, List<NavItem> navItems)
    {
        _navItems = navItems;

        Text = $"CORE ERP — {roleName}";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 800);
        BackColor = AppTheme.AppBackground;
        DoubleBuffered = true;
        Font = AppTheme.FontBody;

        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 240,
            BackColor = AppTheme.SidebarBg
        };
        _sidebarPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawLine(pen, _sidebarPanel.Width - 1, 0, _sidebarPanel.Width - 1, _sidebarPanel.Height);
        };

        BuildSidebar(roleName, userEmail);

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.AppBackground,
            Padding = new Padding(28, 24, 28, 20)
        };

        Controls.Add(_contentPanel);
        Controls.Add(_sidebarPanel);

        if (_navItems.Count > 0)
            NavigateTo(_navItems[0].Key);
    }

    private void BuildSidebar(string roleName, string userEmail)
    {
        // Logo mark (sun + CORE ERP) — matches D.CC brand block style
        var brandPanel = new Panel
        {
            Location = new Point(16, 16),
            Size = new Size(208, 48),
            BackColor = Color.Transparent
        };

        // Orange circle logo mark
        var logoMark = new Panel
        {
            Location = new Point(0, 4),
            Size = new Size(36, 36),
            BackColor = Color.Transparent
        };
        logoMark.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(AppTheme.Primary);
            e.Graphics.FillEllipse(brush, 0, 0, 34, 34);
            using var font = new Font("Segoe UI", 14, FontStyle.Bold);
            using var white = new SolidBrush(Color.White);
            var sz = e.Graphics.MeasureString("☀", font);
            e.Graphics.DrawString("☀", font, white, (34 - sz.Width) / 2, (34 - sz.Height) / 2 - 1);
        };

        var lblBrand = new Label
        {
            Text = "CORE ERP",
            ForeColor = AppTheme.TextPrimary,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Location = new Point(44, 4),
            AutoSize = true
        };
        var lblRole = new Label
        {
            Text = roleName,
            ForeColor = AppTheme.TextMuted,
            Font = new Font("Segoe UI", 8.5f),
            Location = new Point(44, 26),
            AutoSize = true
        };

        brandPanel.Controls.Add(logoMark);
        brandPanel.Controls.Add(lblBrand);
        brandPanel.Controls.Add(lblRole);
        _sidebarPanel.Controls.Add(brandPanel);

        // Divider
        var divider = new Panel
        {
            Location = new Point(16, 76),
            Size = new Size(208, 1),
            BackColor = AppTheme.Border
        };
        _sidebarPanel.Controls.Add(divider);

        // Section label
        var lblMenu = new Label
        {
            Text = "MENU",
            ForeColor = AppTheme.TextMuted,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Location = new Point(20, 90),
            AutoSize = true
        };
        _sidebarPanel.Controls.Add(lblMenu);

        int y = 112;
        foreach (var item in _navItems)
        {
            var btn = CreateNavButton(item);
            btn.Location = new Point(12, y);
            _sidebarPanel.Controls.Add(btn);
            _navButtons[item.Key] = btn;
            y += 48;
        }

        // User footer
        var userPanel = new Panel
        {
            Height = 64,
            Dock = DockStyle.Bottom,
            BackColor = AppTheme.SidebarBg,
            Padding = new Padding(16, 8, 16, 8)
        };
        userPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, userPanel.Width, 0);
        };

        var avatar = new Panel
        {
            Location = new Point(16, 14),
            Size = new Size(36, 36),
            BackColor = Color.Transparent
        };
        avatar.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(AppTheme.SidebarActive);
            e.Graphics.FillEllipse(brush, 0, 0, 34, 34);
            using var font = new Font("Segoe UI", 11, FontStyle.Bold);
            using var textBrush = new SolidBrush(AppTheme.Primary);
            var initial = string.IsNullOrEmpty(userEmail) ? "U" : userEmail[0].ToString().ToUpper();
            var sz = e.Graphics.MeasureString(initial, font);
            e.Graphics.DrawString(initial, font, textBrush, (34 - sz.Width) / 2, (34 - sz.Height) / 2);
        };

        var lblUser = new Label
        {
            Text = userEmail.Length > 18 ? userEmail[..18] + "…" : userEmail,
            ForeColor = AppTheme.TextPrimary,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Location = new Point(60, 14),
            AutoSize = true
        };
        var lblAdmin = new Label
        {
            Text = roleName,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Location = new Point(60, 34),
            AutoSize = true
        };

        var btnSignOut = new Button
        {
            Text = "⏻",
            Size = new Size(32, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = AppTheme.Danger,
            Font = new Font("Segoe UI", 12),
            Cursor = Cursors.Hand,
            Location = new Point(190, 16)
        };
        btnSignOut.FlatAppearance.BorderSize = 0;
        btnSignOut.Click += (s, e) =>
        {
            if (MessageBox.Show("Sign out of CORE ERP?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Application.Restart();
        };

        userPanel.Controls.Add(avatar);
        userPanel.Controls.Add(lblUser);
        userPanel.Controls.Add(lblAdmin);
        userPanel.Controls.Add(btnSignOut);
        _sidebarPanel.Controls.Add(userPanel);
    }

    private Button CreateNavButton(NavItem item)
    {
        var btn = new Button
        {
            Text = $"   {item.Icon}    {item.Label}",
            Size = new Size(216, 44),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SidebarBg,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontNavItem,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Cursor = Cursors.Hand,
            Tag = item.Key
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.MouseEnter += (s, e) =>
        {
            if (btn.BackColor != AppTheme.SidebarActive && btn.BackColor != AppTheme.Primary)
                btn.BackColor = AppTheme.SurfaceRaised;
        };
        btn.MouseLeave += (s, e) =>
        {
            if (btn.BackColor != AppTheme.SidebarActive && btn.BackColor != AppTheme.Primary)
                btn.BackColor = AppTheme.SidebarBg;
        };
        btn.Click += (s, e) => NavigateTo(item.Key);
        return btn;
    }

    private void NavigateTo(string key)
    {
        var item = _navItems.FirstOrDefault(n => n.Key == key);
        if (item == null) return;

        foreach (var kv in _navButtons)
        {
            if (kv.Key == key)
            {
                // Solid orange active (like D.CC Orders button)
                kv.Value.BackColor = AppTheme.Primary;
                kv.Value.ForeColor = Color.White;
                kv.Value.Font = AppTheme.FontNavItemBold;
            }
            else
            {
                kv.Value.BackColor = AppTheme.SidebarBg;
                kv.Value.ForeColor = AppTheme.TextSecondary;
                kv.Value.Font = AppTheme.FontNavItem;
            }
        }

        _contentPanel.SuspendLayout();
        _contentPanel.Controls.Clear();
        var view = item.ViewFactory();
        view.Dock = DockStyle.Fill;
        _contentPanel.Controls.Add(view);
        _contentPanel.ResumeLayout();
    }
}
