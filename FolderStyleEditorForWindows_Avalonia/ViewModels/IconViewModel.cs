using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace FolderStyleEditorForWindows.ViewModels
{
    public class IconViewModel : INotifyPropertyChanged, IDisposable
    {
        private const int BitmapDisposeBatchSize = 16;
        private static readonly TimeSpan BitmapDisposeBatchDelay = TimeSpan.FromMilliseconds(24);
        private static readonly object PendingBitmapDisposalsSync = new();
        private static readonly Queue<Bitmap> PendingBitmapDisposals = new();
        private static Task? _bitmapDisposeDrainTask;
        private bool _isSelected;
        private bool _isPreviewed;
        private Bitmap? _image;
        private bool _disposeScheduled;

        public Bitmap? Image
        {
            get => _image;
            private set
            {
                if (ReferenceEquals(_image, value)) return;
                _image = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
            }
        }
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
            _image = image;
            FilePath = filePath;
            Index = index;
        }

        public void Dispose()
        {
            if (_disposeScheduled)
            {
                return;
            }

            _disposeScheduled = true;
            var bitmap = _image;
            if (bitmap == null)
            {
                GC.SuppressFinalize(this);
                return;
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                ReleaseBitmap(bitmap);
            }
            else
            {
                Dispatcher.UIThread.Post(() => ReleaseBitmap(bitmap), DispatcherPriority.Background);
            }

            GC.SuppressFinalize(this);
        }

        private void ReleaseBitmap(Bitmap bitmap)
        {
            if (!ReferenceEquals(_image, bitmap))
            {
                return;
            }

            // Clear the binding target first so layout no longer measures a disposed bitmap.
            Image = null;
            EnqueueBitmapDispose(bitmap);
        }

        private static void EnqueueBitmapDispose(Bitmap bitmap)
        {
            lock (PendingBitmapDisposalsSync)
            {
                PendingBitmapDisposals.Enqueue(bitmap);
                _bitmapDisposeDrainTask ??= Task.Run(DrainPendingBitmapDisposalsAsync);
            }
        }

        private static async Task DrainPendingBitmapDisposalsAsync()
        {
            while (true)
            {
                List<Bitmap> batch = new(BitmapDisposeBatchSize);
                lock (PendingBitmapDisposalsSync)
                {
                    while (PendingBitmapDisposals.Count > 0 && batch.Count < BitmapDisposeBatchSize)
                    {
                        batch.Add(PendingBitmapDisposals.Dequeue());
                    }

                    if (batch.Count == 0)
                    {
                        _bitmapDisposeDrainTask = null;
                        return;
                    }
                }

                foreach (var bitmap in batch)
                {
                    try
                    {
                        bitmap.Dispose();
                    }
                    catch
                    {
                        // Ignore best-effort cleanup failures.
                    }
                }

                await Task.Delay(BitmapDisposeBatchDelay).ConfigureAwait(false);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
