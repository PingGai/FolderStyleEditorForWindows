using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using FolderStyleEditorForWindows.Services;
using System.ComponentModel;

namespace FolderStyleEditorForWindows.Controls
{
    public partial class ImageCropEditor : UserControl
    {
        private const double RadiusHandleBaseInset = 18d;
        private const double RadiusHandleVisualOffset = -6d;
        private const double RadiusHandleHitRadius = 8d;
        private const double CornerHandleHitRadius = 12d;
        private const double SideHandleHitRadius = 7d;
        private const double SideHandleCornerGuard = 28d;
        private Grid? _cropSurface;
        private Grid? _imageViewport;
        private Border? _cropSurfaceHost;
        private Canvas? _overlayCanvas;
        private Rectangle? _topMask;
        private Rectangle? _leftMask;
        private Rectangle? _rightMask;
        private Rectangle? _bottomMask;
        private Rectangle? _imageBoundsOutline;
        private Rectangle? _selectionRect;
        private Ellipse? _radiusHandleTopLeftTop;
        private Ellipse? _radiusHandleTopLeftLeft;
        private Ellipse? _radiusHandleTopRightTop;
        private Ellipse? _radiusHandleTopRightRight;
        private Ellipse? _radiusHandleBottomLeftBottom;
        private Ellipse? _radiusHandleBottomLeftLeft;
        private Ellipse? _radiusHandleBottomRightBottom;
        private Ellipse? _radiusHandleBottomRightRight;
        private Ellipse? _topLeftHandle;
        private Ellipse? _topRightHandle;
        private Ellipse? _bottomLeftHandle;
        private Ellipse? _bottomRightHandle;
        private Rectangle? _topHandleBar;
        private Rectangle? _rightHandleBar;
        private Rectangle? _bottomHandleBar;
        private Rectangle? _leftHandleBar;
        private DialogImageCropFieldItem? _currentField;
        private DragMode _dragMode;
        private bool _showRadiusHandles;
        private Point _dragStartPoint;
        private Rect _dragStartRect;
        private double _dragStartCornerRadius;

        private enum DragMode
        {
            None,
            Move,
            RadiusTopLeftTop,
            RadiusTopLeftLeft,
            RadiusTopRightTop,
            RadiusTopRightRight,
            RadiusBottomLeftBottom,
            RadiusBottomLeftLeft,
            RadiusBottomRightBottom,
            RadiusBottomRightRight,
            TopLeft,
            Top,
            TopRight,
            Right,
            Bottom,
            BottomLeft,
            BottomRight,
            Left
        }

        public ImageCropEditor()
        {
            InitializeComponent();
            _cropSurfaceHost = this.FindControl<Border>("CropSurfaceHost");
            _cropSurface = this.FindControl<Grid>("CropSurface");
            _imageViewport = this.FindControl<Grid>("ImageViewport");
            _overlayCanvas = this.FindControl<Canvas>("OverlayCanvas");
            _topMask = this.FindControl<Rectangle>("TopMask");
            _leftMask = this.FindControl<Rectangle>("LeftMask");
            _rightMask = this.FindControl<Rectangle>("RightMask");
            _bottomMask = this.FindControl<Rectangle>("BottomMask");
            _imageBoundsOutline = this.FindControl<Rectangle>("ImageBoundsOutline");
            _selectionRect = this.FindControl<Rectangle>("SelectionRect");
            _radiusHandleTopLeftTop = this.FindControl<Ellipse>("RadiusHandleTopLeftTop");
            _radiusHandleTopLeftLeft = this.FindControl<Ellipse>("RadiusHandleTopLeftLeft");
            _radiusHandleTopRightTop = this.FindControl<Ellipse>("RadiusHandleTopRightTop");
            _radiusHandleTopRightRight = this.FindControl<Ellipse>("RadiusHandleTopRightRight");
            _radiusHandleBottomLeftBottom = this.FindControl<Ellipse>("RadiusHandleBottomLeftBottom");
            _radiusHandleBottomLeftLeft = this.FindControl<Ellipse>("RadiusHandleBottomLeftLeft");
            _radiusHandleBottomRightBottom = this.FindControl<Ellipse>("RadiusHandleBottomRightBottom");
            _radiusHandleBottomRightRight = this.FindControl<Ellipse>("RadiusHandleBottomRightRight");
            _topLeftHandle = this.FindControl<Ellipse>("TopLeftHandle");
            _topRightHandle = this.FindControl<Ellipse>("TopRightHandle");
            _bottomLeftHandle = this.FindControl<Ellipse>("BottomLeftHandle");
            _bottomRightHandle = this.FindControl<Ellipse>("BottomRightHandle");
            _topHandleBar = this.FindControl<Rectangle>("TopHandleBar");
            _rightHandleBar = this.FindControl<Rectangle>("RightHandleBar");
            _bottomHandleBar = this.FindControl<Rectangle>("BottomHandleBar");
            _leftHandleBar = this.FindControl<Rectangle>("LeftHandleBar");
            DataContextChanged += ImageCropEditor_DataContextChanged;
            SizeChanged += (_, _) => UpdateHostSquareSize();
            if (_cropSurface != null)
            {
                _cropSurface.SizeChanged += (_, _) => UpdateOverlay();
            }
            if (_imageViewport != null)
            {
                _imageViewport.SizeChanged += (_, _) => UpdateOverlay();
            }
            if (_cropSurfaceHost != null)
            {
                _cropSurfaceHost.AddHandler(PointerPressedEvent, CropSurfaceHost_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
        }

        private void ImageCropEditor_DataContextChanged(object? sender, EventArgs e)
        {
            if (_currentField != null)
            {
                _currentField.PropertyChanged -= CropField_PropertyChanged;
            }

            _currentField = DataContext as DialogImageCropFieldItem;
            if (_currentField != null)
            {
                _currentField.PropertyChanged += CropField_PropertyChanged;
            }

            UpdateOverlay();
        }

        private void CropField_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DialogImageCropFieldItem.SelectionX):
                case nameof(DialogImageCropFieldItem.SelectionY):
                case nameof(DialogImageCropFieldItem.SelectionWidth):
                case nameof(DialogImageCropFieldItem.SelectionHeight):
                case nameof(DialogImageCropFieldItem.CornerRadiusNormalized):
                case nameof(DialogImageCropFieldItem.PreviewImage):
                    UpdateOverlay();
                    break;
            }
        }

        private void UpdateHostSquareSize()
        {
            if (_cropSurfaceHost == null)
            {
                return;
            }

            var availableWidth = Bounds.Width - 8;
            if (availableWidth <= 0)
            {
                return;
            }

            var edge = Math.Min(396, availableWidth);
            _cropSurfaceHost.Width = edge;
            _cropSurfaceHost.Height = edge;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void CropSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not DialogImageCropFieldItem field || _imageViewport == null)
            {
                return;
            }

            if (e.GetCurrentPoint(_cropSurface).Properties.IsLeftButtonPressed &&
                e.ClickCount >= 2 &&
                field.ResetCropCommand.CanExecute(null))
            {
                field.ResetCropCommand.Execute(null);
                _dragMode = DragMode.None;
                e.Handled = true;
                return;
            }

            var currentRect = GetDisplaySelectionRect(field, GetDisplayedImageRect(field));
            var point = e.GetPosition(_imageViewport);
            var currentCornerRadius = field.CornerRadiusNormalized * Math.Min(currentRect.Width, currentRect.Height);
            _dragMode = HitTest(point, currentRect, currentCornerRadius);
            if (_dragMode == DragMode.None)
            {
                return;
            }

            _dragStartPoint = point;
            _dragStartRect = currentRect;
            _dragStartCornerRadius = field.CornerRadiusNormalized;
            e.Pointer.Capture(_cropSurface);
            e.Handled = true;
        }

        private void CropSurfaceHost_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not DialogImageCropFieldItem field || _cropSurfaceHost == null)
            {
                return;
            }

            if (!e.GetCurrentPoint(_cropSurfaceHost).Properties.IsLeftButtonPressed || e.ClickCount < 2)
            {
                return;
            }

            if (!field.ResetCropCommand.CanExecute(null))
            {
                return;
            }

            field.ResetCropCommand.Execute(null);
            _dragMode = DragMode.None;
            if (_cropSurface != null)
            {
                e.Pointer.Capture(null);
            }
            e.Handled = true;
        }

        private void CropSurface_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (DataContext is not DialogImageCropFieldItem field || _imageViewport == null || _dragMode == DragMode.None)
            {
                return;
            }

            var currentBounds = new Rect(_imageViewport.Bounds.Size);
            var imageBounds = GetDisplayedImageRect(field);
            if (currentBounds.Width <= 0 || currentBounds.Height <= 0 || imageBounds.Width <= 0 || imageBounds.Height <= 0)
            {
                return;
            }

            var point = e.GetPosition(_imageViewport);
            var deltaX = point.X - _dragStartPoint.X;
            var deltaY = point.Y - _dragStartPoint.Y;
            var rect = _dragStartRect;
            const double minSize = 28;

            switch (_dragMode)
            {
                case DragMode.Move:
                    rect = rect.Translate(new Vector(deltaX, deltaY));
                    break;
                case DragMode.RadiusTopLeftTop:
                case DragMode.RadiusTopLeftLeft:
                case DragMode.RadiusTopRightTop:
                case DragMode.RadiusTopRightRight:
                case DragMode.RadiusBottomLeftBottom:
                case DragMode.RadiusBottomLeftLeft:
                case DragMode.RadiusBottomRightBottom:
                case DragMode.RadiusBottomRightRight:
                    field.SetCornerRadius(ResolveCornerRadiusFromPointer(point, _dragStartRect));
                    UpdateOverlay();
                    e.Handled = true;
                    return;
                case DragMode.TopLeft:
                    rect = ResizeSquare(_dragStartRect, _dragMode, point, imageBounds, minSize);
                    break;
                case DragMode.Top:
                    rect = ResizeSquare(_dragStartRect, _dragMode, point, imageBounds, minSize);
                    break;
                case DragMode.TopRight:
                    rect = ResizeSquare(_dragStartRect, _dragMode, point, imageBounds, minSize);
                    break;
                case DragMode.Right:
                    rect = ResizeSquare(_dragStartRect, _dragMode, point, imageBounds, minSize);
                    break;
                case DragMode.Bottom:
                    rect = ResizeSquare(_dragStartRect, _dragMode, point, imageBounds, minSize);
                    break;
                case DragMode.BottomLeft:
                    rect = ResizeSquare(_dragStartRect, _dragMode, point, imageBounds, minSize);
                    break;
                case DragMode.BottomRight:
                    rect = ResizeSquare(_dragStartRect, _dragMode, point, imageBounds, minSize);
                    break;
                case DragMode.Left:
                    rect = ResizeSquare(_dragStartRect, _dragMode, point, imageBounds, minSize);
                    break;
            }

            if (_dragMode != DragMode.Move)
            {
                rect = new Rect(
                    rect.X,
                    rect.Y,
                    Math.Max(minSize, rect.Width),
                    Math.Max(minSize, rect.Height));
            }

            rect = ClampRect(rect, imageBounds, _dragMode == DragMode.Move);
            field.SetSelection(
                (rect.X - imageBounds.X) / imageBounds.Width,
                (rect.Y - imageBounds.Y) / imageBounds.Height,
                rect.Width / imageBounds.Width,
                rect.Height / imageBounds.Height);
            UpdateOverlay();
            e.Handled = true;
        }

        private void CropSurface_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_cropSurface != null)
            {
                e.Pointer.Capture(null);
            }

            _dragMode = DragMode.None;
            e.Handled = true;
        }

        private void CropSurface_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _dragMode = DragMode.None;
        }

        private void CropSurfaceHost_PointerEntered(object? sender, PointerEventArgs e)
        {
            _showRadiusHandles = true;
            UpdateOverlay();
        }

        private void CropSurfaceHost_PointerExited(object? sender, PointerEventArgs e)
        {
            if (_dragMode != DragMode.None)
            {
                return;
            }

            _showRadiusHandles = false;
            UpdateOverlay();
        }

        private void UpdateOverlay()
        {
            if (DataContext is not DialogImageCropFieldItem field ||
                _imageViewport == null ||
                _overlayCanvas == null ||
                _topMask == null ||
                _leftMask == null ||
                _rightMask == null ||
                _bottomMask == null ||
                _imageBoundsOutline == null ||
                _selectionRect == null ||
                _radiusHandleTopLeftTop == null ||
                _radiusHandleTopLeftLeft == null ||
                _radiusHandleTopRightTop == null ||
                _radiusHandleTopRightRight == null ||
                _radiusHandleBottomLeftBottom == null ||
                _radiusHandleBottomLeftLeft == null ||
                _radiusHandleBottomRightBottom == null ||
                _radiusHandleBottomRightRight == null ||
                _topLeftHandle == null ||
                _topRightHandle == null ||
                _bottomLeftHandle == null ||
                _bottomRightHandle == null ||
                _topHandleBar == null ||
                _rightHandleBar == null ||
                _bottomHandleBar == null ||
                _leftHandleBar == null)
            {
                return;
            }

            var imageBounds = GetDisplayedImageRect(field);
            var rect = GetDisplaySelectionRect(field, imageBounds);
            _overlayCanvas.Width = _imageViewport.Bounds.Width;
            _overlayCanvas.Height = _imageViewport.Bounds.Height;

            Canvas.SetLeft(_imageBoundsOutline, imageBounds.X);
            Canvas.SetTop(_imageBoundsOutline, imageBounds.Y);
            _imageBoundsOutline.Width = imageBounds.Width;
            _imageBoundsOutline.Height = imageBounds.Height;

            SetMaskRect(_topMask, imageBounds.X, imageBounds.Y, imageBounds.Width, Math.Max(0, rect.Y - imageBounds.Y));
            SetMaskRect(_bottomMask, imageBounds.X, rect.Bottom, imageBounds.Width, Math.Max(0, imageBounds.Bottom - rect.Bottom));
            SetMaskRect(_leftMask, imageBounds.X, rect.Y, Math.Max(0, rect.X - imageBounds.X), rect.Height);
            SetMaskRect(_rightMask, rect.Right, rect.Y, Math.Max(0, imageBounds.Right - rect.Right), rect.Height);

            Canvas.SetLeft(_selectionRect, rect.X);
            Canvas.SetTop(_selectionRect, rect.Y);
            _selectionRect.Width = rect.Width;
            _selectionRect.Height = rect.Height;
            var cornerRadius = Math.Clamp(field.CornerRadiusNormalized * Math.Min(rect.Width, rect.Height), 0, Math.Min(rect.Width, rect.Height) / 2d);
            _selectionRect.RadiusX = cornerRadius;
            _selectionRect.RadiusY = cornerRadius;

            var showRadiusHandles = _showRadiusHandles || IsRadiusDragMode(_dragMode);
            PositionRadiusHandle(_radiusHandleTopLeftTop, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusTopLeftTop), showRadiusHandles);
            PositionRadiusHandle(_radiusHandleTopLeftLeft, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusTopLeftLeft), showRadiusHandles);
            PositionRadiusHandle(_radiusHandleTopRightTop, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusTopRightTop), showRadiusHandles);
            PositionRadiusHandle(_radiusHandleTopRightRight, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusTopRightRight), showRadiusHandles);
            PositionRadiusHandle(_radiusHandleBottomLeftBottom, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusBottomLeftBottom), showRadiusHandles);
            PositionRadiusHandle(_radiusHandleBottomLeftLeft, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusBottomLeftLeft), showRadiusHandles);
            PositionRadiusHandle(_radiusHandleBottomRightBottom, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusBottomRightBottom), showRadiusHandles);
            PositionRadiusHandle(_radiusHandleBottomRightRight, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusBottomRightRight), showRadiusHandles);

            PositionHandle(_topLeftHandle, rect.TopLeft);
            PositionHandle(_topRightHandle, rect.TopRight);
            PositionHandle(_bottomLeftHandle, rect.BottomLeft);
            PositionHandle(_bottomRightHandle, rect.BottomRight);
            PositionHorizontalBar(_topHandleBar, new Point(rect.Center.X, rect.Top));
            PositionVerticalBar(_rightHandleBar, new Point(rect.Right, rect.Center.Y));
            PositionHorizontalBar(_bottomHandleBar, new Point(rect.Center.X, rect.Bottom));
            PositionVerticalBar(_leftHandleBar, new Point(rect.Left, rect.Center.Y));
        }

        private Rect GetDisplaySelectionRect(DialogImageCropFieldItem field, Rect imageBounds)
        {
            if (_cropSurface == null || imageBounds.Width <= 0 || imageBounds.Height <= 0)
            {
                return new Rect(0, 0, 0, 0);
            }

            return new Rect(
                imageBounds.X + (field.SelectionX * imageBounds.Width),
                imageBounds.Y + (field.SelectionY * imageBounds.Height),
                field.SelectionWidth * imageBounds.Width,
                field.SelectionHeight * imageBounds.Height);
        }

        private Rect GetDisplayedImageRect(DialogImageCropFieldItem field)
        {
            if (_imageViewport == null || field.PreviewImage == null)
            {
                return new Rect(0, 0, 0, 0);
            }

            var availableWidth = _imageViewport.Bounds.Width;
            var availableHeight = _imageViewport.Bounds.Height;
            if (availableWidth <= 0 || availableHeight <= 0)
            {
                return new Rect(0, 0, 0, 0);
            }

            var imageWidth = Math.Max(1, field.PreviewImage.PixelSize.Width);
            var imageHeight = Math.Max(1, field.PreviewImage.PixelSize.Height);
            var scale = Math.Min(availableWidth / imageWidth, availableHeight / imageHeight);
            var displayWidth = imageWidth * scale;
            var displayHeight = imageHeight * scale;
            var x = (availableWidth - displayWidth) / 2d;
            var y = (availableHeight - displayHeight) / 2d;
            return new Rect(x, y, displayWidth, displayHeight);
        }

        private static void SetMaskRect(Control control, double x, double y, double width, double height)
        {
            Canvas.SetLeft(control, x);
            Canvas.SetTop(control, y);
            control.Width = Math.Max(0, width);
            control.Height = Math.Max(0, height);
            control.IsVisible = width > 0 && height > 0;
        }

        private DragMode HitTest(Point point, Rect rect, double cornerRadius)
        {
            if (Distance(point, rect.TopLeft) <= CornerHandleHitRadius)
            {
                return DragMode.TopLeft;
            }
            if (Math.Abs(point.Y - rect.Top) <= SideHandleHitRadius && point.X >= rect.X + SideHandleCornerGuard && point.X <= rect.Right - SideHandleCornerGuard)
            {
                return DragMode.Top;
            }
            if (Distance(point, rect.TopRight) <= CornerHandleHitRadius)
            {
                return DragMode.TopRight;
            }
            if (Math.Abs(point.X - rect.Right) <= SideHandleHitRadius && point.Y >= rect.Y + SideHandleCornerGuard && point.Y <= rect.Bottom - SideHandleCornerGuard)
            {
                return DragMode.Right;
            }
            if (Math.Abs(point.Y - rect.Bottom) <= SideHandleHitRadius && point.X >= rect.X + SideHandleCornerGuard && point.X <= rect.Right - SideHandleCornerGuard)
            {
                return DragMode.Bottom;
            }
            if (Distance(point, rect.BottomLeft) <= CornerHandleHitRadius)
            {
                return DragMode.BottomLeft;
            }
            if (Math.Abs(point.X - rect.Left) <= SideHandleHitRadius && point.Y >= rect.Y + SideHandleCornerGuard && point.Y <= rect.Bottom - SideHandleCornerGuard)
            {
                return DragMode.Left;
            }
            if (Distance(point, rect.BottomRight) <= CornerHandleHitRadius)
            {
                return DragMode.BottomRight;
            }

            var showRadiusHandles = _showRadiusHandles || IsRadiusDragMode(_dragMode);
            if (!showRadiusHandles)
            {
                if (rect.Contains(point))
                {
                    return DragMode.Move;
                }

                return DragMode.None;
            }

            if (Distance(point, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusTopLeftTop)) <= RadiusHandleHitRadius)
            {
                return DragMode.RadiusTopLeftTop;
            }
            if (Distance(point, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusTopLeftLeft)) <= RadiusHandleHitRadius)
            {
                return DragMode.RadiusTopLeftLeft;
            }
            if (Distance(point, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusTopRightTop)) <= RadiusHandleHitRadius)
            {
                return DragMode.RadiusTopRightTop;
            }
            if (Distance(point, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusTopRightRight)) <= RadiusHandleHitRadius)
            {
                return DragMode.RadiusTopRightRight;
            }
            if (Distance(point, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusBottomLeftBottom)) <= RadiusHandleHitRadius)
            {
                return DragMode.RadiusBottomLeftBottom;
            }
            if (Distance(point, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusBottomLeftLeft)) <= RadiusHandleHitRadius)
            {
                return DragMode.RadiusBottomLeftLeft;
            }
            if (Distance(point, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusBottomRightBottom)) <= RadiusHandleHitRadius)
            {
                return DragMode.RadiusBottomRightBottom;
            }
            if (Distance(point, GetRadiusHandlePosition(rect, cornerRadius, DragMode.RadiusBottomRightRight)) <= RadiusHandleHitRadius)
            {
                return DragMode.RadiusBottomRightRight;
            }
            if (rect.Contains(point))
            {
                return DragMode.Move;
            }

            return DragMode.None;
        }

        private static bool IsRadiusDragMode(DragMode mode)
        {
            return mode is DragMode.RadiusTopLeftTop or DragMode.RadiusTopLeftLeft
                or DragMode.RadiusTopRightTop or DragMode.RadiusTopRightRight
                or DragMode.RadiusBottomLeftBottom or DragMode.RadiusBottomLeftLeft
                or DragMode.RadiusBottomRightBottom or DragMode.RadiusBottomRightRight;
        }

        private double ResolveCornerRadiusFromPointer(Point point, Rect rect)
        {
            var maxDistance = Math.Min(rect.Width, rect.Height) / 2d;
            if (maxDistance <= 0 || _dragMode == DragMode.None)
            {
                return _dragStartCornerRadius;
            }

            var travelRange = Math.Max(1d, maxDistance - RadiusHandleBaseInset);

            double handleDistance = _dragMode switch
            {
                DragMode.RadiusTopLeftTop => Math.Clamp(point.X - rect.Left, RadiusHandleBaseInset, maxDistance),
                DragMode.RadiusTopLeftLeft => Math.Clamp(point.Y - rect.Top, RadiusHandleBaseInset, maxDistance),
                DragMode.RadiusTopRightTop => Math.Clamp(rect.Right - point.X, RadiusHandleBaseInset, maxDistance),
                DragMode.RadiusTopRightRight => Math.Clamp(point.Y - rect.Top, RadiusHandleBaseInset, maxDistance),
                DragMode.RadiusBottomLeftBottom => Math.Clamp(point.X - rect.Left, RadiusHandleBaseInset, maxDistance),
                DragMode.RadiusBottomLeftLeft => Math.Clamp(rect.Bottom - point.Y, RadiusHandleBaseInset, maxDistance),
                DragMode.RadiusBottomRightBottom => Math.Clamp(rect.Right - point.X, RadiusHandleBaseInset, maxDistance),
                DragMode.RadiusBottomRightRight => Math.Clamp(rect.Bottom - point.Y, RadiusHandleBaseInset, maxDistance),
                _ => RadiusHandleBaseInset + (_dragStartCornerRadius * travelRange / 0.5d)
            };

            var normalized = (handleDistance - RadiusHandleBaseInset) / travelRange;
            return Math.Clamp(normalized * 0.5d, 0, 0.5d);
        }

        private static double Distance(Point a, Point b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static Rect ClampRect(Rect rect, Rect bounds, bool preserveSize)
        {
            if (preserveSize)
            {
                var x = Math.Clamp(rect.X, bounds.X, bounds.Right - rect.Width);
                var y = Math.Clamp(rect.Y, bounds.Y, bounds.Bottom - rect.Height);
                return new Rect(x, y, rect.Width, rect.Height);
            }

            var clampedX = Math.Clamp(rect.X, bounds.X, bounds.Right);
            var clampedY = Math.Clamp(rect.Y, bounds.Y, bounds.Bottom);
            var right = Math.Clamp(rect.Right, bounds.X, bounds.Right);
            var bottom = Math.Clamp(rect.Bottom, bounds.Y, bounds.Bottom);
            var width = Math.Max(28, right - clampedX);
            var height = Math.Max(28, bottom - clampedY);
            var size = Math.Max(28, Math.Min(width, height));
            return new Rect(clampedX, clampedY, size, size);
        }

        private static Rect ResizeSquare(Rect startRect, DragMode dragMode, Point point, Rect bounds, double minSize)
        {
            Point anchor;
            double desiredSize;
            double maxSize;

            switch (dragMode)
            {
                case DragMode.TopLeft:
                    anchor = startRect.BottomRight;
                    desiredSize = Math.Max(anchor.X - point.X, anchor.Y - point.Y);
                    maxSize = Math.Min(anchor.X - bounds.X, anchor.Y - bounds.Y);
                    break;
                case DragMode.Top:
                    anchor = startRect.BottomRight;
                    desiredSize = anchor.Y - point.Y;
                    maxSize = Math.Min(startRect.Center.X - bounds.X, anchor.Y - bounds.Y) * 2d;
                    break;
                case DragMode.TopRight:
                    anchor = startRect.BottomLeft;
                    desiredSize = Math.Max(point.X - anchor.X, anchor.Y - point.Y);
                    maxSize = Math.Min(bounds.Right - anchor.X, anchor.Y - bounds.Y);
                    break;
                case DragMode.Right:
                    anchor = startRect.TopLeft;
                    desiredSize = point.X - anchor.X;
                    maxSize = Math.Min(bounds.Right - anchor.X, startRect.Center.Y - bounds.Y) * 2d;
                    break;
                case DragMode.Bottom:
                    anchor = startRect.TopLeft;
                    desiredSize = point.Y - anchor.Y;
                    maxSize = Math.Min(bounds.Bottom - anchor.Y, bounds.Right - startRect.Center.X) * 2d;
                    break;
                case DragMode.BottomLeft:
                    anchor = startRect.TopRight;
                    desiredSize = Math.Max(anchor.X - point.X, point.Y - anchor.Y);
                    maxSize = Math.Min(anchor.X - bounds.X, bounds.Bottom - anchor.Y);
                    break;
                case DragMode.BottomRight:
                    anchor = startRect.TopLeft;
                    desiredSize = Math.Max(point.X - anchor.X, point.Y - anchor.Y);
                    maxSize = Math.Min(bounds.Right - anchor.X, bounds.Bottom - anchor.Y);
                    break;
                case DragMode.Left:
                    anchor = startRect.TopRight;
                    desiredSize = anchor.X - point.X;
                    maxSize = Math.Min(anchor.X - bounds.X, bounds.Bottom - startRect.Center.Y) * 2d;
                    break;
                default:
                    return startRect;
            }

            if (maxSize <= 0)
            {
                return startRect;
            }

            var size = Math.Clamp(desiredSize, Math.Min(minSize, maxSize), maxSize);
            return dragMode switch
            {
                DragMode.TopLeft => new Rect(anchor.X - size, anchor.Y - size, size, size),
                DragMode.Top => new Rect(startRect.Center.X - (size / 2d), anchor.Y - size, size, size),
                DragMode.TopRight => new Rect(anchor.X, anchor.Y - size, size, size),
                DragMode.Right => new Rect(anchor.X, startRect.Center.Y - (size / 2d), size, size),
                DragMode.Bottom => new Rect(startRect.Center.X - (size / 2d), anchor.Y, size, size),
                DragMode.BottomLeft => new Rect(anchor.X - size, anchor.Y, size, size),
                DragMode.BottomRight => new Rect(anchor.X, anchor.Y, size, size),
                DragMode.Left => new Rect(anchor.X - size, startRect.Center.Y - (size / 2d), size, size),
                _ => startRect
            };
        }

        private static void PositionHandle(Control handle, Point center)
        {
            Canvas.SetLeft(handle, center.X - handle.Width / 2);
            Canvas.SetTop(handle, center.Y - handle.Height / 2);
        }

        private static void PositionHorizontalBar(Control handle, Point center)
        {
            Canvas.SetLeft(handle, center.X - handle.Width / 2);
            Canvas.SetTop(handle, center.Y - handle.Height / 2);
        }

        private static void PositionVerticalBar(Control handle, Point center)
        {
            Canvas.SetLeft(handle, center.X - handle.Width / 2);
            Canvas.SetTop(handle, center.Y - handle.Height / 2);
        }

        private static void PositionRadiusHandle(Control handle, Point center, bool isVisible)
        {
            handle.IsVisible = isVisible;
            if (!isVisible)
            {
                return;
            }

            Canvas.SetLeft(handle, center.X - handle.Width / 2);
            Canvas.SetTop(handle, center.Y - handle.Height / 2);
        }

        private static Point GetRadiusHandlePosition(Rect rect, double cornerRadius, DragMode dragMode)
        {
            var maxDistance = Math.Min(rect.Width, rect.Height) / 2d;
            var travelRange = Math.Max(1d, maxDistance - RadiusHandleBaseInset);
            var radiusRatio = maxDistance <= 0 ? 0 : Math.Clamp(cornerRadius / maxDistance, 0, 1);
            var edgeDistance = RadiusHandleBaseInset + (radiusRatio * travelRange);

            return dragMode switch
            {
                DragMode.RadiusTopLeftTop => new Point(rect.Left + edgeDistance, rect.Top - RadiusHandleVisualOffset),
                DragMode.RadiusTopLeftLeft => new Point(rect.Left - RadiusHandleVisualOffset, rect.Top + edgeDistance),
                DragMode.RadiusTopRightTop => new Point(rect.Right - edgeDistance, rect.Top - RadiusHandleVisualOffset),
                DragMode.RadiusTopRightRight => new Point(rect.Right + RadiusHandleVisualOffset, rect.Top + edgeDistance),
                DragMode.RadiusBottomLeftBottom => new Point(rect.Left + edgeDistance, rect.Bottom + RadiusHandleVisualOffset),
                DragMode.RadiusBottomLeftLeft => new Point(rect.Left - RadiusHandleVisualOffset, rect.Bottom - edgeDistance),
                DragMode.RadiusBottomRightBottom => new Point(rect.Right - edgeDistance, rect.Bottom + RadiusHandleVisualOffset),
                DragMode.RadiusBottomRightRight => new Point(rect.Right + RadiusHandleVisualOffset, rect.Bottom - edgeDistance),
                _ => rect.TopLeft
            };
        }
    }
}
