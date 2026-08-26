using System.Threading.Tasks;
using DiagnosticExplorer;
using DiagnosticExplorer.Common;
using DiagnosticExplorer.Logging;

namespace Diagnostics.Service.Common.Hubs;

public interface IWebHubClient
{
    Task ShowDiagnostics(string id, DiagnosticResponse response);
    Task ShowDiagnosticsError(string id, string message);
    Task SetProcesses(DiagProcess[] processes);
    Task UpdateProcess(DiagProcess processes);
    Task RemoveProcess(string id);
    Task InitializeLogStream(string id, LogStreamInitialization initialization);
    Task StreamLogEvents(string id, LogStreamEvent[] events);
    Task ProcessSearchResults(RetroSearchResult result);
    Task ProcessSearchEnd(int searchId);
    Task ProcessSearchError(int searchId, string message, string detail);
}
