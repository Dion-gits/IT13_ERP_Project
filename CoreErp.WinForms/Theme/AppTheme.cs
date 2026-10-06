using System.Drawing;

namespace CoreErp.WinForms.Theme;

/// <summary>
/// Palette taken from the Core logo: white canvas, pure black type, orange rays.
/// Existing property names are kept so every view re-themes without code changes.
/// </summary>
public static class AppTheme
{
    // ── Logo colours ──
    public static readonly Color Accent = Color.FromArgb(255, 149, 0);       // logo orange
    public static readonly Color AccentSoft = Color.FromArgb(255, 244, 227); // tint for selected rows / active nav
    public static readonly Color Ink = Color.Black;                          // logo wordmark

    // ── Backgrounds ──
    public static readonly Color AppBackground = Color.FromArgb(251, 251, 250);
    public static readonly Color CardBackground = Color.White;
    public static readonly Color SidebarBg = Color.White;
    public static readonly Color SidebarActive = AccentSoft;
    public static readonly Color InputBg = Color.White;

    // ── Text ──
    public static readonly Color TextPrimary = Color.Black;
    public static readonly Color TextSecondary = Color.FromArgb(82, 82, 91);
    public static readonly Color TextMuted = Color.FromArgb(161, 161, 170);

    // ── Borders ──
    public static readonly Color Border = Color.FromArgb(228, 228, 231);
    public static readonly Color BorderLight = Color.FromArgb(244, 244, 245);

    // ── Semantic colours (re-mapped to the brand) ──
    public static readonly Color Primary = Color.Black;                      // main buttons, selected toggles
    public static readonly Color Success = Accent;                           // totals, "Add", "Place order"
    public static readonly Color Warning = Color.FromArgb(82, 82, 91);       // neutral dark grey (secondary action)
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);       // destructive / low stock only
    public static readonly Color Purple = Color.FromArgb(82, 82, 91);        // unused, kept for compatibility

    // ── Fonts: Segoe UI for reading, Consolas to echo the monospace logo ──
    public static readonly Font FontTitle = new("Consolas", 20, FontStyle.Bold);
    public static readonly Font FontSubtitle = new("Segoe UI", 10);
    public static readonly Font FontHeading = new("Segoe UI", 11, FontStyle.Bold);
    public static readonly Font FontBody = new("Segoe UI", 9.5f);
    public static readonly Font FontSmall = new("Segoe UI", 8.5f);
    public static readonly Font FontNavItem = new("Segoe UI", 10);
    public static readonly Font FontNavItemBold = new("Segoe UI Semibold", 10);
    public static readonly Font FontButton = new("Segoe UI Semibold", 9.5f);
    public static readonly Font FontBrand = new("Courier New", 17, FontStyle.Bold);
    public static readonly Font FontMono = new("Consolas", 10);
}