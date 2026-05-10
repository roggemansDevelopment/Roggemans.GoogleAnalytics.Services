using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Roggemans.GoogleAnalytics.Services;

public static class GoogleAnalyticsServiceCollectionExtensions
{
    public static IServiceCollection AddGoogleAnalyticsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<GoogleAnalyticsOptions>()
            .Bind(configuration.GetSection(GoogleAnalyticsOptions.SectionName));

        services.AddHttpClient<IGoogleAnalyticsReportService, GoogleAnalyticsReportService>();

        return services;
    }
}
