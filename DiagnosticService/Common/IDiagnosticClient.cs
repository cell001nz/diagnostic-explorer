using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticExplorer.Logging;

namespace DiagnosticExplorer;

public interface IDiagnosticClient
{
    Task<DiagnosticResponse> GetDiagnostics(CancellationToken cancel);
    Task<DrillDownResponse> GetDrillDown(DrillDownRequest request);
    Task<OperationResponse> SetProperty(SetPropertyRequest request);
    Task<OperationResponse> ExecuteOperation(OperationRequest request);
    Task SubscribeEvents();
    Task UnsubscribeEvents();

    Subject<LogStreamInitialization> LogStreamInitialized { get; }
    Subject<LogStreamEvent[]> LogStreamEvents { get; }
}
