using Avalonia.Media.Imaging;
using System;
using System.ComponentModel;

namespace FolderStyleEditorForWindows.ViewModels
{
    public class IconViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool _isSelected;

        public Bitmap Image { get; }
        public string FilePath { get; }
        public int Index { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public IconViewModel(Bitmap image, string filePath, int index)
        {
            Image = image;
            FilePath = filePath;
            Index = index;
        }

        public void Dispose()
        {
            Image.Dispose();
            GC.SuppressFinalize(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}