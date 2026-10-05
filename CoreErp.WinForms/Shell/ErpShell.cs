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

        _sidebarPanel = new Panel { Dock = DockStyle.Left, Width = 240, BackColor = AppTheme.SidebarBg };
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
            Padding = new Padding(28)
        };

        Controls.Add(_contentPanel);
        Controls.Add(_sidebarPanel);

        if (_navItems.Count > 0)
            NavigateTo(_navItems[0].Key);
    }

    private void BuildSidebar(string roleName, string userEmail)
    {
        var lblBrand = new Label
        {
            Text = "⚡  CORE ERP",
            ForeColor = AppTheme.TextPrimary,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Location = new Point(20, 22),
            AutoSize = true
        };
        var lblRole = new Label
        {
            Text = roleName,
            ForeColor = AppTheme.Primary,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Location = new Point(22, 54),
            AutoSize = true
        };
        var lblEmail = new Label
        {
            Text = userEmail,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Location = new Point(22, 72),
            AutoSize = true
        };

        _sidebarPanel.Controls.AddRange(new Control[] { lblBrand, lblRole, lblEmail });

        int top = 110;
        foreach (var item in _navItems)
        {
            var btn = CreateNavButton(item);
            btn.Location = new Point(12, top);
            top += 46;
            _sidebarPanel.Controls.Add(btn);
            _navButtons[item.Key] = btn;
        }

        var btnSignOut = new Button
        {
            Text = "🚪   Sign Out",
            Size = new Size(216, 42),
            Location = new Point(12, _sidebarPanel.Height - 60),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SidebarBg,
            ForeColor = AppTheme.Danger,
            Font = AppTheme.FontNavItem,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnSignOut.FlatAppearance.BorderSize = 0;
        btnSignOut.Click += (s, e) =>
        {
            if (MessageBox.Show("Sign out?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Application.Restart();
        };
        _sidebarPanel.Controls.Add(btnSignOut);
    }

    private Button CreateNavButton(NavItem item)
    {
        var btn = new Button
        {
            Text = $"   {item.Icon}    {item.Label}",
            Size = new Size(216, 42),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SidebarBg,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontNavItem,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Cursor = Cursors.Hand,
            Tag = item.Key
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.MouseEnter += (s, e) => { if (btn.BackColor != AppTheme.SidebarActive) btn.BackColor = Color.FromArgb(249, 250, 251); };
        btn.MouseLeave += (s, e) => { if (btn.BackColor != AppTheme.SidebarActive) btn.BackColor = AppTheme.SidebarBg; };
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
                kv.Value.BackColor = AppTheme.SidebarActive;
                kv.Value.ForeColor = AppTheme.Primary;
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