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
            var id = Guid.NewGuid();
            var toast = new ToastViewModel(message, color, id);
            _ = ShowAndDismissAsync(toast, duration ?? TimeSpan.FromSeconds(3));
        }

        private async Task ShowAndDismissAsync(ToastViewModel toast, TimeSpan duration)
        {
            var animationDuration = TimeSpan.FromMilliseconds(
                Math.Max(80, ConfigManager.Config.Animations.ToastAnimationDuration));

            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Set visible before template binding to avoid entry flicker.
                    toast.IsVisible = true;
                    Toasts.Add(toast);
                });

                await Task.Delay(duration);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    toast.IsVisible = false;
                });

                await Task.Delay(animationDuration);
            }
            catch
            {
                // Keep service resilient: lifecycle errors should not crash the app.
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Toasts.Remove(toast);
                });
            }
        }
    }
}
