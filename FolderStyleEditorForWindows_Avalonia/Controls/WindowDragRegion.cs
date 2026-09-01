using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace FolderStyleEditorForWindows.Controls;

/// <summary>
/// Transparent, hit-testable surface for the parent window's shared drag pipeline.
/// </summary>
public sealed class WindowDragRegion : Border
{
    public WindowDragRegion()
    {
        Background = Brushes.Transparent;
        IsHitTestVisible = true;
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }
}
