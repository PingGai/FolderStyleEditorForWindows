using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FolderStyleEditorForWindows.Behaviors
{
    public class AutoHideScrollBehavior : Behavior<ScrollViewer>
    {
        private DispatcherTimer? _hideTimer;
        private DispatcherTimer? _showTimer;
        private List<ScrollBar> _scrollBars = new();
        private bool _isVisible = false; // Tracks whether scrollbars are currently intended to be visible

        protected override void OnAttachedToVisualTree()
        {
            base.OnAttachedToVisualTree();
            
            if (AssociatedObject == null) return;

            // Initialize Timers
            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2) // Modified to 2 seconds
            };
            _hideTimer.Tick += OnHideTimerTick;

            _showTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.3) // Delay for hover to prevent accidental triggers
            };
            _showTimer.Tick += OnShowTimerTick;

            // Subscribe to ScrollViewer changes
            AssociatedObject.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
            
            // Try to find ScrollBars after a short delay
            Dispatcher.UIThread.Post(FindAndSetupScrollBars, DispatcherPriority.Loaded);
        }

        protected override void OnDetachedFromVisualTree()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
            }

            if (_hideTimer != null)
            {
                _hideTimer.Stop();
                _hideTimer.Tick -= OnHideTimerTick;
                _hideTimer = null;
            }

            if (_showTimer != null)
            {
                _showTimer.Stop();
                _showTimer.Tick -= OnShowTimerTick;
                _showTimer = null;
            }

            CleanupScrollBars();

            base.OnDetachedFromVisualTree();
        }

        private void FindAndSetupScrollBars()
        {
            CleanupScrollBars();

            if (AssociatedObject == null) return;

            var scrollBars = AssociatedObject.GetVisualDescendants().OfType<ScrollBar>();
            foreach (var sb in scrollBars)
            {
                _scrollBars.Add(sb);
                sb.PointerEntered += OnScrollBarPointerEntered;
                sb.PointerExited += OnScrollBarPointerExited;
                sb.Opacity = 0;
            }
            _isVisible = false;
        }

        private void CleanupScrollBars()
        {
            foreach (var sb in _scrollBars)
            {
                sb.PointerEntered -= OnScrollBarPointerEntered;
                sb.PointerExited -= OnScrollBarPointerExited;
            }
            _scrollBars.Clear();
        }

        private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            // Only show if there is an actual scroll change in offset
            if (e.ExtentDelta.Length == 0 && e.ViewportDelta.Length == 0 && Math.Abs(e.OffsetDelta.Length) < 0.001)
                return;

            // Scrolling shows immediately, cancelling any pending show
            StopShowTimer();
            ShowScrollBars();
            StartHideTimer();
        }

        private void OnScrollBarPointerEntered(object? sender, PointerEventArgs e)
        {
            if (_isVisible)
            {
                // Already visible, just cancel hiding
                StopHideTimer();
            }
            else
            {
                // Not visible, start delay timer
                StartShowTimer();
            }
        }

        private void OnScrollBarPointerExited(object? sender, PointerEventArgs e)
        {
            StopShowTimer(); // Cancel pending show if any
            StartHideTimer(); // Start hiding countdown
        }

        private void OnHideTimerTick(object? sender, EventArgs e)
        {
            StopHideTimer();
            HideScrollBars();
        }

        private void OnShowTimerTick(object? sender, EventArgs e)
        {
            StopShowTimer();
            ShowScrollBars();
            StopHideTimer(); // Ensure we don't hide immediately after showing via hover
        }

        private void ShowScrollBars()
        {
            _isVisible = true;
            foreach (var sb in _scrollBars)
            {
                sb.Opacity = 1;
            }
        }

        private void HideScrollBars()
        {
            _isVisible = false;
            foreach (var sb in _scrollBars)
            {
                sb.Opacity = 0;
            }
        }

        private void StartHideTimer()
        {
            _hideTimer?.Stop();
            _hideTimer?.Start();
        }

        private void StopHideTimer()
        {
            _hideTimer?.Stop();
        }

        private void StartShowTimer()
        {
            _showTimer?.Stop();
            _showTimer?.Start();
        }

        private void StopShowTimer()
        {
            _showTimer?.Stop();
        }
    }
}
