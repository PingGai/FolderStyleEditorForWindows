using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FolderStyleEditorForWindows.Views
{
    public partial class Toast : UserControl
    {
        public Toast()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}