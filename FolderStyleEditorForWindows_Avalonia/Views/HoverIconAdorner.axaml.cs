using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FolderStyleEditorForWindows.Views
{
    public partial class HoverIconAdorner : UserControl
    {
        public HoverIconAdorner()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}