using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace FolderStyleEditorForWindows
{
    public static class IconExtractor
    {
        // ===== Win32 基本常量/类型 =====
        private static readonly IntPtr RT_ICON = (IntPtr)3;
        private static readonly IntPtr RT_GROUP_ICON = (IntPtr)14;

        private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        private const uint LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;

        private delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpszType, EnumResNameProc lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        // ===== 资源结构（打包=2，对齐须与资源格式一致）=====
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct GRPICONDIR
        {
            public ushort Reserved; // 0
            public ushort Type;     // 1 for icon
            public ushort Count;    // number of entries
                                    // followed by GRPICONDIRENTRY[Count]
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct GRPICONDIRENTRY
        {
            public byte Width;       // 0 => 256
            public byte Height;      // 0 => 256
            public byte ColorCount;  // palette entries (0 if >=8bpp)
            public byte Reserved;    // 0
            public ushort Planes;
            public ushort BitCount;
            public uint BytesInRes;   // size of the corresponding RT_ICON
            public ushort ID;         // resource ID of RT_ICON
        }

        // ICONDIRENTRY for ICO file layout（Pack=2）
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct ICONDIR
        {
            public ushort Reserved; // 0
            public ushort Type;     // 1
            public ushort Count;    // n
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct ICONDIRENTRY
        {
            public byte Width;
            public byte Height;
            public byte ColorCount;
            public byte Reserved;
            public ushort Planes;
            public ushort BitCount;
            public uint BytesInRes;
            public uint ImageOffset;
        }

        public static List<string> ListIconGroups(string modulePath)
        {
            if (string.IsNullOrWhiteSpace(modulePath)) return new List<string>();

            IntPtr hMod = LoadLibraryEx(modulePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
            if (hMod == IntPtr.Zero) return new List<string>();

            var groupNames = new List<string>();
            try
            {
                EnumResourceNames(hMod, RT_GROUP_ICON, (m, type, name, lp) =>
                {
                    groupNames.Add(IS_INTRESOURCE(name) ? $"#{name}" : Marshal.PtrToStringUni(name) ?? "");
                    return true; // Continue enumeration
                }, IntPtr.Zero);
            }
            finally
            {
                if (hMod != IntPtr.Zero) FreeLibrary(hMod);
            }
            return groupNames;
        }

        public static byte[]? ExtractIconGroupAsIco(string modulePath, int groupIndex = 0)
        {
            if (string.IsNullOrWhiteSpace(modulePath)) throw new ArgumentException("modulePath is null/empty.");

            IntPtr hMod = LoadLibraryEx(modulePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
            if (hMod == IntPtr.Zero) return null;

            try
            {
                var groupNames = new List<IntPtr>();
                EnumResourceNames(hMod, RT_GROUP_ICON, (m, type, name, lp) =>
                {
                    groupNames.Add(name);
                    return true;
                }, IntPtr.Zero);

                if (groupIndex < 0 || groupIndex >= groupNames.Count)
                    return null; // Index out of bounds

                var hResInfo = FindResource(hMod, groupNames[groupIndex], RT_GROUP_ICON);
                if (hResInfo == IntPtr.Zero)
                    return null;

                var hResData = LoadResource(hMod, hResInfo);
                if (hResData == IntPtr.Zero)
                    return null;

                var pRes = LockResource(hResData);
                var cbRes = (int)SizeofResource(hMod, hResInfo);
                if (pRes == IntPtr.Zero || cbRes <= 0)
                    return null;

                byte[] groupBytes = new byte[cbRes];
                Marshal.Copy(pRes, groupBytes, 0, cbRes);

                GRPICONDIR hdr;
                unsafe
                {
                    fixed (byte* p = groupBytes)
                        hdr = Marshal.PtrToStructure<GRPICONDIR>((IntPtr)p);
                }
                if (hdr.Type != 1 || hdr.Count == 0)
                    return null;

                int entrySize = Marshal.SizeOf<GRPICONDIRENTRY>();
                var entries = new List<GRPICONDIRENTRY>(hdr.Count);
                int offset = Marshal.SizeOf<GRPICONDIR>();
                for (int i = 0; i < hdr.Count; i++)
                {
                    var e = BytesToStruct<GRPICONDIRENTRY>(groupBytes, offset + i * entrySize);
                    entries.Add(e);
                }

                var imageBlobs = new List<byte[]>(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    var id = (IntPtr)entries[i].ID;
                    var hIconRes = FindResource(hMod, id, RT_ICON);
                    if (hIconRes == IntPtr.Zero)
                        return null;

                    var hIconData = LoadResource(hMod, hIconRes);
                    var pIcon = LockResource(hIconData);
                    var cbIcon = (int)SizeofResource(hMod, hIconRes);
                    if (pIcon == IntPtr.Zero || cbIcon <= 0)
                        return null;

                    var bytes = new byte[cbIcon];
                    Marshal.Copy(pIcon, bytes, 0, cbIcon);
                    imageBlobs.Add(bytes);
                }

                using var ms = new MemoryStream();
                WriteStruct(ms, new ICONDIR { Reserved = 0, Type = 1, Count = (ushort)entries.Count });

                long entriesPos = ms.Position;
                ms.Position += entries.Count * Marshal.SizeOf<ICONDIRENTRY>();

                var icondirEntries = new ICONDIRENTRY[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    var blob = imageBlobs[i];

                    icondirEntries[i] = new ICONDIRENTRY
                    {
                        Width = (byte)(e.Width == 0 ? 0 : e.Width),
                        Height = (byte)(e.Height == 0 ? 0 : e.Height),
                        ColorCount = e.ColorCount,
                        Reserved = 0,
                        Planes = e.Planes,
                        BitCount = e.BitCount,
                        BytesInRes = (uint)blob.Length,
                        ImageOffset = (uint)ms.Position
                    };

                    ms.Write(blob, 0, blob.Length);
                }

                long endPos = ms.Position;
                ms.Position = entriesPos;
                for (int i = 0; i < icondirEntries.Length; i++)
                    WriteStruct(ms, icondirEntries[i]);
                ms.Position = endPos;

                return ms.ToArray();
            }
            finally
            {
                if (hMod != IntPtr.Zero) FreeLibrary(hMod);
            }
        }

        // ===== 工具函数 =====
        private static T BytesToStruct<T>(byte[] buffer, int index) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            if (index < 0 || index + size > buffer.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(IntPtr.Add(handle.AddrOfPinnedObject(), index));
            }
            finally { handle.Free(); }
        }

        private static void WriteStruct<T>(Stream s, T value) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] tmp = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, ptr, false);
                Marshal.Copy(ptr, tmp, 0, size);
                s.Write(tmp, 0, size);
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        private static bool IS_INTRESOURCE(IntPtr value)
        {
            return ((ulong)value) <= ushort.MaxValue;
        }
    }
}