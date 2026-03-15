using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using FolderStyleEditorForWindows.ViewModels;

namespace FolderStyleEditorForWindows
{
    public sealed class IconScanProgress
    {
        public required List<string> Found { get; init; }
        public bool IsCompleted { get; init; }
    }

    public sealed class IconExtractionProgress
    {
        public required List<IconViewModel> Batch { get; init; }
        public bool IsCompleted { get; init; }
    }

    public class IconFinderService
    {
        private const int IncrementalScanReportIntervalMs = 180;

        [SupportedOSPlatform("windows")]
        public Task<List<string>> FindIconsAsync(string folderPath)
        {
            return Task.Run(() =>
            {
                var iconPaths = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!Directory.Exists(folderPath))
                {
                    return iconPaths;
                }

                // 1. Scan root directory
                var rootFiles = Directory.EnumerateFiles(folderPath)
                    .Where(f => SafeHasIcon(f))
                    .OrderBy(f => !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(f => f.Contains("uninstall", StringComparison.OrdinalIgnoreCase));
                foreach (var file in rootFiles)
                {
                    if (seen.Add(file))
                    {
                        iconPaths.Add(file);
                    }
                }

                if (iconPaths.Count >= 4)
                {
                    return iconPaths.Take(4).ToList();
                }

                // 2. Scan second-level directories
                try
                {
                    var subDirs = Directory.EnumerateDirectories(folderPath);
                    foreach (var dir in subDirs)
                    {
                        var subFiles = Directory.EnumerateFiles(dir)
                            .Where(f => SafeHasIcon(f))
                            .OrderBy(f => !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            .ThenBy(f => f.Contains("uninstall", StringComparison.OrdinalIgnoreCase));
                        foreach (var file in subFiles)
                        {
                            if (seen.Add(file))
                            {
                                iconPaths.Add(file);
                            }
                        }

                        if (iconPaths.Count >= 4)
                        {
                            return iconPaths.Take(4).ToList();
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore directories we can't access
                }

                // 3. If still not enough, scan all directories recursively
                if (iconPaths.Count < 4)
                {
                    try
                    {
                        var allFiles = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                            .Where(SafeHasIcon)
                            .OrderBy(f => !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            .ThenBy(f => f.Contains("uninstall", StringComparison.OrdinalIgnoreCase));
                        foreach (var file in allFiles)
                        {
                            if (seen.Add(file))
                            {
                                iconPaths.Add(file);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Ignore directories we can't access
                    }
                }

                return iconPaths;
            });
        }

        /// <summary>
        /// 渐进式扫描：优先根目录与一层子目录，超时会提前上报已发现的图标路径。
        /// </summary>
        [SupportedOSPlatform("windows")]
        public async Task<List<string>> FindIconsIncrementalAsync(string folderPath, IProgress<IconScanProgress>? progress, CancellationToken cancellationToken)
        {
            var found = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasReportedFirstResult = false;
            if (!Directory.Exists(folderPath)) return found;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            void Report(bool isCompleted = false)
            {
                progress?.Report(new IconScanProgress
                {
                    Found = new List<string>(found),
                    IsCompleted = isCompleted
                });
                stopwatch.Restart();
            }

            await Task.Run(() =>
            {
                var queue = new Queue<(string path, int depth)>();
                queue.Enqueue((folderPath, 0));
                const int maxDepth = 6;

                while (queue.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var (current, depth) = queue.Dequeue();

                    try
                    {
                        var files = Directory.EnumerateFiles(current);
                        foreach (var file in files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                if (SafeHasIcon(file) && seen.Add(file))
                                {
                                    found.Add(file);
                                    if (!hasReportedFirstResult)
                                    {
                                        Report();
                                        hasReportedFirstResult = true;
                                    }
                                }
                            }
                            catch
                            {
                                // 鏌愪簺鏂囦欢鍦ㄦ鏌ュ浘鏍囨椂鍙兘鎶涘紓甯革紝蹇界暐浠ヤ繚璇佹壂鎻忎笉涓柇
                            }

                            if (stopwatch.ElapsedMilliseconds >= IncrementalScanReportIntervalMs)
                            {
                                Report();
                            }
                        }

                        if (depth < maxDepth)
                        {
                            foreach (var dir in Directory.EnumerateDirectories(current))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                try
                                {
                                    var attr = File.GetAttributes(dir);
                                    if (attr.HasFlag(FileAttributes.ReparsePoint))
                                    {
                                        continue;
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                                queue.Enqueue((dir, depth + 1));
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // skip
                    }
                    catch (IOException)
                    {
                        // skip
                    }
                }
            }, cancellationToken);

            Report(isCompleted: true);
            return found;
        }

        /// <summary>
        /// 从指定文件中提取可供选择器展示的图标列表。
        /// <param name="filePath">要解析的文件路径。</param>
        /// <returns>包含图标预览数据的列表。</returns>
        [SupportedOSPlatform("windows")]
        public Task<List<IconViewModel>> ExtractIconsFromFileAsync(string filePath)
        {
            return Task.Run(() =>
            {
                var iconViewModels = new List<IconViewModel>();
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".ico")
                {
                    try
                    {
                        var icoBytes = File.ReadAllBytes(filePath);
                        using (var ms = new MemoryStream(icoBytes))
                        {
                            var bitmap = new Bitmap(ms);
                            iconViewModels.Add(new IconViewModel(bitmap, filePath, 0));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load .ico file: {ex.Message}");
                    }
                }
                else // For .exe, .dll
                {
                    var icons = ShellHelper.ExtractIconsForPreview(filePath);
                    for (int i = 0; i < icons.Count; i++)
                    {
                        iconViewModels.Add(new IconViewModel(icons[i], filePath, i));
                    }
                }
                
                return iconViewModels;
            });
        }

        [SupportedOSPlatform("windows")]
        public Task ExtractIconsFromFileIncrementalAsync(
            string filePath,
            IProgress<IconExtractionProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".ico")
                {
                    try
                    {
                        var icoBytes = File.ReadAllBytes(filePath);
                        using var ms = new MemoryStream(icoBytes);
                        var bitmap = new Bitmap(ms);
                        progress?.Report(new IconExtractionProgress
                        {
                            Batch = new List<IconViewModel> { new IconViewModel(bitmap, filePath, 0) },
                            IsCompleted = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load .ico file: {ex.Message}");
                        progress?.Report(new IconExtractionProgress
                        {
                            Batch = new List<IconViewModel>(),
                            IsCompleted = true
                        });
                    }

                    return;
                }

                var batch = new List<IconViewModel>();
                const int batchSize = 15;
                var flushStopwatch = Stopwatch.StartNew();

                void Flush(bool isCompleted)
                {
                    if (batch.Count == 0 && !isCompleted)
                    {
                        return;
                    }

                    progress?.Report(new IconExtractionProgress
                    {
                        Batch = batch.Count == 0 ? new List<IconViewModel>() : batch,
                        IsCompleted = isCompleted
                    });
                    batch = new List<IconViewModel>();
                    flushStopwatch.Restart();
                }

                ShellHelper.ExtractIconsForPreviewIncremental(
                    filePath,
                    (bitmap, index) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        batch.Add(new IconViewModel(bitmap, filePath, index));
                        if (batch.Count >= batchSize || flushStopwatch.ElapsedMilliseconds >= 24)
                        {
                            Flush(isCompleted: false);
                        }
                    },
                    cancellationToken);

                Flush(isCompleted: true);
            }, cancellationToken);
        }

        [SupportedOSPlatform("windows")]
        private bool SafeHasIcon(string filePath)
        {
            try
            {
                return HasIconInternal(filePath);
            }
            catch
            {
                return false;
            }
        }

        [SupportedOSPlatform("windows")]
        private bool HasIconInternal(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".ico")
            {
                return true;
            }

            if (ext == ".exe" || ext == ".dll")
            {
                // We'll use P/Invoke to check for icons.
                // For now, we assume they have icons.
                return ShellHelper.HasIcons(filePath);
            }

            return false;
        }
    }
}
