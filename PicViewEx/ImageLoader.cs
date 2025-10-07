using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Leadtools;
using Leadtools.Codecs;
using Leadtools.Pdf;

namespace PicViewEx
{
    /// <summary>
    /// 集中管理图片及相关资源的加载逻辑，便于在不同场景复用。
    /// </summary>
    public class ImageLoader
    {
        /// <summary>
        /// 图像引擎类型枚举
        /// </summary>
        public enum ImageEngine
        {
            Magick,
            Leadtools
        }

        private readonly double backgroundOpacity;
        private ImageEngine currentEngine;
        private RasterCodecs leadtoolsCodecs;

        public ImageLoader(double backgroundOpacity = 0.3, ImageEngine engine = ImageEngine.Magick)
        {
            this.backgroundOpacity = backgroundOpacity;
            
            // 智能选择引擎：如果指定的引擎不可用，自动回退到可用的引擎
            if (engine == ImageEngine.Leadtools && !IsLeadtoolsAvailable())
            {
                System.Diagnostics.Debug.WriteLine("LEADTOOLS not available, falling back to ImageMagick");
                this.currentEngine = ImageEngine.Magick;
            }
            else
            {
                this.currentEngine = engine;
            }
            
            if (this.currentEngine == ImageEngine.Leadtools)
            {
                if (!InitializeLeadtools())
                {
                    // 如果LEADTOOLS初始化失败，回退到Magick
                    this.currentEngine = ImageEngine.Magick;
                }
            }
        }

        /// <summary>
        /// 切换图像引擎
        /// </summary>
        public void SwitchEngine(ImageEngine engine)
        {
            if (currentEngine == engine) return;

            // 检查目标引擎是否可用
            if (engine == ImageEngine.Leadtools && !IsLeadtoolsAvailable())
            {
                System.Diagnostics.Debug.WriteLine("Cannot switch to LEADTOOLS: not available");
                return;
            }

            // 清理旧引擎资源
            if (currentEngine == ImageEngine.Leadtools && leadtoolsCodecs != null)
            {
                leadtoolsCodecs.Dispose();
                leadtoolsCodecs = null;
            }

            currentEngine = engine;

            // 初始化新引擎
            if (engine == ImageEngine.Leadtools)
            {
                InitializeLeadtools();
            }
        }

        /// <summary>
        /// 检查LEADTOOLS DLL是否存在
        /// </summary>
        private bool IsLeadtoolsAvailable()
        {
            try
            {
                // 检查核心LEADTOOLS DLL是否存在
                string[] requiredDlls = {
                    "Leadtools.dll",
                    "Leadtools.Codecs.dll",
                    "Ltkrnx.dll"
                };

                foreach (string dll in requiredDlls)
                {
                    if (!File.Exists(dll))
                    {
                        System.Diagnostics.Debug.WriteLine($"LEADTOOLS DLL not found: {dll}");
                        return false;
                    }
                }

                // 尝试加载Leadtools程序集
                Assembly.LoadFrom("Leadtools.dll");
                Assembly.LoadFrom("Leadtools.Codecs.dll");
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LEADTOOLS availability check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取可用的引擎列表
        /// </summary>
        public List<ImageEngine> GetAvailableEngines()
        {
            var engines = new List<ImageEngine> { ImageEngine.Magick }; // ImageMagick总是可用
            
            if (IsLeadtoolsAvailable())
            {
                engines.Add(ImageEngine.Leadtools);
            }
            
            return engines;
        }

        /// <summary>
        /// 获取当前使用的引擎
        /// </summary>
        public ImageEngine GetCurrentEngine()
        {
            return currentEngine;
        }
        /// <summary>
        /// 初始化LEADTOOLS
        /// </summary>
        private bool InitializeLeadtools()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting LEADTOOLS initialization...");
                
                // 检查许可证文件是否存在
                string keyPath = "full_license.key";
                string licPath = "full_license.lic";
                
                if (File.Exists(keyPath) && File.Exists(licPath))
                {
                    System.Diagnostics.Debug.WriteLine("License files found, loading license...");
                    try
                    {
                        var key = File.ReadAllText(keyPath);
                        var lic = File.ReadAllBytes(licPath);
                        RasterSupport.SetLicense(lic, key);
                        System.Diagnostics.Debug.WriteLine("License loaded successfully");
                    }
                    catch (Exception licEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"License loading failed: {licEx.Message}");
                        // 继续尝试初始化，可能在评估模式下工作
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("License files not found, running in evaluation mode");
                }

                System.Diagnostics.Debug.WriteLine("Creating RasterCodecs instance...");
                leadtoolsCodecs = new RasterCodecs();
                
                System.Diagnostics.Debug.WriteLine("Setting codec options...");
                leadtoolsCodecs.Options.Load.AllPages = true;
                leadtoolsCodecs.Options.RasterizeDocument.Load.XResolution = 300;
                leadtoolsCodecs.Options.RasterizeDocument.Load.YResolution = 300;
                
                System.Diagnostics.Debug.WriteLine("LEADTOOLS initialization completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LEADTOOLS initialization failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 加载常规图片资源，根据当前引擎选择加载方式。
        /// </summary>
        public BitmapImage LoadImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("imagePath 不能为空", nameof(imagePath));

            if (currentEngine == ImageEngine.Leadtools)
            {
                return LoadImageWithLeadtools(imagePath);
            }
            else
            {
                return LoadImageWithMagick(imagePath);
            }
        }

        /// <summary>
        /// 使用LEADTOOLS加载图片
        /// </summary>
        private BitmapImage LoadImageWithLeadtools(string imagePath)
        {
            try
            {
                if (leadtoolsCodecs == null)
                {
                    System.Diagnostics.Debug.WriteLine("LEADTOOLS codecs not initialized, falling back to ImageMagick");
                    return LoadImageWithMagick(imagePath);
                }

                using (var rasterImage = leadtoolsCodecs.Load(imagePath))
                {
                    return ConvertRasterImageToBitmapImage(rasterImage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LEADTOOLS failed to load {imagePath}: {ex.Message}");
                // 如果LEADTOOLS加载失败，回退到Magick
                return LoadImageWithMagick(imagePath);
            }
        }

        /// <summary>
        /// 使用ImageMagick加载图片
        /// </summary>
        private BitmapImage LoadImageWithMagick(string imagePath)
        {
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
        /// 将LEADTOOLS RasterImage转换为BitmapImage
        /// </summary>
        private BitmapImage ConvertRasterImageToBitmapImage(RasterImage rasterImage)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    leadtoolsCodecs.Save(rasterImage, ms, RasterImageFormat.Png, 0);
                    ms.Position = 0;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                // 如果PNG格式失败，尝试BMP格式
                using (var ms = new MemoryStream())
                {
                    leadtoolsCodecs.Save(rasterImage, ms, RasterImageFormat.Bmp, 24);
                    ms.Position = 0;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
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

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            /*
            if (leadtoolsCodecs != null)
            {
                leadtoolsCodecs.Dispose();
                leadtoolsCodecs = null;
            }
            */
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
