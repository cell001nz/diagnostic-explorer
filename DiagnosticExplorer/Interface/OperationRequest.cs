namespace DiagnosticExplorer;

public class OperationRequest
{
    public string[] ObjectPaths { get; set; }

    public string Path { get; set; }

    public string Operation { get; set; }

    public string[] Arguments { get; set; }
}
