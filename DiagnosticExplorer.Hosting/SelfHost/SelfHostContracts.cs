using System.Collections.Generic;
using System.Threading.Tasks;
using DiagnosticExplorer.Logging;

namespace DiagnosticExplorer;

/// <summary>SignalR callbacks delivered to a self-host viewer.</summary>
public interface ISelfHostClient
{
    Task ShowDiagnostics(string processId, DiagnosticResponse response);
    Task ShowDiagnosticsError(string processId, string message);
    Task InitializeLogStream(string processId, LogStreamInitialization initialization);
    Task StreamLogEvents(string processId, LogStreamEvent[] events);
}

/// <summary>SignalR operations exposed by a self-host viewer.</summary>
public interface ISelfHostHub
{
    Task<SelfHostProcessInfo> GetProcessInfo();
    Task Subscribe(string processId);
    Task Unsubscribe(string processId);
    Task<DrillDownResponse> GetDrillDown(string processId, DrillDownRequest request);
    Task<OperationResponse> SetProperty(string processId, SetPropertyRequest request);
    Task<OperationResponse> ExecuteOperation(string processId, OperationRequest request);
}

/// <summary>Identity of the process exposed by a self-hosted viewer.</summary>
public sealed class SelfHostProcessInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string MachineName { get; set; }
    public string UserName { get; set; }
}
