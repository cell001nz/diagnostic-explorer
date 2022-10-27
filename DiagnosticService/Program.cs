using DiagnosticExplorer;
using Microsoft.AspNetCore.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => {
        options.ServiceName = "Diagnostic Explorer Service";
    })
    .ConfigureServices(services => {
        services.AddDiagnosticExplorer();
    })
    .ConfigureWebHostDefaults(webBuilder => {
        webBuilder.UseUrls("http://*:2803");
        webBuilder.UseStartup<WebStartup>();
    })
    .Build();

await host.RunAsync();