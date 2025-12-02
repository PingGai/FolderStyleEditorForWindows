using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
            if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
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
            }
            
            var iconInput = this.FindControl<TextBox>("iconInput");
            if (iconInput != null)
            {
                iconInput.LostFocus += IconInput_LostFocus;
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

 }
}