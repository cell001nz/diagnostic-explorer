// See https://aka.ms/new-console-template for more information
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DiagnosticExplorer.Common;
using DiagWebService;
using DiagWebService.Hubs;
using log4net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;


public class Program
{
    public static async Task Main(string[] args)
    {
        await CreateHostBuilder(args).Build().RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://*:2803");
                webBuilder.UseStartup<WebStartup>();
            });
}


