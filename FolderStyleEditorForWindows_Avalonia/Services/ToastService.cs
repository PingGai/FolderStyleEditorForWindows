using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using FolderStyleEditorForWindows.ViewModels;

namespace FolderStyleEditorForWindows.Services
{
    public class ToastService : IToastService
    {
        public ObservableCollection<ToastViewModel> Toasts { get; } = new();

        public void Show(string message, SolidColorBrush? color = null, TimeSpan? duration = null)
        {
            duration ??= TimeSpan.FromSeconds(3);
            var id = Guid.NewGuid();
            var toast = new ToastViewModel(message, color, id);
            
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Toasts.Add(toast);
                toast.IsVisible = true;
            });

            Task.Delay(duration.Value).ContinueWith(_ =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    toast.IsVisible = false;
                });
            });
            
            var animationDuration = TimeSpan.FromMilliseconds(ConfigManager.Features.Animations.ToastAnimationDuration);
            // 等待动画完成再移除
            Task.Delay(duration.Value.Add(animationDuration)).ContinueWith(_ =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Toasts.Remove(toast);
                });
            });
        }
    }
}