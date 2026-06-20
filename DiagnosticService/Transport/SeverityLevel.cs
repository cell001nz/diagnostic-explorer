namespace Diagnostics.Service.Common.Transport;

/// <summary>
/// Mirrors Microsoft.Extensions.Logging.LogLevel ordinals, the canonical
/// numeric level scheme used across the backend and web clients.
/// </summary>
public enum SeverityLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6
}