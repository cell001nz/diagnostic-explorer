namespace DiagnosticExplorer;

public class OperationRequest
{
    public string Path { get; set; }

    public string Operation { get; set; }

    public string[] Arguments { get; set; }
}
