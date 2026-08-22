namespace DiagnosticExplorer;

public class DiagnosticOptions
{
    public const string RemoteUrlConfigurationKey = "DiagnosticExplorer:RemoteUrl";

    public DiagnosticOptions() { }

    public DiagnosticOptions(string uri)
    {
        Uri = uri;
    }

    public string Uri { get; set; }
    public bool Enabled { get; set; } = true;
}
