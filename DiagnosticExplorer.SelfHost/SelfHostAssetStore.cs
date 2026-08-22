using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiagnosticExplorer.SelfHost;

internal static class SelfHostAssetStore
{
    private const string ResourcePrefix = "DiagnosticExplorer.SelfHost.Assets.";

    public static bool TryOpen(string path, out Stream stream, out string contentType, out bool isIndex)
    {
        string assetPath = Normalize(path);
        isIndex = string.Equals(assetPath, "index.html", StringComparison.OrdinalIgnoreCase);
        contentType = GetContentType(assetPath);
        stream = typeof(SelfHostAssetStore).Assembly.GetManifestResourceStream(ResourcePrefix + assetPath.Replace('/', '.'));

        if (stream != null)
            return true;

        if (!Path.HasExtension(assetPath))
        {
            assetPath = "index.html";
            isIndex = true;
            contentType = "text/html; charset=utf-8";
            stream = typeof(SelfHostAssetStore).Assembly.GetManifestResourceStream(ResourcePrefix + assetPath);
        }

        return stream != null;
    }

    private static string Normalize(string path)
    {
        string value = (path ?? string.Empty).Trim().TrimStart('/');
        if (string.IsNullOrEmpty(value) || value.Contains("..", StringComparison.Ordinal))
            return "index.html";

        return value;
    }

    private static string GetContentType(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
    }
}