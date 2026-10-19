namespace Bisuteria.Domain.Security;

/// <summary>Roles y permisos conocidos por el dominio de la tienda.</summary>
public static class StoreRoles
{
    public const string Developer = "Developer";
    public const string Owner = "Owner";
    public const string Operator = "Operator";
    public const string Customer = "Customer";
    public const string LegacyAdmin = "Admin";
}

public static class StorePermissions
{
    public const string DashboardView = "Dashboard.View";
    public const string OrdersView = "Orders.View";
    public const string OrdersUpdateStatus = "Orders.UpdateStatus";
    public const string OrdersSetShipping = "Orders.SetShipping";
    public const string PaymentsUpdate = "Payments.Update";
    public const string PaymentsViewProof = "Payments.ViewProof";
    public const string PaymentsReviewProof = "Payments.ReviewProof";
    public const string ProductsManage = "Products.Manage";
    public const string InventoryAdjust = "Inventory.Adjust";
    public const string CategoriesManage = "Categories.Manage";
    public const string CouponsManage = "Coupons.Manage";
    public const string ReviewsModerate = "Reviews.Moderate";
    public const string MessagesView = "Messages.View";
    public const string BlogManage = "Blog.Manage";
    public const string GalleryManage = "Gallery.Manage";
    public const string ReportsView = "Reports.View";
    public const string ReportsExport = "Reports.Export";
    public const string StoreSettingsEdit = "StoreSettings.Edit";
    public const string CustomersViewLimited = "Customers.ViewLimited";
    public const string CustomersManage = "Customers.Manage";
    public const string CustomersReveal = "Customers.Reveal";
    public const string UsersManageOwner = "Users.ManageOwner";
    public const string UsersManageStaff = "Users.ManageStaff";
    public const string RolesViewMatrix = "Roles.ViewMatrix";
    public const string RolesEditMatrix = "Roles.EditMatrix";
    public const string AuditView = "Audit.View";
    public const string SystemView = "System.View";
    public const string SmtpConfigure = "Smtp.Configure";
    public const string SecurityConfigure = "Security.Configure";
    public const string AdminSearch = "Admin.Search";
    public const string AdminNotifications = "Admin.Notifications";

    public static IReadOnlyList<string> All { get; } =
    [
        DashboardView, OrdersView, OrdersUpdateStatus, OrdersSetShipping, PaymentsUpdate, PaymentsViewProof, PaymentsReviewProof, ProductsManage,
        InventoryAdjust, CategoriesManage, CouponsManage, ReviewsModerate, MessagesView,
        BlogManage, GalleryManage, ReportsView, ReportsExport, StoreSettingsEdit,
        CustomersViewLimited, CustomersManage, CustomersReveal, UsersManageOwner, UsersManageStaff, RolesViewMatrix,
        RolesEditMatrix, AuditView, SystemView, SmtpConfigure, SecurityConfigure,
        AdminSearch, AdminNotifications
    ];

    public static IReadOnlySet<string> OwnerDefault { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        DashboardView, OrdersView, OrdersUpdateStatus, OrdersSetShipping, PaymentsUpdate, PaymentsViewProof, PaymentsReviewProof, ProductsManage,
        InventoryAdjust, CategoriesManage, CouponsManage, ReviewsModerate, MessagesView,
        BlogManage, GalleryManage, ReportsView, ReportsExport, StoreSettingsEdit,
        CustomersViewLimited, CustomersManage, UsersManageStaff, AdminSearch, AdminNotifications
    };

    public static IReadOnlySet<string> OperatorDefault { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        DashboardView, OrdersView, OrdersUpdateStatus, InventoryAdjust, AdminSearch, AdminNotifications
    };

    /// <summary>Techo de privilegios: la matriz puede reducir facultades, nunca elevar un rol comercial.</summary>
    public static bool CanAssignTo(string role, string permission) => role switch
    {
        StoreRoles.Developer => All.Contains(permission),
        StoreRoles.Owner => OwnerDefault.Contains(permission),
        StoreRoles.Operator => OperatorDefault.Contains(permission),
        _ => false
    };
}
