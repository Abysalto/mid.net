using AbySalto.Mid.Application.Auth;
using AbySalto.Mid.Application.Baskets;
using AbySalto.Mid.Application.Favorites;
using AbySalto.Mid.Application.Products;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AbySalto.Mid.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<BasketOptions>()
            .Bind(configuration.GetSection(BasketOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IBasketService, BasketService>();
        services.AddScoped<IFavoriteService, FavoriteService>();

        return services;
    }
}
