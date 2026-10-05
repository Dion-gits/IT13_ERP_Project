using System.Drawing;

namespace CoreErp.WinForms.Theme;

public static class AppTheme
{
    // Backgrounds
    public static readonly Color AppBackground = Color.FromArgb(247, 248, 250);
    public static readonly Color CardBackground = Color.White;
    public static readonly Color SidebarBg = Color.White;
    public static readonly Color SidebarActive = Color.FromArgb(239, 246, 255);
    public static readonly Color InputBg = Color.FromArgb(249, 250, 251);

    // Text
    public static readonly Color TextPrimary = Color.FromArgb(17, 24, 39);
    public static readonly Color TextSecondary = Color.FromArgb(107, 114, 128);
    public static readonly Color TextMuted = Color.FromArgb(156, 163, 175);

    // Borders
    public static readonly Color Border = Color.FromArgb(229, 231, 235);
    public static readonly Color BorderLight = Color.FromArgb(243, 244, 246);

    // Accents
    public static readonly Color Primary = Color.FromArgb(37, 99, 235);
    public static readonly Color Success = Color.FromArgb(16, 185, 129);
    public static readonly Color Warning = Color.FromArgb(245, 158, 11);
    public static readonly Color Danger = Color.FromArgb(239, 68, 68);
    public static readonly Color Purple = Color.FromArgb(139, 92, 246);

    // Fonts
    public static readonly Font FontTitle = new("Segoe UI", 18, FontStyle.Bold);
    public static readonly Font FontSubtitle = new("Segoe UI", 10);
    public static readonly Font FontHeading = new("Segoe UI", 11, FontStyle.Bold);
    public static readonly Font FontBody = new("Segoe UI", 9.5f);
    public static readonly Font FontSmall = new("Segoe UI", 8.5f);
    public static readonly Font FontNavItem = new("Segoe UI", 10);
    public static readonly Font FontNavItemBold = new("Segoe UI", 10, FontStyle.Bold);
    public static readonly Font FontButton = new("Segoe UI", 9.5f, FontStyle.Bold);
}