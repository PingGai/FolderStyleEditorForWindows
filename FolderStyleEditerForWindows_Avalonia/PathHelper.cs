using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using FolderStyleEditerForWindows;

namespace FolderStyleEditerForWindows
{
    [SupportedOSPlatform("windows")]
    public static class PathHelper
    {
        /// <summary>
        /// 将给定的图标路径处理成最终要写入 desktop.ini 的相对路径。
        /// </summary>
        /// <param name="targetFolderPath">正在编辑的目标文件夹。</param>
        /// <param name="rawIconPath">原始的图标路径（可能包含索引）。</param>
        /// <returns>一个表示相对路径的字符串。</returns>
        public static async Task<string> ProcessIconPathAsync(string targetFolderPath, string rawIconPath)
        {
            if (string.IsNullOrEmpty(rawIconPath))
            {
                return "";
            }

            var parts = rawIconPath.Split(',');
            var iconFilePath = parts[0];
            int.TryParse(parts.Length > 1 ? parts[1] : "0", out int iconIndex);

            // 检查 iconFilePath 是否已经是相对路径
            if (!Path.IsPathRooted(iconFilePath))
            {
                return rawIconPath; // 已经是相对路径，直接返回
            }

            // 检查图标文件是否在目标文件夹内
            if (iconFilePath.StartsWith(targetFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                return GetRelativePath(targetFolderPath, iconFilePath) + "," + iconIndex;
            }
            
            // 处理外部图标
            return await HandleExternalIconAsync(targetFolderPath, iconFilePath, iconIndex);
        }

        private static async Task<string> HandleExternalIconAsync(string targetFolderPath, string iconFilePath, int iconIndex)
        {
            // 检查是否是系统 shell32.dll
            var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var shell32Path = Path.Combine(systemPath, "shell32.dll");
            if (string.Equals(iconFilePath, shell32Path, StringComparison.OrdinalIgnoreCase))
            {
                return $"{iconFilePath},{iconIndex}"; // 如果是，则直接返回原始路径
            }

            var iconDir = Path.Combine(targetFolderPath, ".ICON");
            if (!Directory.Exists(iconDir))
            {
                var di = Directory.CreateDirectory(iconDir);
                di.Attributes |= FileAttributes.Hidden | FileAttributes.System;
            }

            var extension = Path.GetExtension(iconFilePath).ToLowerInvariant();
            var newIconName = Path.GetFileNameWithoutExtension(iconFilePath) + $"_{iconIndex}" + ".ico";
            var destinationPath = Path.Combine(iconDir, newIconName);

            if (extension == ".ico" || extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp")
            {
                await Task.Run(() => File.Copy(iconFilePath, destinationPath, true));
                return GetRelativePath(targetFolderPath, destinationPath) + ",0";
            }
            
            if (extension == ".exe" || extension == ".dll")
            {
                await Task.Run(() => ShellHelper.SaveIconToFile(iconFilePath, iconIndex, destinationPath));
                return GetRelativePath(targetFolderPath, destinationPath) + ",0";

            }

            // 对于不支持的类型，返回其原始的绝对路径作为回退
            return $"{iconFilePath},{iconIndex}";
        }

        /// <summary>
        /// 计算从一个路径到另一个路径的相对路径。
        /// </summary>
        /// <param name="fromPath">起始文件夹。</param>
        /// <param name="toPath">目标文件。</param>
        /// <returns>相对路径字符串。</returns>
        public static string GetRelativePath(string fromPath, string toPath)
        {
            var fromUri = new Uri(fromPath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? fromPath : fromPath + Path.DirectorySeparatorChar);
            var toUri = new Uri(toPath);
            var relativeUri = fromUri.MakeRelativeUri(toUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}