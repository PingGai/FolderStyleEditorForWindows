using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using FolderStyleEditorForWindows.Models;
using FolderStyleEditorForWindows.Services;

namespace FolderStyleEditorForWindows.ViewModels
{
    public partial class DebugOverlayViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private bool _showSvgTest;

        [ObservableProperty]
        private bool _showPngTest;

        [ObservableProperty]
        private bool _showHoverIconClone;

        private const string DefaultSvgTestPath = "avares://FolderStyleEditorForWindows/Resources/SVG/book-image.svg";
        private const string DefaultPngTestPath = "avares://FolderStyleEditorForWindows/Resources/PNG/test-pin.png";

        public string SvgTestPath { get; }
        public string PngTestPath { get; }
        public Bitmap? PngTestImage { get; }
        public IBrush OverlayBackgroundBrush { get; }

        // This will be bound to the main HoverIconViewModel instance
        public HoverIconViewModel HoverIconClone { get; }

        public DebugOverlayViewModel(AppConfig config, HoverIconViewModel hoverIconClone)
        {
            _isEnabled = config.Debug.EnableOverlay;
            _showSvgTest = config.Debug.ShowSvgTest;
            _showPngTest = config.Debug.ShowPngTest;
            _showHoverIconClone = config.Debug.ShowHoverIconClone;
            HoverIconClone = hoverIconClone;
            SvgTestPath = string.IsNullOrWhiteSpace(config.Debug.SvgTestPath)
                ? DefaultSvgTestPath
                : config.Debug.SvgTestPath;
            PngTestPath = string.IsNullOrWhiteSpace(config.Debug.PngTestPath)
                ? DefaultPngTestPath
                : config.Debug.PngTestPath;
            PngTestImage = LoadBitmap(PngTestPath);
            OverlayBackgroundBrush = BuildOverlayBrush(config.Debug);
        }

        private static Bitmap? LoadBitmap(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
                {
                    if (string.Equals(uri.Scheme, "avares", StringComparison.OrdinalIgnoreCase))
                    {
                        using var stream = AssetLoader.Open(uri);
                        return new Bitmap(stream);
                    }

                    if (uri.IsFile && File.Exists(uri.LocalPath))
                    {
                        return new Bitmap(uri.LocalPath);
                    }
                }
                else if (File.Exists(path))
                {
                    return new Bitmap(path);
                }
            }
            catch
            {
                // Ignored - fallback will handle null bitmap
            }

            return null;
        }

        private static IBrush BuildOverlayBrush(DebugConfig debugConfig)
        {
            // default semi-transparent white for fallback
            var defaultColor = Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);

            if (!debugConfig.ShowHoverIconBackgroundColor)
            {
                return new SolidColorBrush(defaultColor);
            }

            var colorString = string.IsNullOrWhiteSpace(debugConfig.HoverIconBgColor)
                ? "#DD4444"
                : debugConfig.HoverIconBgColor;

            try
            {
                var parsed = Color.Parse(colorString);
                return new SolidColorBrush(parsed);
            }
            catch (FormatException)
            {
                return new SolidColorBrush(defaultColor);
            }
        }
    }
}