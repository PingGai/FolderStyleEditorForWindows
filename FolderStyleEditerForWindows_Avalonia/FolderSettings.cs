namespace FolderStyleEditerForWindows
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
            if (string.IsNullOrEmpty(iconResource))
            {
                IconResource = string.Empty;
                IconIndex = 0;
            }
            else
            {
                var parts = iconResource.Split(',');
                IconResource = parts[0];
                if (parts.Length > 1 && int.TryParse(parts[1], out var index))
                {
                    IconIndex = index;
                }
                else
                {
                    IconIndex = 0;
                }
            }
        }
    }
}