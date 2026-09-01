using System;
using System.Reflection;

namespace FolderStyleEditorForWindows.Services;

public sealed class AppVersionService
{
    private readonly string _version;
    private readonly string _productVersion;

    public AppVersionService()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersionService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        _version = NormalizeVersion(informationalVersion)
            ?? assembly.GetName().Version?.ToString(3)
            ?? "unknown";
        _productVersion = ToProductVersion(_version);
    }

    public string Version => _productVersion;

    public string BuildVersion => _version;

    private static string? NormalizeVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var version = value.Trim();
        var metadataSeparator = version.IndexOf('+', StringComparison.Ordinal);
        if (metadataSeparator >= 0)
        {
            version = version[..metadataSeparator];
        }

        return version.TrimStart('v', 'V');
    }

    private static string ToProductVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length == 3 && parts[2].Length == 12 && long.TryParse(parts[2], out _))
        {
            return $"{parts[0]}.{parts[1]}";
        }

        return version;
    }
}
