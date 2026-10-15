using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Bisuteria.Infrastructure.Seed;

/// <summary>
/// Inicializa la base de datos: aplica migraciones, crea roles, el usuario administrador,
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
        IConfiguration configuration)
    {
        await context.Database.MigrateAsync();

        // ---- Roles ----
        var adminRole = "Admin";
        var customerRole = "Customer";

        if (!await roleManager.RoleExistsAsync(adminRole))
            await roleManager.CreateAsync(new IdentityRole(adminRole));

        if (!await roleManager.RoleExistsAsync(customerRole))
            await roleManager.CreateAsync(new IdentityRole(customerRole));

        var legacyAdmin = await userManager.FindByEmailAsync(LegacyDefaultAdminEmail);
        if (legacyAdmin is { IsActive: true })
        {
            legacyAdmin.IsActive = false;
            legacyAdmin.LockoutEnabled = true;
            legacyAdmin.LockoutEnd = DateTimeOffset.MaxValue;
            var disableResult = await userManager.UpdateAsync(legacyAdmin);
            if (!disableResult.Succeeded)
                throw new InvalidOperationException("No se pudo desactivar la cuenta administrativa histórica.");
        }

        // ---- Usuario administrador inicial ----
        // Las credenciales nunca se incluyen en el código ni en appsettings.
        var adminEmail = configuration["InitialAdmin:Email"];
        var adminPassword = configuration["InitialAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword) &&
            await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Dann",
                LastName = "Administrador",
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, adminRole);
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

    private static Category CreateCategory(string name, string slug, int order) => new()
    {
        Name = name,
        Slug = slug,
        IsActive = true,
        DisplayOrder = order,
        Description = $"Accesorios de la categoría {name}."
    };

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
