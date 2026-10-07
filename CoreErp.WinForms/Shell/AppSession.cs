using CoreErp.WinForms.Views.Auth;

namespace CoreErp.WinForms.Shell;

public static class AppSession
{
    public static LoginResult? User { get; private set; }
    public static int? CurrentCompanyId { get; private set; }

    public static void Start(LoginResult user)
    {
        User = user;
        CurrentCompanyId = user.CompanyId;    // null for SuperAdmin
    }

    /// <summary>SuperAdmin: act on behalf of a specific tenant.</summary>
    public static void ImpersonateTenant(int companyId) => CurrentCompanyId = companyId;

    public static void Clear() => CurrentCompanyId = null;

    public static bool HasTenant => CurrentCompanyId.HasValue;
}