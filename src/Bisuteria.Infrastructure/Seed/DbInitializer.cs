using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Bisuteria.Domain.Security;

namespace Bisuteria.Infrastructure.Seed;

/// <summary>
/// Inicializa la base de datos: aplica migraciones, crea roles y, con secretos externos, el desarrollador inicial,
/// la configuración del sitio y las categorías base de Bisuterias Dann's.
/// El catálogo inicia vacío: los productos se cargan desde el panel de administración.
/// </summary>
public static class DbInitializer
{
    // Cuenta histórica de ejemplo. Si quedó en una base de datos previa, se desactiva
    // en el siguiente arranque para impedir que siga teniendo acceso administrativo.
    private const string LegacyDefaultAdminEmail = "admin@bisuteriasdanns.com";

    public static async Task InitializeAsync(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger? logger = null,
        bool isProduction = false,
        bool applyMigrations = true)
    {
        if (applyMigrations) await context.Database.MigrateAsync();

        // ---- Roles ----
        var adminRole = "Admin";
        var customerRole = "Customer";
        foreach (var roleName in new[] { StoreRoles.Developer, StoreRoles.Owner, StoreRoles.Operator })
            if (!await roleManager.RoleExistsAsync(roleName))
                Ensure(await roleManager.CreateAsync(new IdentityRole(roleName)));

        if (!await roleManager.RoleExistsAsync(adminRole))
            Ensure(await roleManager.CreateAsync(new IdentityRole(adminRole)));

        if (!await roleManager.RoleExistsAsync(customerRole))
            Ensure(await roleManager.CreateAsync(new IdentityRole(customerRole)));

        await SeedRolePermissionsAsync(context);

        var legacyAdmin = await userManager.FindByEmailAsync(LegacyDefaultAdminEmail);
        if (legacyAdmin is not null && (legacyAdmin.IsActive || !legacyAdmin.LockoutEnabled ||
            legacyAdmin.LockoutEnd != DateTimeOffset.MaxValue))
        {
            legacyAdmin.IsActive = false;
            legacyAdmin.LockoutEnabled = true;
            legacyAdmin.LockoutEnd = DateTimeOffset.MaxValue;
            // Invalida las sesiones anteriores incluso si la cuenta ya estaba inactiva.
            legacyAdmin.SecurityStamp = Guid.NewGuid().ToString("N");
            var disableResult = await userManager.UpdateAsync(legacyAdmin);
            if (!disableResult.Succeeded)
                throw new InvalidOperationException("No se pudo desactivar la cuenta administrativa histórica.");
        }

        // ---- Desarrollador inicial ----
        // Las credenciales solo se leen desde user-secrets o variables de entorno.
        var developerEmail = configuration["InitialDeveloper:Email"];
        var developerPassword = configuration["InitialDeveloper:Password"];
        if (!(await userManager.GetUsersInRoleAsync(StoreRoles.Developer)).Any())
        {
            if (string.IsNullOrWhiteSpace(developerEmail) || string.IsNullOrWhiteSpace(developerPassword))
            {
                const string message = "No hay cuenta Developer. Configura InitialDeveloper:Email y InitialDeveloper:Password para crearla.";
                if (isProduction) throw new InvalidOperationException(message);
                logger?.LogWarning("{Message}", message);
            }
            else if (await userManager.FindByEmailAsync(developerEmail) is null)
            {
                var developer = new ApplicationUser
                {
                    UserName = developerEmail,
                    Email = developerEmail,
                    FirstName = "Desarrollador",
                    EmailConfirmed = true,
                    MustChangePassword = true,
                    RequireTwoFactorSetup = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(developer, developerPassword);
                Ensure(result, logger, "crear el usuario Developer");
                developer.InitialPasswordHash = developer.PasswordHash;
                Ensure(await userManager.UpdateAsync(developer), logger, "guardar la protección de contraseña inicial");
                Ensure(await userManager.AddToRoleAsync(developer, StoreRoles.Developer), logger, "asignar el rol Developer");
                logger?.LogInformation("Se creó la cuenta Developer inicial; el primer acceso exige cambiar contraseña y configurar TOTP.");
            }
            else
                throw new InvalidOperationException("El correo configurado para InitialDeveloper ya pertenece a otra cuenta. No se convirtió automáticamente al rol Developer.");
        }

        // ---- Configuración del sitio ----
        await SeedSettingsAsync(context);

        // ---- Categorías base (sin imágenes; se agregan desde el panel) ----
        if (!await context.Categories.AnyAsync())
        {
            context.Categories.AddRange(
                CreateCategory("Pulseras", "pulseras", 1),
                CreateCategory("Collares", "collares", 2),
                CreateCategory("Anillos", "anillos", 3),
                CreateCategory("Lapiceros personalizados", "lapiceros-personalizados", 4),
                CreateCategory("Diseños personalizados", "disenos-personalizados", 5),
                CreateCategory("Otros accesorios", "otros-accesorios", 6)
            );
            await context.SaveChangesAsync();
        }

    }

    private static void Ensure(IdentityResult result, ILogger? logger = null, string operation = "inicializar Identity")
    {
        if (result.Succeeded) return;
        logger?.LogError("No se pudo {Operation}. Códigos de error de Identity: {Codes}", operation,
            string.Join(",", result.Errors.Select(error => error.Code)));
        throw new InvalidOperationException("No se pudo inicializar Identity. Revisa la configuración y la política de contraseñas.");
    }

    private static Category CreateCategory(string name, string slug, int order) => new()
    {
        Name = name,
        Slug = slug,
        IsActive = true,
        DisplayOrder = order,
        Description = $"Accesorios de la categoría {name}."
    };

    private static async Task SeedRolePermissionsAsync(AppDbContext context)
    {
        foreach (var (role, permissions) in new (string Role, IEnumerable<string> Permissions)[]
        {
            (StoreRoles.Developer, StorePermissions.All),
            (StoreRoles.Owner, StorePermissions.OwnerDefault),
            (StoreRoles.Operator, StorePermissions.OperatorDefault)
        })
        {
            if (await context.RolePermissions.AnyAsync(x => x.RoleName == role)) continue;
            context.RolePermissions.AddRange(permissions.Select(permission => new RolePermission { RoleName = role, Permission = permission }));
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedSettingsAsync(AppDbContext context)
    {
        var settings = new Dictionary<string, (string Value, string Group)>
        {
            ["StoreName"] = ("Bisuterias Dann's", "General"),
            ["StoreSlogan"] = ("Bisutería hecha a mano para realzar tu estilo ✨.", "General"),
            ["StoreDescription"] = ("Descubre Bisuterias Dann's, accesorios hechos a mano y personalizados para realzar tu estilo.", "General"),
            ["WhatsApp"] = ("", "Contacto"),
            ["ContactEmail"] = ("", "Contacto"),
            ["ContactPhone"] = ("", "Contacto"),
            ["ContactAddress"] = ("", "Contacto"),
            ["FacebookUrl"] = ("", "Redes"),
            ["InstagramUrl"] = ("", "Redes"),
            ["InstagramHandle"] = ("", "Redes"),
            ["TikTokUrl"] = ("", "Redes"),
            ["TikTokHandle"] = ("", "Redes"),
            ["ShippingCost"] = ("5", "Compras"),
            ["CouponSingleUsePerCustomer"] = ("false", "Compras"),
            ["FreeShippingThreshold"] = ("50", "Compras"),
            ["HeroTitle"] = ("Bisutería que cuenta tu historia", "Hero"),
            ["HeroSubtitle"] = ("Piezas únicas y personalizadas, hechas a mano con elegancia y pasión para los momentos que importan.", "Hero")
        };

        foreach (var setting in settings)
        {
            var existing = await context.SiteSettings.FirstOrDefaultAsync(s => s.Key == setting.Key);
            if (existing is null)
                context.SiteSettings.Add(new SiteSetting { Key = setting.Key, Value = setting.Value.Value, Group = setting.Value.Group });
        }

        await context.SaveChangesAsync();
    }
}
