using System;

namespace DiagnosticExplorer;

public class DiagExplorerOptions
{
    public const string ConfigurationSectionName = "DiagnosticExplorer";

    public bool Enabled { get; set; } = true;
    public string RemoteUrl { get; set; }
    public string SelfHostUrl { get; set; }
    public EventRetentionOptions EventRetention { get; set; } = new();
}

[Obsolete("Use DiagExplorerOptions instead.")]
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
