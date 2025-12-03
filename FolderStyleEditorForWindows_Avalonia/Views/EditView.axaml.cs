using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FolderStyleEditorForWindows;

namespace FolderStyleEditorForWindows.Views
{
    public partial class EditView : UserControl, IDisposable
    {
        public EditView()
        {
            InitializeComponent();
        }

        public void Dispose()
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.ClearIconPreview();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
            {
                if (DataContext is ViewModels.MainViewModel vm)
                {
                    vm.UndoLastChange();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
            {
                if (DataContext is ViewModels.MainViewModel vm && vm.SaveCommand.CanExecute(null))
                    vm.SaveCommand.Execute(null);
            }
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            var btnBack = this.FindControl<Button>("btnBack");
            if (btnBack != null)
            {
                btnBack.Click += BtnBack_Click;
            }

            var btnMin = this.FindControl<Button>("btnMin");
            if (btnMin != null)
            {
                btnMin.Click += BtnMin_Click;
            }

            var btnClose = this.FindControl<Button>("btnClose");
            if (btnClose != null)
            {
                btnClose.Click += BtnClose_Click;
            }

            var btnPickDir = this.FindControl<Button>("btnPickDir");
            if (btnPickDir != null)
            {
                btnPickDir.Click += BtnPickDir_Click;
            }

            var btnPickIcon = this.FindControl<Button>("btnPickIcon");
            if (btnPickIcon != null)
            {
                btnPickIcon.Click += BtnPickIcon_Click;
            }

            var btnOpenExplorer = this.FindControl<Button>("btnOpenExplorer");
            if (btnOpenExplorer != null)
            {
                btnOpenExplorer.Click += BtnOpenExplorer_Click;
            }

            var aliasInput = this.FindControl<TextBox>("aliasInput");
            if (aliasInput != null)
            {
                aliasInput.GotFocus += AliasInput_GotFocus;
                aliasInput.LostFocus += AliasInput_LostFocus;
                aliasInput.KeyDown += AliasInput_KeyDown;
                aliasInput.TextChanged += AliasInput_TextChanged;

                // Add drag and drop support for alias input
                aliasInput.AddHandler(DragDrop.DragOverEvent, AliasInput_DragOver);
                aliasInput.AddHandler(DragDrop.DropEvent, AliasInput_Drop);
            }
            
            var iconInput = this.FindControl<TextBox>("iconInput");
            if (iconInput != null)
            {
                iconInput.LostFocus += IconInput_LostFocus;
            }

            var iconCounterDisplay = this.FindControl<TextBlock>("iconCounterDisplay");
            if (iconCounterDisplay != null)
            {
                iconCounterDisplay.PointerWheelChanged += IconCounterDisplay_PointerWheelChanged;
                iconCounterDisplay.PointerPressed += IconCounterDisplay_PointerPressed;
                iconCounterDisplay.PointerEntered += IconCounterDisplay_PointerEntered;
                iconCounterDisplay.PointerExited += IconCounterDisplay_PointerExited;
            }

            var iconCounterHost = this.FindControl<Border>("iconCounterHost");
            if (iconCounterHost != null)
            {
                iconCounterHost.AddHandler(PointerWheelChangedEvent, IconCounterDisplay_PointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
                EnsureTooltipAlwaysShows(iconCounterHost);
            }

            var iconCounterInput = this.FindControl<TextBox>("iconCounterInput");
            if (iconCounterInput != null)
            {
                iconCounterInput.PointerWheelChanged += IconCounterDisplay_PointerWheelChanged;
                iconCounterInput.KeyDown += IconCounterInput_KeyDown;
                iconCounterInput.LostFocus += IconCounterInput_LostFocus;
            }

            var editScrollViewer = this.FindControl<ScrollViewer>("editScrollViewer");
            if (editScrollViewer != null && iconCounterHost != null)
            {
                editScrollViewer.AddHandler(PointerWheelChangedEvent, (sender, e) =>
                {
                    if (iconCounterHost.IsPointerOver)
                    {
                        e.Handled = true;
                    }
                }, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
        }

        private void AliasInput_GotFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm && vm.IsAliasAsPlaceholder)
            {
                vm.IsAliasAsPlaceholder = false;
            }
        }

        private void AliasInput_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.RestoreDefaultAliasIfNeeded();
            }
        }

        private void IconInput_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.RestoreDefaultIconIfNeeded();
            }
        }

        private void IconCounterDisplay_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                var delta = e.Delta.Y > 0 ? -1 : 1;
                vm.MoveIconIndex(delta, wrap: true);
                e.Handled = true;
            }
        }

        private void IconCounterDisplay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (DataContext is ViewModels.MainViewModel vm)
            {
                BeginIconCounterEdit(vm);
                e.Handled = true;
            }
        }

        private void IconCounterDisplay_PointerEntered(object? sender, PointerEventArgs e)
        {
            SetIconCounterScale(1.06);
            var host = this.FindControl<Border>("iconCounterHost");
            if (host != null && ToolTip.GetTip(host) != null)
            {
                ToolTip.SetIsOpen(host, true);
            }
        }

        private void IconCounterDisplay_PointerExited(object? sender, PointerEventArgs e)
        {
            SetIconCounterScale(1.0);
            var host = this.FindControl<Border>("iconCounterHost");
            if (host != null)
            {
                ToolTip.SetIsOpen(host, false);
            }
        }

        private void IconCounterInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ViewModels.MainViewModel vm)
            {
                CommitIconCounterInput(sender as TextBox, vm);
                e.Handled = true;
            }
        }

        private void IconCounterInput_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                CommitIconCounterInput(sender as TextBox, vm);
            }
        }

        private void CommitIconCounterInput(TextBox? textBox, ViewModels.MainViewModel vm)
        {
            if (textBox == null)
            {
                EndIconCounterEdit();
                return;
            }
            if (!vm.IsIconCounterVisible || !vm.IconCounterDenominator.Any(char.IsDigit))
            {
                textBox.Text = vm.IconCounterNumerator;
                EndIconCounterEdit();
                return;
            }

            if (int.TryParse(textBox.Text, out var value))
            {
                vm.JumpToIconIndex(value);
            }
            else
            {
                textBox.Text = vm.IconCounterNumerator;
            }
            EndIconCounterEdit();
        }

        private void AliasInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is ViewModels.MainViewModel vm && vm.SaveCommand.CanExecute(null))
                {
                    vm.SaveCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void AliasInput_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Text?.Length >= 260)
                {
                    // Set to red color for warning (#FFFF5555)
                    textBox.Foreground = new SolidColorBrush(Color.Parse("#FFFF5555"));
                }
                else
                {
                    // Restore default color - use the default foreground from resources
                    if (textBox.TryFindResource("Fg1Brush", out var brush) && brush is SolidColorBrush colorBrush)
                    {
                        textBox.Foreground = colorBrush;
                    }
                    else
                    {
                        textBox.Foreground = Brushes.White;
                    }
                }
            }
        }

        private void AliasInput_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Text))
            {
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void AliasInput_Drop(object? sender, DragEventArgs e)
        {
            if (sender is TextBox textBox && e.Data.Contains(DataFormats.Text))
            {
                string textData = e.Data.GetText()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(textData))
                {
                    // Clean the text by removing quotes
                    string cleanedText = textData.Trim('"', '\'');

                    if (DataContext is ViewModels.MainViewModel vm)
                    {
                        // Set the alias directly - the property setter will handle undo recording
                        vm.Alias = cleanedText;
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }

                e.Handled = true;
            }
        }
 
        private void BtnBack_Click(object? sender, RoutedEventArgs e)
        {
            if (this.VisualRoot is MainWindow mainWindow)
            {
                // TODO: Add "unsaved changes" dialog logic
                mainWindow.GoToHomeView();
            }
        }

        private void BtnMin_Click(object? sender, RoutedEventArgs e)
        {
            if (this.VisualRoot is Window window)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        private void BtnClose_Click(object? sender, RoutedEventArgs e)
        {
            if (this.VisualRoot is Window window)
            {
                window.Close();
            }
        }

        private async void BtnPickDir_Click(object? sender, RoutedEventArgs e)
        {
            if (this.VisualRoot is Window window)
            {
                var topLevel = TopLevel.GetTopLevel(window);
                if (topLevel != null)
                {
                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "选择文件夹",
                        AllowMultiple = false
                    });

                    if (folders.Count > 0)
                    {
                        var pathInput = this.FindControl<TextBox>("pathInput");
                        if (pathInput != null)
                        {
                            pathInput.Text = folders[0].Path.LocalPath;
                        }
                    }
                }
            }
        }

        private async void BtnPickIcon_Click(object? sender, RoutedEventArgs e)
        {
            if (this.VisualRoot is Window window)
            {
                var topLevel = TopLevel.GetTopLevel(window);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "选择图标文件",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("图标文件")
                            {
                                Patterns = new[] { "*.ico", "*.exe", "*.dll", "*.png", "*.jpg", "*.jpeg", "*.svg", "*.gif", "*.bmp" }
                            }
                        }
                    });

                    if (files.Count > 0)
                    {
                        var iconInput = this.FindControl<TextBox>("iconInput");
                        if (iconInput != null)
                        {
                            iconInput.Text = files[0].Path.LocalPath;
                        }
                    }
                }
            }
        }

        private void BtnOpenExplorer_Click(object? sender, RoutedEventArgs e)
        {
            var pathInput = this.FindControl<TextBox>("pathInput");
            if (pathInput != null && !string.IsNullOrEmpty(pathInput.Text))
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", pathInput.Text);
                }
                catch (Exception ex)
                {
                    // Handle error - could show a toast or dialog
                    Console.WriteLine($"Failed to open explorer: {ex.Message}");
                }
            }
        }

        private void BeginIconCounterEdit(ViewModels.MainViewModel vm)
        {
            var display = this.FindControl<TextBlock>("iconCounterDisplay");
            var input = this.FindControl<TextBox>("iconCounterInput");
            if (display == null || input == null) return;

            display.IsVisible = false;
            input.IsVisible = true;
            input.Text = vm.IconCounterNumerator;
            input.CaretIndex = input.Text?.Length ?? 0;
            input.Focus();
            input.SelectAll();
        }

        private void EndIconCounterEdit()
        {
            var display = this.FindControl<TextBlock>("iconCounterDisplay");
            var input = this.FindControl<TextBox>("iconCounterInput");
            if (display == null || input == null) return;

            input.IsVisible = false;
            display.IsVisible = true;
            SetIconCounterScale(1.0);
        }

        private void SetIconCounterScale(double scale)
        {
            var display = this.FindControl<TextBlock>("iconCounterDisplay");
            if (display?.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.ScaleX = scale;
                scaleTransform.ScaleY = scale;
            }
        }

        private void EnsureTooltipAlwaysShows(Control? control)
        {
            if (control == null) return;
            control.PointerEntered += (s, e) =>
            {
                var tip = ToolTip.GetTip(control);
                if (tip != null)
                {
                    ToolTip.SetIsOpen(control, true);
                }
            };
            control.PointerExited += (s, e) => ToolTip.SetIsOpen(control, false);
        }

 }
}
