using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FolderStyleEditorForWindows
{
    public static class DesktopIniHelper
    {
        // 使用 P/Invoke 调用 kernel32.dll 中的 API 来读写 .ini 文件
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string section, string key, string? val, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        private const string SectionName = ".ShellClassInfo";
        private const string IniFileName = "desktop.ini";

        /// <summary>
        /// 读取指定文件夹中 desktop.ini 的一个键值
        /// </summary>
        public static string ReadValue(string folderPath, string key)
        {
            var iniPath = Path.Combine(folderPath, IniFileName);
            if (!File.Exists(iniPath))
            {
                return "";
            }

            StringBuilder sb = new StringBuilder(255);
            GetPrivateProfileString(SectionName, key, "", sb, 255, iniPath);
            return sb.ToString();
        }

        /// <summary>
        /// 向指定文件夹的 desktop.ini 写入一个键值
        /// </summary>
        public static void WriteValue(string folderPath, string key, string? value)
        {
            try
            {
                var iniPath = Path.Combine(folderPath, IniFileName);

                // 确保文件存在且具有正确的编码和属性
                if (!File.Exists(iniPath))
                {
                    // 使用系统默认 ANSI 编码创建文件
                    File.WriteAllText(iniPath, "", Encoding.Default);
                    File.SetAttributes(iniPath, FileAttributes.System | FileAttributes.Hidden);
                }

                WritePrivateProfileString(SectionName, key, value, iniPath);
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore. The user might be trying to edit a folder they don't have access to.
                // In the future, we can show a toast notification here.
            }
            catch (IOException)
            {
                // Ignore. Another I/O error occurred.
            }
        }
    }
}