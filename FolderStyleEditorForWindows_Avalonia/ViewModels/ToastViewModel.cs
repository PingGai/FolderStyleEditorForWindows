using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FolderStyleEditorForWindows.ViewModels
{
    public partial class ToastViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _message;

        [ObservableProperty]
        private SolidColorBrush _color;

        [ObservableProperty]
        private bool _isVisible;

        public Guid Id { get; }

        public ToastViewModel(string message, SolidColorBrush? color, Guid id)
        {
            _message = message;
            _color = color ?? new SolidColorBrush(Colors.Green);
            Id = id;
            _isVisible = false;
        }
    }
}