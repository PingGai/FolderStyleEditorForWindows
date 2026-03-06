using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FolderStyleEditorForWindows.Services;
using FolderStyleEditorForWindows.ViewModels;

namespace FolderStyleEditorForWindows.Views
{
    public partial class Toast : UserControl
    {
        private const double ExpandedHeight = 50;
        private ToastViewModel? _viewModel;
        private Transitions? _savedTransitions;

        public Toast()
        {
            InitializeComponent();

            var duration = TimeSpan.FromMilliseconds(ConfigManager.Config.Animations.ToastAnimationDuration);

            this.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = duration,
                    Easing = new CubicEaseOut()
                },
                new DoubleTransition
                {
                    Property = HeightProperty,
                    Duration = duration,
                    Easing = new CubicEaseOut()
                }
            };
            _savedTransitions = this.Transitions;

            Height = 0;
            Opacity = 0;
            IsHitTestVisible = false;

            DataContextChanged += OnDataContextChanged;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnToastViewModelPropertyChanged;
            }

            _viewModel = DataContext as ToastViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnToastViewModelPropertyChanged;
                ApplyVisualState(_viewModel.IsVisible, animate: false);
            }
            else
            {
                ApplyVisualState(false, animate: false);
            }
        }

        private void OnToastViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ToastViewModel.IsVisible) && sender is ToastViewModel vm)
            {
                // Avoid entry animation flicker; only animate collapse on exit.
                ApplyVisualState(vm.IsVisible, animate: !vm.IsVisible);
            }
        }

        private void ApplyVisualState(bool visible, bool animate)
        {
            var originalTransitions = this.Transitions;
            if (!animate)
            {
                this.Transitions = null;
            }

            Height = visible ? ExpandedHeight : 0;
            Opacity = visible ? 1 : 0;
            IsHitTestVisible = visible;

            if (!animate)
            {
                this.Transitions = _savedTransitions ?? originalTransitions;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            DataContextChanged -= OnDataContextChanged;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnToastViewModelPropertyChanged;
                _viewModel = null;
            }

            base.OnDetachedFromVisualTree(e);
        }
    }
}
