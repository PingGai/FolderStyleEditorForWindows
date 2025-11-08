using System;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FolderStyleEditorForWindows.Services;

namespace FolderStyleEditorForWindows.Views
{
    public partial class Toast : UserControl
    {
        public Toast()
        {
            InitializeComponent();
            
            var duration = TimeSpan.FromMilliseconds(ConfigManager.Features.Animations.ToastAnimationDuration);

            this.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = duration,
                    Easing = new CubicEaseOut()
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = duration,
                    Easing = new CubicEaseOut()
                }
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}