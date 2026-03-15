using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FolderStyleEditorForWindows.Services;

namespace FolderStyleEditorForWindows.Controls;

public partial class LiquidSegmentedSelector : UserControl
{
    public static readonly StyledProperty<IList<LiquidSegmentedSelectorItem>?> ItemsProperty =
        AvaloniaProperty.Register<LiquidSegmentedSelector, IList<LiquidSegmentedSelectorItem>?>(nameof(Items));

    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<LiquidSegmentedSelector, string?>(nameof(SelectedKey), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private readonly Dictionary<string, Button> _segmentButtons = new(StringComparer.Ordinal);
    private Grid _segmentsGrid = null!;
    private Grid _trackGrid = null!;
    private Border _selectionHighlight = null!;
    private TranslateTransform _selectionTranslateTransform = null!;
    private ScaleTransform _selectionScaleTransform = null!;
    private bool _isLoaded;
    private bool _isDragging;
    private bool _isPendingDrag;
    private bool _suppressSelectedKeyAnimation;
    private Point _pressPoint;
    private int _dragPreviewIndex = -1;
    private IPointer? _activePointer;
    private INotifyCollectionChanged? _itemsNotifier;
    private readonly FrameRateSettings? _frameRateSettings;
    private InterruptibleScalarAnimator _highlightXAnimator = null!;
    private InterruptibleScalarAnimator _highlightWidthAnimator = null!;
    private int _widthSettleVersion;
    private string? _lastSelectionClassKey;
    private int _lastPreviewClassIndex = int.MinValue;
    private const double DragScale = 1.0;
    private static readonly MotionProfile HighlightPositionMotion = MotionProfile.SoftSettle(TimeSpan.FromMilliseconds(220), 0.028d, 0.86d);
    private static readonly MotionProfile HighlightWidthStretchMotion = MotionProfile.Smooth(TimeSpan.FromMilliseconds(120));
    private static readonly MotionProfile HighlightWidthSettleMotion = MotionProfile.SoftSettle(TimeSpan.FromMilliseconds(170), 0.02d, 0.84d);
    private static readonly MotionProfile DragPositionMotion = MotionProfile.Smooth(TimeSpan.FromMilliseconds(96));
    private static readonly MotionProfile DragWidthMotion = MotionProfile.Smooth(TimeSpan.FromMilliseconds(96));
    private const double DragStartThreshold = 10d;
    private const double DragHorizontalBias = 1.2d;

    public LiquidSegmentedSelector()
    {
        InitializeComponent();
        _frameRateSettings = App.Services?.GetService(typeof(FrameRateSettings)) as FrameRateSettings;
        if (_frameRateSettings != null)
        {
            _frameRateSettings.PropertyChanged += FrameRateSettings_PropertyChanged;
        }

        _segmentsGrid = this.FindControl<Grid>("SegmentsGrid")
            ?? throw new InvalidOperationException("SegmentsGrid not found.");
        _trackGrid = this.FindControl<Grid>("TrackGrid")
            ?? throw new InvalidOperationException("TrackGrid not found.");
        _selectionHighlight = this.FindControl<Border>("SelectionHighlight")
            ?? throw new InvalidOperationException("SelectionHighlight not found.");
        if (_selectionHighlight.RenderTransform is not TransformGroup transformGroup ||
            transformGroup.Children.Count < 2 ||
            transformGroup.Children[0] is not ScaleTransform scaleTransform ||
            transformGroup.Children[1] is not TranslateTransform translateTransform)
        {
            throw new InvalidOperationException("Selection highlight transforms are not configured correctly.");
        }

        _selectionScaleTransform = scaleTransform;
        _selectionTranslateTransform = translateTransform;
        _highlightXAnimator = new InterruptibleScalarAnimator(
            () => _selectionTranslateTransform.X,
            value => _selectionTranslateTransform.X = value);
        _highlightWidthAnimator = new InterruptibleScalarAnimator(
            () => _selectionHighlight.Width,
            value => _selectionHighlight.Width = value);
        UpdateAnimationTimerInterval();

        PropertyChanged += LiquidSegmentedSelector_PropertyChanged;

        AddHandler(PointerPressedEvent, OnPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);

        Loaded += (_, _) =>
        {
            _isLoaded = true;
            _suppressSelectedKeyAnimation = true;
            UpdateHighlightForSelection(animated: false);
            _suppressSelectedKeyAnimation = false;
        };
        SizeChanged += (_, _) =>
        {
            if (_isDragging)
            {
                UpdateDragHighlight(_pressPoint.X);
                return;
            }

            UpdateHighlightForSelection(animated: false);
        };
    }

    public IList<LiquidSegmentedSelectorItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string? SelectedKey
    {
        get => GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LiquidSegmentedSelector_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ItemsProperty)
        {
            AttachItemsNotifier(Items);
            RebuildSegments();
            return;
        }

        if (e.Property == SelectedKeyProperty)
        {
            if (_isDragging || !_isLoaded)
            {
                UpdateSelectedButtonClasses();
                return;
            }

            UpdateHighlightForSelection(animated: !_suppressSelectedKeyAnimation);
        }
    }

    private void ConfigureTransitions()
    {
    }

    private void RebuildSegments()
    {
        _segmentButtons.Clear();
        _segmentsGrid.Children.Clear();
        _segmentsGrid.ColumnDefinitions.Clear();

        var items = Items;
        if (items == null || items.Count == 0)
        {
            _selectionHighlight.IsVisible = false;
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            _segmentsGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            var item = items[i];
            var button = CreateSegmentButton(item, i);
            Grid.SetColumn(button, i);
            _segmentsGrid.Children.Add(button);
            _segmentButtons[item.Key] = button;
        }

        _selectionHighlight.IsVisible = true;
        UpdateHighlightForSelection(animated: _isLoaded);
    }

    private Button CreateSegmentButton(LiquidSegmentedSelectorItem item, int index)
    {
        var label = new TextBlock
        {
            Text = item.Label,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var button = new Button
        {
            Classes = { "LiquidSegmentButton" },
            Content = label,
            Tag = index,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        if (!string.IsNullOrWhiteSpace(item.Tooltip))
        {
            ToolTip.SetTip(button, item.Tooltip);
        }

        button.Click += (_, _) =>
        {
            if (_isDragging)
            {
                return;
            }

            CommitSelection(index);
        };

        return button;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        if (!TryFindInteractiveSegmentIndex(e.Source, out _))
        {
            return;
        }

        _activePointer = e.Pointer;
        _pressPoint = e.GetPosition(_trackGrid);
        _dragPreviewIndex = ResolveSegmentIndexFromPoint(_pressPoint.X);
        _isPendingDrag = true;
        _isDragging = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_activePointer != e.Pointer || !_isPendingDrag)
        {
            return;
        }

        var currentPoint = e.GetPosition(_trackGrid);
        if (!_isDragging)
        {
            var delta = currentPoint - _pressPoint;
            var absX = Math.Abs(delta.X);
            var absY = Math.Abs(delta.Y);
            if (absX < DragStartThreshold || absX < absY * DragHorizontalBias)
            {
                return;
            }

            _isDragging = true;
            e.Pointer.Capture(this);
        }

        UpdateDragHighlight(currentPoint.X);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_activePointer != e.Pointer)
        {
            return;
        }

        if (_isDragging)
        {
            var releasePoint = e.GetPosition(_trackGrid);
            CommitSelection(ResolveSegmentIndexFromPoint(releasePoint.X));
            e.Handled = true;
        }

        EndPointerInteraction();
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndPointerInteraction();
    }

    private void EndPointerInteraction()
    {
        _activePointer = null;
        _isPendingDrag = false;

        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _dragPreviewIndex = -1;
        UpdateHighlightForSelection(animated: true);
    }

    private void UpdateDragHighlight(double pointerX)
    {
        if (!TryGetSegmentMetrics(out var segmentWidth, out var count))
        {
            return;
        }

        CancelWidthSettle();
        var targetIndex = ResolveSegmentIndexFromPoint(pointerX, count);
        var clamped = Math.Clamp((segmentWidth * targetIndex), 0, Math.Max(0, _trackGrid.Bounds.Width - segmentWidth));
        _selectionScaleTransform.ScaleX = DragScale;
        _selectionScaleTransform.ScaleY = DragScale;
        _highlightXAnimator.AnimateTo(clamped, DragPositionMotion);
        _highlightWidthAnimator.AnimateTo(segmentWidth, DragWidthMotion);

        _dragPreviewIndex = targetIndex;
        UpdateSelectedButtonClasses();
    }

    private void CommitSelection(int index)
    {
        var items = Items;
        if (items == null || items.Count == 0)
        {
            return;
        }

        index = Math.Clamp(index, 0, items.Count - 1);
        var selectedItem = items[index];
        SetCurrentValue(SelectedKeyProperty, selectedItem.Key);
        UpdateHighlightForSelection(animated: true);
    }

    private void UpdateHighlightForSelection(bool animated)
    {
        if (!TryGetSegmentMetrics(out var segmentWidth, out var count))
        {
            _selectionHighlight.IsVisible = false;
            return;
        }

        var index = ResolveSelectedIndex(count);
        var targetX = segmentWidth * index;

        _selectionHighlight.IsVisible = true;
        if (animated)
        {
            StartHighlightAnimation(targetX, segmentWidth);
        }
        else
        {
            CancelWidthSettle();
            StopHighlightAnimation();
            _selectionHighlight.Width = segmentWidth;
            _selectionTranslateTransform.X = targetX;
            _selectionScaleTransform.ScaleX = 1;
            _selectionScaleTransform.ScaleY = 1;
        }
        UpdateSelectedButtonClasses();
    }

    private void StartHighlightAnimation(double targetX, double targetWidth)
    {
        CancelWidthSettle();
        _selectionScaleTransform.ScaleX = 1d;
        _selectionScaleTransform.ScaleY = 1d;
        var currentCenter = _selectionTranslateTransform.X + (_selectionHighlight.Width * 0.5d);
        var targetCenter = targetX + (targetWidth * 0.5d);
        var travel = Math.Abs(targetCenter - currentCenter);
        var widthBoost = Math.Min(18d, travel * 0.22d);
        _highlightXAnimator.AnimateTo(targetX, HighlightPositionMotion);
        if (widthBoost <= 0.25d)
        {
            _highlightWidthAnimator.AnimateTo(targetWidth, HighlightWidthSettleMotion);
            return;
        }

        _highlightWidthAnimator.AnimateTo(targetWidth + widthBoost, HighlightWidthStretchMotion);
        ScheduleWidthSettle(targetWidth, HighlightWidthStretchMotion.Duration * 0.55d);
    }

    private void StopHighlightAnimation()
    {
        CancelWidthSettle();
        _selectionScaleTransform.ScaleX = 1d;
        _selectionScaleTransform.ScaleY = 1d;
    }

    private void CancelWidthSettle()
    {
        Interlocked.Increment(ref _widthSettleVersion);
    }

    private void ScheduleWidthSettle(double targetWidth, TimeSpan delay)
    {
        var version = Interlocked.Increment(ref _widthSettleVersion);
        _ = ScheduleWidthSettleAsync(version, targetWidth, delay);
    }

    private async Task ScheduleWidthSettleAsync(int version, double targetWidth, TimeSpan delay)
    {
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay).ConfigureAwait(false);
        }

        if (version != Volatile.Read(ref _widthSettleVersion))
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (version != Volatile.Read(ref _widthSettleVersion))
            {
                return;
            }

            _highlightWidthAnimator.AnimateTo(targetWidth, HighlightWidthSettleMotion);
        });
    }

    private void UpdateAnimationTimerInterval()
    {
        var configured = Math.Clamp(_frameRateSettings?.LiquidSegmentedSelectorFps ?? 120, 1, 120);
        var displayHz = Math.Clamp(_frameRateSettings?.DisplayRefreshRateHz ?? configured, 1, 500);
        var fps = Math.Min(configured, displayHz);
        _highlightXAnimator.SetFrameRate(fps);
        _highlightWidthAnimator.SetFrameRate(fps);
    }

    private void FrameRateSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(FrameRateSettings.LiquidSegmentedSelectorFps) ||
            e.PropertyName == nameof(FrameRateSettings.DisplayRefreshRateHz))
        {
            UpdateAnimationTimerInterval();
        }
    }

    private void UpdateSelectedButtonClasses()
    {
        var selectedKey = SelectedKey;
        var previewIndex = _isDragging ? _dragPreviewIndex : -1;
        if (string.Equals(_lastSelectionClassKey, selectedKey, StringComparison.Ordinal) &&
            _lastPreviewClassIndex == previewIndex)
        {
            return;
        }

        foreach (var pair in _segmentButtons)
        {
            var index = pair.Value.Tag is int value ? value : -1;
            var isSelected = string.Equals(pair.Key, selectedKey, StringComparison.Ordinal);
            var isPreview = _isDragging && index == _dragPreviewIndex && !isSelected;
            pair.Value.Classes.Set("selected", isSelected);
            pair.Value.Classes.Set("preview", isPreview);
        }

        _lastSelectionClassKey = selectedKey;
        _lastPreviewClassIndex = previewIndex;
    }

    private bool TryGetSegmentMetrics(out double segmentWidth, out int count)
    {
        count = Items?.Count ?? 0;
        if (count <= 0 || _trackGrid.Bounds.Width <= 0)
        {
            segmentWidth = 0;
            return false;
        }

        segmentWidth = _trackGrid.Bounds.Width / count;
        return true;
    }

    private int ResolveSelectedIndex(int count)
    {
        var items = Items;
        if (items == null || items.Count == 0)
        {
            return 0;
        }

        var selectedKey = SelectedKey;
        var selectedIndex = items
            .Select((item, index) => (item, index))
            .FirstOrDefault(pair => string.Equals(pair.item.Key, selectedKey, StringComparison.Ordinal))
            .index;

        return selectedIndex >= 0 ? Math.Clamp(selectedIndex, 0, count - 1) : 0;
    }

    private int ResolveSegmentIndexFromPoint(double pointX, int? knownCount = null)
    {
        var count = knownCount ?? (Items?.Count ?? 0);
        if (count <= 0 || _trackGrid.Bounds.Width <= 0)
        {
            return 0;
        }

        var normalized = Math.Clamp(pointX / _trackGrid.Bounds.Width, 0, 0.999999);
        return Math.Clamp((int)Math.Floor(normalized * count), 0, count - 1);
    }

    private bool TryFindInteractiveSegmentIndex(object? source, out int index)
    {
        switch (source)
        {
            case Button { Tag: int buttonIndex }:
                index = buttonIndex;
                return true;
            case Visual visual:
            {
                var ancestorButton = visual.FindAncestorOfType<Button>();
                if (ancestorButton?.Tag is int ancestorIndex)
                {
                    index = ancestorIndex;
                    return true;
                }

                index = ResolveSegmentIndexFromPoint(_pressPoint.X);
                return true;
            }
            default:
                index = 0;
                return Items is { Count: > 0 };
        }
    }

    private void AttachItemsNotifier(IList<LiquidSegmentedSelectorItem>? items)
    {
        if (_itemsNotifier != null)
        {
            _itemsNotifier.CollectionChanged -= ItemsNotifier_CollectionChanged;
            _itemsNotifier = null;
        }

        if (items is INotifyCollectionChanged notifier)
        {
            _itemsNotifier = notifier;
            _itemsNotifier.CollectionChanged += ItemsNotifier_CollectionChanged;
        }
    }

    private void ItemsNotifier_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildSegments();
    }
}
