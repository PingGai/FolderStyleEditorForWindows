namespace FolderStyleEditorForWindows.Controls;

public sealed class LiquidSegmentedSelectorItem
{
    public LiquidSegmentedSelectorItem(string key, string label, string? tooltip = null)
    {
        Key = key;
        Label = label;
        Tooltip = tooltip;
    }

    public string Key { get; }

    public string Label { get; }

    public string? Tooltip { get; }
}
