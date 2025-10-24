using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Media.Imaging;
using FolderStyleEditerForWindows;

namespace FolderStyleEditerForWindows
{
    [SupportedOSPlatform("windows")]
    public static class ShellHelper
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint SHGetSetFolderCustomSettings(ref LPSHFOLDERCUSTOMSETTINGS pfcs, string pszPath, uint dwReadWrite);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct LPSHFOLDERCUSTOMSETTINGS
        {
            public uint dwSize;
            public uint dwMask;
            public IntPtr pvid;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszWebViewTemplate;
            public uint cchWebViewTemplate;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszWebViewTemplateVersion;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszInfoTip;
            public uint cchInfoTip;
            public IntPtr pclsid;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszIconFile;
            public uint cchIconFile;
            public int iIconIndex;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszLogo;
            public uint cchLogo;
        }

        private const uint FCSM_ICONFILE = 0x00000010;
        private const uint FCS_FORCEWRITE = 0x00000002;

        /// <summary>
        /// 使用 SHGetSetFolderCustomSettings API 设置文件夹图标，可即时生效。
        /// </summary>
        /// <param name="folderPath">文件夹的完整路径。</param>
        /// <param name="iconResource">图标资源路径，格式为 "C:\path\to\icon.ico,0"。</param>
        public static void SetFolderIcon(string folderPath, string iconResource)
        {
            string iconFile;
            int iconIndex = 0;

            if (string.IsNullOrEmpty(iconResource))
            {
                RemoveFolderIcon(folderPath);
                return;
            }

            var parts = iconResource.Split(',');
            iconFile = parts[0];
            if (parts.Length > 1)
            {
                int.TryParse(parts[1], out iconIndex);
            }

            var settings = new LPSHFOLDERCUSTOMSETTINGS
            {
                dwSize = (uint)Marshal.SizeOf(typeof(LPSHFOLDERCUSTOMSETTINGS)),
                dwMask = FCSM_ICONFILE,
                pszIconFile = iconFile,
                iIconIndex = iconIndex
            };

            uint hr = SHGetSetFolderCustomSettings(ref settings, folderPath, FCS_FORCEWRITE);
            if (hr != 0)
            {
                // 可以添加错误处理
                Console.WriteLine($"设置图标失败，HRESULT: 0x{hr:X}");
            }
        }

        /// <summary>
        /// 移除文件夹的自定义图标。
        /// </summary>
        /// <param name="folderPath">文件夹路径。</param>
        public static void RemoveFolderIcon(string folderPath)
        {
            var settings = new LPSHFOLDERCUSTOMSETTINGS
            {
                dwSize = (uint)Marshal.SizeOf(typeof(LPSHFOLDERCUSTOMSETTINGS)),
                dwMask = FCSM_ICONFILE,
                pszIconFile = "", // 传递空字符串或null来清除
                iIconIndex = 0
            };

            uint hr = SHGetSetFolderCustomSettings(ref settings, folderPath, FCS_FORCEWRITE);
            if (hr != 0)
            {
                Console.WriteLine($"移除图标失败，HRESULT: 0x{hr:X}");
            }

            // API调用后，清理desktop.ini中的相关条目
            string desktopIniPath = Path.Combine(folderPath, "desktop.ini");
            if (File.Exists(desktopIniPath))
            {
                try
                {
                    File.SetAttributes(desktopIniPath, File.GetAttributes(desktopIniPath) & ~FileAttributes.ReadOnly & ~FileAttributes.System);
                    DesktopIniHelper.WriteValue(folderPath, "IconResource", null);
                    DesktopIniHelper.WriteValue(folderPath, "IconFile", null);
                    DesktopIniHelper.WriteValue(folderPath, "IconIndex", null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"清理 desktop.ini 文件失败: {ex.Message}");
                }
            }
            
            // 确保文件夹的只读属性被移除，因为设置图标会自动添加它
            try
            {
                var dirInfo = new DirectoryInfo(folderPath);
                if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    dirInfo.Attributes &= ~FileAttributes.ReadOnly;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"移除文件夹只读属性失败: {ex.Message}");
            }
        }
        
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);
        
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);
        
        public static bool HasIcons(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            try
            {
                return ExtractIconEx(filePath, -1, null, null, 0) > 0;
            }
            catch
            {
                return false;
            }
        }
        
        public static List<Bitmap> ExtractIconsFromFile(string filePath)
        {
            var extractedIcons = new List<Bitmap>();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return extractedIcons;
            }

            uint iconCount = ExtractIconEx(filePath, -1, null, null, 0);
            if (iconCount == 0)
            {
                return extractedIcons;
            }

            var largeIconHandles = new IntPtr[iconCount];
            uint extractedCount = ExtractIconEx(filePath, 0, largeIconHandles, null, iconCount);
            if (extractedCount == 0)
            {
                return extractedIcons;
            }

            for (int i = 0; i < extractedCount; i++)
            {
                IntPtr hIcon = largeIconHandles[i];
                if (hIcon == IntPtr.Zero) continue;
                
                try
                {
                    using (var icon = System.Drawing.Icon.FromHandle(hIcon))
                    {
                        using (var gdiBitmap = icon.ToBitmap())
                        {
                            using (var ms = new MemoryStream())
                            {
                                gdiBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                ms.Seek(0, SeekOrigin.Begin);
                                var avaloniaBitmap = new Bitmap(ms);
                                extractedIcons.Add(avaloniaBitmap);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error converting icon at index {i}: {ex.Message}");
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }

            return extractedIcons;
        }

        /// <summary>
        /// 从指定文件中提取单个图标并将其保存为 .ico 文件。
        /// </summary>
        /// <param name="sourceFile">包含图标的源文件路径 (.exe, .dll)。</param>
        /// <param name="iconIndex">要提取的图标的索引。</param>
        /// <param name="destinationPath">保存 .ico 文件的目标路径。</param>
        public static void SaveIconToFile(string sourceFile, int iconIndex, string destinationPath)
        {
            if (string.IsNullOrEmpty(sourceFile) || !File.Exists(sourceFile))
            {
                throw new FileNotFoundException("源文件未找到。", sourceFile);
            }

            var iconHandles = new IntPtr[1];
            uint extractedCount = ExtractIconEx(sourceFile, iconIndex, iconHandles, null, 1);

            if (extractedCount == 0 || iconHandles[0] == IntPtr.Zero)
            {
                throw new Exception($"无法从 '{sourceFile}' 提取索引为 {iconIndex} 的图标。");
            }

            IntPtr hIcon = iconHandles[0];
            try
            {
                using (var icon = System.Drawing.Icon.FromHandle(hIcon))
                {
                    using (var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                    {
                        icon.Save(fs);
                    }
                }
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
    }
}