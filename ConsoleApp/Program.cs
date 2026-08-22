using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DiagnosticExplorer.SelfHost;

// using Grpc.Core;
// using Microsoft.AspNetCore.SignalR.Client;
// using Microsoft.CodeAnalysis;
namespace ConsoleApp;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Trace.Listeners.Add(new ConsoleTraceListener());

        while (true)
        {
            DiagnosticSelfHost host = await DiagnosticSelfHostingService.StartAsync("http://localhost:2803");
            Console.WriteLine("Diagnostics started");
            Console.ReadLine();
            await host.StopAsync();
            Console.WriteLine("Diagnostics stopped");
            Console.ReadLine();
        }
    }
}
