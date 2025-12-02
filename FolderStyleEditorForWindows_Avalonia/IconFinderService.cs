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

    public class IconFinderService
    {
        [SupportedOSPlatform("windows")]
        public Task<List<string>> FindIconsAsync(string folderPath)
        {
            return Task.Run(() =>
            {
                var iconPaths = new List<string>();
                if (!Directory.Exists(folderPath))
                {
                    return iconPaths;
                }

                // 1. Scan root directory
                var rootFiles = Directory.GetFiles(folderPath)
                    .Where(f => SafeHasIcon(f))
                    .OrderBy(f => !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(f => f.Contains("uninstall", StringComparison.OrdinalIgnoreCase));
                iconPaths.AddRange(rootFiles);

                if (iconPaths.Count >= 4)
                {
                    return iconPaths.Take(4).ToList();
                }

                // 2. Scan second-level directories
                try
                {
                    var subDirs = Directory.GetDirectories(folderPath);
                    foreach (var dir in subDirs)
                    {
                        var subFiles = Directory.GetFiles(dir)
                            .Where(f => SafeHasIcon(f))
                            .OrderBy(f => !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            .ThenBy(f => f.Contains("uninstall", StringComparison.OrdinalIgnoreCase));
                        iconPaths.AddRange(subFiles);

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
                        var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                            .Where(f => SafeHasIcon(f) && !iconPaths.Contains(f))
                             .OrderBy(f => !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            .ThenBy(f => f.Contains("uninstall", StringComparison.OrdinalIgnoreCase));
                        iconPaths.AddRange(allFiles);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Ignore directories we can't access
                    }
                }

                return iconPaths.Distinct().ToList();
            });
        }

        /// <summary>
        /// 渐进式扫描：优先根目录与一层子目录，超时会提前上报已发现的图标路径。
        /// </summary>
        [SupportedOSPlatform("windows")]
        public async Task<List<string>> FindIconsIncrementalAsync(string folderPath, IProgress<IconScanProgress>? progress, CancellationToken cancellationToken)
        {
            var found = new List<string>();
            if (!Directory.Exists(folderPath)) return found;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            void Report(bool isCompleted = false)
            {
                progress?.Report(new IconScanProgress
                {
                    Found = found.Distinct().ToList(),
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
                        var files = Directory.GetFiles(current);
                        foreach (var file in files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                if (SafeHasIcon(file) && !found.Contains(file))
                                {
                                    found.Add(file);
                                }
                            }
                            catch
                            {
                                // 鏌愪簺鏂囦欢鍦ㄦ鏌ュ浘鏍囨椂鍙兘鎶涘紓甯革紝蹇界暐浠ヤ繚璇佹壂鎻忎笉涓柇
                            }

                            if (stopwatch.ElapsedMilliseconds >= 1000)
                            {
                                Report();
                            }
                        }

                        if (depth < maxDepth)
                        {
                            foreach (var dir in Directory.GetDirectories(current))
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
            return found.Distinct().ToList();
        }

        /// <summary>
        /// 浠庢寚瀹氱殑鏂囦欢寮傛鎻愬彇鎵€鏈夊浘鏍囷紝骞跺皢鍏跺寘瑁呬负 IconViewModel 鍒楄〃銆?        /// </summary>
        /// <param name="filePath">瑕佹彁鍙栧浘鏍囩殑鏂囦欢璺緞銆?/param>
        /// <returns>涓€涓寘鍚墍鏈夊浘鏍囪鍥炬ā鍨嬬殑鍒楄〃銆?/returns>
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

