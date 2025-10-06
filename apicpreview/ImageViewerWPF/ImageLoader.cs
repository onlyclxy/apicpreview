using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageViewerWPF
{
    internal static class ImageLoader
    {
        private static readonly string[] supportedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".ico",
            ".tga", ".dds", ".psd", ".webp", ".a"
        };

        private static readonly HashSet<string> supportedExtensionSet =
            new HashSet<string>(supportedExtensions, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> SupportedExtensions => supportedExtensionSet;

        public static bool IsSupportedExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            return supportedExtensionSet.Contains(extension.StartsWith(".")
                ? extension.ToLowerInvariant()
                : "." + extension.ToLowerInvariant());
        }

        public static BitmapSource LoadImage(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}");
            }

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                switch (extension)
                {
                    case ".tga":
                        return LoadTgaImage(filePath);
                    case ".dds":
                        return LoadDdsImage(filePath);
                    case ".psd":
                        return LoadPsdImage(filePath);
                    case ".webp":
                        return LoadWebPImage(filePath);
                    case ".a":
                        return LoadAFileImage(filePath);
                    default:
                        return LoadBitmapImage(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法加载 {extension} 格式: {ex.Message}", ex);
            }
        }

        public static BitmapSource LoadBackgroundImage(string filePath)
        {
            return LoadImage(filePath);
        }

        public static Bitmap CreateChannelBitmap(BitmapSource bitmapSource)
        {
            if (bitmapSource == null)
            {
                throw new ArgumentNullException(nameof(bitmapSource));
            }

            using (var outStream = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(outStream);
                return new Bitmap(outStream);
            }
        }

        private static BitmapSource LoadBitmapImage(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(filePath));
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource LoadAFileImage(string filePath)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            using (var stream = new MemoryStream(fileBytes))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        private static BitmapSource LoadTgaImage(string filePath)
        {
            return CreatePlaceholderImage("TGA", "需要安装 Pfim 库");
        }

        private static BitmapSource LoadDdsImage(string filePath)
        {
            return CreatePlaceholderImage("DDS", "需要安装 Pfim 库");
        }

        private static BitmapSource LoadPsdImage(string filePath)
        {
            return CreatePlaceholderImage("PSD", "需要安装 ImageSharp 库");
        }

        private static BitmapSource LoadWebPImage(string filePath)
        {
            return CreatePlaceholderImage("WebP", "需要安装 WebP 支持库");
        }

        private static BitmapSource CreatePlaceholderImage(string format, string message)
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(Brushes.LightGray, null, new System.Windows.Rect(0, 0, 400, 300));

                var text = new FormattedText($"{format} 格式\n\n{message}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei"),
                    16, Brushes.Black, 96);

                context.DrawText(text, new System.Windows.Point(200 - text.Width / 2, 150 - text.Height / 2));
            }

            var bitmap = new RenderTargetBitmap(400, 300, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
