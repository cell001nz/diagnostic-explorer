#if NET5_0_OR_GREATER

using System;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiagnosticExplorer
{
    public static class DiagnosticHostingExtensions
    {
        public static IServiceCollection AddDiagnosticExplorer(
            this IServiceCollection services,
            IConfiguration config,
            Action<HttpConnectionOptions> configureHttp = null
        )
        {
            bool enabled = config.GetValue<bool?>(DiagnosticOptions.EnabledConfigurationKey) ?? true;
            DiagnosticManager.Enabled = enabled;
            services.Configure<DiagnosticOptions>(options =>
            {
                options.Enabled = enabled;
                options.Uri = config[DiagnosticOptions.RemoteUrlConfigurationKey];
            });
            services.AddHostedService(sp => new DiagnosticHostingService(sp.GetService<IOptions<DiagnosticOptions>>(), configureHttp));
            return services;
        }
    }
}

#endif
