namespace DiagnosticExplorer;

public class DiagnosticOptions
{
    public static string DiagnosticExplorer = "DiagnosticExplorer";

    public DiagnosticOptions()
    {
    }

    public DiagnosticOptions(string uri)
    {
        Uri = uri;
    }

    public string Uri { get; set; }
    public bool Enabled { get; set; } = true;
}