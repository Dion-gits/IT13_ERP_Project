using CoreErp.WinForms.Theme;
using CoreErp.WinForms.Views.Auth;

namespace CoreErp.WinForms.Views.Roles;

public class RoleHomeView : UserControl
{
    public RoleHomeView(LoginResult user)
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;

        // ── Page header ──
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = AppTheme.AppBackground };

        pnlHeader.Controls.Add(new Label
        {
            Text = $"Welcome, {user.FullName}",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        });

        pnlHeader.Controls.Add(new Label
        {
            Text = $"{user.Role} workspace   ·   {user.Email}",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(2, 36),
            AutoSize = true
        });

        // ── Blank content card ──
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(1)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(AppTheme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        card.Controls.Add(new Label
        {
            Text = $"{user.Role} dashboard — coming soon",
            Font = new Font("Segoe UI", 13),
            ForeColor = AppTheme.TextMuted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        });

        Controls.Add(card);
        Controls.Add(pnlHeader);
    }
}