using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FolderStyleEditorForWindows
{
    public static class DesktopIniHelper
    {
        // 使用 P/Invoke 调用 kernel32.dll 中的 API 来读写 .ini 文件
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool WritePrivateProfileString(string section, string key, string? val, string filePath);

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
            var iniPath = Path.Combine(folderPath, IniFileName);

            if (!File.Exists(iniPath))
            {
                File.WriteAllText(iniPath, "", Encoding.Default);
                File.SetAttributes(iniPath, FileAttributes.System | FileAttributes.Hidden);
            }

            if (WritePrivateProfileString(SectionName, key, value, iniPath))
            {
                return;
            }

            var error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new Win32Exception(error);
            }

            throw new IOException($"Failed to write [{SectionName}] {key} in {iniPath}.");
        }
    }
}
