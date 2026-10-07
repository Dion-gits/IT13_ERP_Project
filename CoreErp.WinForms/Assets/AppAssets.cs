using System.Drawing;
using System.IO;

namespace CoreErp.WinForms.Assets;

public static class AppAssets
{
    private static Image? _logo;
    private static readonly object _lock = new();

    /// <summary>Returns the Core logo, loading it once and caching it.</summary>
    public static Image? Logo
    {
        get
        {
            if (_logo != null) return _logo;
            lock (_lock)
            {
                if (_logo != null) return _logo;

                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
                if (File.Exists(path))
                {
                    // Load into memory so the file isn't locked while the app runs.
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    _logo = Image.FromStream(fs);
                }
                return _logo;
            }
        }
    }
}