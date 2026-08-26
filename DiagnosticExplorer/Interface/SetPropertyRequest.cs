namespace DiagnosticExplorer;

public class SetPropertyRequest
{
    public string[] ObjectPaths { get; set; }

    public string Path { get; set; }

    public string? Value { get; set; }
}
