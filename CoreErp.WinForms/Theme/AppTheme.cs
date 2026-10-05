using System.Drawing;

namespace CoreErp.WinForms.Theme;

/// <summary>
/// Light UI theme inspired by the D.CC restaurant POS designs:
/// white surfaces, soft cards, coral-orange accents, clean badges.
/// Logo orange is preserved as the primary brand color.
/// </summary>
public static class AppTheme
{
    // ── Backgrounds ────────────────────────────────────────────────────
    public static readonly Color AppBackground = Color.FromArgb(245, 246, 248);   // page wash
    public static readonly Color CardBackground = Color.White;                      // elevated cards
    public static readonly Color SidebarBg = Color.White;
    public static readonly Color SidebarActive = Color.FromArgb(255, 237, 230);   // soft orange tint
    public static readonly Color InputBg = Color.FromArgb(248, 249, 250);
    public static readonly Color SurfaceRaised = Color.FromArgb(241, 243, 245);
    public static readonly Color TableHeaderBg = Color.FromArgb(255, 247, 242);   // warm header

    // ── Text ────────────────────────────────────────────────────────────
    public static readonly Color TextPrimary = Color.FromArgb(26, 26, 30);
    public static readonly Color TextSecondary = Color.FromArgb(100, 106, 115);
    public static readonly Color TextMuted = Color.FromArgb(150, 155, 162);

    // ── Borders ─────────────────────────────────────────────────────────
    public static readonly Color Border = Color.FromArgb(228, 230, 235);
    public static readonly Color BorderLight = Color.FromArgb(238, 240, 243);
    public static readonly Color BorderFocus = Color.FromArgb(255, 107, 44);

    // ── Accents (logo orange + D.CC coral) ──────────────────────────────
    public static readonly Color Primary = Color.FromArgb(255, 107, 44);    // #FF6B2C
    public static readonly Color PrimaryHover = Color.FromArgb(255, 130, 70);
    public static readonly Color PrimaryDim = Color.FromArgb(230, 90, 30);
    public static readonly Color Success = Color.FromArgb(34, 180, 100);
    public static readonly Color SuccessBg = Color.FromArgb(220, 245, 230);
    public static readonly Color Warning = Color.FromArgb(245, 170, 30);
    public static readonly Color WarningBg = Color.FromArgb(255, 245, 220);
    public static readonly Color Danger = Color.FromArgb(235, 80, 80);
    public static readonly Color DangerBg = Color.FromArgb(255, 230, 230);
    public static readonly Color Info = Color.FromArgb(60, 140, 230);
    public static readonly Color InfoBg = Color.FromArgb(230, 240, 255);

    // ── Fonts ───────────────────────────────────────────────────────────
    public static readonly Font FontTitle = FontOf(22, FontStyle.Bold);
    public static readonly Font FontSubtitle = FontOf(10.5f);
    public static readonly Font FontHeading = FontOf(12, FontStyle.Bold);
    public static readonly Font FontBody = FontOf(9.5f);
    public static readonly Font FontSmall = FontOf(8.5f);
    public static readonly Font FontNavItem = FontOf(10.5f);
    public static readonly Font FontNavItemBold = FontOf(10.5f, FontStyle.Bold);
    public static readonly Font FontButton = FontOf(10, FontStyle.Bold);
    public static readonly Font FontMono = new("Consolas", 10f);

    // ── Helpers ─────────────────────────────────────────────────────────
    public static Button MakePrimaryButton(string text, int width = 140, int height = 40)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(width, height),
            FlatStyle = FlatStyle.Flat,
            BackColor = Primary,
            ForeColor = Color.White,
            Font = FontButton,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = PrimaryHover;
        return btn;
    }

    // ── DPI-aware font sizing ──────────────────────────────────────────
    // Returns a font scaled for the current DPI. Use everywhere instead of
    // `new Font("Segoe UI", N)` so layout stays consistent on 125%/150% screens.
    public static Font FontOf(float pointSize, FontStyle style = FontStyle.Regular)
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        float scale = g.DpiY / 96f;
        return new Font("Segoe UI", pointSize * scale, style, GraphicsUnit.Point);
    }

    // Consistent card padding / gaps
    public const int Gap = 12;
    public const int Pad = 16;

    public static Button MakeGhostButton(string text, int width = 120, int height = 36)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(width, height),
            FlatStyle = FlatStyle.Flat,
            BackColor = CardBackground,
            ForeColor = TextSecondary,
            Font = FontButton,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = Border;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.MouseOverBackColor = SurfaceRaised;
        return btn;
    }

    public static void StyleGrid(DataGridView g)
    {
        g.AutoGenerateColumns = false;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle.BackColor = TableHeaderBg;
        g.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = TableHeaderBg;
        g.ColumnHeadersHeight = 44;
        g.DefaultCellStyle.BackColor = CardBackground;
        g.DefaultCellStyle.ForeColor = TextPrimary;
        g.DefaultCellStyle.SelectionBackColor = SidebarActive;
        g.DefaultCellStyle.SelectionForeColor = Primary;
        g.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        g.BackgroundColor = CardBackground;
        g.GridColor = BorderLight;
        g.BorderStyle = BorderStyle.None;
        g.RowHeadersVisible = false;
        g.RowTemplate.Height = 48;
        g.AllowUserToAddRows = false;
        g.AllowUserToResizeRows = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.MultiSelect = false;
        g.ReadOnly = true;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 253);
    }
}