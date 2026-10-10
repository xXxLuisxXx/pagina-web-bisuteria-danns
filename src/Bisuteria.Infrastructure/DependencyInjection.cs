using Bisuteria.Application.Interfaces;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Infrastructure.Repositories;
using Bisuteria.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bisuteria.Infrastructure;

/// <summary>
/// Registro de los servicios de la capa de Infraestructura.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(3);
            }));

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Política de contraseñas robusta.
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.SignIn.RequireConfirmedEmail = configuration.GetValue("Identity:RequireConfirmedEmail", false);

                options.User.RequireUniqueEmail = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddPasswordValidator<CommonPasswordValidator>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.Cookie.Name = "Bisuteria.Auth";
            options.Events.OnSigningIn = context =>
            {
                if (context.Principal?.IsInRole("Admin") == true)
                {
                    context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30);
                    context.Properties.AllowRefresh = false;
                }
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async context =>
            {
                var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await manager.GetUserAsync(context.Principal!);
                var stamp = context.Principal?.FindFirst("AspNet.Identity.SecurityStamp")?.Value;
                if (user is null || !user.IsActive || stamp != await manager.GetSecurityStampAsync(user))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>().SignOutAsync();
                }
            };
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}
