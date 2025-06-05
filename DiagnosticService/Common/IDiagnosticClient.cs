using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace DiagnosticExplorer;

public interface IDiagnosticClient
{
    Task<DiagnosticResponse> GetDiagnostics(CancellationToken cancel);
    Task<OperationResponse> SetProperty(string path, string? value);
    Task<OperationResponse> ExecuteOperation(string path, string operation, string[] arguments);
    Task SubscribeEvents();
    Task UnsubscribeEvents();

    Subject<SystemEvent[]> EventsSet { get; }
    Subject<SystemEvent[]> EventsStreamed { get; }

}