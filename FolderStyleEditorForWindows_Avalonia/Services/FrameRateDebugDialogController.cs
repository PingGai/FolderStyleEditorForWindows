using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Threading;
using FolderStyleEditorForWindows.ViewModels;

namespace FolderStyleEditorForWindows.Services
{
    public sealed class FrameRateDebugDialogController : IDisposable
    {
        private enum DebugTabKind
        {
            Error,
            FrameRate
        }

        private readonly FrameRateSettings _settings;
        private readonly PerformanceTelemetryService _telemetry;
        private readonly IToastService _toastService;
        private readonly InterruptDialogState _state;
        private readonly LocalizationManager _loc;
        private readonly SolidColorBrush _savedBrush = new(Color.Parse("#4F7A57"));
        private readonly SolidColorBrush _dirtyBrush = new(Color.Parse("#B26F1A"));
        private readonly SolidColorBrush _invalidBrush = new(Color.Parse("#D46258"));
        private readonly SolidColorBrush _neutralBrush = new(Color.Parse("#303034"));
        private readonly List<DialogNumberFieldItem> _numberFields = new();
        private readonly IReadOnlyList<DialogTabItem> _tabs;
        private readonly ObservableCollection<DialogFormSectionItem> _frameRateSections;
        private readonly DialogCodeBlockItem _debugCodeBlock;
        private readonly RelayCommand _showErrorTabCommand;
        private readonly RelayCommand _showFrameRateTabCommand;
        private readonly DialogNumberFieldItem _staticRefreshField;
        private readonly DialogNumberFieldItem _backgroundAmbientField;
        private readonly DialogNumberFieldItem _homeTitleAmbientField;
        private readonly DialogNumberFieldItem _adminTitleAmbientField;
        private readonly DialogNumberFieldItem _activeInteractionField;
        private readonly DialogToggleFieldItem _followDisplayRefreshField;
        private readonly DialogNumberFieldItem _manualMaxField;
        private readonly DialogNumberFieldItem _hoverCooldownField;
        private readonly DialogNumberFieldItem _scrollCooldownField;
        private readonly DialogNumberFieldItem _dragCooldownField;
        private readonly DialogToggleFieldItem _showPerformanceMonitorField;
        private readonly DialogToggleFieldItem _showDetailedPerformanceMonitorField;
        private readonly DialogToggleFieldItem _showComponentFpsBadgesField;
        private readonly DialogToggleFieldItem _enableComponentExcludeModeField;
        private readonly DialogToggleFieldItem _excludePinGlowField;
        private readonly DialogToggleFieldItem _excludeBottomActionButtonsField;
        private readonly DialogToggleFieldItem _excludeActualTopmostField;
        private readonly DialogToggleFieldItem _disableEditScrollAnimationsField;
        private readonly DialogStatusFieldItem _displayRefreshRateStatus;
        private readonly DialogStatusFieldItem _foregroundTargetStatus;
        private readonly DialogStatusFieldItem _ambientTargetStatus;
        private readonly DialogStatusFieldItem _renderModeStatus;
        private readonly DialogStatusFieldItem _saveStateStatus;
#if DEBUG
        private readonly DialogToggleFieldItem _pauseAnimationsField;
        private readonly DialogStatusFieldItem _managedMemoryStatus;
        private readonly DialogStatusFieldItem _privateMemoryStatus;
        private readonly DialogStatusFieldItem _workingSetStatus;
        private readonly DialogStatusFieldItem _gcCollectionsStatus;
        private readonly DialogActionFieldItem _memoryTrimAction;
        private readonly DispatcherTimer _debugMemoryTimer;
#endif
        private bool _disposed;
        private DebugTabKind _selectedTab = DebugTabKind.FrameRate;

#if DEBUG
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);
#endif

        public FrameRateDebugDialogController(
            FrameRateSettings settings,
            PerformanceTelemetryService telemetry,
            IToastService toastService,
            InterruptDialogState state)
        {
            _settings = settings;
            _telemetry = telemetry;
            _toastService = toastService;
            _state = state;
            _loc = LocalizationManager.Instance;

            SaveCommand = new RelayCommand(SaveToConfig, () => !HasValidationErrors);
            RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
            _showErrorTabCommand = new RelayCommand(() => SelectTab(DebugTabKind.Error));
            _showFrameRateTabCommand = new RelayCommand(() => SelectTab(DebugTabKind.FrameRate));
            _tabs = new[]
            {
                new DialogTabItem(_loc["Dialog_Debug_Tab_Error"], _showErrorTabCommand),
                new DialogTabItem(_loc["Dialog_Debug_Tab_FrameRate"], _showFrameRateTabCommand, isSelected: true)
            };

            _staticRefreshField = CreateNumberField(
                _loc["Dialog_FrameRate_Field_StaticRefresh"],
                _loc["Dialog_FrameRate_Field_StaticRefresh_Desc"],
                _settings.StaticContentRefreshFps,
                0,
                240,
                value => _settings.StaticContentRefreshFps = value);
            _backgroundAmbientField = CreateNumberField(
                _loc["Dialog_FrameRate_Field_BackgroundAmbient"],
                _loc["Dialog_FrameRate_Field_BackgroundAmbient_Desc"],
                _settings.BackgroundAmbientFps,
                1,
                120,
                value => _settings.BackgroundAmbientFps = value);
            _homeTitleAmbientField = CreateNumberField(
                _loc["Dialog_FrameRate_Field_HomeTitleAmbient"],
                _loc["Dialog_FrameRate_Field_HomeTitleAmbient_Desc"],
                _settings.HomeTitleAmbientFps,
                1,
                120,
                value => _settings.HomeTitleAmbientFps = value);
            _adminTitleAmbientField = CreateNumberField(
                _loc["Dialog_FrameRate_Field_AdminTitleAmbient"],
                _loc["Dialog_FrameRate_Field_AdminTitleAmbient_Desc"],
                _settings.AdminTitleAmbientFps,
                1,
                120,
                value => _settings.AdminTitleAmbientFps = value);
            _activeInteractionField = CreateNumberField(
                _loc["Dialog_FrameRate_Field_ActiveInteraction"],
                _loc["Dialog_FrameRate_Field_ActiveInteraction_Desc"],
                _settings.ActiveInteractionFps,
                1,
                240,
                value => _settings.ActiveInteractionFps = value);
            _followDisplayRefreshField = new DialogToggleFieldItem(
                _loc["Dialog_FrameRate_Field_FollowDisplayRefresh"],
                _loc["Dialog_FrameRate_Field_FollowDisplayRefresh_Desc"],
                _settings.UseDisplayRefreshRateAsMaxFps,
                value => _settings.UseDisplayRefreshRateAsMaxFps = value);
            _manualMaxField = CreateNumberField(
                _loc["Dialog_FrameRate_Field_ManualMax"],
                _loc["Dialog_FrameRate_Field_ManualMax_Desc"],
                _settings.ManualMaxFps,
                1,
                500,
                value => _settings.ManualMaxFps = value);
            _hoverCooldownField = CreateNumberField(
                _loc["Dialog_FrameRate_Field_HoverCooldown"],
                _loc["Dialog_FrameRate_Field_HoverCooldown_Desc"],
                _settings.HoverCooldownMs,
                0,
                5000,
                value => _settings.HoverCooldownMs = value);
            _scrollCooldownField = CreateNumberField(
                _loc["Dialog_FrameRate_Field_ScrollCooldown"],
                _loc["Dialog_FrameRate_Field_ScrollCooldown_Desc"],
                _settings.ScrollCooldownMs,
                0,
                5000,
                value => _settings.ScrollCooldownMs = value);
            _dragCooldownField = CreateNumberField(
                _loc["Dialog_FrameRate_Field_DragCooldown"],
                _loc["Dialog_FrameRate_Field_DragCooldown_Desc"],
                _settings.DragCooldownMs,
                0,
                5000,
                value => _settings.DragCooldownMs = value);
            _showPerformanceMonitorField = new DialogToggleFieldItem(
                _loc["Dialog_FrameRate_Field_ShowPerformanceMonitor"],
                _loc["Dialog_FrameRate_Field_ShowPerformanceMonitor_Desc"],
                _settings.ShowPerformanceMonitor,
                value => _settings.ShowPerformanceMonitor = value);
            _showDetailedPerformanceMonitorField = new DialogToggleFieldItem(
                _loc["Dialog_FrameRate_Field_ShowDetailedPerformanceMonitor"],
                _loc["Dialog_FrameRate_Field_ShowDetailedPerformanceMonitor_Desc"],
                _settings.ShowDetailedPerformanceMonitor,
                value => _settings.ShowDetailedPerformanceMonitor = value);
            _showComponentFpsBadgesField = new DialogToggleFieldItem(
                _loc["Dialog_FrameRate_Field_ShowComponentFpsBadges"],
                _loc["Dialog_FrameRate_Field_ShowComponentFpsBadges_Desc"],
                _settings.ShowComponentFpsBadges,
                value => _settings.ShowComponentFpsBadges = value);
            _enableComponentExcludeModeField = new DialogToggleFieldItem(
                _loc["Dialog_FrameRate_Field_EnableComponentExcludeMode"],
                _loc["Dialog_FrameRate_Field_EnableComponentExcludeMode_Desc"],
                _settings.EnableComponentExcludeMode,
                value => _settings.EnableComponentExcludeMode = value);
            _excludePinGlowField = new DialogToggleFieldItem(
                _loc["Dialog_FrameRate_Field_ExcludePinGlow"],
                _loc["Dialog_FrameRate_Field_ExcludePinGlow_Desc"],
                _settings.ExcludePinGlow,
                value => _settings.ExcludePinGlow = value);
            _excludeBottomActionButtonsField = new DialogToggleFieldItem(
                _loc["Dialog_FrameRate_Field_ExcludeBottomActionButtons"],
                _loc["Dialog_FrameRate_Field_ExcludeBottomActionButtons_Desc"],
                _settings.ExcludeBottomActionButtons,
                value => _settings.ExcludeBottomActionButtons = value);
            _excludeActualTopmostField = new DialogToggleFieldItem(
                _loc["Dialog_FrameRate_Field_ExcludeActualTopmost"],
                _loc["Dialog_FrameRate_Field_ExcludeActualTopmost_Desc"],
                _settings.ExcludeActualTopmost,
                value => _settings.ExcludeActualTopmost = value);
            _disableEditScrollAnimationsField = new DialogToggleFieldItem(
                _loc["Dialog_FrameRate_Field_DisableEditScrollAnimations"],
                _loc["Dialog_FrameRate_Field_DisableEditScrollAnimations_Desc"],
                _settings.DisableEditScrollAnimations,
                value => _settings.DisableEditScrollAnimations = value);

            _displayRefreshRateStatus = CreateStatusField(
                _loc["Dialog_FrameRate_Status_DisplayRefreshRate"],
                _loc["Dialog_FrameRate_Status_DisplayRefreshRate_Desc"]);
            _foregroundTargetStatus = CreateStatusField(
                _loc["Dialog_FrameRate_Status_ForegroundTarget"],
                _loc["Dialog_FrameRate_Status_ForegroundTarget_Desc"]);
            _ambientTargetStatus = CreateStatusField(
                _loc["Dialog_FrameRate_Status_AmbientTarget"],
                _loc["Dialog_FrameRate_Status_AmbientTarget_Desc"]);
            _renderModeStatus = CreateStatusField(
                _loc["Dialog_FrameRate_Status_RenderMode"],
                _loc["Dialog_FrameRate_Status_RenderMode_Desc"]);
            _saveStateStatus = CreateStatusField(
                _loc["Dialog_FrameRate_Status_SaveState"],
                _loc["Dialog_FrameRate_Status_SaveState_Desc"]);

#if DEBUG
            _pauseAnimationsField = new DialogToggleFieldItem(
                _loc["Dialog_FrameRate_Field_PauseAnimations"],
                _loc["Dialog_FrameRate_Field_PauseAnimations_Desc"],
                DebugRuntimeAnalysis.PauseAnimations,
                value => DebugRuntimeAnalysis.PauseAnimations = value);
            _managedMemoryStatus = CreateStatusField(
                _loc["Dialog_DebugMemory_Status_ManagedHeap"],
                _loc["Dialog_DebugMemory_Status_ManagedHeap_Desc"]);
            _privateMemoryStatus = CreateStatusField(
                _loc["Dialog_DebugMemory_Status_PrivateMemory"],
                _loc["Dialog_DebugMemory_Status_PrivateMemory_Desc"]);
            _workingSetStatus = CreateStatusField(
                _loc["Dialog_DebugMemory_Status_WorkingSet"],
                _loc["Dialog_DebugMemory_Status_WorkingSet_Desc"]);
            _gcCollectionsStatus = CreateStatusField(
                _loc["Dialog_DebugMemory_Status_GcCollections"],
                _loc["Dialog_DebugMemory_Status_GcCollections_Desc"]);
            _memoryTrimAction = new DialogActionFieldItem(
                _loc["Dialog_DebugMemory_Action_Trim"],
                _loc["Dialog_DebugMemory_Action_Trim_Desc"],
                _loc["Dialog_DebugMemory_Action_Trim_Button"],
                new RelayCommand(ForceMemoryTrim),
                new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                _neutralBrush,
                new SolidColorBrush(Color.Parse("#EEAAAAAA")));
#endif

            _frameRateSections = new ObservableCollection<DialogFormSectionItem>
            {
                new(
                    _loc["Dialog_FrameRate_Section_Tiers"],
                    new DialogFormFieldItem[]
                    {
                        _staticRefreshField,
                        _backgroundAmbientField,
                        _homeTitleAmbientField,
                        _adminTitleAmbientField,
                        _activeInteractionField,
                        _followDisplayRefreshField,
                        _manualMaxField
                    }),
                new(
                    _loc["Dialog_FrameRate_Section_Cooldowns"],
                    new DialogFormFieldItem[]
                    {
                        _hoverCooldownField,
                        _scrollCooldownField,
                        _dragCooldownField
                    }),
                new(
                    _loc["Dialog_FrameRate_Section_DebugDisplay"],
                    new DialogFormFieldItem[]
                    {
                        _showPerformanceMonitorField,
                        _showDetailedPerformanceMonitorField,
                        _showComponentFpsBadgesField,
                        new DialogActionFieldItem(
                            _loc["Dialog_FrameRate_Action_RestoreDefaults"],
                            _loc["Dialog_FrameRate_Action_RestoreDefaults_Desc"],
                            _loc["Dialog_FrameRate_Action_RestoreDefaults_Button"],
                            RestoreDefaultsCommand,
                            new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                            _neutralBrush,
                            new SolidColorBrush(Color.Parse("#EEAAAAAA")))
                    }),
                new(
                    _loc["Dialog_FrameRate_Section_Exclusions"],
                    new DialogFormFieldItem[]
                    {
                        _enableComponentExcludeModeField,
                        _excludePinGlowField,
                        _excludeBottomActionButtonsField,
                        _excludeActualTopmostField,
                        _disableEditScrollAnimationsField
                    }),
                new(
                    _loc["Dialog_FrameRate_Section_Runtime"],
                    new DialogFormFieldItem[]
                    {
                        _displayRefreshRateStatus,
                        _foregroundTargetStatus,
                        _ambientTargetStatus,
                        _renderModeStatus,
                        _saveStateStatus
                    })
            };

#if DEBUG
            _frameRateSections.Add(
                new DialogFormSectionItem(
                    _loc["Dialog_DebugMemory_Section_Title"],
                    new DialogFormFieldItem[]
                    {
                        _pauseAnimationsField,
                        _managedMemoryStatus,
                        _privateMemoryStatus,
                        _workingSetStatus,
                        _gcCollectionsStatus,
                        _memoryTrimAction
                    },
                    _loc["Dialog_DebugMemory_Section_Desc"]));

            _debugMemoryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _debugMemoryTimer.Tick += DebugMemoryTimer_Tick;
            _debugMemoryTimer.Start();
#endif

            _debugCodeBlock = new DialogCodeBlockItem(
                _loc["Dialog_Debug_TestError_Details"],
                Array.Empty<DialogContextMenuItem>());

            _settings.PropertyChanged += Settings_PropertyChanged;
            _telemetry.PropertyChanged += Telemetry_PropertyChanged;
            SyncInputsFromSettings();
            UpdateRuntimeStatus();
            RaiseSaveCommandState();
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand RestoreDefaultsCommand { get; }

        private bool HasValidationErrors => _numberFields.Exists(field => field.HasError);

        public InterruptDialogOptions BuildOptions()
        {
            SelectTab(DebugTabKind.FrameRate, updateState: false);
            return new InterruptDialogOptions
            {
                Title = _loc["Dialog_Debug_Title"],
                Content = _loc["Dialog_FrameRate_Content"],
                PrimaryButtonText = _loc["Dialog_FrameRate_Primary_Save"],
                SecondaryButtonText = _loc["Dialog_FrameRate_Secondary_Close"],
                WidthRatio = 1.25,
                PrimaryButtonCommand = SaveCommand,
                FormSections = _frameRateSections,
                Tabs = _tabs
            };
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _settings.PropertyChanged -= Settings_PropertyChanged;
            _telemetry.PropertyChanged -= Telemetry_PropertyChanged;
#if DEBUG
            _debugMemoryTimer.Stop();
            _debugMemoryTimer.Tick -= DebugMemoryTimer_Tick;
#endif
            foreach (var field in _numberFields)
            {
                field.ValidationStateChanged -= NumberField_ValidationStateChanged;
            }
        }

        private void SelectTab(DebugTabKind tab, bool updateState = true)
        {
            _selectedTab = tab;
            for (var i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].IsSelected = (i == (int)tab);
            }

            if (!updateState)
            {
                return;
            }

            ApplyTabToState();
        }

        private void ApplyTabToState()
        {
            _state.Title = _loc["Dialog_Debug_Title"];
            _state.TitleForeground = new SolidColorBrush(Color.Parse("#303034"));
            _state.HeaderMeta = null;
            _state.CenterIconPath = null;
            _state.SubText = null;
            _state.EmphasisText = null;
            _state.CodeBlock = null;
            _state.ActionLinks = new ObservableCollection<DialogActionLinkItem>();
            _state.ExpandableSections = new ObservableCollection<DialogExpandableSectionItem>();

            switch (_selectedTab)
            {
                case DebugTabKind.Error:
                    _state.SectionTitle = _loc["Dialog_Debug_Tab_Error"];
                    _state.Content = _loc["Dialog_Debug_TestError_Content"];
                    _state.CodeBlock = _debugCodeBlock;
                    _state.FormSections = new ObservableCollection<DialogFormSectionItem>();
                    _state.ShowPrimaryButton = false;
                    _state.PrimaryButtonText = string.Empty;
                    _state.SecondaryButtonText = _loc["Dialog_FrameRate_Secondary_Close"];
                    _state.ShowSecondaryButton = true;
                    _state.PrimaryActionCommand = _state.ConfirmCommand;
                    _state.SecondaryActionCommand = _state.CancelCommand;
                    break;
                case DebugTabKind.FrameRate:
                default:
                    _state.SectionTitle = _loc["Dialog_Debug_Tab_FrameRate"];
                    _state.Content = _loc["Dialog_FrameRate_Content"];
                    _state.FormSections = new ObservableCollection<DialogFormSectionItem>(_frameRateSections);
                    _state.ShowPrimaryButton = true;
                    _state.PrimaryButtonText = _loc["Dialog_FrameRate_Primary_Save"];
                    _state.SecondaryButtonText = _loc["Dialog_FrameRate_Secondary_Close"];
                    _state.ShowSecondaryButton = true;
                    _state.PrimaryActionCommand = SaveCommand;
                    _state.SecondaryActionCommand = _state.CancelCommand;
                    break;
            }
        }

        private void SaveToConfig()
        {
            if (HasValidationErrors)
            {
                UpdateRuntimeStatus();
                RaiseSaveCommandState();
                return;
            }

            ConfigManager.Config.FrameRate = _settings.Export();
            ConfigManager.SaveConfig();
            _settings.MarkSaved();
            UpdateRuntimeStatus();
            _toastService.Show(_loc["Dialog_FrameRate_SaveSuccess"], new SolidColorBrush(Color.Parse("#EBB762")));
        }

        private void RestoreDefaults()
        {
            _settings.ApplyDefaults();
            SyncInputsFromSettings();
            UpdateRuntimeStatus();
            RaiseSaveCommandState();
        }

        private DialogNumberFieldItem CreateNumberField(
            string label,
            string description,
            int initialValue,
            int minValue,
            int maxValue,
            Action<int> applyValue)
        {
            var field = new DialogNumberFieldItem(
                label,
                description,
                initialValue,
                minValue,
                maxValue,
                _loc["Dialog_FrameRate_Validation_IntegerRange"],
                applyValue);
            field.ValidationStateChanged += NumberField_ValidationStateChanged;
            _numberFields.Add(field);
            return field;
        }

        private DialogStatusFieldItem CreateStatusField(string label, string description)
        {
            return new DialogStatusFieldItem(label, description, string.Empty, _neutralBrush);
        }

        private void NumberField_ValidationStateChanged(object? sender, EventArgs e)
        {
            UpdateRuntimeStatus();
            RaiseSaveCommandState();
        }

        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == string.Empty)
            {
                SyncInputsFromSettings();
                UpdateRuntimeStatus();
                RaiseSaveCommandState();
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(FrameRateSettings.ShowPerformanceMonitor):
                case nameof(FrameRateSettings.ShowDetailedPerformanceMonitor):
                case nameof(FrameRateSettings.ShowComponentFpsBadges):
                case nameof(FrameRateSettings.EnableComponentExcludeMode):
                case nameof(FrameRateSettings.ExcludePinGlow):
                case nameof(FrameRateSettings.ExcludeBottomActionButtons):
                case nameof(FrameRateSettings.ExcludeActualTopmost):
                case nameof(FrameRateSettings.DisableEditScrollAnimations):
                    SyncInputsFromSettings();
                    UpdateRuntimeStatus();
                    RaiseSaveCommandState();
                    break;
                case nameof(FrameRateSettings.DisplayRefreshRateHz):
                case nameof(FrameRateSettings.CurrentForegroundTargetFps):
                case nameof(FrameRateSettings.CurrentAmbientTargetFps):
                case nameof(FrameRateSettings.UseDisplayRefreshRateAsMaxFps):
                case nameof(FrameRateSettings.ManualMaxFps):
                case nameof(FrameRateSettings.IsDirty):
                    UpdateRuntimeStatus();
                    RaiseSaveCommandState();
                    break;
            }
        }

        private void Telemetry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PerformanceTelemetryService.RenderMode))
            {
                UpdateRuntimeStatus();
            }
        }

        private void SyncInputsFromSettings()
        {
            _staticRefreshField.SyncFromValue(_settings.StaticContentRefreshFps);
            _backgroundAmbientField.SyncFromValue(_settings.BackgroundAmbientFps);
            _homeTitleAmbientField.SyncFromValue(_settings.HomeTitleAmbientFps);
            _adminTitleAmbientField.SyncFromValue(_settings.AdminTitleAmbientFps);
            _activeInteractionField.SyncFromValue(_settings.ActiveInteractionFps);
            _followDisplayRefreshField.SyncFromValue(_settings.UseDisplayRefreshRateAsMaxFps);
            _manualMaxField.SyncFromValue(_settings.ManualMaxFps);
            _hoverCooldownField.SyncFromValue(_settings.HoverCooldownMs);
            _scrollCooldownField.SyncFromValue(_settings.ScrollCooldownMs);
            _dragCooldownField.SyncFromValue(_settings.DragCooldownMs);
            _showPerformanceMonitorField.SyncFromValue(_settings.ShowPerformanceMonitor);
            _showDetailedPerformanceMonitorField.SyncFromValue(_settings.ShowDetailedPerformanceMonitor);
            _showComponentFpsBadgesField.SyncFromValue(_settings.ShowComponentFpsBadges);
            _enableComponentExcludeModeField.SyncFromValue(_settings.EnableComponentExcludeMode);
            _excludePinGlowField.SyncFromValue(_settings.ExcludePinGlow);
            _excludeBottomActionButtonsField.SyncFromValue(_settings.ExcludeBottomActionButtons);
            _excludeActualTopmostField.SyncFromValue(_settings.ExcludeActualTopmost);
            _disableEditScrollAnimationsField.SyncFromValue(_settings.DisableEditScrollAnimations);
#if DEBUG
            _pauseAnimationsField.SyncFromValue(DebugRuntimeAnalysis.PauseAnimations);
#endif
        }

        private void UpdateRuntimeStatus()
        {
            _displayRefreshRateStatus.Value = FormatHz(_settings.DisplayRefreshRateHz);
            _foregroundTargetStatus.Value = FormatFps(_settings.CurrentForegroundTargetFps);
            _ambientTargetStatus.Value = FormatFps(_settings.CurrentAmbientTargetFps);
            _renderModeStatus.Value = _telemetry.RenderMode;

            if (HasValidationErrors)
            {
                _saveStateStatus.Value = _loc["Dialog_FrameRate_Status_SaveState_Invalid"];
                _saveStateStatus.ValueForeground = _invalidBrush;
                return;
            }

            if (_settings.IsDirty)
            {
                _saveStateStatus.Value = _loc["Dialog_FrameRate_Status_SaveState_Unsaved"];
                _saveStateStatus.ValueForeground = _dirtyBrush;
                return;
            }

            _saveStateStatus.Value = _loc["Dialog_FrameRate_Status_SaveState_Saved"];
            _saveStateStatus.ValueForeground = _savedBrush;
#if DEBUG
            UpdateDebugMemoryStatus();
#endif
        }

        private void RaiseSaveCommandState()
        {
            SaveCommand.RaiseCanExecuteChanged();
        }

        private string FormatHz(int value)
        {
            return string.Format(_loc["Dialog_FrameRate_Value_Hz"], value);
        }

        private string FormatFps(int value)
        {
            return string.Format(_loc["Dialog_FrameRate_Value_Fps"], value);
        }

#if DEBUG
        private void DebugMemoryTimer_Tick(object? sender, EventArgs e)
        {
            UpdateDebugMemoryStatus();
        }

        private void UpdateDebugMemoryStatus()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                process.Refresh();
                var managedMb = GC.GetTotalMemory(false) / 1024d / 1024d;
                var privateMb = process.PrivateMemorySize64 / 1024d / 1024d;
                var workingSetMb = process.WorkingSet64 / 1024d / 1024d;
                var gen0 = GC.CollectionCount(0);
                var gen1 = GC.CollectionCount(1);
                var gen2 = GC.CollectionCount(2);

                _managedMemoryStatus.Value = FormatMegabytes(managedMb);
                _privateMemoryStatus.Value = FormatMegabytes(privateMb);
                _workingSetStatus.Value = FormatMegabytes(workingSetMb);
                _gcCollectionsStatus.Value = $"Gen0 {gen0} / Gen1 {gen1} / Gen2 {gen2}";
            }
            catch
            {
                _managedMemoryStatus.Value = "-";
                _privateMemoryStatus.Value = "-";
                _workingSetStatus.Value = "-";
                _gcCollectionsStatus.Value = "-";
            }
        }

        private void ForceMemoryTrim()
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

                    if (OperatingSystem.IsWindows())
                    {
                        using var process = Process.GetCurrentProcess();
                        EmptyWorkingSet(process.Handle);
                    }
                }
                catch
                {
                    // 调试专用，失败时静默处理。
                }

                UpdateDebugMemoryStatus();
            }, DispatcherPriority.Background);
        }

        private string FormatMegabytes(double value)
        {
            return string.Format(_loc["Dialog_DebugMemory_Value_Mb"], value.ToString("F1"));
        }
#endif
    }
}
