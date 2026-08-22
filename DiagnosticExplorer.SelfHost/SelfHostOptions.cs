using System;

namespace DiagnosticExplorer.SelfHost;

/// <summary>Configures the local diagnostics viewer.</summary>
public sealed class SelfHostOptions
{
    /// <summary>The default loopback listener used when no URL is supplied.</summary>
    public const string DefaultUrl = "http://127.0.0.1:1234";

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