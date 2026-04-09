using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace FolderStyleEditorForWindows.Services;

[SupportedOSPlatform("windows")]
public sealed class FolderHiddenVisibilityService
{
    public Task<FolderHiddenVisibilityLevel> GetCurrentLevelAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException("Target folder does not exist.");
            }

            var attributes = File.GetAttributes(folderPath);
            return MapFromAttributes(attributes);
        }, cancellationToken);
    }

    public Task<bool> ApplyLevelAsync(string folderPath, FolderHiddenVisibilityLevel level, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException("Target folder does not exist.");
            }

            var current = File.GetAttributes(folderPath);
            var updated = MapToAttributes(current, level);
            if (updated == current)
            {
                return false;
            }

            File.SetAttributes(folderPath, updated);
            return true;
        }, cancellationToken);
    }

    private static FolderHiddenVisibilityLevel MapFromAttributes(FileAttributes attributes)
    {
        if ((attributes & FileAttributes.System) == FileAttributes.System)
        {
            return FolderHiddenVisibilityLevel.SystemHidden;
        }

        if ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
        {
            return FolderHiddenVisibilityLevel.Hidden;
        }

        return FolderHiddenVisibilityLevel.Visible;
    }

    private static FileAttributes MapToAttributes(FileAttributes current, FolderHiddenVisibilityLevel level)
    {
        var cleared = current & ~(FileAttributes.Hidden | FileAttributes.System);

        return level switch
        {
            FolderHiddenVisibilityLevel.Hidden => cleared | FileAttributes.Hidden,
            FolderHiddenVisibilityLevel.SystemHidden => cleared | FileAttributes.Hidden | FileAttributes.System,
            _ => cleared
        };
    }
}
