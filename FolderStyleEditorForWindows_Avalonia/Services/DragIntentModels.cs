namespace FolderStyleEditorForWindows.Services
{
    public enum DragContext
    {
        Home,
        Edit
    }

    public enum DragIntentType
    {
        SingleFolder,
        MultiFolder,
        Ico,
        ExeInternal,
        ExeExternal,
        Text,
        Unsupported
    }

    public sealed class DragIntentResult
    {
        public static DragIntentResult Unsupported { get; } = new()
        {
            Type = DragIntentType.Unsupported,
            MainTextKey = "Drag_Unsupported_Main",
            IconPath = "avares://FolderStyleEditorForWindows/Resources/SVG/triangle-alert.svg",
            SubTextBrush = "#E07167",
            CanDrop = false
        };

        public DragIntentType Type { get; init; }
        public string MainTextKey { get; init; } = string.Empty;
        public string? SubTextKey { get; init; }
        public string IconPath { get; init; } = string.Empty;
        public string? SubTextBrush { get; init; }
        public bool CanDrop { get; init; }
    }
}
