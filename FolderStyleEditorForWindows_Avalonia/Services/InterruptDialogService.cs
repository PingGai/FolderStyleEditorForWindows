using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FolderStyleEditorForWindows.ViewModels;
using Newtonsoft.Json;

namespace FolderStyleEditorForWindows.Services
{
    public enum InterruptDialogResult
    {
        None,
        Primary,
        Secondary
    }

    public sealed class InterruptDialogResponse
    {
        public InterruptDialogResult Result { get; init; }
        public bool IsCheckboxChecked { get; init; }
    }

    public enum DialogPrimaryButtonKind
    {
        Normal,
        DangerConfirm
    }

    public sealed class DialogButtonCountdownOptions
    {
        public int Seconds { get; init; }
    }

    public sealed class DialogCheckboxOption : INotifyPropertyChanged
    {
        private bool _isChecked;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DialogCheckboxOption(string text, bool isChecked = false)
        {
            Text = text;
            _isChecked = isChecked;
        }

        public string Text { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                {
                    return;
                }

                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }
    }

    public sealed class DialogContextMenuItem
    {
        public DialogContextMenuItem(string text, ICommand command, bool showSeparatorBefore = false)
        {
            Text = text;
            Command = command;
            ShowSeparatorBefore = showSeparatorBefore;
        }

        public string Text { get; }
        public ICommand Command { get; }
        public bool ShowSeparatorBefore { get; }
    }

    public sealed class DialogCodeBlockItem
    {
        public DialogCodeBlockItem(string content, IEnumerable<DialogContextMenuItem> menuItems)
        {
            Content = content;
            MenuItems = new ObservableCollection<DialogContextMenuItem>(menuItems);
        }

        public string Content { get; }
        public ObservableCollection<DialogContextMenuItem> MenuItems { get; }
    }

    public sealed class DialogActionLinkItem
    {
        public string Text { get; }
        public string IconPath { get; }
        public string Url { get; }
        public ICommand OpenCommand { get; }

        public DialogActionLinkItem(string text, string iconPath, string url)
        {
            Text = text;
            IconPath = iconPath;
            Url = url;
            OpenCommand = new RelayCommand(() => OpenShellTarget(url));
        }

        internal static void OpenShellTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open target: {target}. {ex.Message}");
            }
        }
    }

    public sealed class DialogLicenseItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isTextExpanded;

        public string Title { get; }
        public string? Summary { get; }
        public string? MetaText { get; }
        public string? ProjectUrl { get; }
        public string? LicenseUrl { get; }
        public string? FullText { get; }
        public ICommand ToggleTextCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand OpenLicenseCommand { get; }

        public DialogLicenseItem(string title, string? summary, string? metaText, string? projectUrl, string? licenseUrl, string? fullText)
        {
            Title = title;
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
            MetaText = string.IsNullOrWhiteSpace(metaText) ? null : metaText.Trim();
            ProjectUrl = string.IsNullOrWhiteSpace(projectUrl) ? null : projectUrl.Trim();
            LicenseUrl = string.IsNullOrWhiteSpace(licenseUrl) ? null : licenseUrl.Trim();
            FullText = string.IsNullOrWhiteSpace(fullText) ? null : fullText.Trim();
            ToggleTextCommand = new RelayCommand(() => IsTextExpanded = !IsTextExpanded, () => HasFullText);
            OpenProjectCommand = new RelayCommand(() => DialogActionLinkItem.OpenShellTarget(ProjectUrl ?? string.Empty), () => HasProjectUrl);
            OpenLicenseCommand = new RelayCommand(() => DialogActionLinkItem.OpenShellTarget(LicenseUrl ?? string.Empty), () => HasLicenseUrl);
        }

        public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);
        public bool HasMetaText => !string.IsNullOrWhiteSpace(MetaText);
        public bool HasProjectUrl => !string.IsNullOrWhiteSpace(ProjectUrl);
        public bool HasLicenseUrl => !string.IsNullOrWhiteSpace(LicenseUrl);
        public bool HasFullText => !string.IsNullOrWhiteSpace(FullText);

        public bool IsTextExpanded
        {
            get => _isTextExpanded;
            set
            {
                if (_isTextExpanded == value)
                {
                    return;
                }

                _isTextExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTextExpanded)));
            }
        }
    }

    public sealed class DialogExpandableSectionItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isExpanded;

        public string Title { get; }
        public ObservableCollection<DialogLicenseItem> Items { get; }
        public ICommand ToggleCommand { get; }

        public DialogExpandableSectionItem(string title, IEnumerable<DialogLicenseItem> items, bool isExpanded = false)
        {
            Title = title;
            Items = new ObservableCollection<DialogLicenseItem>(items);
            _isExpanded = isExpanded;
            ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        public bool HasItems => Items.Count > 0;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    public sealed class InterruptDialogOptions
    {
        public string Title { get; init; } = string.Empty;
        public IBrush? TitleForeground { get; init; }
        public string? HeaderMeta { get; init; }
        public string? SectionTitle { get; init; }
        public string Content { get; init; } = string.Empty;
        public TextAlignment ContentTextAlignment { get; init; } = TextAlignment.Left;
        public HorizontalAlignment ContentHorizontalAlignment { get; init; } = HorizontalAlignment.Left;
        public string? CenterIconPath { get; init; }
        public string? SubText { get; init; }
        public IBrush? SubTextForeground { get; init; }
        public string? EmphasisText { get; init; }
        public IBrush? EmphasisForeground { get; init; }
        public string PrimaryButtonText { get; init; } = string.Empty;
        public string? SecondaryButtonText { get; init; }
        public bool ShowPrimaryButton { get; init; } = true;
        public bool ShowSecondaryButton { get; init; } = true;
        public bool DismissOnEsc { get; init; } = true;
        public bool AllowOverlayClickDismiss { get; init; }
        public bool HitTestVisible { get; init; } = true;
        public DialogPrimaryButtonKind PrimaryButtonKind { get; init; } = DialogPrimaryButtonKind.Normal;
        public DialogButtonCountdownOptions? PrimaryCountdown { get; init; }
        public bool ShowProgress { get; init; }
        public double ProgressValue { get; init; }
        public bool IsProgressIndeterminate { get; init; }
        public IBrush? PrimaryBackground { get; init; }
        public IBrush? PrimaryForeground { get; init; }
        public IBrush? PrimaryBorderBrush { get; init; }
        public double? PrimaryBorderThickness { get; init; }
        public IBrush? SecondaryBackground { get; init; }
        public IBrush? SecondaryForeground { get; init; }
        public IBrush? SecondaryBorderBrush { get; init; }
        public double? SecondaryBorderThickness { get; init; }
        public DialogCheckboxOption? Checkbox { get; init; }
        public DialogCodeBlockItem? CodeBlock { get; init; }
        public IReadOnlyList<DialogActionLinkItem>? ActionLinks { get; init; }
        public IReadOnlyList<DialogExpandableSectionItem>? ExpandableSections { get; init; }
        public IReadOnlyList<DialogFormSectionItem>? FormSections { get; init; }
        public IReadOnlyList<DialogPassiveChoiceCardItem>? PassiveChoiceCards { get; init; }
        public IReadOnlyList<DialogTabItem>? Tabs { get; init; }
        public double WidthRatio { get; init; } = 1.0;
        public ICommand? PrimaryButtonCommand { get; init; }
        public ICommand? SecondaryButtonCommand { get; init; }
    }

    public sealed class InterruptDialogState : INotifyPropertyChanged
    {
        internal static readonly IBrush DefaultTitleForegroundBrush = new SolidColorBrush(Color.Parse("#303034"));
        internal static readonly IBrush DefaultAccentForegroundBrush = new SolidColorBrush(Color.Parse("#E07167"));
        internal static readonly IBrush TransparentOverlayBrush = new SolidColorBrush(Colors.Transparent);
        internal static readonly IBrush DefaultPrimaryBackgroundBrush = new SolidColorBrush(Color.Parse("#FFEFD7"));
        internal static readonly IBrush DefaultPrimaryForegroundBrush = new SolidColorBrush(Color.Parse("#303034"));
        internal static readonly IBrush DefaultSecondaryBackgroundBrush = new SolidColorBrush(Color.Parse("#F8F9FB"));
        internal static readonly IBrush DefaultSecondaryForegroundBrush = new SolidColorBrush(Color.Parse("#303034"));
        internal static readonly IBrush DefaultSecondaryBorderBrush = new SolidColorBrush(Color.Parse("#E6E6EB"));
        internal static readonly IBrush ButtonSecondaryBackgroundBrush = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
        internal static readonly IBrush ButtonSecondaryForegroundBrush = new SolidColorBrush(Color.Parse("#303034"));
        internal static readonly IBrush ButtonSecondaryBorderBrush = new SolidColorBrush(Color.Parse("#EEAAAAAA"));
        internal static readonly IBrush DangerPrimaryBackgroundBrush = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
        internal static readonly IBrush DangerPrimaryForegroundBrush = new SolidColorBrush(Color.Parse("#C73A22"));
        internal static readonly IBrush DangerPrimaryBorderBrush = new SolidColorBrush(Color.Parse("#DAE4341D"));
        internal static readonly IBrush NormalPrimaryBackgroundBrush = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
        internal static readonly IBrush NormalPrimaryForegroundBrush = new SolidColorBrush(Color.Parse("#303034"));
        internal static readonly IBrush NormalPrimaryBorderBrush = new SolidColorBrush(Color.Parse("#EEAAAAAA"));
        internal static readonly IBrush PassiveOverlayActiveBrush = new SolidColorBrush(Color.Parse("#6D8F9999"));
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isActive;
        private bool _isHitTestVisible;
        private string _title = string.Empty;
        private IBrush _titleForeground = DefaultTitleForegroundBrush;
        private string _content = string.Empty;
        private TextAlignment _contentTextAlignment = TextAlignment.Left;
        private HorizontalAlignment _contentHorizontalAlignment = HorizontalAlignment.Left;
        private string? _headerMeta;
        private string? _sectionTitle;
        private string? _centerIconPath;
        private string? _subText;
        private IBrush _subTextForeground = DefaultAccentForegroundBrush;
        private string? _emphasisText;
        private IBrush _emphasisForeground = DefaultAccentForegroundBrush;
        private bool _showProgress;
        private bool _isProgressIndeterminate;
        private double _progressValue;
        private string _primaryButtonText = string.Empty;
        private string? _secondaryButtonText;
        private bool _showPrimaryButton = true;
        private bool _showSecondaryButton = true;
        private bool _dismissOnEsc = true;
        private bool _allowOverlayClickDismiss;
        private bool _isPrimaryDanger;
        private double _overlayOpacity;
        private double _cardOpacity;
        private double _cardScale = 0.96;
        private double _dialogWidthRatio = 1.0;
        private IBrush _overlayBrush = TransparentOverlayBrush;
        private IBrush _primaryBackground = DefaultPrimaryBackgroundBrush;
        private IBrush _primaryForeground = DefaultPrimaryForegroundBrush;
        private IBrush? _primaryBorderBrush;
        private double _primaryBorderThickness;
        private IBrush _secondaryBackground = DefaultSecondaryBackgroundBrush;
        private IBrush _secondaryForeground = DefaultSecondaryForegroundBrush;
        private IBrush _secondaryBorderBrush = DefaultSecondaryBorderBrush;
        private double _secondaryBorderThickness = 1;
        private double _cardOffsetX;
        private double _cardOffsetY;
        private bool _hasPrimaryCountdown;
        private int _primaryCountdownRemainingSeconds;
        private string _primaryCountdownText = string.Empty;
        private bool _isPassiveOverlay;
        private DialogCheckboxOption? _checkbox;
        private DialogCodeBlockItem? _codeBlock;
        private ObservableCollection<DialogActionLinkItem> _actionLinks = new();
        private ObservableCollection<DialogExpandableSectionItem> _expandableSections = new();
        private ObservableCollection<DialogFormSectionItem> _formSections = new();
        private ObservableCollection<DialogPassiveChoiceCardItem> _passiveChoiceCards = new();
        private ObservableCollection<DialogTabItem> _tabs = new();
        private ICommand _primaryActionCommand;
        private ICommand _secondaryActionCommand;

        public InterruptDialogState(Action onConfirm, Action onCancel)
        {
            ConfirmCommand = new RelayCommand(() => onConfirm());
            CancelCommand = new RelayCommand(() => onCancel());
            _primaryActionCommand = ConfirmCommand;
            _secondaryActionCommand = CancelCommand;
            ApplyPrimaryButtonKind(DialogPrimaryButtonKind.Normal);
            SecondaryBackground = ButtonSecondaryBackgroundBrush;
            SecondaryForeground = ButtonSecondaryForegroundBrush;
            SecondaryBorderBrush = ButtonSecondaryBorderBrush;
            SecondaryBorderThickness = 1;
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetField(ref _isActive, value))
                {
                    OnPropertyChanged(nameof(IsStandardOverlay));
                    OnPropertyChanged(nameof(IsPassiveOverlayVisible));
                }
            }
        }
        public bool IsHitTestVisible { get => _isHitTestVisible; set => SetField(ref _isHitTestVisible, value); }
        public string Title { get => _title; set => SetField(ref _title, value); }
        public IBrush TitleForeground { get => _titleForeground; set => SetField(ref _titleForeground, value); }
        public string Content { get => _content; set => SetField(ref _content, value); }
        public TextAlignment ContentTextAlignment { get => _contentTextAlignment; set => SetField(ref _contentTextAlignment, value); }
        public HorizontalAlignment ContentHorizontalAlignment { get => _contentHorizontalAlignment; set => SetField(ref _contentHorizontalAlignment, value); }
        public string? HeaderMeta { get => _headerMeta; set { if (SetField(ref _headerMeta, value)) OnPropertyChanged(nameof(HasHeaderMeta)); } }
        public string? SectionTitle { get => _sectionTitle; set { if (SetField(ref _sectionTitle, value)) OnPropertyChanged(nameof(HasSectionTitle)); } }
        public string? CenterIconPath { get => _centerIconPath; set { if (SetField(ref _centerIconPath, value)) OnPropertyChanged(nameof(HasCenterIcon)); } }
        public string? SubText { get => _subText; set { if (SetField(ref _subText, value)) OnPropertyChanged(nameof(HasSubText)); } }
        public IBrush SubTextForeground { get => _subTextForeground; set => SetField(ref _subTextForeground, value); }
        public string? EmphasisText { get => _emphasisText; set { if (SetField(ref _emphasisText, value)) OnPropertyChanged(nameof(HasEmphasisText)); } }
        public IBrush EmphasisForeground { get => _emphasisForeground; set => SetField(ref _emphasisForeground, value); }
        public bool ShowProgress { get => _showProgress; set => SetField(ref _showProgress, value); }
        public bool IsProgressIndeterminate { get => _isProgressIndeterminate; set => SetField(ref _isProgressIndeterminate, value); }
        public double ProgressValue { get => _progressValue; set => SetField(ref _progressValue, value); }
        public string PrimaryButtonText { get => _primaryButtonText; set => SetField(ref _primaryButtonText, value); }
        public string? SecondaryButtonText
        {
            get => _secondaryButtonText;
            set
            {
                if (SetField(ref _secondaryButtonText, value))
                {
                    OnPropertyChanged(nameof(HasSecondaryButton));
                    OnPropertyChanged(nameof(ShowSecondaryButtonEffective));
                    OnPropertyChanged(nameof(ShowActionButtons));
                }
            }
        }
        public bool ShowPrimaryButton
        {
            get => _showPrimaryButton;
            set
            {
                if (SetField(ref _showPrimaryButton, value))
                {
                    OnPropertyChanged(nameof(ShowPrimaryButtonEffective));
                    OnPropertyChanged(nameof(ShowActionButtons));
                }
            }
        }
        public bool ShowSecondaryButton
        {
            get => _showSecondaryButton;
            set
            {
                if (SetField(ref _showSecondaryButton, value))
                {
                    OnPropertyChanged(nameof(ShowSecondaryButtonEffective));
                    OnPropertyChanged(nameof(ShowActionButtons));
                }
            }
        }
        public bool DismissOnEsc { get => _dismissOnEsc; set => SetField(ref _dismissOnEsc, value); }
        public bool AllowOverlayClickDismiss { get => _allowOverlayClickDismiss; set => SetField(ref _allowOverlayClickDismiss, value); }
        public bool IsPrimaryDanger { get => _isPrimaryDanger; set => SetField(ref _isPrimaryDanger, value); }
        public double OverlayOpacity { get => _overlayOpacity; set => SetField(ref _overlayOpacity, value); }
        public double CardOpacity { get => _cardOpacity; set => SetField(ref _cardOpacity, value); }
        public double CardScale { get => _cardScale; set => SetField(ref _cardScale, value); }
        public double DialogWidthRatio { get => _dialogWidthRatio; set => SetField(ref _dialogWidthRatio, value); }
        public IBrush OverlayBrush { get => _overlayBrush; set => SetField(ref _overlayBrush, value); }
        public IBrush PrimaryBackground { get => _primaryBackground; set => SetField(ref _primaryBackground, value); }
        public IBrush PrimaryForeground { get => _primaryForeground; set => SetField(ref _primaryForeground, value); }
        public IBrush? PrimaryBorderBrush { get => _primaryBorderBrush; set => SetField(ref _primaryBorderBrush, value); }
        public double PrimaryBorderThickness { get => _primaryBorderThickness; set => SetField(ref _primaryBorderThickness, value); }
        public IBrush SecondaryBackground { get => _secondaryBackground; set => SetField(ref _secondaryBackground, value); }
        public IBrush SecondaryForeground { get => _secondaryForeground; set => SetField(ref _secondaryForeground, value); }
        public IBrush SecondaryBorderBrush { get => _secondaryBorderBrush; set => SetField(ref _secondaryBorderBrush, value); }
        public double SecondaryBorderThickness { get => _secondaryBorderThickness; set => SetField(ref _secondaryBorderThickness, value); }
        public double CardOffsetX { get => _cardOffsetX; set => SetField(ref _cardOffsetX, value); }
        public double CardOffsetY { get => _cardOffsetY; set => SetField(ref _cardOffsetY, value); }
        public bool HasPrimaryCountdown { get => _hasPrimaryCountdown; set { if (SetField(ref _hasPrimaryCountdown, value)) OnPropertyChanged(nameof(PrimaryCountdownVisible)); } }
        public int PrimaryCountdownRemainingSeconds { get => _primaryCountdownRemainingSeconds; set => SetField(ref _primaryCountdownRemainingSeconds, value); }
        public string PrimaryCountdownText { get => _primaryCountdownText; set { if (SetField(ref _primaryCountdownText, value)) OnPropertyChanged(nameof(PrimaryCountdownVisible)); } }
        public bool IsPassiveOverlay
        {
            get => _isPassiveOverlay;
            set
            {
                if (SetField(ref _isPassiveOverlay, value))
                {
                    OnPropertyChanged(nameof(IsStandardOverlay));
                    OnPropertyChanged(nameof(IsPassiveOverlayVisible));
                }
            }
        }
        public DialogCheckboxOption? Checkbox { get => _checkbox; set { if (SetField(ref _checkbox, value)) OnPropertyChanged(nameof(HasCheckbox)); } }
        public DialogCodeBlockItem? CodeBlock { get => _codeBlock; set { if (SetField(ref _codeBlock, value)) OnPropertyChanged(nameof(HasCodeBlock)); } }
        public ObservableCollection<DialogActionLinkItem> ActionLinks { get => _actionLinks; set { if (SetField(ref _actionLinks, value)) OnPropertyChanged(nameof(HasActionLinks)); } }
        public ObservableCollection<DialogExpandableSectionItem> ExpandableSections { get => _expandableSections; set { if (SetField(ref _expandableSections, value)) OnPropertyChanged(nameof(HasExpandableSections)); } }
        public ObservableCollection<DialogFormSectionItem> FormSections { get => _formSections; set { if (SetField(ref _formSections, value)) OnPropertyChanged(nameof(HasFormSections)); } }
        public ObservableCollection<DialogPassiveChoiceCardItem> PassiveChoiceCards { get => _passiveChoiceCards; set { if (SetField(ref _passiveChoiceCards, value)) OnPropertyChanged(nameof(HasPassiveChoiceCards)); } }
        public ObservableCollection<DialogTabItem> Tabs { get => _tabs; set { if (SetField(ref _tabs, value)) OnPropertyChanged(nameof(HasTabs)); } }
        public ICommand PrimaryActionCommand { get => _primaryActionCommand; set => SetField(ref _primaryActionCommand, value); }
        public ICommand SecondaryActionCommand { get => _secondaryActionCommand; set => SetField(ref _secondaryActionCommand, value); }

        public bool HasSectionTitle => !string.IsNullOrWhiteSpace(SectionTitle);
        public bool HasHeaderMeta => !string.IsNullOrWhiteSpace(HeaderMeta);
        public bool HasCenterIcon => !string.IsNullOrWhiteSpace(CenterIconPath);
        public bool HasSubText => !string.IsNullOrWhiteSpace(SubText);
        public bool HasEmphasisText => !string.IsNullOrWhiteSpace(EmphasisText);
        public bool HasSecondaryButton => !string.IsNullOrWhiteSpace(SecondaryButtonText);
        public bool ShowPrimaryButtonEffective => ShowPrimaryButton;
        public bool ShowSecondaryButtonEffective => ShowSecondaryButton && HasSecondaryButton;
        public bool ShowActionButtons => ShowPrimaryButtonEffective || ShowSecondaryButtonEffective;
        public bool HasActionLinks => ActionLinks.Count > 0;
        public bool HasPairedActionLinks => ActionLinks.Count == 2;
        public DialogActionLinkItem? FirstActionLink => ActionLinks.Count > 0 ? ActionLinks[0] : null;
        public DialogActionLinkItem? SecondActionLink => ActionLinks.Count > 1 ? ActionLinks[1] : null;
        public bool HasExpandableSections => ExpandableSections.Count > 0;
        public bool HasFormSections => FormSections.Count > 0;
        public bool HasPassiveChoiceCards => PassiveChoiceCards.Count > 0;
        public bool HasTabs => Tabs.Count > 0;
        public bool HasCheckbox => Checkbox != null;
        public bool HasCodeBlock => CodeBlock != null && !string.IsNullOrWhiteSpace(CodeBlock.Content);
        public bool PrimaryCountdownVisible => HasPrimaryCountdown && !string.IsNullOrWhiteSpace(PrimaryCountdownText);
        public bool IsStandardOverlay => IsActive && !IsPassiveOverlay;
        public bool IsPassiveOverlayVisible => IsActive && IsPassiveOverlay;

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        internal void ApplyPrimaryButtonKind(DialogPrimaryButtonKind kind)
        {
            IsPrimaryDanger = kind == DialogPrimaryButtonKind.DangerConfirm;

            if (IsPrimaryDanger)
            {
                PrimaryBackground = DangerPrimaryBackgroundBrush;
                PrimaryForeground = DangerPrimaryForegroundBrush;
                PrimaryBorderBrush = DangerPrimaryBorderBrush;
                PrimaryBorderThickness = 1;
                return;
            }

            PrimaryBackground = NormalPrimaryBackgroundBrush;
            PrimaryForeground = NormalPrimaryForegroundBrush;
            PrimaryBorderBrush = NormalPrimaryBorderBrush;
            PrimaryBorderThickness = 1;
        }

        internal void ResetVisualState(bool isActive)
        {
            IsActive = isActive;
            IsHitTestVisible = isActive;
            OverlayOpacity = isActive ? 0.9 : 0.0;
            CardOpacity = isActive ? 1.0 : 0.0;
            CardScale = isActive ? 1.0 : 0.96;
            CardOffsetX = isActive ? 0 : 6;
            CardOffsetY = 0;
            OverlayBrush = isActive
                ? PassiveOverlayActiveBrush
                : TransparentOverlayBrush;
        }

        internal void ApplyClosingVisualState(bool isPassiveOverlay)
        {
            OverlayOpacity = 0.0;
            CardOpacity = 0.0;
            CardScale = 0.96;
            CardOffsetX = isPassiveOverlay ? 8 : 6;
            CardOffsetY = isPassiveOverlay ? -4 : 0;
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            if (propertyName == nameof(ActionLinks))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPairedActionLinks)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FirstActionLink)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SecondActionLink)));
            }
        }
    }

    public sealed class LicenseCatalogService
    {
        private readonly string _baseDirectory;

        public LicenseCatalogService()
        {
            _baseDirectory = AppContext.BaseDirectory;
        }

        public DialogExpandableSectionItem BuildLicenseSection(string sectionTitle)
        {
            var configuredEntries = LoadConfiguredEntries();
            var licenseFiles = GetLicenseFiles();
            var configuredItems = configuredEntries
                .OrderBy(x => x.SortOrder)
                .Select(BuildConfiguredItem)
                .Where(x => x != null)
                .Cast<DialogLicenseItem>()
                .ToList();

            var seenIds = new HashSet<string>(configuredEntries.Select(x => NormalizeId(x.Id ?? x.Title)), StringComparer.OrdinalIgnoreCase);
            foreach (var configuredEntry in configuredEntries)
            {
                var normalizedTitle = NormalizeId(configuredEntry.Title);
                if (!string.IsNullOrWhiteSpace(normalizedTitle))
                {
                    seenIds.Add(normalizedTitle);
                }
            }

            var automaticItems = ParseThirdPartyNotices(seenIds, licenseFiles).ToList();
            return new DialogExpandableSectionItem(sectionTitle, configuredItems.Concat(automaticItems));
        }

        private List<ConfiguredLicenseEntry> LoadConfiguredEntries()
        {
            var path = Path.Combine(_baseDirectory, "licenses.config.json");
            if (!File.Exists(path))
            {
                return new List<ConfiguredLicenseEntry>();
            }

            try
            {
                var json = File.ReadAllText(path);
                var config = JsonConvert.DeserializeObject<LicenseCatalogConfig>(json);
                return config?.Entries ?? new List<ConfiguredLicenseEntry>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to parse licenses.config.json: {ex.Message}");
                return new List<ConfiguredLicenseEntry>();
            }
        }

        private DialogLicenseItem? BuildConfiguredItem(ConfiguredLicenseEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Title))
            {
                return null;
            }

            var fullText = entry.ShowFullText ? LoadLocalText(entry.SourcePath) : null;
            var meta = BuildMetaText(entry.Kind, null);
            var loc = LocalizationManager.Instance;
            var title = ResolveLocalizedValue(entry.TitleKey, entry.Title, loc) ?? entry.Title;
            var summary = ResolveLocalizedValue(entry.SummaryKey, entry.Summary, loc);
            return new DialogLicenseItem(title, summary, meta, entry.ProjectUrl, entry.LicenseUrl, fullText);
        }

        private IEnumerable<DialogLicenseItem> ParseThirdPartyNotices(HashSet<string> seenIds, IReadOnlyList<string> licenseFiles)
        {
            var noticesPath = Path.Combine(_baseDirectory, "THIRD-PARTY-NOTICES.md");
            if (!File.Exists(noticesPath))
            {
                yield break;
            }

            var content = File.ReadAllText(noticesPath);
            var blocks = Regex.Split(content, "\\r?\\n#{20,}\\r?\\n")
                .Where(block => !string.IsNullOrWhiteSpace(block));

            foreach (var block in blocks)
            {
                var fields = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split(':', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

                if (!fields.TryGetValue("Package", out var packageName) || string.IsNullOrWhiteSpace(packageName))
                {
                    continue;
                }

                var normalizedPackage = NormalizeId(packageName);
                if (seenIds.Contains(normalizedPackage))
                {
                    continue;
                }

                seenIds.Add(normalizedPackage);
                fields.TryGetValue("Version", out var version);
                fields.TryGetValue("project URL", out var projectUrl);
                fields.TryGetValue("licenseUrl", out var licenseUrl);
                fields.TryGetValue("license Type", out var licenseType);

                var fullText = ResolveAutomaticFullText(packageName, licenseType, licenseFiles);
                yield return new DialogLicenseItem(
                    packageName,
                    null,
                    BuildMetaText(licenseType, version),
                    projectUrl,
                    licenseUrl,
                    fullText);
            }
        }

        private string? ResolveAutomaticFullText(string packageName, string? licenseType, IReadOnlyList<string> licenseFiles)
        {
            if (licenseFiles.Count == 0)
            {
                return null;
            }

            var normalizedPackageName = NormalizeId(packageName);
            var packageMatch = licenseFiles.FirstOrDefault(path =>
                NormalizeId(Path.GetFileNameWithoutExtension(path)).StartsWith(normalizedPackageName, StringComparison.OrdinalIgnoreCase));
            if (packageMatch != null)
            {
                return LoadLocalText(Path.GetRelativePath(_baseDirectory, packageMatch));
            }

            if (!string.IsNullOrWhiteSpace(licenseType))
            {
                var normalizedLicenseType = NormalizeId(licenseType);
                var licenseTypeMatch = licenseFiles.FirstOrDefault(path => string.Equals(
                        NormalizeId(Path.GetFileNameWithoutExtension(path)),
                        normalizedLicenseType,
                        StringComparison.OrdinalIgnoreCase));

                if (licenseTypeMatch != null)
                {
                    return LoadLocalText(Path.GetRelativePath(_baseDirectory, licenseTypeMatch));
                }
            }

            return null;
        }

        private IReadOnlyList<string> GetLicenseFiles()
        {
            var licensesDir = ResolveLicensesDirectory();
            if (licensesDir == null)
            {
                return Array.Empty<string>();
            }

            try
            {
                return Directory.EnumerateFiles(licensesDir).ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to enumerate license files from {licensesDir}: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        private string? LoadLocalText(string? sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return null;
            }

            var combinedPath = Path.IsPathRooted(sourcePath)
                ? sourcePath
                : Path.Combine(_baseDirectory, sourcePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(combinedPath))
            {
                return null;
            }

            try
            {
                var extension = Path.GetExtension(combinedPath).ToLowerInvariant();
                var text = File.ReadAllText(combinedPath);
                return extension == ".html" || extension == ".htm"
                    ? HtmlToPlainText(text)
                    : text.Trim();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read license text from {combinedPath}: {ex.Message}");
                return null;
            }
        }

        private string? ResolveLicensesDirectory()
        {
            var uppercase = Path.Combine(_baseDirectory, "LICENSES");
            if (Directory.Exists(uppercase))
            {
                return uppercase;
            }

            var lowercase = Path.Combine(_baseDirectory, "licenses");
            if (Directory.Exists(lowercase))
            {
                return lowercase;
            }

            return null;
        }

        private static string HtmlToPlainText(string html)
        {
            var withoutScript = Regex.Replace(html, "<(script|style)[^>]*>.*?</\\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var withLineBreaks = Regex.Replace(withoutScript, "<(br|/p|/div|/li|/tr|/h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
            var withoutTags = Regex.Replace(withLineBreaks, "<[^>]+>", string.Empty);
            var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
            var normalized = Regex.Replace(decoded, "\n{3,}", "\n\n");
            return normalized.Trim();
        }

        private static string BuildMetaText(string? kindOrLicenseType, string? version)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(version))
            {
                parts.Add(version.Trim());
            }

            if (!string.IsNullOrWhiteSpace(kindOrLicenseType))
            {
                parts.Add(kindOrLicenseType.Trim());
            }

            return string.Join(" | ", parts);
        }

        private static string NormalizeId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().ToLowerInvariant();
            normalized = normalized.Replace("-license", string.Empty).Replace("license", string.Empty);
            return Regex.Replace(normalized, "[^a-z0-9]+", string.Empty);
        }

        private static string? ResolveLocalizedValue(string? localizationKey, string? fallback, LocalizationManager manager)
        {
            if (!string.IsNullOrWhiteSpace(localizationKey))
            {
                var localized = manager[localizationKey];
                if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, localizationKey, StringComparison.Ordinal))
                {
                    return localized;
                }
            }

            return fallback;
        }
    }

    public sealed class InterruptDialogService
    {
        private readonly Dispatcher _dispatcher = Dispatcher.UIThread;
        private readonly LicenseCatalogService _licenseCatalogService;
        private readonly IToastService _toastService;
        private readonly AnimationStateSource _animationStateSource;
        private readonly FrameRateSettings _frameRateSettings;
        private readonly PerformanceTelemetryService _performanceTelemetryService;
        private readonly DispatcherTimer _primaryCountdownTimer;
        private TaskCompletionSource<InterruptDialogResponse>? _pendingCompletion;
        private bool _isPassiveOverlayActive;
        private Rect? _passiveChoiceBounds;

        public InterruptDialogState State { get; }

        public InterruptDialogService(
            LicenseCatalogService licenseCatalogService,
            IToastService toastService,
            AnimationStateSource animationStateSource,
            FrameRateSettings frameRateSettings,
            PerformanceTelemetryService performanceTelemetryService)
        {
            _licenseCatalogService = licenseCatalogService;
            _toastService = toastService;
            _animationStateSource = animationStateSource;
            _frameRateSettings = frameRateSettings;
            _performanceTelemetryService = performanceTelemetryService;
            State = new InterruptDialogState(Confirm, Cancel);
            State.ResetVisualState(false);
            _primaryCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _primaryCountdownTimer.Tick += PrimaryCountdownTimer_Tick;
        }

        public async Task<InterruptDialogResponse> ShowAsync(InterruptDialogOptions options)
        {
            _isPassiveOverlayActive = false;
            _animationStateSource.MarkTransitionActivity(220);
            StopPrimaryCountdown();
            _pendingCompletion?.TrySetResult(new InterruptDialogResponse { Result = InterruptDialogResult.None });
            var tcs = new TaskCompletionSource<InterruptDialogResponse>();
            _pendingCompletion = tcs;

            await _dispatcher.InvokeAsync(() =>
            {
                State.IsPassiveOverlay = false;
                State.Title = options.Title ?? string.Empty;
                State.TitleForeground = options.TitleForeground ?? InterruptDialogState.DefaultTitleForegroundBrush;
                State.HeaderMeta = options.HeaderMeta;
                State.SectionTitle = options.SectionTitle;
                State.Content = options.Content ?? string.Empty;
                State.ContentTextAlignment = options.ContentTextAlignment;
                State.ContentHorizontalAlignment = options.ContentHorizontalAlignment;
                State.CenterIconPath = options.CenterIconPath;
                State.SubText = options.SubText;
                State.SubTextForeground = options.SubTextForeground ?? InterruptDialogState.DefaultAccentForegroundBrush;
                State.EmphasisText = options.EmphasisText;
                State.EmphasisForeground = options.EmphasisForeground ?? InterruptDialogState.DefaultAccentForegroundBrush;
                State.PrimaryButtonText = options.PrimaryButtonText ?? string.Empty;
                State.SecondaryButtonText = options.SecondaryButtonText;
                State.ShowPrimaryButton = options.ShowPrimaryButton;
                State.ShowSecondaryButton = options.ShowSecondaryButton;
                State.DismissOnEsc = options.DismissOnEsc;
                State.AllowOverlayClickDismiss = options.AllowOverlayClickDismiss;
                State.ApplyPrimaryButtonKind(options.PrimaryButtonKind);
                State.ShowProgress = options.ShowProgress;
                State.ProgressValue = options.ProgressValue;
                State.IsProgressIndeterminate = options.IsProgressIndeterminate;
                State.PrimaryBackground = options.PrimaryBackground ?? State.PrimaryBackground;
                State.PrimaryForeground = options.PrimaryForeground ?? State.PrimaryForeground;
                State.PrimaryBorderBrush = options.PrimaryBorderBrush ?? State.PrimaryBorderBrush;
                State.PrimaryBorderThickness = options.PrimaryBorderThickness ?? State.PrimaryBorderThickness;
                State.SecondaryBackground = options.SecondaryBackground ?? State.SecondaryBackground;
                State.SecondaryForeground = options.SecondaryForeground ?? State.SecondaryForeground;
                State.SecondaryBorderBrush = options.SecondaryBorderBrush ?? State.SecondaryBorderBrush;
                State.SecondaryBorderThickness = options.SecondaryBorderThickness ?? State.SecondaryBorderThickness;
                State.Checkbox = options.Checkbox;
                State.CodeBlock = options.CodeBlock;
                State.ActionLinks = new ObservableCollection<DialogActionLinkItem>(options.ActionLinks ?? Array.Empty<DialogActionLinkItem>());
                State.ExpandableSections = new ObservableCollection<DialogExpandableSectionItem>(options.ExpandableSections ?? Array.Empty<DialogExpandableSectionItem>());
                State.FormSections = new ObservableCollection<DialogFormSectionItem>(options.FormSections ?? Array.Empty<DialogFormSectionItem>());
                State.PassiveChoiceCards = new ObservableCollection<DialogPassiveChoiceCardItem>(options.PassiveChoiceCards ?? Array.Empty<DialogPassiveChoiceCardItem>());
                State.Tabs = new ObservableCollection<DialogTabItem>(options.Tabs ?? Array.Empty<DialogTabItem>());
                State.DialogWidthRatio = options.WidthRatio > 0 ? options.WidthRatio : 1.0;
                State.PrimaryActionCommand = options.PrimaryButtonCommand ?? State.ConfirmCommand;
                State.SecondaryActionCommand = options.SecondaryButtonCommand ?? State.CancelCommand;
                State.IsHitTestVisible = options.HitTestVisible;
                State.ResetVisualState(true);
                State.IsHitTestVisible = options.HitTestVisible;

                if (options.PrimaryCountdown is { Seconds: > 0 } countdown)
                {
                    State.HasPrimaryCountdown = true;
                    State.PrimaryCountdownRemainingSeconds = countdown.Seconds;
                    State.PrimaryCountdownText = $"({countdown.Seconds}s)";
                    _primaryCountdownTimer.Start();
                }
                else
                {
                    State.HasPrimaryCountdown = false;
                    State.PrimaryCountdownRemainingSeconds = 0;
                    State.PrimaryCountdownText = string.Empty;
                }
            });

            return await tcs.Task.ConfigureAwait(false);
        }

        public void ShowPassiveOverlay(InterruptDialogOptions options)
        {
            if (_pendingCompletion != null)
            {
                return;
            }

            _isPassiveOverlayActive = true;
            _animationStateSource.MarkTransitionActivity(160);
            StopPrimaryCountdown();

            _dispatcher.Post(() =>
            {
                State.IsPassiveOverlay = true;
                State.Title = options.Title ?? string.Empty;
                State.TitleForeground = options.TitleForeground ?? InterruptDialogState.DefaultTitleForegroundBrush;
                State.HeaderMeta = options.HeaderMeta;
                State.SectionTitle = options.SectionTitle;
                State.Content = options.Content ?? string.Empty;
                State.ContentTextAlignment = options.ContentTextAlignment;
                State.ContentHorizontalAlignment = options.ContentHorizontalAlignment;
                State.CenterIconPath = options.CenterIconPath;
                State.SubText = options.SubText;
                State.SubTextForeground = options.SubTextForeground ?? InterruptDialogState.DefaultAccentForegroundBrush;
                State.EmphasisText = options.EmphasisText;
                State.EmphasisForeground = options.EmphasisForeground ?? InterruptDialogState.DefaultAccentForegroundBrush;
                State.PrimaryButtonText = options.PrimaryButtonText ?? string.Empty;
                State.SecondaryButtonText = options.SecondaryButtonText;
                State.ShowPrimaryButton = options.ShowPrimaryButton;
                State.ShowSecondaryButton = options.ShowSecondaryButton;
                State.DismissOnEsc = options.DismissOnEsc;
                State.AllowOverlayClickDismiss = options.AllowOverlayClickDismiss;
                State.ShowProgress = false;
                State.ProgressValue = 0;
                State.IsProgressIndeterminate = false;
                State.Checkbox = null;
                State.CodeBlock = null;
                State.ActionLinks = new ObservableCollection<DialogActionLinkItem>();
                State.ExpandableSections = new ObservableCollection<DialogExpandableSectionItem>();
                State.FormSections = new ObservableCollection<DialogFormSectionItem>();
                State.PassiveChoiceCards = new ObservableCollection<DialogPassiveChoiceCardItem>(options.PassiveChoiceCards ?? Array.Empty<DialogPassiveChoiceCardItem>());
                State.Tabs = new ObservableCollection<DialogTabItem>();
                State.DialogWidthRatio = options.WidthRatio > 0 ? options.WidthRatio : 1.0;
                State.PrimaryActionCommand = State.ConfirmCommand;
                State.SecondaryActionCommand = State.CancelCommand;
                State.IsActive = true;
                State.IsHitTestVisible = options.HitTestVisible;
                State.OverlayOpacity = 0.9;
                State.CardOpacity = 1.0;
                State.CardScale = 1.0;
                State.CardOffsetX = 0;
                State.OverlayBrush = PassiveOverlayBrush;
            });
        }

        public void HidePassiveOverlay()
        {
            if (!_isPassiveOverlayActive || _pendingCompletion != null)
            {
                return;
            }

            _isPassiveOverlayActive = false;
            _passiveChoiceBounds = null;
            _animationStateSource.MarkTransitionActivity(180);
            _dispatcher.Post(() =>
            {
                State.ApplyClosingVisualState(isPassiveOverlay: true);
            });

            _ = FinishPassiveOverlayHideAsync();
        }

        public void UpdatePassiveOverlayMotion(double pointerX, double pointerY, double viewportWidth, double viewportHeight)
        {
            if (!_isPassiveOverlayActive || viewportWidth <= 0 || viewportHeight <= 0)
            {
                return;
            }

            var ratioX = Math.Clamp(pointerX / viewportWidth, 0.0, 1.0);
            var ratioY = Math.Clamp(pointerY / viewportHeight, 0.0, 1.0);
            var offsetX = (ratioX - 0.5) * 12.0;
            var offsetY = (ratioY - 0.5) * 8.0;

            _dispatcher.Post(() =>
            {
                if (_isPassiveOverlayActive)
                {
                    State.CardOffsetX = offsetX;
                    State.CardOffsetY = offsetY;
                }
            });
        }

        public async Task PulsePassiveOverlayAsync()
        {
            if (!_isPassiveOverlayActive)
            {
                return;
            }

            await _dispatcher.InvokeAsync(() =>
            {
                if (_isPassiveOverlayActive)
                {
                    State.CardScale = 0.975;
                }
            });

            await Task.Delay(85);

            await _dispatcher.InvokeAsync(() =>
            {
                if (_isPassiveOverlayActive)
                {
                    State.CardScale = 1.0;
                }
            });
        }

        public void UpdatePassiveOverlayChoice(string? key)
        {
            if (!_isPassiveOverlayActive)
            {
                return;
            }

            _dispatcher.Post(() =>
            {
                foreach (var item in State.PassiveChoiceCards)
                {
                    item.IsSelected = !string.IsNullOrWhiteSpace(key) &&
                                      string.Equals(item.Key, key, StringComparison.Ordinal);
                }
            });
        }

        public void UpdatePassiveChoiceBounds(Rect? bounds)
        {
            _passiveChoiceBounds = bounds;
        }

        public string? ResolvePassiveChoiceAt(double pointerX, double pointerY, string? fallbackKey = null)
        {
            if (!_isPassiveOverlayActive || !_passiveChoiceBounds.HasValue || State.PassiveChoiceCards.Count == 0)
            {
                return fallbackKey;
            }

            var bounds = _passiveChoiceBounds.Value;
            if (!bounds.Contains(new Point(pointerX, pointerY)))
            {
                return fallbackKey;
            }

            var sectionHeight = bounds.Height / State.PassiveChoiceCards.Count;
            if (sectionHeight <= 0)
            {
                return fallbackKey;
            }

            var index = Math.Min(State.PassiveChoiceCards.Count - 1, (int)((pointerY - bounds.Y) / sectionHeight));
            return State.PassiveChoiceCards[index].Key;
        }

        public async Task ShowSingleActionAsync(string title, string content, string acknowledgeText, string? sectionTitle = null)
        {
            await ShowAsync(new InterruptDialogOptions
            {
                Title = title,
                SectionTitle = sectionTitle,
                Content = content,
                PrimaryButtonText = acknowledgeText
            });
        }

        public async Task ShowDebugDialogAsync()
        {
            var memoryProfileService = App.Services?.GetService(typeof(MemoryProfileService)) as MemoryProfileService;
            using var controller = new FrameRateDebugDialogController(_frameRateSettings, _performanceTelemetryService, _toastService, State, memoryProfileService);
            await ShowAsync(controller.BuildOptions());
        }

        public Task ShowFrameRateDebugDialogAsync()
        {
            return ShowDebugDialogAsync();
        }

        public async Task ShowAboutDialogAsync()
        {
            var loc = LocalizationManager.Instance;
            var config = ConfigManager.Config.AppInfo;
            var licenseSection = await Task.Run(() =>
                _licenseCatalogService.BuildLicenseSection(loc["Home_AboutDialog_LicenseSectionTitle"]));

            await ShowAsync(new InterruptDialogOptions
            {
                Title = loc["Home_AboutDialog_Title"],
                HeaderMeta = loc["Home_AboutDialog_Publisher"],
                Content = loc["Home_AboutDialog_Content"],
                PrimaryButtonText = loc["Dialog_Primary_Acknowledge"],
                ActionLinks = new[]
                {
                    new DialogActionLinkItem(loc["Home_AboutDialog_ProfileButton"], config.GitHubIcon, "https://github.com/PingGai"),
                    new DialogActionLinkItem(loc["Home_AboutDialog_StarButton"], config.StarIcon, "https://github.com/PingGai/FolderStyleEditorForWindows")
                },
                ExpandableSections = new[] { licenseSection }
            });
        }

        public Task<InterruptDialogResponse> ShowDangerConfirmAsync(string title, string content, string confirmText, string cancelText, string? sectionTitle = null)
        {
            return ShowAsync(new InterruptDialogOptions
            {
                Title = title,
                SectionTitle = sectionTitle,
                Content = content,
                PrimaryButtonText = confirmText,
                SecondaryButtonText = cancelText,
                PrimaryButtonKind = DialogPrimaryButtonKind.DangerConfirm
            });
        }

        public Task<InterruptDialogResponse> ShowElevationPromptAsync()
        {
            var loc = LocalizationManager.Instance;
            return ShowAsync(new InterruptDialogOptions
            {
                Title = loc["Dialog_Elevation_Title"],
                EmphasisText = loc["Dialog_Elevation_RequiredHeadline"],
                Content = loc["Dialog_Elevation_Content"],
                PrimaryButtonText = loc["Dialog_Elevation_Confirm"],
                SecondaryButtonText = loc["Dialog_Elevation_Cancel"],
                Checkbox = new DialogCheckboxOption(loc["Dialog_Elevation_DoNotShow"]),
                PrimaryCountdown = new DialogButtonCountdownOptions { Seconds = 10 }
            });
        }

        public Task ShowFailureAsync(string title, string emphasisText, string content, string? details)
        {
            var loc = LocalizationManager.Instance;
            DialogCodeBlockItem? codeBlock = null;
            if (!string.IsNullOrWhiteSpace(details))
            {
                var copyCommand = new RelayCommand(async () =>
                {
                    if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ||
                        desktop.MainWindow?.Clipboard == null)
                    {
                        return;
                    }

                    await desktop.MainWindow.Clipboard.SetTextAsync(details);
                    _toastService.Show(loc["Toast_CopySuccess"], new SolidColorBrush(Color.Parse("#EBB762")));
                });

                codeBlock = new DialogCodeBlockItem(details, new[]
                {
                    new DialogContextMenuItem(loc["Dialog_CodeBlock_Copy"], copyCommand)
                });
            }

            return ShowAsync(new InterruptDialogOptions
            {
                Title = title,
                EmphasisText = emphasisText,
                Content = content,
                PrimaryButtonText = loc["Dialog_Primary_Acknowledge"],
                CodeBlock = codeBlock
            });
        }

        public Task ShowInfoAsync(string title, string sectionTitle, string content, string acknowledgeText)
        {
            return ShowSingleActionAsync(title, content, acknowledgeText, sectionTitle);
        }

        private void Confirm() => Complete(InterruptDialogResult.Primary);

        private void Cancel() => Complete(State.HasSecondaryButton ? InterruptDialogResult.Secondary : InterruptDialogResult.None);

        private void Complete(InterruptDialogResult result)
        {
            StopPrimaryCountdown();
            _isPassiveOverlayActive = false;
            var captured = _pendingCompletion;
            if (captured == null)
            {
                return;
            }

            var response = new InterruptDialogResponse
            {
                Result = result,
                IsCheckboxChecked = State.Checkbox?.IsChecked == true
            };

            _pendingCompletion = null;
            _animationStateSource.MarkTransitionActivity(220);
            _dispatcher.Post(() => State.ApplyClosingVisualState(isPassiveOverlay: false));

            _ = CompleteAfterCloseAnimationAsync(captured, response);
        }

        private static readonly IBrush PassiveOverlayBrush = new SolidColorBrush(Color.Parse("#6D8F9999"));

        private async Task FinishPassiveOverlayHideAsync()
        {
            await Task.Delay(135).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() =>
            {
                if (_isPassiveOverlayActive || _pendingCompletion != null)
                {
                    return;
                }

                State.ResetVisualState(false);
                State.IsPassiveOverlay = false;
            });
        }

        private async Task CompleteAfterCloseAnimationAsync(TaskCompletionSource<InterruptDialogResponse> captured, InterruptDialogResponse response)
        {
            await Task.Delay(200).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() => State.ResetVisualState(false));
            captured.TrySetResult(response);
        }

        private void PrimaryCountdownTimer_Tick(object? sender, EventArgs e)
        {
            if (!State.IsActive || !State.HasPrimaryCountdown)
            {
                StopPrimaryCountdown();
                return;
            }

            State.PrimaryCountdownRemainingSeconds = Math.Max(0, State.PrimaryCountdownRemainingSeconds - 1);
            State.PrimaryCountdownText = State.PrimaryCountdownRemainingSeconds > 0
                ? $"({State.PrimaryCountdownRemainingSeconds}s)"
                : string.Empty;

            if (State.PrimaryCountdownRemainingSeconds > 0)
            {
                return;
            }

            StopPrimaryCountdown();
            Confirm();
        }

        private void StopPrimaryCountdown()
        {
            _primaryCountdownTimer.Stop();
            State.HasPrimaryCountdown = false;
            State.PrimaryCountdownRemainingSeconds = 0;
            State.PrimaryCountdownText = string.Empty;
        }
    }

    public sealed class LicenseCatalogConfig
    {
        [JsonProperty("entries")]
        public List<ConfiguredLicenseEntry> Entries { get; set; } = new();
    }

    public sealed class ConfiguredLicenseEntry
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("titleKey")]
        public string? TitleKey { get; set; }

        [JsonProperty("summary")]
        public string? Summary { get; set; }

        [JsonProperty("summaryKey")]
        public string? SummaryKey { get; set; }

        [JsonProperty("projectUrl")]
        public string? ProjectUrl { get; set; }

        [JsonProperty("licenseUrl")]
        public string? LicenseUrl { get; set; }

        [JsonProperty("sourcePath")]
        public string? SourcePath { get; set; }

        [JsonProperty("sortOrder")]
        public int SortOrder { get; set; }

        [JsonProperty("showFullText")]
        public bool ShowFullText { get; set; } = true;

        [JsonProperty("kind")]
        public string? Kind { get; set; }
    }
}
