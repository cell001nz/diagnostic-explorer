using System.Text.Json;
using System.Text.Json.Serialization;
using DiagnosticExplorer;
using DiagnosticExplorer.Common;
using DiagnosticsExplorer.WebApi.Controllers;
using DiagWebService.Hubs;
using log4net.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class Startup
{

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(opt => {
            opt.AddPolicy("CorsPolicy", builder => {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
        services.AddSignalR();


        services.Configure<RealtimeOptions>(Configuration.GetSection(RealtimeOptions.Realtime));
        services.Configure<RetroOptions>(Configuration.GetSection(RetroOptions.Retro));
        services.Configure<DiagnosticOptions>(Configuration.GetSection(DiagnosticOptions.DiagnosticExplorer));
        services.AddSingleton<RealtimeManager>();
        services.AddSingleton<RetroManager>();
        services.AddSignalR().AddHubOptions<DiagnosticHub>(options => 
        {
            options.MaximumReceiveMessageSize = int.MaxValue;
            options.MaximumParallelInvocationsPerClient = 5;
        }).AddHubOptions<WebHub>(options => {
            options.MaximumReceiveMessageSize = int.MaxValue;
            options.MaximumParallelInvocationsPerClient = 5;
            options.EnableDetailedErrors = true;
        }).AddJsonProtocol(options => {
            options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
        });

        services.AddControllers()
            .AddApplicationPart(typeof(RegistrationController).Assembly)
            .AddJsonOptions(options => {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.WriteIndented = true;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });

        services.AddDiagnosticExplorer();

        services.AddControllers()
            .AddJsonOptions(options => {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.WriteIndented = true;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        app.UseRouting();
        app.UseCors(x => x.SetIsOriginAllowed(x => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
        app.UseEndpoints(endpoints => {
            endpoints.MapControllers();
            endpoints.MapHub<WebHub>("/web-hub");
            endpoints.MapHub<DiagnosticHub>("/diagnostics");
        });

        lifetime.ApplicationStarted.Register(() => {

            app.ApplicationServices.GetService<RealtimeManager>()!.Start();
            app.ApplicationServices.GetService<RetroManager>()!.Start();
            // DiagnosticHostingService.Start(Configuration.GetSection("Diagnostics").GetValue<string>("Url"));
        });

        lifetime.ApplicationStopping.Register(() => {

            app.ApplicationServices.GetService<RealtimeManager>()!.Stop();
            app.ApplicationServices.GetService<RetroManager>()!.Stop();
            // DiagnosticHostingService.Stop();
        });

    }

}