using CoreErp.WinForms.Shell;
using CoreErp.WinForms.Views.Inventory;
using CoreErp.WinForms.Views.POS;

namespace CoreErp.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var navItems = new List<ErpShell.NavItem>
            {
                new()
                {
                    Key = "pos",
                    Icon = "🛒",
                    Label = "Point of Sale",
                    ViewFactory = () => new PosView()
                },
                new()
                {
                    Key = "transactions",           // ← NEW
                    Icon = "📜",                     // ← NEW
                    Label = "Transaction History",  // ← NEW
                    ViewFactory = () => new TransactionHistoryView()  // ← NEW
                },
                new()
                {
                    Key = "inventory",
                    Icon = "📦",
                    Label = "Inventory",
                    ViewFactory = () => new InventoryListView()
                },
            };

            Application.Run(new ErpShell(
                roleName: "Cashier / Inventory",
                userEmail: "staff@tenant1.com",
                navItems: navItems));
        }
    }
}