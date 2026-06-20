using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace DiagnosticExplorer;

public interface IDiagnosticClient
{
    Task<DiagnosticResponse> GetDiagnostics(CancellationToken cancel);
    Task<OperationResponse> SetProperty(SetPropertyRequest request);
    Task<OperationResponse> ExecuteOperation(OperationRequest request);
    Task SubscribeEvents();
    Task UnsubscribeEvents();

    Subject<SystemEvent[]> EventsSet { get; }
    Subject<SystemEvent[]> EventsStreamed { get; }

}