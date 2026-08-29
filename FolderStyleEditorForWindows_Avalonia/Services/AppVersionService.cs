using System;
using System.Reflection;

namespace FolderStyleEditorForWindows.Services;

public sealed class AppVersionService
{
    private readonly string _version;

    public AppVersionService()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersionService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        _version = NormalizeVersion(informationalVersion)
            ?? assembly.GetName().Version?.ToString(3)
            ?? "unknown";
    }

    public string Version => _version;

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
}
