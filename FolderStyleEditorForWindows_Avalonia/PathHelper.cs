using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace FolderStyleEditorForWindows
{
    [SupportedOSPlatform("windows")]
    public static class PathHelper
    {
        public static async Task<(string key, string value)[]> ProcessIconPathAsync(
            string targetFolderPath, string rawIconPath)
        {
            if (string.IsNullOrEmpty(rawIconPath))
            {
                return Array.Empty<(string, string)>();
            }

            var parts = rawIconPath.Split(',');
            var iconFilePath = parts[0];
            int.TryParse(parts.Length > 1 ? parts[1] : "0", out int iconIndex);

            if (!Path.IsPathRooted(iconFilePath))
            {
                return new[] { ("IconResource", rawIconPath) };
            }

            if (iconFilePath.StartsWith(targetFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { ("IconResource", GetRelativePath(targetFolderPath, iconFilePath) + "," + iconIndex) };
            }

            return await HandleExternalIconAsync(targetFolderPath, iconFilePath, iconIndex);
        }

        private static async Task<(string key, string value)[]> HandleExternalIconAsync(
            string targetFolderPath, string iconFilePath, int chosenGroupIndex)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd(Path.DirectorySeparatorChar);
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System).TrimEnd(Path.DirectorySeparatorChar);

            bool underSystem = IsUnder(iconFilePath, system);

            if (underSystem)
            {
                var groups = IconExtractor.ListIconGroups(iconFilePath);
                if (chosenGroupIndex < 0 || chosenGroupIndex >= groups.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(chosenGroupIndex), $"索引超出范围（共有 {groups.Count} 个 RT_GROUP_ICON）");
                }

                ushort groupId = ParseGroupId(groups[chosenGroupIndex]);
                string norm = NormalizeToSystem32(iconFilePath, windows, system);
                string env = norm.Replace(windows, "%SystemRoot%", StringComparison.OrdinalIgnoreCase);

                return new[] {
                    ("IconResource", $"{env},-{groupId}")
                };
            }

            string iconDir = Path.Combine(targetFolderPath, ".ICON");
            if (!Directory.Exists(iconDir))
            {
                var di = Directory.CreateDirectory(iconDir);
                di.Attributes |= FileAttributes.Hidden | FileAttributes.System;
            }

            string newIconName = Path.GetFileNameWithoutExtension(iconFilePath) + $"_{chosenGroupIndex}.ico";
            string destination = Path.Combine(iconDir, newIconName);

            byte[]? icoBytes = IconExtractor.ExtractIconGroupAsIco(iconFilePath, chosenGroupIndex);
            if (icoBytes != null)
            {
                await File.WriteAllBytesAsync(destination, icoBytes);

                string rel = GetRelativePath(targetFolderPath, destination);

                return new[] {
                    ("IconFile", rel),
                    ("IconIndex", "0")
                };
            }

            return new[] { ("IconResource", $"{iconFilePath},{chosenGroupIndex}") };
        }

        private static ushort ParseGroupId(string name)
        {
            if (name.Length > 1 && name[0] == '#' && ushort.TryParse(name.AsSpan(1), out var id))
                return id;
            throw new NotSupportedException($"该组名不是整数资源：{name}（请走提取 .ico 路径）");
        }

        private static bool IsUnder(string path, string root)
        {
            var full = Path.GetFullPath(path);
            var r = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            return full.StartsWith(r, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeToSystem32(string src, string windows, string system32)
        {
            string syswow64 = Path.Combine(windows, "SysWOW64");
            if (src.StartsWith(syswow64, StringComparison.OrdinalIgnoreCase))
            {
                string file = Path.GetFileName(src);
                return Path.Combine(system32, file);
            }
            return src;
        }

        public static string GetRelativePath(string fromPath, string toPath)
        {
            var fromUri = new Uri(fromPath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? fromPath : fromPath + Path.DirectorySeparatorChar);
            var toUri = new Uri(toPath);
            var relativeUri = fromUri.MakeRelativeUri(toUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}