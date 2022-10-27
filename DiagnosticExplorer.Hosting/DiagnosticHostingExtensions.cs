#if NET5_0_OR_GREATER

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiagnosticExplorer
{
    public static class DiagnosticHostingExtensions
    {
        public static IServiceCollection AddDiagnosticExplorer(this IServiceCollection services)
        {
            using (ServiceProvider provider = services.BuildServiceProvider())
            {
                IConfiguration configuration = provider.GetService<IConfiguration>();
                services.Configure<DiagnosticOptions>(configuration.GetSection("DiagnosticExplorer"));
            }
            services.AddHostedService<DiagnosticHostingService>();
            return services;
        }
    }
}

#endif