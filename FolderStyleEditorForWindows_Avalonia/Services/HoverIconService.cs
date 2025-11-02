using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using FolderStyleEditorForWindows.Models;
using FolderStyleEditorForWindows.ViewModels;

namespace FolderStyleEditorForWindows.Services
{
    public class HoverIconService
    {
        public HoverIconViewModel ViewModel { get; }
        private readonly AppFeaturesConfig _config;

        public HoverIconService(HoverIconViewModel viewModel)
        {
            ViewModel = viewModel;
            _config = ConfigManager.Features;
        }

        public void UpdatePosition(Point position)
        {
            ViewModel.Position = new Point(position.X - 16, position.Y + 8);
        }

        public void ShowPinIcon(string state = "Ready")
        {
            // 同时设置 SVG 和 PNG 路径以供测试
            ViewModel.MainIconPath = _config.PinIcon.MainIcon;
            ViewModel.MainIconPngPath = _config.PinIcon.TestPngPath;
            
            var badge = _config.PinIcon.BadgeIcons.FirstOrDefault(b => b.State == state);
            ViewModel.BadgeIconPath = badge?.IconPath;
            ViewModel.IsVisible = true;
        }

        public void ShowFileIcon(IDataObject data, string currentDirectory)
        {
            var file = data.GetFiles()?.FirstOrDefault();
            if (file == null)
            {
                ShowErrorIcon();
                return;
            }

            var filePath = file.Path.LocalPath;
            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

            string status;
            if (_config.HoverIcon.FileTypes.Supported.Contains(extension))
            {
                status = "Supported";
            }
            else if (_config.HoverIcon.FileTypes.SupportedToConvert.Contains(extension))
            {
                status = "SupportedToConvert";
            }
            else
            {
                status = "Error";
                ShowErrorIcon();
                return;
            }

            var mainIconRule = _config.HoverIcon.MainIcons.FirstOrDefault(r => r.Extensions.Contains(extension));
            ViewModel.MainIconPath = mainIconRule?.IconPath ?? _config.HoverIcon.DefaultIcon;

            var badgeIconRule = _config.HoverIcon.BadgeIcons.FirstOrDefault(r => r.Status == status);
            ViewModel.BadgeIconPath = badgeIconRule?.IconPath;
            
            ViewModel.IsVisible = true;
        }

        public void ShowErrorIcon()
        {
            ViewModel.MainIconPath = _config.HoverIcon.ErrorIcon;
            ViewModel.BadgeIconPath = null;
            ViewModel.IsVisible = true;
        }

        public void Hide()
        {
            ViewModel.IsVisible = false;
        }
    }
}