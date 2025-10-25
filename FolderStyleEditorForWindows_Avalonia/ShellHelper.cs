using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FolderStyleEditorForWindows
{
    [SupportedOSPlatform("windows")]
    public static class ShellHelper
    {
        #region P/Invoke Definitions

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

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);
        
        private const uint FCSM_ICONFILE = 0x00000010;
        private const uint FCS_FORCEWRITE = 0x00000002;
        
        #endregion

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public uint xHotspot;
            public uint yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
        }

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[]? lpvBits, ref BITMAPINFO lpbi, uint uUsage);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        private const int BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;

        /// <summary>
        /// 使用 SHGetSetFolderCustomSettings API 设置文件夹图标，可即时生效。
        /// </summary>
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
                Console.WriteLine($"设置图标失败，HRESULT: 0x{hr:X}");
            }
        }

        /// <summary>
        /// 移除文件夹的自定义图标。
        /// </summary>
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
        
        public static bool HasIcons(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            try
            {
                // A simple check: if ExtractIconEx reports at least one icon, we consider it to have icons.
                return ExtractIconEx(filePath, -1, null, null, 0) > 0;
            }
            catch
            {
                return false;
            }
        }
        
        public static void SaveIconToFile(string sourceFile, int iconIndex, string destinationPath)
        {
            if (string.IsNullOrEmpty(sourceFile) || !File.Exists(sourceFile))
            {
                throw new FileNotFoundException("源文件未找到。", sourceFile);
            }

            var icoBytes = IconExtractor.ExtractIconGroupAsIco(sourceFile, iconIndex);

            if (icoBytes == null || icoBytes.Length == 0)
            {
                throw new Exception($"无法从 '{sourceFile}' 提取图标。");
            }

            File.WriteAllBytes(destinationPath, icoBytes);
        }

        public static List<Avalonia.Media.Imaging.Bitmap> ExtractIconsForPreview(string filePath)
        {
            var extractedIcons = new List<Avalonia.Media.Imaging.Bitmap>();
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
                    var avaloniaBitmap = CreateAvaloniaBitmapFromIconHandle(hIcon);
                    if (avaloniaBitmap != null)
                    {
                        extractedIcons.Add(avaloniaBitmap);
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

        private static Avalonia.Media.Imaging.Bitmap? CreateAvaloniaBitmapFromIconHandle(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero) return null;
            if (!GetIconInfo(hIcon, out ICONINFO iconInfo))
                return null;

            try
            {
                var dc = CreateCompatibleDC(IntPtr.Zero);
                if (dc == IntPtr.Zero) return null;

                try
                {
                    var bmi = new BITMAPINFO();
                    bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();

                    if (GetDIBits(dc, iconInfo.hbmColor, 0, 0, null, ref bmi, DIB_RGB_COLORS) == 0)
                        return null;

                    int width = bmi.bmiHeader.biWidth;
                    int height = Math.Abs(bmi.bmiHeader.biHeight);
                    if (width <= 0 || height <= 0) return null;

                    if (bmi.bmiHeader.biBitCount != 32)
                        return null;

                    bmi.bmiHeader.biPlanes = 1;
                    bmi.bmiHeader.biBitCount = 32;
                    bmi.bmiHeader.biCompression = BI_RGB;
                    bmi.bmiHeader.biHeight = -height;
                    int stride = checked(width * 4);
                    var pixels = new byte[stride * height];

                    if (GetDIBits(dc, iconInfo.hbmColor, 0, (uint)height, pixels, ref bmi, DIB_RGB_COLORS) == 0)
                        return null;

                    bool alphaAllZero = true;
                    for (int i = 3; i < pixels.Length; i += 4)
                    {
                        if (pixels[i] != 0) { alphaAllZero = false; break; }
                    }

                    if (alphaAllZero && iconInfo.hbmMask != IntPtr.Zero)
                    {
                        var maskBmi = new BITMAPINFO();
                        maskBmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
                        if (GetDIBits(dc, iconInfo.hbmMask, 0, 0, null, ref maskBmi, DIB_RGB_COLORS) != 0)
                        {
                            int mw = maskBmi.bmiHeader.biWidth;
                            int mh = Math.Abs(maskBmi.bmiHeader.biHeight);
                            if (mw == width && mh >= height)
                            {
                                int maskStride = ((mw + 31) / 32) * 4;
                                var maskBits = new byte[maskStride * height];
                                maskBmi.bmiHeader.biPlanes = 1;
                                maskBmi.bmiHeader.biBitCount = 1;
                                maskBmi.bmiHeader.biCompression = BI_RGB;
                                maskBmi.bmiHeader.biHeight = -height;

                                if (GetDIBits(dc, iconInfo.hbmMask, 0, (uint)height, maskBits, ref maskBmi, DIB_RGB_COLORS) != 0)
                                {
                                    for (int y = 0; y < height; y++)
                                    {
                                        int rowOffset = y * stride;
                                        int mrow = y * maskStride;
                                        for (int x = 0; x < width; x++)
                                        {
                                            int mbyte = mrow + (x >> 3);
                                            int mbit = 7 - (x & 7);
                                            bool isTransparent = (maskBits[mbyte] & (1 << mbit)) != 0;
                                            pixels[rowOffset + x * 4 + 3] = (byte)(isTransparent ? 0 : 255);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        byte b = pixels[i];
                        byte g = pixels[i + 1];
                        byte r = pixels[i + 2];
                        byte a = pixels[i + 3];

                        if (a == 0)
                        {
                            pixels[i] = pixels[i + 1] = pixels[i + 2] = 0;
                        }
                        else if (a < 255)
                        {
                            pixels[i] = (byte)((b * a + 127) / 255);
                            pixels[i + 1] = (byte)((g * a + 127) / 255);
                            pixels[i + 2] = (byte)((r * a + 127) / 255);
                        }
                    }

                    var bmp = new Avalonia.Media.Imaging.WriteableBitmap(
                        new Avalonia.PixelSize(width, height),
                        new Avalonia.Vector(96, 96),
                        Avalonia.Platform.PixelFormat.Bgra8888,
                        Avalonia.Platform.AlphaFormat.Premul);

                    using (var fb = bmp.Lock())
                    {
                        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
                    }
                    return bmp;
                }
                finally
                {
                    if (dc != IntPtr.Zero) DeleteDC(dc);
                }
            }
            finally
            {
                if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
                if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
            }
        }
    }
}