using System;

namespace DiagnosticExplorer.SelfHost;

/// <summary>Configures the local diagnostics viewer.</summary>
public sealed class SelfHostOptions
{
    /// <summary>The configuration key used by the self-host startup helpers.</summary>
    public const string SelfHostUrlConfigurationKey = "DiagnosticExplorer:SelfHostUrl";

    /// <summary>The default loopback listener used when no URL is supplied.</summary>
    public const string DefaultUrl = "http://127.0.0.1:50001";

    /// <summary>Gets or sets the URL at which the standalone viewer listens.</summary>
    public string Url { get; set; } = DefaultUrl;

    /// <summary>Gets or sets whether local diagnostics are enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the relative base path for the viewer.</summary>
    public string PathBase { get; set; } = "/";

    /// <summary>Gets or sets whether detailed hub errors are sent to the local viewer.</summary>
    public bool EnableDetailedErrors { get; set; }

    internal string GetNormalizedPathBase()
    {
        if (string.IsNullOrWhiteSpace(PathBase) || PathBase == "/")
            return string.Empty;

        return "/" + PathBase.Trim('/');
    }
}
