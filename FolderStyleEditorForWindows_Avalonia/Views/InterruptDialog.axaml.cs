using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FolderStyleEditorForWindows.Views
{
    public partial class InterruptDialog : UserControl
    {
        public InterruptDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
