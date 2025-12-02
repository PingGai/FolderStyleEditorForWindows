using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using FolderStyleEditorForWindows.ViewModels;

namespace FolderStyleEditorForWindows.Services
{
    public enum InterruptDialogResult
    {
        None,
        Primary,
        Secondary
    }

    public sealed class InterruptDialogOptions
    {
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string PrimaryButtonText { get; init; } = string.Empty;
        public string? SecondaryButtonText { get; init; }
        public bool ShowProgress { get; init; }
        public double ProgressValue { get; init; }
        public bool IsProgressIndeterminate { get; init; }
        public IBrush? PrimaryBackground { get; init; }
        public IBrush? PrimaryForeground { get; init; }
        public IBrush? PrimaryBorderBrush { get; init; }
        public double? PrimaryBorderThickness { get; init; }
        public IBrush? SecondaryBackground { get; init; }
        public IBrush? SecondaryForeground { get; init; }
        public IBrush? SecondaryBorderBrush { get; init; }
        public double? SecondaryBorderThickness { get; init; }
    }

    public sealed class InterruptDialogState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        private bool _isHitTestVisible;
        public bool IsHitTestVisible
        {
            get => _isHitTestVisible;
            set => SetField(ref _isHitTestVisible, value);
        }

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set => SetField(ref _title, value);
        }

        private string _content = string.Empty;
        public string Content
        {
            get => _content;
            set => SetField(ref _content, value);
        }

        private bool _showProgress;
        public bool ShowProgress
        {
            get => _showProgress;
            set => SetField(ref _showProgress, value);
        }

        private bool _isProgressIndeterminate;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetField(ref _isProgressIndeterminate, value);
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetField(ref _progressValue, value);
        }

        private string _primaryButtonText = string.Empty;
        public string PrimaryButtonText
        {
            get => _primaryButtonText;
            set => SetField(ref _primaryButtonText, value);
        }

        private string? _secondaryButtonText;
        public string? SecondaryButtonText
        {
            get => _secondaryButtonText;
            set
            {
                if (SetField(ref _secondaryButtonText, value))
                {
                    OnPropertyChanged(nameof(HasSecondaryButton));
                }
            }
        }

        public bool HasSecondaryButton => !string.IsNullOrWhiteSpace(SecondaryButtonText);

        private double _overlayOpacity;
        public double OverlayOpacity
        {
            get => _overlayOpacity;
            set => SetField(ref _overlayOpacity, value);
        }

        private double _cardOpacity;
        public double CardOpacity
        {
            get => _cardOpacity;
            set => SetField(ref _cardOpacity, value);
        }

        private double _cardScale = 0.96;
        public double CardScale
        {
            get => _cardScale;
            set => SetField(ref _cardScale, value);
        }

        private IBrush _overlayBrush = new SolidColorBrush(Colors.Transparent);
        public IBrush OverlayBrush
        {
            get => _overlayBrush;
            set => SetField(ref _overlayBrush, value);
        }

        private IBrush _primaryBackground = new SolidColorBrush(Color.Parse("#FFEFD7"));
        public IBrush PrimaryBackground
        {
            get => _primaryBackground;
            set => SetField(ref _primaryBackground, value);
        }

        private IBrush _primaryForeground = new SolidColorBrush(Color.Parse("#303034"));
        public IBrush PrimaryForeground
        {
            get => _primaryForeground;
            set => SetField(ref _primaryForeground, value);
        }

        private IBrush? _primaryBorderBrush;
        public IBrush? PrimaryBorderBrush
        {
            get => _primaryBorderBrush;
            set => SetField(ref _primaryBorderBrush, value);
        }

        private double _primaryBorderThickness = 0;
        public double PrimaryBorderThickness
        {
            get => _primaryBorderThickness;
            set => SetField(ref _primaryBorderThickness, value);
        }

        private IBrush _secondaryBackground = new SolidColorBrush(Color.Parse("#F8F9FB"));
        public IBrush SecondaryBackground
        {
            get => _secondaryBackground;
            set => SetField(ref _secondaryBackground, value);
        }

        private IBrush _secondaryForeground = new SolidColorBrush(Color.Parse("#303034"));
        public IBrush SecondaryForeground
        {
            get => _secondaryForeground;
            set => SetField(ref _secondaryForeground, value);
        }

        private IBrush _secondaryBorderBrush = new SolidColorBrush(Color.Parse("#E6E6EB"));
        public IBrush SecondaryBorderBrush
        {
            get => _secondaryBorderBrush;
            set => SetField(ref _secondaryBorderBrush, value);
        }

        private double _secondaryBorderThickness = 1;
        public double SecondaryBorderThickness
        {
            get => _secondaryBorderThickness;
            set => SetField(ref _secondaryBorderThickness, value);
        }

        private double _cardOffsetX;
        public double CardOffsetX
        {
            get => _cardOffsetX;
            set => SetField(ref _cardOffsetX, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public InterruptDialogState(Action onConfirm, Action onCancel)
        {
            ConfirmCommand = new RelayCommand(() => onConfirm());
            CancelCommand = new RelayCommand(() => onCancel());
            PrimaryBackground = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
            PrimaryForeground = new SolidColorBrush(Color.Parse("#D66B6B"));
            PrimaryBorderBrush = new SolidColorBrush(Color.Parse("#D66B6B"));
            PrimaryBorderThickness = 1;
            SecondaryBackground = new SolidColorBrush(Color.Parse("#F8F9FB"));
            SecondaryForeground = new SolidColorBrush(Color.Parse("#303034"));
            SecondaryBorderBrush = new SolidColorBrush(Color.Parse("#E6E6EB"));
            SecondaryBorderThickness = 1;
        }

        internal void ResetVisualState(bool isActive)
        {
            IsActive = isActive;
            IsHitTestVisible = isActive;
            OverlayOpacity = isActive ? 0.9 : 0.0;
            CardOpacity = isActive ? 1.0 : 0.0;
            CardScale = isActive ? 1.0 : 0.96;
            CardOffsetX = isActive ? 0 : 6;
            OverlayBrush = isActive
                ? new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x00, 0x00))
                : new SolidColorBrush(Colors.Transparent);
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class InterruptDialogService
    {
        private readonly Dispatcher _dispatcher = Dispatcher.UIThread;
        private TaskCompletionSource<InterruptDialogResult>? _pendingCompletion;

        public InterruptDialogState State { get; }

        public InterruptDialogService()
        {
            State = new InterruptDialogState(Confirm, Cancel);
            State.ResetVisualState(false);
        }

        public async Task<InterruptDialogResult> ShowAsync(InterruptDialogOptions options)
        {
            _pendingCompletion?.TrySetResult(InterruptDialogResult.None);
            var tcs = new TaskCompletionSource<InterruptDialogResult>();
            _pendingCompletion = tcs;

            await _dispatcher.InvokeAsync(() =>
            {
                State.Title = options.Title ?? string.Empty;
                State.Content = options.Content ?? string.Empty;
                State.PrimaryButtonText = options.PrimaryButtonText ?? string.Empty;
                State.SecondaryButtonText = options.SecondaryButtonText;
                State.ShowProgress = options.ShowProgress;
                State.ProgressValue = options.ProgressValue;
                State.IsProgressIndeterminate = options.IsProgressIndeterminate;
                State.PrimaryBackground = options.PrimaryBackground ?? State.PrimaryBackground;
                State.PrimaryForeground = options.PrimaryForeground ?? State.PrimaryForeground;
                State.PrimaryBorderBrush = options.PrimaryBorderBrush;
                State.PrimaryBorderThickness = options.PrimaryBorderThickness ?? State.PrimaryBorderThickness;
                State.SecondaryBackground = options.SecondaryBackground ?? State.SecondaryBackground;
                State.SecondaryForeground = options.SecondaryForeground ?? State.SecondaryForeground;
                State.SecondaryBorderBrush = options.SecondaryBorderBrush ?? State.SecondaryBorderBrush;
                State.SecondaryBorderThickness = options.SecondaryBorderThickness ?? State.SecondaryBorderThickness;
                State.ResetVisualState(true);
            });

            return await tcs.Task.ConfigureAwait(false);
        }

        private void Confirm() => Complete(InterruptDialogResult.Primary);

        private void Cancel() => Complete(State.HasSecondaryButton ? InterruptDialogResult.Secondary : InterruptDialogResult.None);

        private void Complete(InterruptDialogResult result)
        {
            var captured = _pendingCompletion;
            if (captured == null) return;
            _pendingCompletion = null;

            _dispatcher.Post(() => State.ResetVisualState(false));

            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                captured.TrySetResult(result);
            });
        }
    }
}
