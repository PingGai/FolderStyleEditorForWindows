using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace FolderStyleEditorForWindows.Services;

[SupportedOSPlatform("windows")]
public sealed class ExplorerHiddenVisibilityService
{
    private const string ExplorerAdvancedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string HiddenValueName = "Hidden";
    private const string ShowSuperHiddenValueName = "ShowSuperHidden";
    private const int HiddenDisabledValue = 2;
    private const int HiddenEnabledValue = 1;
    private const int SuperHiddenDisabledValue = 0;
    private const int SuperHiddenEnabledValue = 1;

    private static readonly IntPtr HwndBroadcast = new(0xffff);
    private const uint WmSettingChange = 0x001A;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint ShcneAssocChanged = 0x08000000;
    private const uint ShcnfIdList = 0x0000;

    public Task<ExplorerHiddenVisibilityLevel> GetCurrentLevelAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedPath, writable: false)
                ?? throw new InvalidOperationException("Explorer advanced registry key is unavailable.");

            var hidden = ReadIntValue(key, HiddenValueName, HiddenDisabledValue);
            var showSuperHidden = ReadIntValue(key, ShowSuperHiddenValueName, SuperHiddenDisabledValue);
            return MapFromRegistry(hidden, showSuperHidden);
        }, cancellationToken);
    }

    public Task ApplyLevelAsync(ExplorerHiddenVisibilityLevel level, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedPath, writable: true)
                ?? throw new InvalidOperationException("Explorer advanced registry key is unavailable.");

            var (hidden, showSuperHidden) = MapToRegistry(level);
            key.SetValue(HiddenValueName, hidden, RegistryValueKind.DWord);
            key.SetValue(ShowSuperHiddenValueName, showSuperHidden, RegistryValueKind.DWord);
            key.Flush();

            NotifyExplorerSettingsChanged();
        }, cancellationToken);
    }

    private static ExplorerHiddenVisibilityLevel MapFromRegistry(int hidden, int showSuperHidden)
    {
        if (hidden == HiddenEnabledValue)
        {
            return showSuperHidden == SuperHiddenEnabledValue
                ? ExplorerHiddenVisibilityLevel.ShowSystemHidden
                : ExplorerHiddenVisibilityLevel.ShowHidden;
        }

        return ExplorerHiddenVisibilityLevel.HideAll;
    }

    private static (int Hidden, int ShowSuperHidden) MapToRegistry(ExplorerHiddenVisibilityLevel level)
    {
        return level switch
        {
            ExplorerHiddenVisibilityLevel.HideAll => (HiddenDisabledValue, SuperHiddenDisabledValue),
            ExplorerHiddenVisibilityLevel.ShowHidden => (HiddenEnabledValue, SuperHiddenDisabledValue),
            ExplorerHiddenVisibilityLevel.ShowSystemHidden => (HiddenEnabledValue, SuperHiddenEnabledValue),
            _ => (HiddenDisabledValue, SuperHiddenDisabledValue)
        };
    }

    private static int ReadIntValue(RegistryKey key, string valueName, int fallback)
    {
        var raw = key.GetValue(valueName);
        return raw switch
        {
            int intValue => intValue,
            byte byteValue => byteValue,
            _ => fallback
        };
    }

    private static void NotifyExplorerSettingsChanged()
    {
        try
        {
            _ = SendMessageTimeout(
                HwndBroadcast,
                WmSettingChange,
                IntPtr.Zero,
                "ShellState",
                SmtoAbortIfHung,
                250,
                out _);
        }
        catch
        {
            // Best effort notification only.
        }

        try
        {
            SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // Best effort notification only.
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
