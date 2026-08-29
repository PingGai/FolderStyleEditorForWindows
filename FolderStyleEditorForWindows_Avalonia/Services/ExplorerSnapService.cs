using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace FolderStyleEditorForWindows.Services;

public enum ExplorerSnapSide
{
    Left,
    Right
}

public enum ExplorerSnapPlacement
{
    Outside,
    Inside
}

public enum ExplorerSnapPreviewEdge
{
    Left,
    Right
}

public readonly record struct NativeRectPx(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);
    public int Height => Math.Max(0, Bottom - Top);
    public int CenterX => Left + (Width / 2);
    public int CenterY => Top + (Height / 2);

    public PixelPoint TopLeft => new(Left, Top);
}

public readonly record struct ExplorerWindowSnapshot(IntPtr Hwnd, NativeRectPx Bounds, bool IsForeground);

public readonly record struct ExplorerSnapCandidate(
    IntPtr ExplorerHwnd,
    NativeRectPx ExplorerBounds,
    ExplorerSnapSide Side,
    ExplorerSnapPlacement Placement,
    PixelPoint TargetPosition,
    int GapPx,
    bool UseVerticalCenter,
    int RelativeTopOffsetPx)
{
    public ExplorerSnapPreviewEdge PreviewEdge =>
        Placement == ExplorerSnapPlacement.Inside
            ? (Side == ExplorerSnapSide.Left ? ExplorerSnapPreviewEdge.Left : ExplorerSnapPreviewEdge.Right)
            : (Side == ExplorerSnapSide.Left ? ExplorerSnapPreviewEdge.Right : ExplorerSnapPreviewEdge.Left);
}

public sealed class ExplorerSnapActivityChangedEventArgs : EventArgs
{
    public ExplorerSnapActivityChangedEventArgs(bool isSnapped, bool isAnimating)
    {
        IsSnapped = isSnapped;
        IsAnimating = isAnimating;
    }

    public bool IsSnapped { get; }

    public bool IsAnimating { get; }
}

public sealed class ExplorerSnapService : IDisposable
{
    private const int SnapGapPx = 12;
    private const int SnapTriggerRangePx = 56;
    private const int VerticalCenterThresholdPx = 64;
    private const int ForegroundBiasScore = 10;
    private const int ExplorerCacheMilliseconds = 360;
    private const int ActiveTickMilliseconds = 8;
    private const int PassiveTickMilliseconds = 16;
    private const int ExplorerMotionKeepAliveMilliseconds = 96;
    private const int OcclusionProbeWidthPx = 24;
    private const int OcclusionProbeHeightPx = 72;
    private const int OcclusionSampleColumns = 3;
    private const int OcclusionSampleRows = 3;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
    private const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    private const uint EVENT_OBJECT_DESTROY = 0x8001;
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    private const int OBJID_WINDOW = 0;
    private const int CHILDID_SELF = 0;
    private const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const uint DWMWA_CLOAKED = 14;
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint GA_ROOT = 2;
    private const uint GA_ROOTOWNER = 3;
    private const uint GW_HWNDNEXT = 2;
    private const int ForegroundRaiseThrottleMilliseconds = 96;

    private readonly DispatcherTimer _tickTimer;
    private readonly SnapSpringAnimator _springAnimator = new();
    private readonly WinEventDelegate _winEventDelegate;
    private readonly List<IntPtr> _eventHooks = new();
    private readonly List<ExplorerWindowSnapshot> _explorerCache = new();
    private readonly HashSet<IntPtr> _cachedExplorerHwnds = new();
    private DateTime _explorerCacheExpiresUtc = DateTime.MinValue;
    private SnapSession? _session;
    private Window? _hostWindow;
    private PixelSize _hostWindowSizePx;
    private NativeRectPx _trackedExplorerRectPx;
    private DateTime _lastExplorerMotionUtc = DateTime.MinValue;
    private bool _trackedExplorerIsMoving;
    private bool _disposed;
    private bool _lastReportedIsSnapped;
    private bool _lastReportedIsAnimating;
    private long _lastTickTimestamp;
    private PixelPoint? _lastAppliedHostPositionPx;
    private DateTime _lastForegroundRaiseUtc = DateTime.MinValue;
    private bool _isHostWindowTemporarilyTopmost;

    public ExplorerSnapService()
    {
        _tickTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(PassiveTickMilliseconds)
        };
        _tickTimer.Tick += TickTimerOnTick;

        _winEventDelegate = HandleWinEvent;
        if (OperatingSystem.IsWindows())
        {
            RegisterEventHooks();
        }
    }

    public event EventHandler<ExplorerSnapActivityChangedEventArgs>? ActivityChanged;

    public bool IsSnapped => _session != null;

    public void NotifyHostWindowManualTopmostChanged(bool isTopmost)
    {
        if (isTopmost)
        {
            _isHostWindowTemporarilyTopmost = false;
            return;
        }

        if (_session is { } session)
        {
            RefreshHostWindowZOrder(session, force: true);
        }
    }

    public ExplorerSnapCandidate? FindBestCandidate(PixelPoint appPositionPx, PixelSize appSizePx, IntPtr hostWindowHwnd = default)
    {
        if (!OperatingSystem.IsWindows() || appSizePx.Width <= 0 || appSizePx.Height <= 0)
        {
            return null;
        }

        var appRectPx = CreateRect(appPositionPx, appSizePx);
        ExplorerSnapCandidate? bestCandidate = null;
        var bestScore = double.MaxValue;

        foreach (var explorer in GetExplorerWindows())
        {
            foreach (var side in new[] { ExplorerSnapSide.Left, ExplorerSnapSide.Right })
            {
                foreach (var placement in new[] { ExplorerSnapPlacement.Outside, ExplorerSnapPlacement.Inside })
                {
                    if (placement == ExplorerSnapPlacement.Inside &&
                        explorer.Bounds.Width <= appRectPx.Width + (SnapGapPx * 2))
                    {
                        continue;
                    }

                    var useVerticalCenter = Math.Abs(appRectPx.CenterY - explorer.Bounds.CenterY) <= VerticalCenterThresholdPx;
                    var targetPositionPx = ComputeTargetPosition(explorer.Bounds, appRectPx, side, placement, useVerticalCenter);
                    var targetRectPx = CreateRect(targetPositionPx, appSizePx);
                    var horizontalDelta = Math.Abs(appRectPx.CenterX - targetRectPx.CenterX);
                    if (horizontalDelta > SnapTriggerRangePx)
                    {
                        continue;
                    }

                    if (!IsExplorerSnapTargetVisible(explorer.Hwnd, hostWindowHwnd, explorer.Bounds, targetRectPx, side))
                    {
                        continue;
                    }

                    double score = horizontalDelta;
                    if (useVerticalCenter)
                    {
                        score += Math.Abs(appRectPx.CenterY - explorer.Bounds.CenterY) * 0.25;
                    }

                    if (explorer.IsForeground)
                    {
                        score -= ForegroundBiasScore;
                    }

                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestCandidate = new ExplorerSnapCandidate(
                        explorer.Hwnd,
                        explorer.Bounds,
                        side,
                        placement,
                        targetPositionPx,
                        SnapGapPx,
                        useVerticalCenter,
                        targetPositionPx.Y - explorer.Bounds.Top);
                }
            }
        }

        return bestCandidate;
    }

    public bool BeginSnap(Window hostWindow, PixelSize hostWindowSizePx, PixelPoint currentPositionPx, ExplorerSnapCandidate candidate)
    {
        if (!TryGetEligibleExplorerRect(candidate.ExplorerHwnd, out var explorerRectPx))
        {
            StopSnap();
            return false;
        }

        _hostWindow = hostWindow;
        _hostWindowSizePx = hostWindowSizePx;
        _trackedExplorerRectPx = explorerRectPx;
        _lastExplorerMotionUtc = DateTime.UtcNow;
        _trackedExplorerIsMoving = false;

        _session = new SnapSession(
            candidate.ExplorerHwnd,
            candidate.Side,
            candidate.Placement,
            candidate.GapPx,
            candidate.UseVerticalCenter,
            candidate.UseVerticalCenter
                ? 0
                : currentPositionPx.Y - explorerRectPx.Top);

        var targetPositionPx = ComputeTargetPosition(_trackedExplorerRectPx, _hostWindowSizePx, _session.Value);
        _springAnimator.Start(ToVector(currentPositionPx), ToVector(targetPositionPx));
        _lastAppliedHostPositionPx = currentPositionPx;
        RefreshHostWindowZOrder(_session.Value, force: true);
        EnsureTimerState();
        RaiseActivityChangedIfNeeded();
        return true;
    }

    public void UpdateHostWindowSize(PixelSize hostWindowSizePx)
    {
        if (hostWindowSizePx.Width <= 0 || hostWindowSizePx.Height <= 0)
        {
            return;
        }

        _hostWindowSizePx = hostWindowSizePx;
        if (_session == null)
        {
            return;
        }

        var nextTarget = ComputeTargetPosition(_trackedExplorerRectPx, _hostWindowSizePx, _session.Value);
        _springAnimator.ShiftBaseTarget(ToVector(nextTarget));
        ApplyHostWindowPositionFromAnimator();
        EnsureTimerState();
        RaiseActivityChangedIfNeeded();
    }

    public void StopSnap()
    {
        ClearHostWindowTemporaryTopmost();
        _session = null;
        _hostWindow = null;
        _trackedExplorerIsMoving = false;
        _lastExplorerMotionUtc = DateTime.MinValue;
        _lastTickTimestamp = 0;
        _lastAppliedHostPositionPx = null;
        _springAnimator.SnapToTarget(default);
        EnsureTimerState();
        RaiseActivityChangedIfNeeded();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tickTimer.Stop();
        _tickTimer.Tick -= TickTimerOnTick;

        foreach (var hook in _eventHooks)
        {
            if (hook != IntPtr.Zero)
            {
                UnhookWinEvent(hook);
            }
        }

        _eventHooks.Clear();
        ClearHostWindowTemporaryTopmost();
        _session = null;
        _hostWindow = null;
    }

    private void TickTimerOnTick(object? sender, EventArgs e)
    {
        if (_session == null || _hostWindow == null)
        {
            EnsureTimerState();
            RaiseActivityChangedIfNeeded();
            return;
        }

        var session = _session.Value;
        if (!TryGetTrackedExplorerRect(session.ExplorerHwnd, out var explorerRectPx))
        {
            StopSnap();
            return;
        }

        if (!explorerRectPx.Equals(_trackedExplorerRectPx))
        {
            _trackedExplorerRectPx = explorerRectPx;
            _lastExplorerMotionUtc = DateTime.UtcNow;
            var nextTargetPx = ComputeTargetPosition(_trackedExplorerRectPx, _hostWindowSizePx, session);
            _springAnimator.SnapToTarget(ToVector(nextTargetPx));
            ApplyHostWindowPositionFromAnimator();
            RefreshHostWindowZOrder(session);
        }

        var now = Stopwatch.GetTimestamp();
        var deltaSeconds = _lastTickTimestamp == 0
            ? _tickTimer.Interval.TotalSeconds
            : (now - _lastTickTimestamp) / (double)Stopwatch.Frequency;
        _lastTickTimestamp = now;
        var currentPosition = _springAnimator.Tick(deltaSeconds);
        ApplyHostWindowPosition(currentPosition);
        RefreshHostWindowZOrder(session);

        EnsureTimerState();
        RaiseActivityChangedIfNeeded();
    }

    private void EnsureTimerState()
    {
        if (_session == null || _hostWindow == null)
        {
            _tickTimer.Stop();
            _lastTickTimestamp = 0;
            return;
        }

        if (_trackedExplorerIsMoving &&
            (DateTime.UtcNow - _lastExplorerMotionUtc) > TimeSpan.FromMilliseconds(ExplorerMotionKeepAliveMilliseconds))
        {
            _trackedExplorerIsMoving = false;
        }

        var shouldUseActiveInterval =
            _springAnimator.IsActive ||
            _trackedExplorerIsMoving ||
            (DateTime.UtcNow - _lastExplorerMotionUtc) <= TimeSpan.FromMilliseconds(ExplorerMotionKeepAliveMilliseconds);
        var desiredInterval = TimeSpan.FromMilliseconds(shouldUseActiveInterval ? ActiveTickMilliseconds : PassiveTickMilliseconds);
        if (_tickTimer.Interval != desiredInterval)
        {
            _tickTimer.Interval = desiredInterval;
        }

        if (!_tickTimer.IsEnabled)
        {
            _tickTimer.Start();
        }
    }

    private void RegisterEventHooks()
    {
        RegisterHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND);
        RegisterHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE);
        RegisterHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_DESTROY);
        RegisterHook(EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZESTART);
        RegisterHook(EVENT_SYSTEM_MOVESIZEEND, EVENT_SYSTEM_MOVESIZEEND);
        RegisterHook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZESTART);
        RegisterHook(EVENT_SYSTEM_MINIMIZEEND, EVENT_SYSTEM_MINIMIZEEND);
    }

    private void RegisterHook(uint eventMin, uint eventMax)
    {
        var hook = SetWinEventHook(
            eventMin,
            eventMax,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (hook != IntPtr.Zero)
        {
            _eventHooks.Add(hook);
        }
    }

    private void HandleWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (_disposed || hwnd == IntPtr.Zero)
        {
            return;
        }

        if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF)
        {
            return;
        }

        if (_cachedExplorerHwnds.Contains(hwnd))
        {
            _explorerCacheExpiresUtc = DateTime.MinValue;
        }

        if (_session is not { } currentSession)
        {
            return;
        }

        if (eventType != EVENT_SYSTEM_FOREGROUND && hwnd != currentSession.ExplorerHwnd)
        {
            return;
        }

        void HandleOnUiThread()
        {
            if (_session is not { } innerSession)
            {
                return;
            }

            if (eventType == EVENT_SYSTEM_FOREGROUND)
            {
                ProcessForegroundWindowChanged(innerSession, hwnd);
                return;
            }

            if (hwnd != innerSession.ExplorerHwnd)
            {
                return;
            }

            if (eventType is EVENT_SYSTEM_MINIMIZESTART or EVENT_OBJECT_DESTROY)
            {
                StopSnap();
                return;
            }

            ProcessTrackedExplorerEvent(eventType);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            HandleOnUiThread();
            return;
        }

        Dispatcher.UIThread.Post(HandleOnUiThread, DispatcherPriority.Input);
    }

    private void ProcessTrackedExplorerEvent(uint eventType)
    {
        switch (eventType)
        {
            case EVENT_SYSTEM_MOVESIZESTART:
                _trackedExplorerIsMoving = true;
                _lastExplorerMotionUtc = DateTime.UtcNow;
                ProcessTrackedExplorerUpdate();
                EnsureTimerState();
                break;
            case EVENT_SYSTEM_MOVESIZEEND:
            case EVENT_SYSTEM_MINIMIZEEND:
                _trackedExplorerIsMoving = false;
                _lastExplorerMotionUtc = DateTime.UtcNow;
                ProcessTrackedExplorerUpdate();
                break;
            case EVENT_OBJECT_LOCATIONCHANGE:
                _trackedExplorerIsMoving = true;
                _lastExplorerMotionUtc = DateTime.UtcNow;
                ProcessTrackedExplorerUpdate();
                break;
        }
    }

    private void ProcessForegroundWindowChanged(SnapSession session, IntPtr? eventHwnd)
    {
        RefreshHostWindowZOrder(session, force: true, eventHwnd);
        EnsureTimerState();
    }

    private void ProcessTrackedExplorerUpdate()
    {
        if (_session is not { } session)
        {
            return;
        }

        if (!TryGetTrackedExplorerRect(session.ExplorerHwnd, out var explorerRectPx))
        {
            StopSnap();
            return;
        }

        _trackedExplorerRectPx = explorerRectPx;
        var nextTarget = ComputeTargetPosition(_trackedExplorerRectPx, _hostWindowSizePx, session);
        _springAnimator.SnapToTarget(ToVector(nextTarget));
        ApplyHostWindowPositionFromAnimator();
        RefreshHostWindowZOrder(session, force: true);
        EnsureTimerState();
        RaiseActivityChangedIfNeeded();
    }

    private void RefreshHostWindowZOrder(SnapSession session, bool force = false, IntPtr? eventHwnd = null)
    {
        var shouldKeepRaised =
            IsTrackedExplorerForeground(session, eventHwnd) ||
            IsHostAppWindowForeground(eventHwnd);
        SetHostWindowTemporaryTopmost(shouldKeepRaised, force);
    }

    private void SetHostWindowTemporaryTopmost(bool enabled, bool force = false)
    {
        if (_hostWindow == null)
        {
            _isHostWindowTemporarilyTopmost = false;
            return;
        }

        if (enabled && !_isHostWindowTemporarilyTopmost && _hostWindow.Topmost)
        {
            _isHostWindowTemporarilyTopmost = false;
            return;
        }

        if (enabled)
        {
            var now = DateTime.UtcNow;
            if (!force &&
                _isHostWindowTemporarilyTopmost &&
                (now - _lastForegroundRaiseUtc) < TimeSpan.FromMilliseconds(ForegroundRaiseThrottleMilliseconds))
            {
                return;
            }

            _lastForegroundRaiseUtc = now;
            SetHostWindowZOrder(HwndTopMost);
            _isHostWindowTemporarilyTopmost = true;
            return;
        }

        ClearHostWindowTemporaryTopmost();
    }

    private void ClearHostWindowTemporaryTopmost()
    {
        if (!_isHostWindowTemporarilyTopmost)
        {
            return;
        }

        SetHostWindowZOrder(HwndNoTopMost);
        PlaceHostWindowBehindForegroundWindow();

        _isHostWindowTemporarilyTopmost = false;
    }

    private void SetHostWindowZOrder(IntPtr zOrder)
    {
        var platformHandle = _hostWindow?.TryGetPlatformHandle();
        var hwnd = platformHandle?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        const uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW;
        _ = SetWindowPos(
            hwnd,
            zOrder,
            0,
            0,
            0,
            0,
            flags);
    }

    private void PlaceHostWindowBehindForegroundWindow()
    {
        var hostHwnd = GetHostWindowHwnd();
        var foregroundWindow = GetForegroundWindow();
        if (hostHwnd == IntPtr.Zero || foregroundWindow == IntPtr.Zero)
        {
            return;
        }

        var foregroundRoot = GetAncestor(foregroundWindow, GA_ROOT);
        if (foregroundRoot == IntPtr.Zero)
        {
            foregroundRoot = foregroundWindow;
        }

        if (IsSameWindowTree(foregroundRoot, hostHwnd))
        {
            return;
        }

        SetHostWindowZOrder(foregroundRoot);
    }

    private static bool IsTrackedExplorerForeground(SnapSession session, IntPtr? eventHwnd)
    {
        var foregroundWindow = GetForegroundWindow();
        return IsSameWindowTree(foregroundWindow, session.ExplorerHwnd) ||
               (eventHwnd is { } hwnd && IsSameWindowTree(hwnd, session.ExplorerHwnd));
    }

    private bool IsHostAppWindowForeground(IntPtr? eventHwnd)
    {
        var hostHwnd = GetHostWindowHwnd();
        if (hostHwnd == IntPtr.Zero)
        {
            return false;
        }

        var foregroundWindow = GetForegroundWindow();
        return IsSameWindowTree(foregroundWindow, hostHwnd) ||
               (eventHwnd is { } hwnd && IsSameWindowTree(hwnd, hostHwnd));
    }

    private IntPtr GetHostWindowHwnd()
    {
        var platformHandle = _hostWindow?.TryGetPlatformHandle();
        return platformHandle?.Handle ?? IntPtr.Zero;
    }

    private static bool IsSameWindowTree(IntPtr candidateHwnd, IntPtr trackedExplorerHwnd)
    {
        if (candidateHwnd == IntPtr.Zero || trackedExplorerHwnd == IntPtr.Zero)
        {
            return false;
        }

        if (candidateHwnd == trackedExplorerHwnd)
        {
            return true;
        }

        var root = GetAncestor(candidateHwnd, GA_ROOT);
        if (root == trackedExplorerHwnd)
        {
            return true;
        }

        var rootOwner = GetAncestor(candidateHwnd, GA_ROOTOWNER);
        if (rootOwner == trackedExplorerHwnd)
        {
            return true;
        }

        return IsChild(trackedExplorerHwnd, candidateHwnd);
    }

    private static bool IsExplorerSnapTargetVisible(
        IntPtr explorerHwnd,
        IntPtr hostWindowHwnd,
        NativeRectPx explorerRectPx,
        NativeRectPx targetRectPx,
        ExplorerSnapSide side)
    {
        var probeRectPx = CreateExplorerEdgeProbeRect(explorerRectPx, targetRectPx, side);
        if (probeRectPx.Width <= 0 || probeRectPx.Height <= 0)
        {
            return true;
        }

        for (var row = 0; row < OcclusionSampleRows; row++)
        {
            var y = GetSampleCoordinate(probeRectPx.Top, probeRectPx.Bottom, row, OcclusionSampleRows);
            for (var column = 0; column < OcclusionSampleColumns; column++)
            {
                var x = GetSampleCoordinate(probeRectPx.Left, probeRectPx.Right, column, OcclusionSampleColumns);
                if (IsExplorerSampleVisible(explorerHwnd, hostWindowHwnd, x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static NativeRectPx CreateExplorerEdgeProbeRect(
        NativeRectPx explorerRectPx,
        NativeRectPx targetRectPx,
        ExplorerSnapSide side)
    {
        var probeWidth = Math.Min(OcclusionProbeWidthPx, explorerRectPx.Width);
        var left = side == ExplorerSnapSide.Left
            ? explorerRectPx.Left
            : explorerRectPx.Right - probeWidth;
        var right = left + probeWidth;

        var top = Math.Max(explorerRectPx.Top, targetRectPx.Top);
        var bottom = Math.Min(explorerRectPx.Bottom, targetRectPx.Bottom);
        var minProbeHeight = Math.Min(OcclusionProbeHeightPx, explorerRectPx.Height);
        if (bottom - top < Math.Min(16, minProbeHeight))
        {
            var centerY = Math.Clamp(targetRectPx.CenterY, explorerRectPx.Top, explorerRectPx.Bottom);
            top = centerY - (minProbeHeight / 2);
            bottom = top + minProbeHeight;
            if (top < explorerRectPx.Top)
            {
                top = explorerRectPx.Top;
                bottom = top + minProbeHeight;
            }

            if (bottom > explorerRectPx.Bottom)
            {
                bottom = explorerRectPx.Bottom;
                top = bottom - minProbeHeight;
            }
        }

        return new NativeRectPx(left, top, right, bottom);
    }

    private static int GetSampleCoordinate(int start, int end, int index, int count)
    {
        if (end <= start)
        {
            return start;
        }

        if (count <= 1)
        {
            return start + ((end - start) / 2);
        }

        var min = start + 1;
        var max = end - 2;
        if (max < min)
        {
            return start + ((end - start) / 2);
        }

        var ratio = index / (double)(count - 1);
        return min + (int)Math.Round((max - min) * ratio);
    }

    private static bool IsExplorerSampleVisible(IntPtr explorerHwnd, IntPtr hostWindowHwnd, int x, int y)
    {
        var hwnd = GetTopWindow(IntPtr.Zero);
        while (hwnd != IntPtr.Zero)
        {
            if (!IsSameWindowTree(hwnd, hostWindowHwnd) &&
                IsPotentialOccludingWindow(hwnd) &&
                TryGetWindowBoundsPx(hwnd, out var rectPx) &&
                ContainsPoint(rectPx, x, y))
            {
                return IsSameWindowTree(hwnd, explorerHwnd);
            }

            hwnd = GetWindow(hwnd, GW_HWNDNEXT);
        }

        return false;
    }

    private static bool IsPotentialOccludingWindow(IntPtr hwnd)
    {
        return hwnd != IntPtr.Zero &&
               IsWindowVisible(hwnd) &&
               !IsIconic(hwnd) &&
               !IsWindowCloaked(hwnd);
    }

    private static bool ContainsPoint(NativeRectPx rectPx, int x, int y)
    {
        return x >= rectPx.Left && x < rectPx.Right && y >= rectPx.Top && y < rectPx.Bottom;
    }

    private IReadOnlyList<ExplorerWindowSnapshot> GetExplorerWindows()
    {
        if (_explorerCacheExpiresUtc > DateTime.UtcNow && _explorerCache.Count > 0)
        {
            return _explorerCache;
        }

        _explorerCache.Clear();
        _cachedExplorerHwnds.Clear();
        var foregroundWindow = GetForegroundWindow();

        EnumWindows(
            (hwnd, _) =>
            {
                if (TryGetExplorerSnapshot(hwnd, foregroundWindow, out var snapshot))
                {
                    _explorerCache.Add(snapshot);
                    _cachedExplorerHwnds.Add(hwnd);
                }

                return true;
            },
            IntPtr.Zero);

        _explorerCacheExpiresUtc = DateTime.UtcNow.AddMilliseconds(ExplorerCacheMilliseconds);
        return _explorerCache;
    }

    private bool TryGetExplorerSnapshot(IntPtr hwnd, IntPtr foregroundWindow, out ExplorerWindowSnapshot snapshot)
    {
        snapshot = default;
        if (!TryGetEligibleExplorerRect(hwnd, out var rectPx))
        {
            return false;
        }

        snapshot = new ExplorerWindowSnapshot(hwnd, rectPx, hwnd == foregroundWindow);
        return true;
    }

    private bool TryGetEligibleExplorerRect(IntPtr hwnd, out NativeRectPx rectPx)
    {
        rectPx = default;
        if (hwnd == IntPtr.Zero || !IsExplorerTopLevelWindow(hwnd))
        {
            return false;
        }

        if (!TryGetWindowBoundsPx(hwnd, out rectPx))
        {
            return false;
        }

        return rectPx.Width > 0 && rectPx.Height > 0;
    }

    private static bool TryGetTrackedExplorerRect(IntPtr hwnd, out NativeRectPx rectPx)
    {
        rectPx = default;
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd) || IsWindowCloaked(hwnd))
        {
            return false;
        }

        return TryGetWindowBoundsPx(hwnd, out rectPx);
    }

    private bool IsExplorerTopLevelWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd))
        {
            return false;
        }

        if (IsWindowCloaked(hwnd))
        {
            return false;
        }

        if (!TryGetWindowClassName(hwnd, out var className))
        {
            return false;
        }

        if (!string.Equals(className, "CabinetWClass", StringComparison.Ordinal) &&
            !string.Equals(className, "ExploreWClass", StringComparison.Ordinal))
        {
            return false;
        }

        return IsExplorerProcess(hwnd);
    }

    private static bool TryGetWindowBoundsPx(IntPtr hwnd, out NativeRectPx rectPx)
    {
        rectPx = default;
        var nativeRect = default(RECT);
        var result = DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out nativeRect, Marshal.SizeOf<RECT>());
        if (result != 0 && !GetWindowRect(hwnd, out nativeRect))
        {
            return false;
        }

        rectPx = new NativeRectPx(nativeRect.Left, nativeRect.Top, nativeRect.Right, nativeRect.Bottom);
        return rectPx.Width > 0 && rectPx.Height > 0;
    }

    private static bool IsWindowCloaked(IntPtr hwnd)
    {
        var cloaked = 0;
        var result = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, sizeof(int));
        return result == 0 && cloaked != 0;
    }

    private static bool IsExplorerProcess(IntPtr hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return string.Equals(process.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetWindowClassName(IntPtr hwnd, out string className)
    {
        className = string.Empty;
        var builder = new StringBuilder(256);
        var length = GetClassName(hwnd, builder, builder.Capacity);
        if (length <= 0)
        {
            return false;
        }

        className = builder.ToString();
        return !string.IsNullOrWhiteSpace(className);
    }

    private static NativeRectPx CreateRect(PixelPoint topLeftPx, PixelSize sizePx)
    {
        return new NativeRectPx(
            topLeftPx.X,
            topLeftPx.Y,
            topLeftPx.X + sizePx.Width,
            topLeftPx.Y + sizePx.Height);
    }

    private static PixelPoint ComputeTargetPosition(
        NativeRectPx explorerRectPx,
        NativeRectPx appRectPx,
        ExplorerSnapSide side,
        ExplorerSnapPlacement placement,
        bool useVerticalCenter)
    {
        var left = ComputeTargetLeft(explorerRectPx, appRectPx.Width, side, placement, SnapGapPx);
        var top = useVerticalCenter
            ? explorerRectPx.CenterY - (appRectPx.Height / 2)
            : appRectPx.Top;
        return new PixelPoint(left, top);
    }

    private static PixelPoint ComputeTargetPosition(
        NativeRectPx explorerRectPx,
        PixelSize hostWindowSizePx,
        SnapSession session)
    {
        var left = ComputeTargetLeft(
            explorerRectPx,
            hostWindowSizePx.Width,
            session.Side,
            session.Placement,
            session.GapPx);
        var top = session.UseVerticalCenter
            ? explorerRectPx.CenterY - (hostWindowSizePx.Height / 2)
            : explorerRectPx.Top + session.RelativeTopOffsetPx;
        return new PixelPoint(left, top);
    }

    private static int ComputeTargetLeft(
        NativeRectPx explorerRectPx,
        int appWidthPx,
        ExplorerSnapSide side,
        ExplorerSnapPlacement placement,
        int gapPx)
    {
        return (side, placement) switch
        {
            (ExplorerSnapSide.Left, ExplorerSnapPlacement.Outside) => explorerRectPx.Left - gapPx - appWidthPx,
            (ExplorerSnapSide.Right, ExplorerSnapPlacement.Outside) => explorerRectPx.Right + gapPx,
            (ExplorerSnapSide.Left, ExplorerSnapPlacement.Inside) => explorerRectPx.Left + gapPx,
            (ExplorerSnapSide.Right, ExplorerSnapPlacement.Inside) => explorerRectPx.Right - gapPx - appWidthPx,
            _ => explorerRectPx.Left
        };
    }

    private static Vector ToVector(PixelPoint pixelPoint) => new(pixelPoint.X, pixelPoint.Y);

    private void ApplyHostWindowPositionFromAnimator()
    {
        ApplyHostWindowPosition(_springAnimator.Position);
    }

    private void ApplyHostWindowPosition(Vector position)
    {
        if (_hostWindow == null)
        {
            return;
        }

        var roundedPosition = new PixelPoint((int)Math.Round(position.X), (int)Math.Round(position.Y));
        if (_lastAppliedHostPositionPx == roundedPosition)
        {
            return;
        }

        if (_hostWindow.Position != roundedPosition)
        {
            _hostWindow.Position = roundedPosition;
        }

        _lastAppliedHostPositionPx = roundedPosition;
    }

    private void RaiseActivityChangedIfNeeded()
    {
        var isSnapped = _session != null;
        var isAnimating = isSnapped && _springAnimator.IsActive;
        if (isSnapped == _lastReportedIsSnapped && isAnimating == _lastReportedIsAnimating)
        {
            return;
        }

        _lastReportedIsSnapped = isSnapped;
        _lastReportedIsAnimating = isAnimating;
        ActivityChanged?.Invoke(this, new ExplorerSnapActivityChangedEventArgs(isSnapped, isAnimating));
    }

    private readonly record struct SnapSession(
        IntPtr ExplorerHwnd,
        ExplorerSnapSide Side,
        ExplorerSnapPlacement Placement,
        int GapPx,
        bool UseVerticalCenter,
        int RelativeTopOffsetPx);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetTopWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out int pvAttribute, int cbAttribute);
}
