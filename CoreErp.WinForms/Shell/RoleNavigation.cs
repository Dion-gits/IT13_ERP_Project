using CoreErp.WinForms.Views.Auth;
using CoreErp.WinForms.Views.Inventory;
using CoreErp.WinForms.Views.POS;
using CoreErp.WinForms.Views.Roles;
using CoreErp.WinForms.Views.Roles.SuperAdmin;

namespace CoreErp.WinForms.Shell;

public static class RoleNavigation
{
    public static List<ErpShell.NavItem> Build(LoginResult user)
    {
        // ═══ SuperAdmin: master DB only ═══
        if (user.IsSuperAdmin)
        {
            return new List<ErpShell.NavItem>
            {
                new()
                {
                    Key = "platform",
                    Icon = "🏢",
                    Label = "Platform Overview",
                    ViewFactory = () => new SuperAdminHomeView(user)
                }
            };
        }

        // ═══ Tenant users ═══
        var items = new List<ErpShell.NavItem>
        {
            new()
            {
                Key = "home",
                Icon = "🏠",
                Label = "Home",
                ViewFactory = () => new RoleHomeView(user)
            }
        };

        switch (user.Role)
        {
            case "Cashier":
                items.Add(new() { Key = "pos", Icon = "🛒", Label = "POS", ViewFactory = () => new PosView() });
                items.Add(new() { Key = "transactions", Icon = "📜", Label = "Transactions", ViewFactory = () => new TransactionHistoryView() });
                break;

            case "InventoryStaff":
                items.Add(new() { Key = "inventory", Icon = "📦", Label = "Inventory", ViewFactory = () => new InventoryListView() });
                break;

            case "BranchManager":
            case "Owner":
                items.Add(new() { Key = "pos", Icon = "🛒", Label = "POS", ViewFactory = () => new PosView() });
                items.Add(new() { Key = "inventory", Icon = "📦", Label = "Inventory", ViewFactory = () => new InventoryListView() });
                items.Add(new() { Key = "transactions", Icon = "📜", Label = "Transactions", ViewFactory = () => new TransactionHistoryView() });
                break;

            case "HrManager":
            default:
                break;
        }

        return items;
    }
}