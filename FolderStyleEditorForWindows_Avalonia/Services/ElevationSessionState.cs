using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FolderStyleEditorForWindows.Services
{
    public sealed class ElevationSessionState : INotifyPropertyChanged
    {
        private bool _isElevatedSessionActive;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsElevatedSessionActive
        {
            get => _isElevatedSessionActive;
            set
            {
                if (_isElevatedSessionActive == value)
                {
                    return;
                }

                _isElevatedSessionActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsElevatedSessionActive)));
            }
        }
    }
}
