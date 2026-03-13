using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Svg.Skia;

namespace FolderStyleEditorForWindows.Services
{
    public enum ImageFitMode
    {
        FillHeight,
        FillWidth,
        ManualCrop
    }

    public readonly record struct ImageCropSelection(double X, double Y, double Width, double Height, double CornerRadius = 0)
    {
        public ImageCropSelection Clamp()
        {
            var width = Math.Clamp(Width, 0.05, 1.0);
            var height = Math.Clamp(Height, 0.05, 1.0);
            var x = Math.Clamp(X, 0.0, 1.0 - width);
            var y = Math.Clamp(Y, 0.0, 1.0 - height);
            var cornerRadius = Math.Clamp(CornerRadius, 0.0, 0.5);
            return new ImageCropSelection(x, y, width, height, cornerRadius);
        }
    }

    public sealed class ImageToIcoRequest
    {
        public required string SourcePath { get; init; }
        public required string TargetFolderPath { get; init; }
        public required ImageFitMode FitMode { get; init; }
        public ImageCropSelection? CropSelection { get; init; }
    }

    public sealed class IcoGenerationResult
    {
        public required string OutputPath { get; init; }
        public required string RelativeOutputPath { get; init; }
        public required IReadOnlyList<int> Sizes { get; init; }
    }

    public sealed class LoadedImageToIcoSource : IDisposable
    {
        public required string SourcePath { get; init; }
        public required string FileName { get; init; }
        public required int PixelWidth { get; init; }
        public required int PixelHeight { get; init; }
        public required bool IsVector { get; init; }
        public required Bitmap PreviewBitmap { get; init; }

        public void Dispose()
        {
            PreviewBitmap.Dispose();
        }
    }

    public sealed class ImageToIcoService
    {
        private static readonly string[] SupportedExtensions = [".svg", ".png", ".jpg", ".jpeg", ".bmp", ".webp"];
        private static readonly int[] IconSizes = [16, 24, 32, 48, 64, 128, 256];

        public bool IsSupportedImagePath(string path)
        {
            var extension = Path.GetExtension(path);
            return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<LoadedImageToIcoSource> LoadPreviewAsync(string sourcePath, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var render = RenderBitmap(sourcePath, 512);
                using var sourceBitmap = render.Bitmap;
                var previewBitmap = ToAvaloniaBitmap(sourceBitmap);
                return new LoadedImageToIcoSource
                {
                    SourcePath = sourcePath,
                    FileName = Path.GetFileName(sourcePath),
                    PixelWidth = render.Width,
                    PixelHeight = render.Height,
                    IsVector = render.IsVector,
                    PreviewBitmap = previewBitmap
                };
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IcoGenerationResult> GenerateAsync(ImageToIcoRequest request, CancellationToken cancellationToken)
        {
            return await Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outputDirectory = Path.Combine(request.TargetFolderPath, ".ICON");
                EnsureHiddenIconDirectory(outputDirectory);

                var outputPath = BuildOutputPath(outputDirectory, request.SourcePath, request.FitMode, request.CropSelection);
                var icoBytes = BuildIcoBytes(request.SourcePath, request.FitMode, request.CropSelection, cancellationToken);
                await File.WriteAllBytesAsync(outputPath, icoBytes, cancellationToken).ConfigureAwait(false);

                return new IcoGenerationResult
                {
                    OutputPath = outputPath,
                    RelativeOutputPath = PathHelper.GetRelativePath(request.TargetFolderPath, outputPath),
                    Sizes = IconSizes
                };
            }, cancellationToken).ConfigureAwait(false);
        }

        private static void EnsureHiddenIconDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                return;
            }

            var directory = Directory.CreateDirectory(path);
            directory.Attributes |= FileAttributes.Hidden | FileAttributes.System;
        }

        private static string BuildOutputPath(string outputDirectory, string sourcePath, ImageFitMode fitMode, ImageCropSelection? cropSelection)
        {
            var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath));
            var suffix = fitMode switch
            {
                ImageFitMode.FillHeight => "fill-height",
                ImageFitMode.FillWidth => "fill-width",
                ImageFitMode.ManualCrop => $"manual-{BuildCropHash(cropSelection)}",
                _ => "icon"
            };

            return Path.Combine(outputDirectory, $"{baseName}-{suffix}.ico");
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                builder.Append(invalid.Contains(ch) ? '-' : ch);
            }

            return builder.ToString().Trim();
        }

        private static string BuildCropHash(ImageCropSelection? cropSelection)
        {
            if (cropSelection is not { } crop)
            {
                return "full";
            }

            var normalized = crop.Clamp();
            var raw = string.Format(
                CultureInfo.InvariantCulture,
                "{0:F4}|{1:F4}|{2:F4}|{3:F4}",
                normalized.X,
                normalized.Y,
                normalized.Width,
                normalized.Height) + "|" + normalized.CornerRadius.ToString("F4", CultureInfo.InvariantCulture);
            var hash = SHA1.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).Substring(0, 8).ToLowerInvariant();
        }

        private static byte[] BuildIcoBytes(string sourcePath, ImageFitMode fitMode, ImageCropSelection? cropSelection, CancellationToken cancellationToken)
        {
            var encodedFrames = new List<byte[]>(IconSizes.Length);

            foreach (var size in IconSizes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rendered = RenderBitmap(sourcePath, size);
                using var sourceBitmap = rendered.Bitmap;
                using var outputBitmap = CreateSizedBitmap(sourceBitmap, size, fitMode, cropSelection);
                using var image = SKImage.FromBitmap(outputBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                encodedFrames.Add(data.ToArray());
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)encodedFrames.Count);

            var offset = 6 + (16 * encodedFrames.Count);
            for (var index = 0; index < IconSizes.Length; index++)
            {
                var size = IconSizes[index];
                var frame = encodedFrames[index];
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(frame.Length);
                writer.Write(offset);
                offset += frame.Length;
            }

            foreach (var frame in encodedFrames)
            {
                writer.Write(frame);
            }

            return stream.ToArray();
        }

        private static SKBitmap CreateSizedBitmap(SKBitmap sourceBitmap, int size, ImageFitMode fitMode, ImageCropSelection? cropSelection)
        {
            var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
            var output = new SKBitmap(info);
            using var canvas = new SKCanvas(output);
            canvas.Clear(SKColors.Transparent);

            var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            var sourceRect = ResolveSourceRect(sourceBitmap.Width, sourceBitmap.Height, fitMode, cropSelection);
            var destinationRect = ResolveDestinationRect(sourceBitmap.Width, sourceBitmap.Height, size, fitMode, cropSelection, sourceRect);
            var cornerRadius = ResolveCornerRadius(size, fitMode, cropSelection);
            if (cornerRadius > 0)
            {
                canvas.Save();
                var roundRect = new SKRoundRect(new SKRect(0, 0, size, size), cornerRadius, cornerRadius);
                canvas.ClipRoundRect(roundRect, antialias: true);
                canvas.DrawBitmap(sourceBitmap, sourceRect, destinationRect, paint);
                canvas.Restore();
            }
            else
            {
                canvas.DrawBitmap(sourceBitmap, sourceRect, destinationRect, paint);
            }
            canvas.Flush();
            return output;
        }

        private static float ResolveCornerRadius(int size, ImageFitMode fitMode, ImageCropSelection? cropSelection)
        {
            if (fitMode != ImageFitMode.ManualCrop || cropSelection is not { } crop)
            {
                return 0;
            }

            var normalized = crop.Clamp();
            return (float)(Math.Clamp(normalized.CornerRadius, 0.0, 0.5) * size);
        }

        private static SKRect ResolveSourceRect(int sourceWidth, int sourceHeight, ImageFitMode fitMode, ImageCropSelection? cropSelection)
        {
            if (fitMode == ImageFitMode.ManualCrop && cropSelection is { } crop)
            {
                var normalized = crop.Clamp();
                return new SKRect(
                    (float)(normalized.X * sourceWidth),
                    (float)(normalized.Y * sourceHeight),
                    (float)((normalized.X + normalized.Width) * sourceWidth),
                    (float)((normalized.Y + normalized.Height) * sourceHeight));
            }

            var full = new SKRect(0, 0, sourceWidth, sourceHeight);
            if (fitMode == ImageFitMode.FillHeight)
            {
                var targetWidth = sourceHeight;
                if (sourceWidth <= targetWidth)
                {
                    return full;
                }

                var excess = sourceWidth - targetWidth;
                return new SKRect(excess / 2f, 0, excess / 2f + targetWidth, sourceHeight);
            }

            if (fitMode == ImageFitMode.FillWidth)
            {
                var targetHeight = sourceWidth;
                if (sourceHeight <= targetHeight)
                {
                    return full;
                }

                var excess = sourceHeight - targetHeight;
                return new SKRect(0, excess / 2f, sourceWidth, excess / 2f + targetHeight);
            }

            return full;
        }

        private static SKRect ResolveDestinationRect(int sourceWidth, int sourceHeight, int size, ImageFitMode fitMode, ImageCropSelection? cropSelection, SKRect sourceRect)
        {
            if (fitMode == ImageFitMode.ManualCrop && cropSelection is not null)
            {
                return new SKRect(0, 0, size, size);
            }

            if (fitMode == ImageFitMode.FillHeight)
            {
                var scale = size / sourceRect.Height;
                var width = sourceRect.Width * scale;
                var x = (size - width) / 2f;
                return new SKRect(x, 0, x + width, size);
            }

            if (fitMode == ImageFitMode.FillWidth)
            {
                var scale = size / sourceRect.Width;
                var height = sourceRect.Height * scale;
                var y = (size - height) / 2f;
                return new SKRect(0, y, size, y + height);
            }

            var uniformScale = Math.Min(size / (float)sourceWidth, size / (float)sourceHeight);
            var destWidth = sourceWidth * uniformScale;
            var destHeight = sourceHeight * uniformScale;
            return new SKRect((size - destWidth) / 2f, (size - destHeight) / 2f, (size + destWidth) / 2f, (size + destHeight) / 2f);
        }

        private static Bitmap ToAvaloniaBitmap(SKBitmap bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            return new Bitmap(stream);
        }

        private static (SKBitmap Bitmap, int Width, int Height, bool IsVector) RenderBitmap(string sourcePath, int preferredSize)
        {
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            return string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase)
                ? RenderSvg(sourcePath, preferredSize)
                : RenderRaster(sourcePath);
        }

        private static (SKBitmap Bitmap, int Width, int Height, bool IsVector) RenderRaster(string sourcePath)
        {
            using var stream = File.OpenRead(sourcePath);
            var bitmap = SKBitmap.Decode(stream) ?? throw new InvalidOperationException($"无法读取图片：{sourcePath}");
            return (bitmap, bitmap.Width, bitmap.Height, false);
        }

        private static (SKBitmap Bitmap, int Width, int Height, bool IsVector) RenderSvg(string sourcePath, int preferredSize)
        {
            using var stream = File.OpenRead(sourcePath);
            var svg = new SKSvg();
            var picture = svg.Load(stream) ?? throw new InvalidOperationException($"无法读取 SVG：{sourcePath}");
            var rect = picture.CullRect;
            var width = Math.Max(1, (int)Math.Ceiling(rect.Width));
            var height = Math.Max(1, (int)Math.Ceiling(rect.Height));
            var maxDimension = Math.Max(width, height);
            var scale = preferredSize > 0 ? preferredSize / (float)maxDimension : 1f;
            scale = Math.Max(scale, 1f);
            var renderWidth = Math.Max(1, (int)Math.Ceiling(width * scale));
            var renderHeight = Math.Max(1, (int)Math.Ceiling(height * scale));
            var bitmap = new SKBitmap(renderWidth, renderHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.Translate(-rect.Left, -rect.Top);
            using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
            canvas.DrawPicture(picture, paint);
            canvas.Flush();
            return (bitmap, width, height, true);
        }
    }
}
