namespace FolderStyleEditorForWindows
{
    public class FolderSettings
    {
        public string FolderPath { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string IconResource { get; set; } = string.Empty;
        public int IconIndex { get; set; }

        public FolderSettings() { }

        public FolderSettings(string folderPath)
        {
            FolderPath = folderPath;
            Alias = DesktopIniHelper.ReadValue(folderPath, "LocalizedResourceName");
            var iconResource = DesktopIniHelper.ReadValue(folderPath, "IconResource");
            if (!string.IsNullOrEmpty(iconResource))
            {
                // Keep the full IconResource value (path + index) so edit page
                // can restore the exact selected icon without forcing a re-pick.
                IconResource = iconResource;

                var parts = iconResource.Split(',');
                IconIndex = (parts.Length > 1 && int.TryParse(parts[1], out var resourceIndex))
                    ? resourceIndex
                    : 0;
                return;
            }

            // Fallback for folders using IconFile/IconIndex pair.
            var iconFile = DesktopIniHelper.ReadValue(folderPath, "IconFile");
            var iconIndexRaw = DesktopIniHelper.ReadValue(folderPath, "IconIndex");
            var iconIndex = int.TryParse(iconIndexRaw, out var parsedIndex) ? parsedIndex : 0;

            if (!string.IsNullOrEmpty(iconFile))
            {
                IconResource = $"{iconFile},{iconIndex}";
                IconIndex = iconIndex;
            }
            else
            {
                IconResource = string.Empty;
                IconIndex = 0;
            }
        }
    }
}
