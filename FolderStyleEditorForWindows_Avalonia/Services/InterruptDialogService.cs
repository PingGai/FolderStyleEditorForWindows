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

    public enum DialogPrimaryButtonKind
    {
        Normal,
        DangerConfirm
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
        public string? HeaderMeta { get; init; }
        public string? SectionTitle { get; init; }
        public string Content { get; init; } = string.Empty;
        public string PrimaryButtonText { get; init; } = string.Empty;
        public string? SecondaryButtonText { get; init; }
        public DialogPrimaryButtonKind PrimaryButtonKind { get; init; } = DialogPrimaryButtonKind.Normal;
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
        public IReadOnlyList<DialogActionLinkItem>? ActionLinks { get; init; }
        public IReadOnlyList<DialogExpandableSectionItem>? ExpandableSections { get; init; }
    }

    public sealed class InterruptDialogState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isActive;
        private bool _isHitTestVisible;
        private string _title = string.Empty;
        private string _content = string.Empty;
        private string? _headerMeta;
        private string? _sectionTitle;
        private bool _showProgress;
        private bool _isProgressIndeterminate;
        private double _progressValue;
        private string _primaryButtonText = string.Empty;
        private string? _secondaryButtonText;
        private bool _isPrimaryDanger;
        private double _overlayOpacity;
        private double _cardOpacity;
        private double _cardScale = 0.96;
        private IBrush _overlayBrush = new SolidColorBrush(Colors.Transparent);
        private IBrush _primaryBackground = new SolidColorBrush(Color.Parse("#FFEFD7"));
        private IBrush _primaryForeground = new SolidColorBrush(Color.Parse("#303034"));
        private IBrush? _primaryBorderBrush;
        private double _primaryBorderThickness;
        private IBrush _secondaryBackground = new SolidColorBrush(Color.Parse("#F8F9FB"));
        private IBrush _secondaryForeground = new SolidColorBrush(Color.Parse("#303034"));
        private IBrush _secondaryBorderBrush = new SolidColorBrush(Color.Parse("#E6E6EB"));
        private double _secondaryBorderThickness = 1;
        private double _cardOffsetX;
        private ObservableCollection<DialogActionLinkItem> _actionLinks = new();
        private ObservableCollection<DialogExpandableSectionItem> _expandableSections = new();

        public InterruptDialogState(Action onConfirm, Action onCancel)
        {
            ConfirmCommand = new RelayCommand(() => onConfirm());
            CancelCommand = new RelayCommand(() => onCancel());
            ApplyPrimaryButtonKind(DialogPrimaryButtonKind.Normal);
            SecondaryBackground = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
            SecondaryForeground = new SolidColorBrush(Color.Parse("#303034"));
            SecondaryBorderBrush = new SolidColorBrush(Color.Parse("#EEAAAAAA"));
            SecondaryBorderThickness = 1;
        }

        public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
        public bool IsHitTestVisible { get => _isHitTestVisible; set => SetField(ref _isHitTestVisible, value); }
        public string Title { get => _title; set => SetField(ref _title, value); }
        public string Content { get => _content; set => SetField(ref _content, value); }
        public string? HeaderMeta { get => _headerMeta; set { if (SetField(ref _headerMeta, value)) OnPropertyChanged(nameof(HasHeaderMeta)); } }
        public string? SectionTitle { get => _sectionTitle; set { if (SetField(ref _sectionTitle, value)) OnPropertyChanged(nameof(HasSectionTitle)); } }
        public bool ShowProgress { get => _showProgress; set => SetField(ref _showProgress, value); }
        public bool IsProgressIndeterminate { get => _isProgressIndeterminate; set => SetField(ref _isProgressIndeterminate, value); }
        public double ProgressValue { get => _progressValue; set => SetField(ref _progressValue, value); }
        public string PrimaryButtonText { get => _primaryButtonText; set => SetField(ref _primaryButtonText, value); }
        public string? SecondaryButtonText { get => _secondaryButtonText; set { if (SetField(ref _secondaryButtonText, value)) OnPropertyChanged(nameof(HasSecondaryButton)); } }
        public bool IsPrimaryDanger { get => _isPrimaryDanger; set => SetField(ref _isPrimaryDanger, value); }
        public double OverlayOpacity { get => _overlayOpacity; set => SetField(ref _overlayOpacity, value); }
        public double CardOpacity { get => _cardOpacity; set => SetField(ref _cardOpacity, value); }
        public double CardScale { get => _cardScale; set => SetField(ref _cardScale, value); }
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
        public ObservableCollection<DialogActionLinkItem> ActionLinks { get => _actionLinks; set { if (SetField(ref _actionLinks, value)) OnPropertyChanged(nameof(HasActionLinks)); } }
        public ObservableCollection<DialogExpandableSectionItem> ExpandableSections { get => _expandableSections; set { if (SetField(ref _expandableSections, value)) OnPropertyChanged(nameof(HasExpandableSections)); } }

        public bool HasSectionTitle => !string.IsNullOrWhiteSpace(SectionTitle);
        public bool HasHeaderMeta => !string.IsNullOrWhiteSpace(HeaderMeta);
        public bool HasSecondaryButton => !string.IsNullOrWhiteSpace(SecondaryButtonText);
        public bool HasActionLinks => ActionLinks.Count > 0;
        public bool HasPairedActionLinks => ActionLinks.Count == 2;
        public DialogActionLinkItem? FirstActionLink => ActionLinks.Count > 0 ? ActionLinks[0] : null;
        public DialogActionLinkItem? SecondActionLink => ActionLinks.Count > 1 ? ActionLinks[1] : null;
        public bool HasExpandableSections => ExpandableSections.Count > 0;

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        internal void ApplyPrimaryButtonKind(DialogPrimaryButtonKind kind)
        {
            IsPrimaryDanger = kind == DialogPrimaryButtonKind.DangerConfirm;

            if (IsPrimaryDanger)
            {
                PrimaryBackground = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
                PrimaryForeground = new SolidColorBrush(Color.Parse("#C73A22"));
                PrimaryBorderBrush = new SolidColorBrush(Color.Parse("#DAE4341D"));
                PrimaryBorderThickness = 1;
                return;
            }

            PrimaryBackground = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
            PrimaryForeground = new SolidColorBrush(Color.Parse("#303034"));
            PrimaryBorderBrush = new SolidColorBrush(Color.Parse("#EEAAAAAA"));
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
            OverlayBrush = isActive
                ? new SolidColorBrush(Color.Parse("#6D8F9999"))
                : new SolidColorBrush(Colors.Transparent);
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

            var automaticItems = ParseThirdPartyNotices(seenIds).ToList();
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

        private IEnumerable<DialogLicenseItem> ParseThirdPartyNotices(HashSet<string> seenIds)
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

                var fullText = ResolveAutomaticFullText(packageName, licenseType);
                yield return new DialogLicenseItem(
                    packageName,
                    null,
                    BuildMetaText(licenseType, version),
                    projectUrl,
                    licenseUrl,
                    fullText);
            }
        }

        private string? ResolveAutomaticFullText(string packageName, string? licenseType)
        {
            var licensesDir = ResolveLicensesDirectory();
            if (licensesDir == null)
            {
                return null;
            }

            var packageCandidates = Directory.GetFiles(licensesDir)
                .Where(path => NormalizeId(Path.GetFileNameWithoutExtension(path)).StartsWith(NormalizeId(packageName), StringComparison.OrdinalIgnoreCase))
                .ToList();

            var packageMatch = packageCandidates.FirstOrDefault();
            if (packageMatch != null)
            {
                return LoadLocalText(Path.GetRelativePath(_baseDirectory, packageMatch));
            }

            if (!string.IsNullOrWhiteSpace(licenseType))
            {
                var licenseTypeMatch = Directory.GetFiles(licensesDir)
                    .FirstOrDefault(path => string.Equals(
                        NormalizeId(Path.GetFileNameWithoutExtension(path)),
                        NormalizeId(licenseType),
                        StringComparison.OrdinalIgnoreCase));

                if (licenseTypeMatch != null)
                {
                    return LoadLocalText(Path.GetRelativePath(_baseDirectory, licenseTypeMatch));
                }
            }

            return null;
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
        private TaskCompletionSource<InterruptDialogResult>? _pendingCompletion;

        public InterruptDialogState State { get; }

        public InterruptDialogService(LicenseCatalogService licenseCatalogService)
        {
            _licenseCatalogService = licenseCatalogService;
            State = new InterruptDialogState(Confirm, Cancel);
            State.ResetVisualState(false);
        }

        public async Task<InterruptDialogResult> ShowAsync(InterruptDialogOptions options)
        {
            _pendingCompletion?.TrySetResult(InterruptDialogResult.None);
            var tcs = new TaskCompletionSource<InterruptDialogResult>();
            _pendingCompletion = tcs;

            await _dispatcher.InvokeAsync(() =>
            {
                State.Title = options.Title ?? string.Empty;
                State.HeaderMeta = options.HeaderMeta;
                State.SectionTitle = options.SectionTitle;
                State.Content = options.Content ?? string.Empty;
                State.PrimaryButtonText = options.PrimaryButtonText ?? string.Empty;
                State.SecondaryButtonText = options.SecondaryButtonText;
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
                State.ActionLinks = new ObservableCollection<DialogActionLinkItem>(options.ActionLinks ?? Array.Empty<DialogActionLinkItem>());
                State.ExpandableSections = new ObservableCollection<DialogExpandableSectionItem>(options.ExpandableSections ?? Array.Empty<DialogExpandableSectionItem>());
                State.ResetVisualState(true);
            });

            return await tcs.Task.ConfigureAwait(false);
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

        public async Task ShowAboutDialogAsync()
        {
            var loc = LocalizationManager.Instance;
            var config = ConfigManager.Config.AppInfo;
            var licenseSection = _licenseCatalogService.BuildLicenseSection(loc["Home_AboutDialog_LicenseSectionTitle"]);

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

        public Task<InterruptDialogResult> ShowDangerConfirmAsync(string title, string content, string confirmText, string cancelText, string? sectionTitle = null)
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

        public Task ShowInfoAsync(string title, string sectionTitle, string content, string acknowledgeText)
        {
            return ShowSingleActionAsync(title, content, acknowledgeText, sectionTitle);
        }

        private void Confirm() => Complete(InterruptDialogResult.Primary);

        private void Cancel() => Complete(State.HasSecondaryButton ? InterruptDialogResult.Secondary : InterruptDialogResult.None);

        private void Complete(InterruptDialogResult result)
        {
            var captured = _pendingCompletion;
            if (captured == null)
            {
                return;
            }

            _pendingCompletion = null;
            _dispatcher.Post(() => State.ResetVisualState(false));

            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                captured.TrySetResult(result);
            });
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
