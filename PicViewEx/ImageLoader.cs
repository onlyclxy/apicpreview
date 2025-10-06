using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PicViewEx
{
    /// <summary>
    /// 集中管理图片及相关资源的加载逻辑，便于在不同场景复用。
    /// </summary>
    public class ImageLoader
    {
        private readonly double backgroundOpacity;

        public ImageLoader(double backgroundOpacity = 0.3)
        {
            this.backgroundOpacity = backgroundOpacity;
        }

        /// <summary>
        /// 加载常规图片资源，优先使用 ImageMagick 以获得更好的格式兼容性。
        /// </summary>
        public BitmapImage LoadImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("imagePath 不能为空", nameof(imagePath));

            try
            {
                using (var magickImage = new MagickImage(imagePath))
                {
                    return CreateBitmapFromMagickImage(magickImage);
                }
            }
            catch
            {
                try
                {
                    return LoadBitmapImageFromFile(imagePath);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"无法加载图片: {imagePath}", ex);
                }
            }
        }

        /// <summary>
        /// 为 GIF 动画加载静态源图像（用于 WpfAnimatedGif 控件）。
        /// </summary>
        public BitmapImage LoadGifAnimationSource(string gifPath)
        {
            if (string.IsNullOrWhiteSpace(gifPath))
                throw new ArgumentException("gifPath 不能为空", nameof(gifPath));

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(gifPath);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }

        /// <summary>
        /// 从文件加载背景图片，并返回包含图像刷的结果。
        /// </summary>
        public BackgroundImageResult LoadBackgroundImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("imagePath 不能为空", nameof(imagePath));
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("背景图片不存在", imagePath);

            var bitmap = LoadBitmapImageFromFile(imagePath);
            var brush = CreateBackgroundBrush(bitmap);
            return new BackgroundImageResult(brush, imagePath, usedFallback: false);
        }

        /// <summary>
        /// 加载默认背景图片，如果默认资源不存在则回退到渐变背景。
        /// </summary>
        public BackgroundImageResult LoadDefaultBackgroundImage(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("baseDirectory 不能为空", nameof(baseDirectory));

            string defaultImagePath = Path.Combine(baseDirectory, "res", "01.jpg");
            if (File.Exists(defaultImagePath))
            {
                var bitmap = LoadBitmapImageFromFile(defaultImagePath);
                var brush = CreateBackgroundBrush(bitmap);
                return new BackgroundImageResult(brush, defaultImagePath, usedFallback: false);
            }

            var fallbackBrush = CreateBackgroundBrush(CreateGradientImage());
            return new BackgroundImageResult(fallbackBrush, null, usedFallback: true);
        }

        /// <summary>
        /// 为指定图片生成 RGB/Alpha 通道。
        /// </summary>
        public List<Tuple<string, BitmapImage>> LoadChannels(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("imagePath 不能为空", nameof(imagePath));

            using (var magickImage = new MagickImage(imagePath))
            {
                return LoadChannelsFromMagickImage(magickImage);
            }
        }

        /// <summary>
        /// 为剪贴板图片生成通道信息。
        /// </summary>
        public List<Tuple<string, BitmapImage>> LoadChannels(BitmapSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            byte[] imageBytes = ConvertBitmapSourceToBytes(source);
            using (var magickImage = new MagickImage(imageBytes))
            {
                return LoadChannelsFromMagickImage(magickImage);
            }
        }

        private List<Tuple<string, BitmapImage>> LoadChannelsFromMagickImage(MagickImage magickImage)
        {
            var channels = new List<Tuple<string, BitmapImage>>();

            var redImage = new MagickImage(magickImage);
            try
            {
                redImage.Evaluate(Channels.Green, EvaluateOperator.Set, 0);
                redImage.Evaluate(Channels.Blue, EvaluateOperator.Set, 0);
                var redBitmap = CreateBitmapFromMagickImage(redImage);
                channels.Add(Tuple.Create("红色 (R)", redBitmap));
            }
            finally
            {
                redImage.Dispose();
            }

            var greenImage = new MagickImage(magickImage);
            try
            {
                greenImage.Evaluate(Channels.Red, EvaluateOperator.Set, 0);
                greenImage.Evaluate(Channels.Blue, EvaluateOperator.Set, 0);
                var greenBitmap = CreateBitmapFromMagickImage(greenImage);
                channels.Add(Tuple.Create("绿色 (G)", greenBitmap));
            }
            finally
            {
                greenImage.Dispose();
            }

            var blueImage = new MagickImage(magickImage);
            try
            {
                blueImage.Evaluate(Channels.Red, EvaluateOperator.Set, 0);
                blueImage.Evaluate(Channels.Green, EvaluateOperator.Set, 0);
                var blueBitmap = CreateBitmapFromMagickImage(blueImage);
                channels.Add(Tuple.Create("蓝色 (B)", blueBitmap));
            }
            finally
            {
                blueImage.Dispose();
            }

            if (magickImage.HasAlpha)
            {
                var alphaImage = new MagickImage(magickImage);
                try
                {
                    alphaImage.Alpha(AlphaOption.Extract);
                    alphaImage.Format = MagickFormat.Png;
                    var alphaBitmap = CreateBitmapFromMagickImage(alphaImage);
                    channels.Add(Tuple.Create("透明 (Alpha)", alphaBitmap));
                }
                finally
                {
                    alphaImage.Dispose();
                }
            }

            int expectedChannels = magickImage.HasAlpha ? 4 : 3;
            if (channels.Count != expectedChannels)
            {
                throw new InvalidOperationException($"通道生成不完整，预期 {expectedChannels} 个通道，实际生成 {channels.Count} 个");
            }

            return channels;
        }

        private BitmapImage LoadBitmapImageFromFile(string imagePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private ImageBrush CreateBackgroundBrush(BitmapSource source)
        {
            return new ImageBrush(source)
            {
                Stretch = Stretch.UniformToFill,
                TileMode = TileMode.Tile,
                Opacity = backgroundOpacity
            };
        }

        private BitmapSource CreateGradientImage()
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var gradientBrush = new LinearGradientBrush();
                gradientBrush.StartPoint = new Point(0, 0);
                gradientBrush.EndPoint = new Point(1, 1);
                gradientBrush.GradientStops.Add(new GradientStop(Colors.LightBlue, 0.0));
                gradientBrush.GradientStops.Add(new GradientStop(Colors.LightGray, 1.0));

                context.DrawRectangle(gradientBrush, null, new Rect(0, 0, 256, 256));
            }

            var renderBitmap = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(visual);
            renderBitmap.Freeze();
            return renderBitmap;
        }

        private BitmapImage CreateBitmapFromMagickImage(MagickImage magickImage)
        {
            magickImage.Format = MagickFormat.Png;
            byte[] imageBytes = magickImage.ToByteArray();

            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(imageBytes);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        private byte[] ConvertBitmapSourceToBytes(BitmapSource bitmapSource)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }
    }

    public class BackgroundImageResult
    {
        public BackgroundImageResult(ImageBrush brush, string sourcePath, bool usedFallback)
        {
            Brush = brush ?? throw new ArgumentNullException(nameof(brush));
            SourcePath = sourcePath;
            UsedFallback = usedFallback;
        }

        public ImageBrush Brush { get; }
        public string SourcePath { get; }
        public bool UsedFallback { get; }
    }
}
