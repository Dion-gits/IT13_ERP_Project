using CoreErp.WinForms.Theme;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;
using System.Text.Json;
using CoreErp.WinForms.Assets;

namespace CoreErp.WinForms.Views.Auth;

public class LoginForm : Form
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5067/") };

    private TextBox _txtEmail = null!;
    private TextBox _txtPassword = null!;
    private Label _lblError = null!;
    private Button _btnLogin = null!;

    public LoginResult? Result { get; private set; }

    public LoginForm() => BuildUI();

    private void BuildUI()
    {
        Text = "Core ERP — Sign In";
        ClientSize = new Size(820, 460);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = AppTheme.CardBackground;
        Font = AppTheme.FontBody;
        DoubleBuffered = true;

        // ── Left: brand panel (logo) ──
        var brand = new BrandPanel
        {
            Location = new Point(0, 0),
            Size = new Size(340, ClientSize.Height)
        };

        // ── Right: form panel ──
        var pnlForm = new Panel
        {
            Location = new Point(340, 0),
            Size = new Size(ClientSize.Width - 340, ClientSize.Height),
            BackColor = AppTheme.CardBackground
        };

        int x = 50;
        int y = 70;

        pnlForm.Controls.Add(new Label
        {
            Text = "Welcome back",
            Font = new Font("Segoe UI Semibold", 20f),
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(x, y),
            AutoSize = true
        });

        y += 46;
        pnlForm.Controls.Add(new Label
        {
            Text = "Sign in with your email to continue.",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(x + 2, y),
            AutoSize = true
        });

        y += 56;
        _txtEmail = AddField(pnlForm, "Email", x, y, isPassword: false);

        y += 78;
        _txtPassword = AddField(pnlForm, "Password", x, y, isPassword: true);

        y += 78;
        _lblError = new Label
        {
            Text = "",
            ForeColor = AppTheme.Danger,
            Font = AppTheme.FontSmall,
            Location = new Point(x, y),
            Size = new Size(400, 20)
        };
        pnlForm.Controls.Add(_lblError);

        y += 28;
        _btnLogin = new Button
        {
            Text = "Sign in",
            Location = new Point(x, y),
            Size = new Size(380, 46),
            BackColor = AppTheme.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 11f),
            Cursor = Cursors.Hand
        };
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.Click += async (s, e) => await LoginAsync();
        pnlForm.Controls.Add(_btnLogin);

        Controls.Add(pnlForm);
        Controls.Add(brand);

        AcceptButton = _btnLogin;   // Enter submits
    }

    private static TextBox AddField(Control parent, string label, int x, int y, bool isPassword)
    {
        parent.Controls.Add(new Label
        {
            Text = label,
            ForeColor = AppTheme.TextSecondary,
            Font = new Font("Segoe UI Semibold", 9f),
            Location = new Point(x, y),
            AutoSize = true
        });

        var tb = new TextBox
        {
            Location = new Point(x, y + 24),
            Width = 380,
            Font = new Font("Segoe UI", 11f),
            BackColor = AppTheme.InputBg,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = isPassword
        };
        parent.Controls.Add(tb);
        return tb;
    }

    private async Task LoginAsync()
    {
        _lblError.Text = "";

        var email = _txtEmail.Text.Trim();
        var password = _txtPassword.Text;

        if (email.Length == 0 || password.Length == 0)
        {
            _lblError.Text = "Email and password are required.";
            return;
        }

        SetBusy(true);
        try
        {
            var resp = await _http.PostAsJsonAsync("auth/login", new { email, password });

            if (!resp.IsSuccessStatusCode)
            {
                _lblError.Text = await ReadError(resp);
                return;
            }

            var result = await resp.Content.ReadFromJsonAsync<LoginResult>();
            if (result == null)
            {
                _lblError.Text = "Unexpected response from server.";
                return;
            }

            Result = result;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (HttpRequestException)
        {
            _lblError.Text = "Cannot reach the API. Is it running?";
        }
        catch (Exception ex)
        {
            _lblError.Text = ex.Message;
        }
        finally
        {
            if (!IsDisposed) SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _btnLogin.Enabled = !busy;
        _btnLogin.Text = busy ? "Signing in..." : "Sign in";
    }

    private static async Task<string> ReadError(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m) && m.GetString() is { Length: > 0 } text)
                return text;
        }
        catch { /* ignore */ }
        return $"Sign in failed ({(int)resp.StatusCode}).";
    }

    // ═══════════════════════════════════════════════════════════
    // Left brand panel — white background + logo + wordmark
    // ═══════════════════════════════════════════════════════════
    private sealed class BrandPanel : Control
    {
        public BrandPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            float cx = Width / 2f;
            float cy = Height / 2f - 40;

            const int box = 340;

            var logo = AppAssets.Logo;
            if (logo != null)
            {
                var ratio = Math.Min((float)box / logo.Width, (float)box / logo.Height);
                int w = (int)(logo.Width * ratio);
                int h = (int)(logo.Height * ratio);
                var dest = new Rectangle((int)(cx - w / 2f), (int)(cy - h / 2f), w, h);

                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, dest);
            }

            // ── Wordmark — Courier New Bold, auto-fit to panel width ──
            const string word = "Enterprise Resource Planning";
            using var wordmark = FitCourier(word, Width - 40);   // 20 px padding each side
            var wmSize = TextRenderer.MeasureText(word, wordmark, Size.Empty, TextFormatFlags.NoPadding);

            int wmY = (int)(cy + box / 2f + 20);                 // 20 px below the logo
            TextRenderer.DrawText(g, word, wordmark,
                new Point((int)(cx - wmSize.Width / 2f), wmY),
                AppTheme.Ink);

            // Optional: divider on the right edge
            using var divider = new Pen(AppTheme.Border, 1);
            g.DrawLine(divider, Width - 1, 0, Width - 1, Height);
        }

        // Picks the largest Courier New Bold size (28 down to 8) where the text fits maxWidth.
        private static Font FitCourier(string text, int maxWidth)
        {
            for (float pt = 28f; pt >= 8f; pt -= 0.5f)
            {
                var f = new Font("Courier New", pt, FontStyle.Bold);
                var s = TextRenderer.MeasureText(text, f, Size.Empty, TextFormatFlags.NoPadding);
                if (s.Width <= maxWidth) return f;
                f.Dispose();
            }
            return new Font("Courier New", 8f, FontStyle.Bold);
        }
    }
}