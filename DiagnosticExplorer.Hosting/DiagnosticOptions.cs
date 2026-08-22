namespace DiagnosticExplorer;

public class DiagnosticOptions
{
    public const string RemoteUrlConfigurationKey = "DiagnosticExplorer:RemoteUrl";
    public const string EnabledConfigurationKey = DiagnosticManager.EnabledConfigurationKey;

    public DiagnosticOptions() { }

    public DiagnosticOptions(string uri)
    {
        Uri = uri;
    }

    public string Uri { get; set; }
    public bool Enabled { get; set; } = true;
}
