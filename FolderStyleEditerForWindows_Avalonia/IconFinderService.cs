using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FolderStyleEditerForWindows.ViewModels;

namespace FolderStyleEditerForWindows
{
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
                    .Where(f => HasIcon(f))
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
                            .Where(f => HasIcon(f))
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
                            .Where(f => HasIcon(f) && !iconPaths.Contains(f))
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
        /// 从指定的文件异步提取所有图标，并将其包装为 IconViewModel 列表。
        /// </summary>
        /// <param name="filePath">要提取图标的文件路径。</param>
        /// <returns>一个包含所有图标视图模型的列表。</returns>
        [SupportedOSPlatform("windows")]
        public Task<List<IconViewModel>> ExtractIconsFromFileAsync(string filePath)
        {
            return Task.Run(() =>
            {
                var icons = ShellHelper.ExtractIconsFromFile(filePath);
                var iconViewModels = new List<IconViewModel>();
                for (int i = 0; i < icons.Count; i++)
                {
                    iconViewModels.Add(new IconViewModel(icons[i], filePath, i));
                }
                return iconViewModels;
            });
        }

        [SupportedOSPlatform("windows")]
        private bool HasIcon(string filePath)
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