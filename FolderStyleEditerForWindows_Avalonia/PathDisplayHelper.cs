using System;
using System.IO;

namespace FolderStyleEditerForWindows
{
    public static class PathDisplayHelper
    {
        /// <summary>
        /// 解析路径，返回父路径和文件夹名称
        /// </summary>
        /// <param name="fullPath">完整路径</param>
        /// <returns>包含父路径和文件夹名称的元组</returns>
        public static (string parentPath, string folderName) ParsePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return (string.Empty, string.Empty);

            try
            {
                var directoryInfo = new DirectoryInfo(fullPath);
                string folderName = directoryInfo.Name;
                string parentPath = directoryInfo.Parent?.FullName ?? string.Empty;
                
                return (parentPath, folderName);
            }
            catch
            {
                // 如果路径无效，尝试简单的字符串分割
                if (fullPath.Contains(Path.DirectorySeparatorChar) || fullPath.Contains(Path.AltDirectorySeparatorChar))
                {
                    string separator = fullPath.Contains(Path.DirectorySeparatorChar) ? 
                        Path.DirectorySeparatorChar.ToString() : Path.AltDirectorySeparatorChar.ToString();
                    
                    var parts = fullPath.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        string folderName = parts[parts.Length - 1];
                        string parentPath = string.Join(separator, parts, 0, parts.Length - 1);
                        return (parentPath, folderName);
                    }
                }
                
                return (string.Empty, fullPath);
            }
        }

        /// <summary>
        /// 截断文件夹名称以适应指定宽度
        /// </summary>
        /// <param name="folderName">文件夹名称</param>
        /// <param name="maxLength">最大长度</param>
        /// <returns>截断后的文件夹名称</returns>
        public static string TruncateFolderName(string folderName, int maxLength = 30)
        {
            if (string.IsNullOrEmpty(folderName) || folderName.Length <= maxLength)
                return folderName;

            return folderName.Substring(folderName.Length - maxLength);
        }

        /// <summary>
        /// 计算路径显示需要的近似宽度（基于字符数估算）
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="charWidth">单个字符的近似宽度（像素）</param>
        /// <returns>近似宽度</returns>
        public static double EstimatePathWidth(string path, double charWidth = 8.0)
        {
            if (string.IsNullOrEmpty(path))
                return 0;

            return path.Length * charWidth;
        }
    }
}