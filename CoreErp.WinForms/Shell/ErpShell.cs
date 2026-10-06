using CoreErp.WinForms.Theme;
using System.ComponentModel;
using System.Drawing.Drawing2D;

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
    private readonly Dictionary<string, NavButton> _navButtons = new();
    private readonly Panel _contentPanel;
    private readonly Panel _sidebarPanel;

    public ErpShell(string roleName, string userEmail, List<NavItem> navItems)
    {
        _navItems = navItems;

        Text = $"Core — {roleName}";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 800);
        BackColor = AppTheme.AppBackground;
        DoubleBuffered = true;
        Font = AppTheme.FontBody;

        _sidebarPanel = new Panel { Dock = DockStyle.Left, Width = 248, BackColor = AppTheme.SidebarBg };
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
            Padding = new Padding(36, 28, 36, 20)
        };

        Controls.Add(_contentPanel);
        Controls.Add(_sidebarPanel);

        if (_navItems.Count > 0)
            NavigateTo(_navItems[0].Key);
    }

    private void BuildSidebar(string roleName, string userEmail)
    {
        // ── Nav list (fill) — added first so docking works out ──
        var navFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppTheme.SidebarBg,
            Padding = new Padding(16, 8, 16, 0)
        };
        foreach (var item in _navItems)
        {
            var btn = CreateNavButton(item);
            navFlow.Controls.Add(btn);
            _navButtons[item.Key] = btn;
        }

        // ── Footer: user + sign out ──
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 108, BackColor = AppTheme.SidebarBg };
        footer.Paint += (s, e) =>
        {
            using var pen = new Pen(AppTheme.BorderLight, 1);
            e.Graphics.DrawLine(pen, 20, 0, footer.Width - 20, 0);
        };

        var lblRole = new Label
        {
            Text = roleName,
            ForeColor = AppTheme.TextPrimary,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Location = new Point(22, 16),
            AutoSize = true
        };
        var lblEmail = new Label
        {
            Text = userEmail,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Location = new Point(22, 38),
            AutoSize = true
        };

        var btnSignOut = new Button
        {
            Text = "Sign out",
            Size = new Size(216, 34),
            Location = new Point(16, 62),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SidebarBg,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontNavItem,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        btnSignOut.FlatAppearance.BorderSize = 0;
        btnSignOut.FlatAppearance.MouseOverBackColor = AppTheme.BorderLight;
        btnSignOut.FlatAppearance.MouseDownBackColor = AppTheme.Border;
        btnSignOut.MouseEnter += (s, e) => btnSignOut.ForeColor = AppTheme.Danger;
        btnSignOut.MouseLeave += (s, e) => btnSignOut.ForeColor = AppTheme.TextSecondary;
        btnSignOut.Click += (s, e) =>
        {
            if (MessageBox.Show("Sign out?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Application.Restart();
        };
        footer.Controls.AddRange(new Control[] { lblRole, lblEmail, btnSignOut });

        // ── Brand header ──
        var header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = AppTheme.SidebarBg };
        var mark = new BrandMark { Size = new Size(44, 44), Location = new Point(22, 26) };
        var lblBrand = new Label
        {
            Text = "Core",
            ForeColor = AppTheme.Ink,
            Font = AppTheme.FontBrand,
            Location = new Point(74, 33),
            AutoSize = true
        };
        header.Controls.AddRange(new Control[] { mark, lblBrand });

        // Order matters: Fill first, then Bottom, then Top
        _sidebarPanel.Controls.Add(navFlow);
        _sidebarPanel.Controls.Add(footer);
        _sidebarPanel.Controls.Add(header);
    }

    private NavButton CreateNavButton(NavItem item)
    {
        var btn = new NavButton
        {
            Text = string.IsNullOrWhiteSpace(item.Icon) ? item.Label : $"{item.Icon}    {item.Label}",
            Size = new Size(216, 42),
            Margin = new Padding(0, 0, 0, 4),
            Tag = item.Key
        };
        btn.Click += (s, e) => NavigateTo(item.Key);
        return btn;
    }

    private void NavigateTo(string key)
    {
        var item = _navItems.FirstOrDefault(n => n.Key == key);
        if (item == null) return;

        foreach (var kv in _navButtons)
            kv.Value.Active = kv.Key == key;

        _contentPanel.SuspendLayout();
        _contentPanel.Controls.Clear();
        var view = item.ViewFactory();
        view.Dock = DockStyle.Fill;
        _contentPanel.Controls.Add(view);
        _contentPanel.ResumeLayout();
    }

    // ═══════════════════════════════════════════════════════════
    // Sidebar button: flat, soft-orange when active, orange bar on the left
    // ═══════════════════════════════════════════════════════════
    private sealed class NavButton : Button
    {
        private bool _active;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Active
        {
            get => _active;
            set
            {
                _active = value;
                BackColor = value ? AppTheme.SidebarActive : AppTheme.SidebarBg;
                ForeColor = value ? AppTheme.TextPrimary : AppTheme.TextSecondary;
                Font = value ? AppTheme.FontNavItemBold : AppTheme.FontNavItem;
                Invalidate();
            }
        }

        public NavButton()
        {
            FlatStyle = FlatStyle.Flat;
            BackColor = AppTheme.SidebarBg;
            ForeColor = AppTheme.TextSecondary;
            Font = AppTheme.FontNavItem;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(16, 0, 0, 0);
            Cursor = Cursors.Hand;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = AppTheme.BorderLight;
            FlatAppearance.MouseDownBackColor = AppTheme.SidebarActive;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            if (!_active) return;
            using var brush = new SolidBrush(AppTheme.Accent);
            pevent.Graphics.FillRectangle(brush, 0, 8, 3, Height - 16);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Small sunburst drawn in code, echoing the Core logo
    // ═══════════════════════════════════════════════════════════
    private sealed class BrandMark : Control
    {
        public BrandMark()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const int rays = 36;
            float cx = Width / 2f, cy = Height / 2f;
            float outerMax = Math.Min(Width, Height) / 2f - 1;
            float inner = outerMax * 0.42f;

            using var pen = new Pen(AppTheme.Accent, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

            for (int i = 0; i < rays; i++)
            {
                double a = i * 2 * Math.PI / rays - Math.PI / 2;
                float t = 0.55f + 0.45f * (float)(0.5 + 0.5 * Math.Sin(a * 1.0 + 0.9));
                float outer = inner + (outerMax - inner) * t;

                g.DrawLine(pen,
                    cx + (float)Math.Cos(a) * inner, cy + (float)Math.Sin(a) * inner,
                    cx + (float)Math.Cos(a) * outer, cy + (float)Math.Sin(a) * outer);
            }
        }
    }
}