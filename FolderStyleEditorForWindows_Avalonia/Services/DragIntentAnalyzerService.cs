using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace FolderStyleEditorForWindows.Services
{
    public sealed class DragIntentAnalyzerService
    {
        private readonly ConcurrentDictionary<string, (bool hasIcons, DateTime cachedAtUtc)> _hasIconsCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".svg",
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp",
            ".webp"
        };
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

        public DragIntentResult AnalyzeImmediate(
            IDataObject data,
            DragContext context,
            string? currentFolderPath)
        {
            var files = MaterializeStorageItems(data);
            var text = data.GetText();

            if (context == DragContext.Home)
            {
                return AnalyzeForHome(files);
            }

            return AnalyzeForEditImmediate(files, text, currentFolderPath);
        }

        public async Task<DragIntentResult> AnalyzeAsync(
            IDataObject data,
            DragContext context,
            string? currentFolderPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files = MaterializeStorageItems(data);
            var text = data.GetText();

            if (context == DragContext.Home)
            {
                return AnalyzeForHome(files);
            }

            return await AnalyzeForEditAsync(files, text, currentFolderPath, cancellationToken).ConfigureAwait(false);
        }

        private static DragIntentResult AnalyzeForHome(IReadOnlyList<IStorageItem> items)
        {
            var folderCount = CountFolders(items);
            if (folderCount == 1 && items.Count == 1)
            {
                return new DragIntentResult
                {
                    Type = DragIntentType.SingleFolder,
                    MainTextKey = "Drag_SingleFolder_Main",
                    IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/folder-plus.svg",
                    CanDrop = true
                };
            }

            if (folderCount > 1 && folderCount == items.Count)
            {
                return new DragIntentResult
                {
                    Type = DragIntentType.MultiFolder,
                    MainTextKey = "Drag_MultiFolder_Main",
                    SubTextKey = "Drag_MultiFolder_Dev",
                    IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/folders.svg",
                    SubTextBrush = "#E07167",
                    CanDrop = false
                };
            }

            return DragIntentResult.Unsupported;
        }

        private async Task<DragIntentResult> AnalyzeForEditAsync(
            IReadOnlyList<IStorageItem> items,
            string? text,
            string? currentFolderPath,
            CancellationToken cancellationToken)
        {
            var folderCount = CountFolders(items);
            if (folderCount == 1 && items.Count == 1)
            {
                return new DragIntentResult
                {
                    Type = DragIntentType.SingleFolder,
                    MainTextKey = "Drag_SingleFolder_Main",
                    IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/folder-plus.svg",
                    CanDrop = true
                };
            }

            if (folderCount > 1 && folderCount == items.Count)
            {
                return new DragIntentResult
                {
                    Type = DragIntentType.MultiFolder,
                    MainTextKey = "Drag_MultiFolder_Main",
                    SubTextKey = "Drag_MultiFolder_Dev",
                    IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/folders.svg",
                    SubTextBrush = "#E07167",
                    CanDrop = false
                };
            }

            if (items.Count == 1 && items[0] is IStorageFile storageFile)
            {
                var path = storageFile.Path.LocalPath;
                var ext = Path.GetExtension(storageFile.Name).ToLowerInvariant();

                if (ext == ".ico")
                {
                    var subKey = IsUnderFolder(path, currentFolderPath) ? null : "Drag_Ico_External_Warn";
                    return new DragIntentResult
                    {
                        Type = DragIntentType.Ico,
                        MainTextKey = "Drag_Ico_Main",
                        SubTextKey = subKey,
                        IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/book-image.svg",
                        SubTextBrush = subKey == null ? null : "#E07167",
                        CanDrop = true
                    };
                }

                if (SupportedImageExtensions.Contains(ext))
                {
                    return new DragIntentResult
                    {
                        Type = DragIntentType.ImageToIcon,
                        MainTextKey = "Drag_Image_Main",
                        SubTextKey = "Drag_Image_Sub",
                        IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/book-image.svg",
                        CanDrop = true
                    };
                }

                if (ext == ".exe")
                {
                    var isInternal = IsUnderFolder(path, currentFolderPath);
                    var hasIcons = await CanUseExecutableAsync(path, cancellationToken).ConfigureAwait(false);
                    if (!hasIcons)
                    {
                        return DragIntentResult.Unsupported;
                    }

                    return new DragIntentResult
                    {
                        Type = isInternal ? DragIntentType.ExeInternal : DragIntentType.ExeExternal,
                        MainTextKey = isInternal ? "Drag_Exe_Internal_Main" : "Drag_Exe_External_Main",
                        SubTextKey = isInternal ? null : "Drag_Exe_External_Sub",
                        IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/app-window.svg",
                        SubTextBrush = isInternal ? null : "#E07167",
                        CanDrop = true
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                return new DragIntentResult
                {
                    Type = DragIntentType.Text,
                    MainTextKey = "Drag_Text_Main",
                    IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/text-initial.svg",
                    CanDrop = true
                };
            }

            return DragIntentResult.Unsupported;
        }

        private static DragIntentResult AnalyzeForEditImmediate(
            IReadOnlyList<IStorageItem> items,
            string? text,
            string? currentFolderPath)
        {
            var folderCount = CountFolders(items);
            if (folderCount == 1 && items.Count == 1)
            {
                return new DragIntentResult
                {
                    Type = DragIntentType.SingleFolder,
                    MainTextKey = "Drag_SingleFolder_Main",
                    IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/folder-plus.svg",
                    CanDrop = true
                };
            }

            if (folderCount > 1 && folderCount == items.Count)
            {
                return new DragIntentResult
                {
                    Type = DragIntentType.MultiFolder,
                    MainTextKey = "Drag_MultiFolder_Main",
                    SubTextKey = "Drag_MultiFolder_Dev",
                    IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/folders.svg",
                    SubTextBrush = "#E07167",
                    CanDrop = false
                };
            }

            if (items.Count == 1 && items[0] is IStorageFile storageFile)
            {
                var path = storageFile.Path.LocalPath;
                var ext = Path.GetExtension(storageFile.Name).ToLowerInvariant();

                if (ext == ".ico")
                {
                    var subKey = IsUnderFolder(path, currentFolderPath) ? null : "Drag_Ico_External_Warn";
                    return new DragIntentResult
                    {
                        Type = DragIntentType.Ico,
                        MainTextKey = "Drag_Ico_Main",
                        SubTextKey = subKey,
                        IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/book-image.svg",
                        SubTextBrush = subKey == null ? null : "#E07167",
                        CanDrop = true
                    };
                }

                if (SupportedImageExtensions.Contains(ext))
                {
                    return new DragIntentResult
                    {
                        Type = DragIntentType.ImageToIcon,
                        MainTextKey = "Drag_Image_Main",
                        SubTextKey = "Drag_Image_Sub",
                        IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/book-image.svg",
                        CanDrop = true
                    };
                }

                if (ext == ".exe")
                {
                    var isInternal = IsUnderFolder(path, currentFolderPath);
                    return new DragIntentResult
                    {
                        Type = isInternal ? DragIntentType.ExeInternal : DragIntentType.ExeExternal,
                        MainTextKey = isInternal ? "Drag_Exe_Internal_Main" : "Drag_Exe_External_Main",
                        SubTextKey = isInternal ? null : "Drag_Exe_External_Sub",
                        IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/app-window.svg",
                        SubTextBrush = isInternal ? null : "#E07167",
                        CanDrop = true
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                return new DragIntentResult
                {
                    Type = DragIntentType.Text,
                    MainTextKey = "Drag_Text_Main",
                    IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/text-initial.svg",
                    CanDrop = true
                };
            }

            return DragIntentResult.Unsupported;
        }

        private async Task<bool> CanUseExecutableAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            if (_hasIconsCache.TryGetValue(path, out var cached))
            {
                if (DateTime.UtcNow - cached.cachedAtUtc < CacheTtl)
                {
                    return cached.hasIcons;
                }
            }

            var hasIcons = await Task.Run(() => ShellHelper.HasIcons(path), cancellationToken).ConfigureAwait(false);
            _hasIconsCache[path] = (hasIcons, DateTime.UtcNow);
            return hasIcons;
        }

        private static IReadOnlyList<IStorageItem> MaterializeStorageItems(IDataObject data)
        {
            if (data.GetFiles() is not { } files)
            {
                return Array.Empty<IStorageItem>();
            }

            if (files is IReadOnlyList<IStorageItem> readOnlyList)
            {
                return readOnlyList;
            }

            var materialized = new List<IStorageItem>();
            foreach (var item in files)
            {
                materialized.Add(item);
            }

            return materialized;
        }

        private static int CountFolders(IReadOnlyList<IStorageItem> items)
        {
            var folderCount = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is IStorageFolder)
                {
                    folderCount++;
                }
            }

            return folderCount;
        }

        private static bool IsUnderFolder(string? filePath, string? folderPath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(folderPath))
            {
                return false;
            }

            var fileDir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(fileDir))
            {
                return false;
            }

            var fullFolder = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullDir = Path.GetFullPath(fileDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullDir.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase);
        }
    }
}
