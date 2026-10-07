using CoreErp.WinForms.Shell;
using CoreErp.WinForms.Views.Auth;

namespace CoreErp.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK || login.Result == null)
                return;

            var user = login.Result;
            AppSession.Start(user);

            var navItems = RoleNavigation.Build(user);
            Application.Run(new ErpShell(
                roleName: user.Role,
                userEmail: user.Email,
                navItems: navItems));
        }
    }
}