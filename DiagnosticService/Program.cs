using System.Configuration;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DiagnosticExplorer;
using DiagnosticExplorer.Common;
using Diagnostics.Service.Common.Hubs;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Options;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "DiagnosticExplorer";
        });

        builder.Configuration.AddJsonFile(Expand(Path.Combine("Config", "settings.json")));

        var services = builder.Services;
        services.Configure<DiagServiceSettings>(builder.Configuration.GetSection(nameof(DiagServiceSettings)));
        services.AddSingleton<RealtimeManager>();
        services.AddSingleton<RetroManager>();
        services.ConfigureDiagnosticExplorer(
            builder.Configuration,
            diagnostics => diagnostics.RegisterObjects(registrar => registrar.RegisterService<RetroManager>("Retro", "Retro Manager"))
        );

        services.AddCors(opt =>
        {
            opt.AddPolicy(
                "CorsPolicy",
                builder =>
                {
                    builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                }
            );
        });
        services.AddSignalR();

        services
            .AddSignalR()
            .AddHubOptions<DiagnosticHub>(options =>
            {
                options.MaximumReceiveMessageSize = int.MaxValue;
                options.MaximumParallelInvocationsPerClient = 5;
            })
            .AddHubOptions<WebHub>(options =>
            {
                options.MaximumReceiveMessageSize = int.MaxValue;
                options.MaximumParallelInvocationsPerClient = 5;
                options.EnableDetailedErrors = true;
            })
            .AddMessagePackProtocol(options =>
            {
                options.SerializerOptions = MessagePackSerializerOptions
                    .Standard.WithResolver(ContractlessStandardResolver.Instance)
                    .WithSecurity(MessagePackSecurity.UntrustedData);
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.PayloadSerializerOptions.TypeInfoResolver = CreateCompactPayloadResolver();
            });

        string spaDir = builder.Configuration.GetValue<string>("DiagServiceSettings:SpaDirectory")!;
        string spaPath = Expand(spaDir);
        services.AddSpaStaticFiles(conf =>
        {
            conf.RootPath = spaPath;
        });

        var app = builder.Build();

        var settings = app.Services.GetService<IOptions<DiagServiceSettings>>().Value;

        if (app.Environment.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            app.UseExceptionHandler(errorApp =>
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync("An unexpected error occurred.");
                })
            );

        app.UseRouting();
        app.UseCors(x => x.SetIsOriginAllowed(x => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHub<WebHub>("/web-hub");
            endpoints.MapHub<DiagnosticHub>("/diagnostics");
        });

        if (!settings.UseSpaProxy && !Directory.Exists(spaPath))
            throw new ApplicationException($"Diagnostics SPA directory not found: {spaPath}");

        app.UseSpa(spa =>
        {
            spa.Options.DefaultPage = "/index.html";
            if (!settings.UseSpaProxy)
                app.UseSpaStaticFiles();

            if (settings.UseSpaProxy)
                spa.UseProxyToSpaDevelopmentServer(settings.SpaProxy);
        });

        if (!app.Urls.IsReadOnly)
        {
            app.Urls.Clear();

            foreach (string url in settings.Urls)
                app.Urls.Add(url);
        }

        app.Run();
    }

    static string? Expand(string? path) =>
        path == null ? null : Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));

    private static DefaultJsonTypeInfoResolver CreateCompactPayloadResolver()
    {
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(typeInfo =>
        {
            foreach (JsonPropertyInfo property in typeInfo.Properties)
            {
                if (property.PropertyType != typeof(bool) && Nullable.GetUnderlyingType(property.PropertyType) != typeof(bool))
                    continue;

                Func<object, object?, bool>? shouldSerialize = property.ShouldSerialize;
                property.ShouldSerialize = (parent, value) => (shouldSerialize?.Invoke(parent, value) ?? true) && !Equals(value, false);
            }
        });
        return resolver;
    }
}
