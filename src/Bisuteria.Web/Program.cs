using System.Globalization;
using Bisuteria.Web.Security;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Bisuteria.Application;
using Bisuteria.Infrastructure;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Identity;
using Bisuteria.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Bisuteria.Domain.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddStoreSecurity();
builder.Services.AddSingleton(TimeProvider.System);
if (builder.Environment.IsProduction() && builder.Configuration.GetValue("Database:ApplyMigrations", false))
    throw new InvalidOperationException("En producción aplica las migraciones como paso controlado antes de iniciar la aplicación.");
builder.Logging.AddJsonConsole();
builder.Services.AddHealthChecks().AddCheck<Bisuteria.Web.Services.DatabaseHealthCheck>("database");

// Cultura invariante: los decimales siempre usan punto (evita errores de binding según el locale del servidor).
var invariantCulture = new CultureInfo("es-EC");
invariantCulture.NumberFormat.NumberDecimalSeparator = ".";
invariantCulture.NumberFormat.NumberGroupSeparator = ",";
invariantCulture.NumberFormat.CurrencySymbol = "$";
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(invariantCulture);
    options.SupportedCultures = new List<CultureInfo> { invariantCulture };
    options.SupportedUICultures = new List<CultureInfo> { invariantCulture };
});

// Capas de Aplicación e Infraestructura (servicios, mapeos explícitos, FluentValidation, Identity, EF Core).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IAuthorizationHandler, StorePermissionHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in StorePermissions.All)
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new StorePermissionRequirement(permission)));
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<Bisuteria.Web.Filters.InputValidationFilter>();
    options.Filters.Add<Bisuteria.Web.Filters.EndpointValidationFilter>();
    options.Filters.Add<Bisuteria.Web.Filters.PersistenceExceptionFilter>();
    options.Filters.Add<AntispamFilter>();
    options.Filters.Add<AdminReauthenticationFilter>();
    options.Filters.Add<AdminAuditFilter>();
    options.Filters.Add<InitialPasswordFilter>();
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((_, field) => $"El valor de {field} no es válido.");
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(_ => "Introduce un número válido.");
    options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(_ => "El valor no es válido.");
});

// Sesión para el carrito de compras.
builder.Services.AddMemoryCache();
var sessionProvider = builder.Configuration["Session:Provider"]?.Trim();
if (builder.Environment.IsProduction() && !string.Equals(sessionProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("En producción configura Session:Provider=SqlServer para conservar las sesiones del carrito fuera del proceso web.");

if (string.IsNullOrWhiteSpace(sessionProvider) || string.Equals(sessionProvider, "Memory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDistributedMemoryCache();
}
else if (string.Equals(sessionProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var sessionConnection = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(sessionConnection))
        throw new InvalidOperationException("Session:Provider=SqlServer requiere ConnectionStrings:DefaultConnection.");
    builder.Services.AddDistributedSqlServerCache(options =>
    {
        options.ConnectionString = sessionConnection;
        options.SchemaName = "dbo";
        options.TableName = "DistributedCache";
    });
}
else
{
    throw new InvalidOperationException("Session:Provider acepta únicamente Memory o SqlServer.");
}
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = "Bisuteria.Cart";
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Bisuteria.Web.Services.SecurityAuditLogger>();
builder.Services.AddScoped<Bisuteria.Web.Cart.CartManager>();
builder.Services.AddScoped<Bisuteria.Web.Services.IImageUploadService, Bisuteria.Web.Services.ImageUploadService>();
builder.Services.AddSingleton<Bisuteria.Web.Services.IPaymentProofStorage, Bisuteria.Web.Services.PaymentProofStorage>();
builder.Services.AddScoped<Bisuteria.Web.Services.IEmailSender, Bisuteria.Web.Services.SmtpEmailSender>();
builder.Services.AddSingleton<Bisuteria.Web.Services.PasswordRecoveryQueue>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Bisuteria.Web.Services.PasswordRecoveryQueue>());
builder.Services.AddScoped<Bisuteria.Application.Interfaces.IGalleryAdminService, Bisuteria.Infrastructure.Services.GalleryAdminService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("public-forms", context =>
        builder.Environment.IsEnvironment("Testing") && !builder.Configuration.GetValue("Security:TestRateLimits", false)
            ? RateLimitPartition.GetNoLimiter("testing")
            : RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseForwardedHeaders();
app.Use(SecurityConfiguration.Headers);

// Inicialización de la base de datos (migraciones + seed).
if (builder.Configuration.GetValue("Database:Initialize", true))
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await DbInitializer.InitializeAsync(context, userManager, roleManager, builder.Configuration,
            services.GetRequiredService<ILogger<Program>>(), app.Environment.IsProduction(), applyMigrations: !app.Environment.IsProduction());
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al inicializar la base de datos.");
        if (app.Environment.IsProduction()) throw;
    }
}

// En desarrollo local muestra la excepción completa para diagnosticar fallos.
// En otros entornos conserva la página genérica y no expone detalles internos.
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/StatusCodePage", "?code={0}");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization();

app.UseRouting();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Rutas con Áreas (Panel de Administración) y ruta por defecto.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "sitemap",
    pattern: "sitemap.xml",
    defaults: new { controller = "Seo", action = "Sitemap" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");



app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
