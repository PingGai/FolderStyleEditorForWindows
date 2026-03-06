using Avalonia.Media.Imaging;
using System;
using System.ComponentModel;

namespace FolderStyleEditorForWindows.ViewModels
{
    public class IconViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool _isSelected;
        private bool _isPreviewed;

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowPreviewOverlay)));
            }
        }

        public bool IsPreviewed
        {
            get => _isPreviewed;
            set
            {
                if (_isPreviewed == value) return;
                _isPreviewed = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPreviewed)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowPreviewOverlay)));
            }
        }

        public bool ShowPreviewOverlay => _isPreviewed && !_isSelected;

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
