using Bisuteria.Application.Interfaces;
using Bisuteria.Application.Validators;
using Bisuteria.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Bisuteria.Application;

/// <summary>
/// Registro de los servicios de la capa de Aplicación.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ProductInputValidator).Assembly);

        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<CartPreparationService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IBlogService, BlogService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<ISettingService, SettingService>();
        services.AddScoped<ICustomizationService, CustomizationService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IProductAdminService, ProductAdminService>();
        services.AddScoped<ICouponAdminService, CouponAdminService>();

        return services;
    }
}
