using DiagnosticExplorer;
using DiagnosticExplorer.SelfHost;

SampleDiagnostics diagnostics = new();
DiagnosticManager.Register(diagnostics, "Sample diagnostics", "Self host");

using DiagnosticSelfHost host = await DiagnosticSelfHostingService.StartAsync();
Console.WriteLine($"Diagnostic Explorer is available at {host.Url}");
Console.WriteLine("Press Ctrl+C to stop.");

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
}
catch (OperationCanceledException)
{
}

public sealed class SampleDiagnostics
{
    [Property(AllowSet = true, Description = "A value that can be edited from the local viewer.")]
    public string Message { get; set; } = "Hello from the self-host sample";

    public int RefreshCount { get; private set; }

    [DiagnosticMethod]
    public string Refresh()
    {
        RefreshCount++;
        return $"Refresh {RefreshCount}";
    }
}