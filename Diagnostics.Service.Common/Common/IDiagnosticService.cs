using System.ServiceModel;

namespace DiagnosticExplorer;

[ServiceContract(Namespace = "http://diagnosticexplorer.com/2010")]
public interface IDiagnosticService
{

    [OperationContract]
    DiagnosticResponse GetDiagnostics(string context);

    [OperationContract]
    OperationResponse ExecuteOperation(string path, string operation, string[] arguments);

    [OperationContract]
    OperationResponse SetProperty(string path, string value);
}