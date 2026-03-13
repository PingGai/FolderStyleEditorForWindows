using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FolderStyleEditorForWindows.ViewModels;

namespace FolderStyleEditorForWindows.Services
{
    public sealed class DialogFormSectionItem : INotifyPropertyChanged
    {
        private string _title;
        private string? _description;
        private ObservableCollection<DialogFormFieldItem> _items;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DialogFormSectionItem(string title, IEnumerable<DialogFormFieldItem> items, string? description = null)
        {
            _title = title;
            _description = description;
            _items = new ObservableCollection<DialogFormFieldItem>(items);
        }

        public string Title
        {
            get => _title;
            set => SetField(ref _title, value);
        }

        public string? Description
        {
            get => _description;
            set
            {
                if (SetField(ref _description, value))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasDescription)));
                }
            }
        }

        public ObservableCollection<DialogFormFieldItem> Items
        {
            get => _items;
            set
            {
                if (SetField(ref _items, value))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasItems)));
                }
            }
        }

        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
        public bool HasItems => Items.Count > 0;

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public abstract class DialogFormFieldItem : INotifyPropertyChanged
    {
        private string _label;
        private string? _description;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected DialogFormFieldItem(string label, string? description = null)
        {
            _label = label;
            _description = description;
        }

        public string Label
        {
            get => _label;
            set => SetField(ref _label, value);
        }

        public string? Description
        {
            get => _description;
            set
            {
                if (SetField(ref _description, value))
                {
                    OnPropertyChanged(nameof(HasDescription));
                }
            }
        }

        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class DialogNumberFieldItem : DialogFormFieldItem
    {
        private readonly Action<int> _applyValue;
        private readonly string _rangeErrorFormat;
        private string _text;
        private string? _errorText;
        private bool _isSyncing;

        public event EventHandler? ValidationStateChanged;

        public DialogNumberFieldItem(
            string label,
            string? description,
            int initialValue,
            int minValue,
            int maxValue,
            string rangeErrorFormat,
            Action<int> applyValue)
            : base(label, description)
        {
            MinValue = minValue;
            MaxValue = maxValue;
            _rangeErrorFormat = rangeErrorFormat;
            _applyValue = applyValue;
            _text = initialValue.ToString(CultureInfo.InvariantCulture);
        }

        public int MinValue { get; }
        public int MaxValue { get; }

        public string Text
        {
            get => _text;
            set
            {
                if (!SetField(ref _text, value))
                {
                    return;
                }

                ValidateAndApply();
            }
        }

        public string? ErrorText
        {
            get => _errorText;
            private set
            {
                if (SetField(ref _errorText, value))
                {
                    OnPropertyChanged(nameof(HasError));
                    ValidationStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

        public void SyncFromValue(int value)
        {
            _isSyncing = true;
            Text = value.ToString(CultureInfo.InvariantCulture);
            ErrorText = null;
            _isSyncing = false;
        }

        private void ValidateAndApply()
        {
            if (_isSyncing)
            {
                return;
            }

            if (!int.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue) ||
                parsedValue < MinValue ||
                parsedValue > MaxValue)
            {
                ErrorText = string.Format(CultureInfo.CurrentCulture, _rangeErrorFormat, MinValue, MaxValue);
                return;
            }

            ErrorText = null;
            _applyValue(parsedValue);
        }
    }

    public sealed class DialogToggleFieldItem : DialogFormFieldItem
    {
        private readonly Action<bool> _applyValue;
        private bool _value;
        private bool _isSyncing;

        public DialogToggleFieldItem(string label, string? description, bool initialValue, Action<bool> applyValue)
            : base(label, description)
        {
            _value = initialValue;
            _applyValue = applyValue;
        }

        public bool Value
        {
            get => _value;
            set
            {
                if (!SetField(ref _value, value))
                {
                    return;
                }

                if (!_isSyncing)
                {
                    _applyValue(value);
                }
            }
        }

        public void SyncFromValue(bool value)
        {
            _isSyncing = true;
            Value = value;
            _isSyncing = false;
        }
    }

    public sealed class DialogStatusFieldItem : DialogFormFieldItem
    {
        private string _value;
        private IBrush _valueForeground;
        private ICommand? _command;

        public DialogStatusFieldItem(string label, string? description, string value, IBrush? valueForeground = null)
            : base(label, description)
        {
            _value = value;
            _valueForeground = valueForeground ?? new SolidColorBrush(Color.Parse("#303034"));
        }

        public string Value
        {
            get => _value;
            set => SetField(ref _value, value);
        }

        public IBrush ValueForeground
        {
            get => _valueForeground;
            set => SetField(ref _valueForeground, value);
        }

        public ICommand? Command
        {
            get => _command;
            set
            {
                if (SetField(ref _command, value))
                {
                    OnPropertyChanged(nameof(HasCommand));
                }
            }
        }

        public bool HasCommand => Command != null;
    }

    public sealed class DialogActionFieldItem : DialogFormFieldItem
    {
        private string _buttonText;
        private ICommand _command;
        private IBrush _buttonBackground;
        private IBrush _buttonForeground;
        private IBrush _buttonBorderBrush;
        private double _buttonBorderThickness;
        private bool _isDefault;

        public DialogActionFieldItem(
            string label,
            string? description,
            string buttonText,
            ICommand command,
            IBrush? buttonBackground = null,
            IBrush? buttonForeground = null,
            IBrush? buttonBorderBrush = null,
            double buttonBorderThickness = 1,
            bool isDefault = false)
            : base(label, description)
        {
            _buttonText = buttonText;
            _command = command;
            _buttonBackground = buttonBackground ?? new SolidColorBrush(Color.Parse("#FFFFFFFF"));
            _buttonForeground = buttonForeground ?? new SolidColorBrush(Color.Parse("#303034"));
            _buttonBorderBrush = buttonBorderBrush ?? new SolidColorBrush(Color.Parse("#EEAAAAAA"));
            _buttonBorderThickness = buttonBorderThickness;
            _isDefault = isDefault;
        }

        public string ButtonText
        {
            get => _buttonText;
            set => SetField(ref _buttonText, value);
        }

        public ICommand Command
        {
            get => _command;
            set => SetField(ref _command, value);
        }

        public IBrush ButtonBackground
        {
            get => _buttonBackground;
            set => SetField(ref _buttonBackground, value);
        }

        public IBrush ButtonForeground
        {
            get => _buttonForeground;
            set => SetField(ref _buttonForeground, value);
        }

        public IBrush ButtonBorderBrush
        {
            get => _buttonBorderBrush;
            set => SetField(ref _buttonBorderBrush, value);
        }

        public double ButtonBorderThickness
        {
            get => _buttonBorderThickness;
            set => SetField(ref _buttonBorderThickness, value);
        }

        public bool IsDefault
        {
            get => _isDefault;
            set => SetField(ref _isDefault, value);
        }
    }

    public sealed class DialogPassiveChoiceCardItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public DialogPassiveChoiceCardItem(string key, string title, string? description = null)
        {
            Key = key;
            Title = title;
            Description = description;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Key { get; }
        public string Title { get; }
        public string? Description { get; }
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public sealed class DialogTabItem : INotifyPropertyChanged
    {
        private string _title;
        private bool _isSelected;
        private ICommand _command;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DialogTabItem(string title, ICommand command, bool isSelected = false)
        {
            _title = title;
            _command = command;
            _isSelected = isSelected;
        }

        public string Title
        {
            get => _title;
            set => SetField(ref _title, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public ICommand Command
        {
            get => _command;
            set => SetField(ref _command, value);
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public sealed class DialogChoiceOptionItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public DialogChoiceOptionItem(string key, string title, string? description = null)
        {
            Key = key;
            Title = title;
            Description = description;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Key { get; }
        public string Title { get; }
        public string? Description { get; }
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
        public ICommand? SelectCommand { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public sealed class DialogChoiceFieldItem : DialogFormFieldItem
    {
        private readonly Action<string> _applySelection;
        private string _selectedKey;
        private bool _isSyncing;

        public DialogChoiceFieldItem(
            string label,
            string? description,
            IEnumerable<DialogChoiceOptionItem> options,
            string initialKey,
            Action<string> applySelection)
            : base(label, description)
        {
            Options = new ObservableCollection<DialogChoiceOptionItem>(options);
            _selectedKey = initialKey;
            _applySelection = applySelection;
            SelectCommand = new RelayCommand<string?>(key =>
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    Select(key);
                }
            });
            foreach (var option in Options)
            {
                option.SelectCommand = SelectCommand;
            }
            ApplySelection(initialKey, notify: false);
        }

        public ObservableCollection<DialogChoiceOptionItem> Options { get; }
        public ICommand SelectCommand { get; }

        public string SelectedKey
        {
            get => _selectedKey;
            set
            {
                if (!SetField(ref _selectedKey, value))
                {
                    return;
                }

                ApplySelection(value, notify: !_isSyncing);
            }
        }

        public void Select(string key)
        {
            SelectedKey = key;
        }

        public void SyncFromValue(string key)
        {
            _isSyncing = true;
            SelectedKey = key;
            _isSyncing = false;
        }

        private void ApplySelection(string key, bool notify)
        {
            foreach (var option in Options)
            {
                option.IsSelected = string.Equals(option.Key, key, StringComparison.Ordinal);
            }

            if (notify)
            {
                _applySelection(key);
            }
        }
    }

    public sealed class DialogImagePreviewFieldItem : DialogFormFieldItem
    {
        private Bitmap? _previewImage;
        private double _previewHeight;

        public DialogImagePreviewFieldItem(
            string label,
            Bitmap? previewImage,
            double previewHeight = 220,
            string? description = null)
            : base(label, description)
        {
            _previewImage = previewImage;
            _previewHeight = previewHeight;
        }

        public Bitmap? PreviewImage
        {
            get => _previewImage;
            set
            {
                if (SetField(ref _previewImage, value))
                {
                    OnPropertyChanged(nameof(HasPreviewImage));
                }
            }
        }

        public double PreviewHeight
        {
            get => _previewHeight;
            set => SetField(ref _previewHeight, value);
        }

        public bool HasPreviewImage => PreviewImage != null;
    }

    public sealed class DialogImageCropFieldItem : DialogFormFieldItem
    {
        private readonly Action<DialogImageCropFieldItem>? _resetAction;
        private Bitmap? _previewImage;
        private double _selectionX;
        private double _selectionY;
        private double _selectionWidth;
        private double _selectionHeight;
        private double _cornerRadiusNormalized;
        private string? _selectionSummary;

        public DialogImageCropFieldItem(
            string label,
            Bitmap? previewImage,
            string? description = null,
            Action<DialogImageCropFieldItem>? resetAction = null)
            : base(label, description)
        {
            _previewImage = previewImage;
            _resetAction = resetAction;
            ResetCropCommand = new RelayCommand(ResetSelection);
            SetSelection(0, 0, 1, 1, updateSummary: true);
        }

        public Bitmap? PreviewImage
        {
            get => _previewImage;
            set
            {
                if (SetField(ref _previewImage, value))
                {
                    OnPropertyChanged(nameof(HasPreviewImage));
                }
            }
        }

        public bool HasPreviewImage => PreviewImage != null;

        public double SelectionX
        {
            get => _selectionX;
            private set => SetField(ref _selectionX, value);
        }

        public double SelectionY
        {
            get => _selectionY;
            private set => SetField(ref _selectionY, value);
        }

        public double SelectionWidth
        {
            get => _selectionWidth;
            private set => SetField(ref _selectionWidth, value);
        }

        public double SelectionHeight
        {
            get => _selectionHeight;
            private set => SetField(ref _selectionHeight, value);
        }

        public string? SelectionSummary
        {
            get => _selectionSummary;
            private set
            {
                if (SetField(ref _selectionSummary, value))
                {
                    OnPropertyChanged(nameof(HasSelectionSummary));
                }
            }
        }

        public bool HasSelectionSummary => !string.IsNullOrWhiteSpace(SelectionSummary);

        public double CornerRadiusNormalized
        {
            get => _cornerRadiusNormalized;
            private set => SetField(ref _cornerRadiusNormalized, value);
        }

        public ICommand ResetCropCommand { get; }

        public void SetSelection(double x, double y, double width, double height, bool updateSummary = true)
        {
            var clampedWidth = Math.Clamp(width, 0.05, 1.0);
            var clampedHeight = Math.Clamp(height, 0.05, 1.0);
            var clampedX = Math.Clamp(x, 0.0, 1.0 - clampedWidth);
            var clampedY = Math.Clamp(y, 0.0, 1.0 - clampedHeight);

            SelectionX = clampedX;
            SelectionY = clampedY;
            SelectionWidth = clampedWidth;
            SelectionHeight = clampedHeight;

            if (updateSummary)
            {
                UpdateSelectionSummary();
            }
        }

        public void SetCornerRadius(double normalizedCornerRadius, bool updateSummary = true)
        {
            CornerRadiusNormalized = Math.Clamp(normalizedCornerRadius, 0.0, 0.5);
            if (updateSummary)
            {
                UpdateSelectionSummary();
            }
        }

        private void UpdateSelectionSummary()
        {
            SelectionSummary = string.Format(
                    CultureInfo.InvariantCulture,
                    "X {0:P0} | Y {1:P0} | W {2:P0} | H {3:P0} | R {4:P0}",
                    SelectionX,
                    SelectionY,
                    SelectionWidth,
                    SelectionHeight,
                    CornerRadiusNormalized * 2d);
        }

        private void ResetSelection()
        {
            if (_resetAction != null)
            {
                _resetAction(this);
                return;
            }

            SetSelection(0, 0, 1, 1, updateSummary: true);
            SetCornerRadius(0, updateSummary: true);
        }
    }
}
