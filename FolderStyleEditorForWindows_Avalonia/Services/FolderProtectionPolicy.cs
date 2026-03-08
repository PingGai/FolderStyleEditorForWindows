using System;
using System.IO;

namespace FolderStyleEditorForWindows.Services
{
    public static class FolderProtectionPolicy
    {
        public static bool RequiresElevation(string? folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar);
                var protectedRoots = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86)
                };

                foreach (var root in protectedRoots)
                {
                    if (string.IsNullOrWhiteSpace(root))
                    {
                        continue;
                    }

                    var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
                    if (fullPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                        fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
