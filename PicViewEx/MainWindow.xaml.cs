using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace PicViewEx
{
    public class OpenWithApp
    {
        public string Name { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public string Arguments { get; set; } = "\"{0}\""; // {0} 将被替换为文件路径
        public bool ShowText { get; set; } = true;
        public string IconPath { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        private List<string> supportedFormats = new List<string> {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif",
            ".ico", ".webp", ".tga", ".dds", ".psd"
        };

        private List<string> currentImageList = new List<string>();
        private int currentImageIndex = -1;
        private string currentImagePath = "";
        private double currentZoom = 1.0;
        private Transform currentTransform = Transform.Identity;
        private bool showChannels = false;
        private SolidColorBrush currentBackgroundBrush = new SolidColorBrush(Colors.Gray); // 默认中性灰
        private ImageBrush backgroundImageBrush;
        private ImageLoader imageLoader; // 移除readonly修饰符

        // 拖拽相关
        private bool isDragging = false;
        private Point lastMousePosition;
        private Point imagePosition = new Point(0, 0);

        // 通道缓存相关
        private string currentChannelCachePath = null;
        //private readonly List<(string name, BitmapImage image)> channelCache = new();
        private readonly List<Tuple<string, BitmapImage>> channelCache = new List<Tuple<string, BitmapImage>>();


        // 打开方式配置
        private List<OpenWithApp> openWithApps = new List<OpenWithApp>();

        // 窗口大小变化时的智能缩放
        private bool isWindowInitialized = false;
        private Size lastWindowSize;

        // 设置管理
        private AppSettings appSettings;
        private bool isLoadingSettings = false;

        // 临时文件路径，用于剪贴板图片的打开方式功能
        private string temporaryImagePath = null;

        // 图片信息打印相关的公共变量
        private Size canvasSize = new Size(0, 0);           // 画布大小
        private Point currentImagePosition = new Point(0, 0); // 当前图片位置
        private Size originalImageSize = new Size(0, 0);     // 原始图片尺寸
        private Size displayImageSize = new Size(0, 0);      // 显示图片尺寸（缩放后）
        private double rotationAngle = 0.0;                  // 旋转角度

        // GIF/WebP 播放器相关字段
        private GifWebpPlayer gifWebpPlayer;
        private bool isGifWebpMode = false;
        private DateTime _lastFrameTime = DateTime.Now;
        private DateTime _fpsStartTime = DateTime.Now;
        private int _frameCount = 0;
        private bool _needsInitialPositioning = false;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // 加载设置
                LoadAppSettings();

                // 根据设置初始化ImageLoader
                InitializeImageLoader();

            InitializeBackgroundSettings();
            UpdateZoomText();

            // 监听窗口大小变化
            this.SizeChanged += MainWindow_SizeChanged;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
                 

            // 初始化窗口透明功能
            InitializeWindowTransparency();

            if (statusText != null)
                statusText.Text = "就绪 - 请打开图片文件或拖拽图片到窗口";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用程序初始化失败: {ex.Message}\n\n详细信息:\n{ex.StackTrace}", 
                    "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void InitializeBackgroundSettings()
        {
            // 设置默认纯色为中性灰
            currentBackgroundBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // #808080

            // 设置默认背景类型为透明方格
            if (rbTransparent != null)
                rbTransparent.IsChecked = true;

            // 设置颜色滑块默认值（中性灰）
            if (sliderHue != null)
                sliderHue.Value = 0;
            if (sliderSaturation != null)
                sliderSaturation.Value = 0;
            if (sliderBrightness != null)
                sliderBrightness.Value = 50;

            UpdateBackground();
        }

        private void UpdateZoomText()
        {
            if (zoomPercentage != null)
            {
                zoomPercentage.Text = $"{(currentZoom * 100):F0}%";
            }
        }

 

        /// <summary>
        /// 获取当前旋转角度
        /// </summary>
        private double GetCurrentRotationAngle()
        {
            // 直接返回全局旋转角度变量
            return rotationAngle;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            isWindowInitialized = true;
            var width = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
            var height = this.ActualHeight > 0 ? this.ActualHeight : this.Height;

            // 确保尺寸有效
            if (width <= 0) width = 1200; // 默认宽度
            if (height <= 0) height = 800; // 默认高度

            lastWindowSize = new Size(width, height);

            // 初始化序列播放器
            InitializeSequencePlayer();

            // 同步工具菜单状态 - 确保菜单勾选状态与实际显示状态一致
            SynchronizeToolMenuStates();
        }

        private void InitializeWindowTransparency()
        {
            // 监听透明模式切换
            if (rbWindowTransparent != null)
            {
                rbWindowTransparent.Checked += (s, e) => EnableWindowTransparency();
                rbWindowTransparent.Unchecked += (s, e) => DisableWindowTransparency();
            }
        }


 

        #region 核心功能实现

        private void LoadImage(string imagePath)
        {
            try
            {
                // 切换到新的文件图片时，清理旧的临时文件
                CleanupTemporaryFile();

                // 如果切换了新图片，清除通道缓存
                if (imagePath != currentChannelCachePath)
                {
                    channelCache.Clear();
                    currentChannelCachePath = null;
                }

                currentImagePath = imagePath;
                if (statusText != null)
                    statusText.Text = $"加载中: {Path.GetFileName(imagePath)}";

                string extension = Path.GetExtension(imagePath).ToLower();
                
                // 检查是否是GIF或WebP文件，如果是则使用专用播放器
                if (extension == ".gif" || extension == ".webp")
                {
                    LoadGifWebpFile(imagePath);
                    return;
                }

                // 对于其他格式，使用原有逻辑
                BitmapImage bitmap = imageLoader.LoadImage(imagePath);
                if (bitmap != null && mainImage != null)
                {
                    // 清除可能的GIF动画和GIF/WebP播放器
                    WpfAnimatedGif.ImageBehavior.SetAnimatedSource(mainImage, null);
                    CleanupGifWebpPlayer();
                    mainImage.Source = bitmap;

                    // 重置变换和缩放
                    currentTransform = Transform.Identity;
                    currentZoom = 1.0;
                    imagePosition = new Point(0, 0);
                    rotationAngle = 0.0;
                    
                    // 重置图片显示变换，确保新GIF不继承上一个图片的缩放
                    if (mainImage != null)
                    {
                        mainImage.RenderTransform = Transform.Identity;
                        mainImage.Stretch = Stretch.Uniform;
                        mainImage.StretchDirection = StretchDirection.Both;
                    }
                    
                    // 重置FPS计算相关字段
                    _frameCount = 0;
                    _fpsStartTime = DateTime.Now; // 重置旋转角度

                    // 检查图片尺寸是否超过窗口尺寸
                    if (imageContainer != null)
                    {
                        // 计算有效显示区域宽度
                        double containerWidth = imageContainer.ActualWidth;
                        double containerHeight = imageContainer.ActualHeight;

                        // 只有当通道面板真正显示时才减去其宽度
                        double effectiveWidth = containerWidth;
                        if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
                        {
                            effectiveWidth = Math.Max(100, containerWidth - 305); // 确保至少有100像素显示区域
                        }

                        // 如果图片尺寸超过容器的80%，自动适应窗口
                        if (bitmap.PixelWidth > effectiveWidth * 0.8 || bitmap.PixelHeight > containerHeight * 0.8)
                        {
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                FitToWindow();
                                PrintImageInfo("图片加载 - 自动适应窗口");
                                if (statusText != null)
                                    statusText.Text = $"已加载并自动适应窗口: {Path.GetFileName(imagePath)}";
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                        else
                        {
                            // 否则居中显示
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                CenterImage();
                                PrintImageInfo("图片加载 - 居中显示");
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                    }

                    UpdateImageInfo(bitmap);

                    if (showChannels)
                    {
                        LoadImageChannels(imagePath);
                    }

                    if (statusText != null && !showChannels)
                        statusText.Text = $"已加载: {Path.GetFileName(imagePath)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                if (statusText != null)
                    statusText.Text = "加载失败";
            }
        }

        public void LoadImageFromCommandLine(string imagePath)
        {
            LoadImage(imagePath);
            var directoryPath = Path.GetDirectoryName(imagePath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                LoadDirectoryImages(directoryPath);
            }
        }

        private void LoadDirectoryImages(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                return;

            currentImageList.Clear();

            foreach (string extension in supportedFormats)
            {
                currentImageList.AddRange(Directory.GetFiles(directoryPath, $"*{extension}", SearchOption.TopDirectoryOnly));
            }

            currentImageList.Sort();
            currentImageIndex = currentImageList.IndexOf(currentImagePath);
        }

        private void UpdateImageInfo(BitmapImage bitmap)
        {
            if (bitmap != null && imageInfoText != null)
            {
                imageInfoText.Text = $"{bitmap.PixelWidth} × {bitmap.PixelHeight} | {FormatFileSize(new FileInfo(currentImagePath).Length)}";
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        private void NavigatePrevious()
        {
            if (currentImageList.Count > 0 && currentImageIndex > 0)
            {
                // 如果当前有序列帧在播放，自动停止并重置到正常图片模式
                if (hasSequenceLoaded)
                {
                    // 停止播放
                    if (isSequencePlaying)
                    {
                        PauseSequence();
                    }

                    // 重置序列帧状态
                    hasSequenceLoaded = false;
                    sequenceFrames.Clear();
                    currentFrameIndex = 0;
                    originalImage = null;

                    // 禁用序列控件
                    EnableSequenceControls(false);
                    UpdateFrameDisplay();

                    if (statusText != null)
                        statusText.Text = "序列帧播放已停止，切换到上一张图片";
                }

                currentImageIndex--;
                LoadImage(currentImageList[currentImageIndex]);
            }
        }

        private void NavigateNext()
        {
            if (currentImageList.Count > 0 && currentImageIndex < currentImageList.Count - 1)
            {
                // 如果当前有序列帧在播放，自动停止并重置到正常图片模式
                if (hasSequenceLoaded)
                {
                    // 停止播放
                    if (isSequencePlaying)
                    {
                        PauseSequence();
                    }

                    // 重置序列帧状态
                    hasSequenceLoaded = false;
                    sequenceFrames.Clear();
                    currentFrameIndex = 0;
                    originalImage = null;

                    // 禁用序列控件
                    EnableSequenceControls(false);
                    UpdateFrameDisplay();

                    if (statusText != null)
                        statusText.Text = "序列帧播放已停止，切换到下一张图片";
                }

                currentImageIndex++;
                LoadImage(currentImageList[currentImageIndex]);
            }
        }

        private void RotateImage(double angle)
        {
            if (mainImage?.Source != null)
            {
                Console.WriteLine($"=== 旋转操作开始 ===");
                Console.WriteLine($"旋转角度: {angle}度");
                
                // 记录旋转前的状态
                var source = mainImage.Source as BitmapSource;
                if (source != null)
                {
                    Console.WriteLine($"原始图片尺寸: {source.PixelWidth} x {source.PixelHeight}");
                    Console.WriteLine($"当前缩放: {currentZoom}");
                    Console.WriteLine($"缩放后尺寸: {source.PixelWidth * currentZoom} x {source.PixelHeight * currentZoom}");
                }
                
                Console.WriteLine($"旋转前图片位置: ({imagePosition.X}, {imagePosition.Y})");
                Console.WriteLine($"旋转前变换: {currentTransform}");
                Console.WriteLine($"旋转前角度: {rotationAngle}度");
                
                // 累积旋转角度
                rotationAngle += angle;
                
                // 将角度标准化到0-360度范围
                rotationAngle = rotationAngle % 360;
                if (rotationAngle < 0) rotationAngle += 360;
                
                Console.WriteLine($"累积后角度: {rotationAngle}度");
                
                // 创建新的旋转变换，使用累积的角度
                // 旋转中心设置为像素尺寸的中心（因为我们强制Image按像素尺寸显示）
                RotateTransform rotate = new RotateTransform(rotationAngle);
                rotate.CenterX = source.PixelWidth / 2.0;
                rotate.CenterY = source.PixelHeight / 2.0;

                // 直接设置旋转变换，而不是累积多个变换
                currentTransform = rotate;

                // 更新图片变换(会同时应用缩放和旋转)
                UpdateImageTransform();
                
                Console.WriteLine($"旋转后变换: {currentTransform}");
                Console.WriteLine($"旋转后图片位置: ({imagePosition.X}, {imagePosition.Y})");

                // 旋转后将图片重新居中
                CenterImageInContainer();

                Console.WriteLine($"=== 旋转操作结束 ===");
            }
        }

        private void SaveRotatedImage(string fileName)
        {
            try
            {
                using (var magickImage = new MagickImage(currentImagePath))
                {
                    if (currentTransform != Transform.Identity)
                    {
                        if (currentTransform is RotateTransform rotate)
                        {
                            magickImage.Rotate(rotate.Angle);
                        }
                        else if (currentTransform is TransformGroup group)
                        {
                            foreach (var transform in group.Children)
                            {
                                if (transform is RotateTransform r)
                                    magickImage.Rotate(r.Angle);
                            }
                        }
                    }

                    magickImage.Write(fileName);
                    if (statusText != null)
                        statusText.Text = $"已保存: {Path.GetFileName(fileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

  

        private Color HsvToRgb(double h, double s, double v)
        {
            h = h % 360.0;
            if (h < 0) h += 360.0;

            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;

            double r = 0, g = 0, b = 0;

            if (h >= 0 && h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h >= 60 && h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h >= 120 && h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h >= 180 && h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h >= 240 && h < 300)
            {
                r = x; g = 0; b = c;
            }
            else if (h >= 300 && h < 360)
            {
                r = c; g = 0; b = x;
            }

            r = (r + m);
            g = (g + m);
            b = (b + m);

            return Color.FromRgb(
                (byte)Math.Round(r * 255),
                (byte)Math.Round(g * 255),
                (byte)Math.Round(b * 255)
            );
        }

        private (double h, double s, double v) RgbToHsv(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            double s = max == 0 ? 0 : delta / max;
            double v = max;

            if (delta != 0)
            {
                if (max == r)
                    h = 60 * (((g - b) / delta) % 6);
                else if (max == g)
                    h = 60 * ((b - r) / delta + 2);
                else if (max == b)
                    h = 60 * ((r - g) / delta + 4);
            }

            if (h < 0) h += 360;

            return (h, s, v);
        }

        private void LoadImageChannels(string imagePath)
        {
            try
            {
                if (channelStackPanel == null) return;
                
                // 每次加载前都清空面板，防止重复添加
                channelStackPanel.Children.Clear();

                // 检查是否可以使用缓存
                if (imagePath == currentChannelCachePath && channelCache.Count > 0)
                {
                    // 直接使用缓存的通道图片
                    foreach (var channelTuple in channelCache)
                    {
                        CreateChannelControl(channelTuple.Item1, channelTuple.Item2);
                    }

                    if (statusText != null)
                        statusText.Text = $"已从缓存加载通道 ({channelCache.Count}个) - {Path.GetFileName(imagePath)}";
                    return;
                }

                // 如果是新图片，清除旧的缓存
                channelCache.Clear();
                currentChannelCachePath = null;

                if (statusText != null)
                    statusText.Text = $"正在生成通道...";
                
                // 只调用一次LoadChannels方法
                var loadedChannels = imageLoader.LoadChannels(imagePath);

                foreach (var channelTuple in loadedChannels)
                {
                    channelCache.Add(channelTuple);
                    CreateChannelControl(channelTuple.Item1, channelTuple.Item2);
                }

                currentChannelCachePath = imagePath;
                if (statusText != null)
                    statusText.Text = $"通道加载完成 ({channelCache.Count}个) - {Path.GetFileName(imagePath)}";
            }
            catch (Exception ex)
            {
                // 如果生成过程中出错，清除可能不完整的缓存
                channelCache.Clear();
                currentChannelCachePath = null;
                if (statusText != null)
                    statusText.Text = $"通道加载失败: {ex.Message}";
            }
        }

        private void CreateChannelControl(string channelName, BitmapImage channelImage)
        {
            if (channelStackPanel == null) return;

            var border = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5)
            };

            var stackPanel = new StackPanel();

            var label = new TextBlock
            {
                Text = channelName,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var image = new System.Windows.Controls.Image
            {
                Source = channelImage,
                Height = 150,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(5),
                Cursor = Cursors.Hand
            };

            // 改为单击事件，而不是双击
            image.MouseLeftButtonDown += (s, e) =>
            {
                var window = new Window
                {
                    Title = $"通道详细 - {channelName}",
                    Width = 600,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                var fullImage = new System.Windows.Controls.Image
                {
                    Source = channelImage,
                    Stretch = Stretch.Uniform
                };

                window.Content = fullImage;
                window.Show();
            };

            stackPanel.Children.Add(label);
            stackPanel.Children.Add(image);
            border.Child = stackPanel;

            channelStackPanel.Children.Add(border);
        }

        #endregion



        #region 图片操作工具方法

        private void UpdateImagePosition()
        {
            if (mainImage != null)
            {
                // 应用边界约束
                ConstrainImagePosition();

                Canvas.SetLeft(mainImage, imagePosition.X);
                Canvas.SetTop(mainImage, imagePosition.Y);
            }
        }

        private void ConstrainImagePosition()
        {
            if (mainImage?.Source == null || imageContainer == null) return;

            var source = mainImage.Source as BitmapSource;
            if (source == null) return;

            var containerWidth = imageContainer.ActualWidth;
            var containerHeight = imageContainer.ActualHeight;

            if (containerWidth <= 0 || containerHeight <= 0) return;

            // 计算有效显示区域宽度
            double effectiveWidth = containerWidth;
            
            // 只有当通道面板真正显示时才减去其宽度
            if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
            {
                // 通道面板宽度是300，还要减去分隔符的5像素宽度
                effectiveWidth = Math.Max(100, containerWidth - 305); // 确保至少有100像素显示区域
            }

            // 计算旋转后的实际边界框尺寸（使用像素尺寸）
            var (actualWidth, actualHeight) = GetRotatedImageBounds(source.PixelWidth, source.PixelHeight);
            var scaledWidth = actualWidth * currentZoom;
            var scaledHeight = actualHeight * currentZoom;

            // 定义最小可见区域（图片必须至少有这么多像素在屏幕内）
            var minVisibleWidth = Math.Min(scaledWidth * 0.3, 200); // 至少30%或200像素可见
            var minVisibleHeight = Math.Min(scaledHeight * 0.3, 200);

            // 计算位置约束 - 使用有效宽度而不是容器全宽
            var maxX = effectiveWidth - minVisibleWidth;
            var minX = -(scaledWidth - minVisibleWidth);
            var maxY = containerHeight - minVisibleHeight;
            var minY = -(scaledHeight - minVisibleHeight);

            // 应用约束
            imagePosition.X = Math.Max(minX, Math.Min(maxX, imagePosition.X));
            imagePosition.Y = Math.Max(minY, Math.Min(maxY, imagePosition.Y));
        }

        // 新增方法：计算旋转后的图片边界框
        private (double width, double height) GetRotatedImageBounds(double originalWidth, double originalHeight)
        {
            double totalRotation = GetCurrentRotationAngle();
            
            // 将角度转换为弧度
            double radians = totalRotation * Math.PI / 180.0;
            
            // 计算旋转后的边界框
            double cosAngle = Math.Abs(Math.Cos(radians));
            double sinAngle = Math.Abs(Math.Sin(radians));
            
            double rotatedWidth = originalWidth * cosAngle + originalHeight * sinAngle;
            double rotatedHeight = originalWidth * sinAngle + originalHeight * cosAngle;
            
            return (rotatedWidth, rotatedHeight);
        }

        // 旋转或缩放后将图片以旋转边界框居中到容器
        private void CenterImageInContainer()
        {
            if (mainImage?.Source == null || imageContainer == null) return;
            var source = mainImage.Source as BitmapSource;
            if (source == null) return;

            double containerWidth = imageContainer.ActualWidth;
            double containerHeight = imageContainer.ActualHeight;
            if (containerWidth <= 0 || containerHeight <= 0) return;

            // 计算有效显示区域宽度
            double effectiveWidth = containerWidth;
            if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
            {
                effectiveWidth = Math.Max(100, containerWidth - 305);
            }

            // 计算旋转后的边界框尺寸并应用缩放（使用像素尺寸）
            var (rotatedWidth, rotatedHeight) = GetRotatedImageBounds(source.PixelWidth, source.PixelHeight);
            double scaledRotatedWidth = rotatedWidth * currentZoom;
            double scaledRotatedHeight = rotatedHeight * currentZoom;

            // 原始图片尺寸(缩放后) - 使用像素尺寸
            double scaledOriginalWidth = source.PixelWidth * currentZoom;
            double scaledOriginalHeight = source.PixelHeight * currentZoom;

            // 期望的旋转后边界框中心位置
            double desiredCenterX = effectiveWidth / 2.0;
            double desiredCenterY = containerHeight / 2.0;

            // 图片元素的旋转中心在Canvas坐标系中的位置
            // 旋转中心相对于图片元素左上角的偏移是 (scaledOriginalWidth/2, scaledOriginalHeight/2)
            double rotationCenterOffsetX = scaledOriginalWidth / 2.0;
            double rotationCenterOffsetY = scaledOriginalHeight / 2.0;

            // Canvas位置 = 期望中心 - 旋转中心偏移
            imagePosition.X = desiredCenterX - rotationCenterOffsetX;
            imagePosition.Y = desiredCenterY - rotationCenterOffsetY;

            Console.WriteLine($"[CenterImageInContainer] 旋转角度={rotationAngle}, 原始尺寸缩放后=({scaledOriginalWidth},{scaledOriginalHeight}), 边界框尺寸=({scaledRotatedWidth},{scaledRotatedHeight}), Canvas位置=({imagePosition.X}, {imagePosition.Y})");

            UpdateImagePosition();
        }

        private void UpdateImageTransform()
        {
            if (mainImage?.Source == null) return;

            var source = mainImage.Source as BitmapSource;
            if (source == null) return;

            // 强制设置图片宽高为像素尺寸，忽略DPI影响
            // 配合 Stretch="Fill" 使用，图片会缩放到这个尺寸而不是裁剪
            mainImage.Width = source.PixelWidth;
            mainImage.Height = source.PixelHeight;

            // 清除 LayoutTransform
            mainImage.LayoutTransform = Transform.Identity;

            // 创建变换组：先旋转，再缩放(顺序很重要!)
            var transformGroup = new TransformGroup();

            // 1. 先添加旋转变换(如果有的话)
            if (currentTransform != Transform.Identity && currentTransform is RotateTransform)
            {
                transformGroup.Children.Add(currentTransform);
            }

            // 2. 再添加缩放变换
            // 缩放变换不设置中心点,默认以(0,0)为中心
            // 这样可以避免与旋转变换的中心点产生冲突
            var scaleTransform = new ScaleTransform(currentZoom, currentZoom);
            transformGroup.Children.Add(scaleTransform);

            // 应用变换
            mainImage.RenderTransform = transformGroup;

            // 更新图片位置
            UpdateImagePosition();
        }

        private void ZoomImage(double factor)
        {
            if (mainImage?.Source == null) return;

            double newZoom = currentZoom * factor;

            // 限制缩放范围
            var originalImage = mainImage.Source as BitmapSource;
            if (originalImage != null)
            {
                double maxZoom = Math.Max(5.0, Math.Max(
                    originalImage.PixelWidth / 100.0,
                    originalImage.PixelHeight / 100.0));
                newZoom = Math.Max(0.1, Math.Min(newZoom, maxZoom));
            }
            else
            {
                newZoom = Math.Max(0.1, Math.Min(newZoom, 10.0));
            }

            // 计算当前图片的中心点在容器中的位置
            var source = mainImage.Source as BitmapSource;
            if (source != null)
            {
                // 使用像素尺寸计算当前显示尺寸
                var currentWidth = source.PixelWidth * currentZoom;
                var currentHeight = source.PixelHeight * currentZoom;

                var centerX = imagePosition.X + currentWidth / 2;
                var centerY = imagePosition.Y + currentHeight / 2;

                // 更新缩放
                currentZoom = newZoom;

                // 重新计算位置，保持中心点不变
                var newWidth = source.PixelWidth * currentZoom;
                var newHeight = source.PixelHeight * currentZoom;

                imagePosition.X = centerX - newWidth / 2;
                imagePosition.Y = centerY - newHeight / 2;
            }
            else
            {
                currentZoom = newZoom;
            }

            UpdateImageTransform();
            UpdateZoomText();
        }

        private void FitToWindow()
        {
            if (mainImage?.Source == null || imageContainer == null) return;

            var source = mainImage.Source as BitmapSource;
            if (source == null) return;

            var containerWidth = imageContainer.ActualWidth;
            var containerHeight = imageContainer.ActualHeight;

            if (containerWidth <= 0 || containerHeight <= 0) return;

            // 计算有效显示区域宽度
            double effectiveWidth = containerWidth;
            
            // 只有当通道面板真正显示时才减去其宽度
            if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
            {
                // 通道面板宽度是300，还要减去分隔符的5像素宽度
                effectiveWidth = Math.Max(100, containerWidth - 305); // 确保至少有100像素显示区域
            }

            // 计算旋转后的实际边界框尺寸（使用像素尺寸）
            var (rotatedWidth, rotatedHeight) = GetRotatedImageBounds(source.PixelWidth, source.PixelHeight);

            // 计算适应窗口的缩放比例 - 使用旋转后的边界框尺寸和有效区域
            double scaleX = effectiveWidth / rotatedWidth;
            double scaleY = containerHeight / rotatedHeight;
            double scale = Math.Min(scaleX, scaleY) * 0.95; // 留一点边距

            currentZoom = Math.Max(0.1, scale);

            // 居中显示
            CenterImage();
        }

        private void SetActualSize()
        {
            if (mainImage?.Source == null) return;

            currentZoom = 1.0;
            CenterImage();
        }

        private void CenterImage()
        {
            if (mainImage?.Source == null || imageContainer == null) return;

            var source = mainImage.Source as BitmapSource;
            if (source == null) return;

            // 直接获取容器尺寸，如果布局未完成则稍后重试
            var containerWidth = imageContainer.ActualWidth;
            var containerHeight = imageContainer.ActualHeight;

            if (containerWidth <= 0 || containerHeight <= 0)
            {
                // 如果容器尺寸还未确定，异步重试
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    CenterImage();
                }), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            // 计算有效显示区域宽度
            double effectiveWidth = containerWidth;

            // 只有当通道面板真正显示时才减去其宽度
            if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
            {
                // 通道面板宽度是300，还要减去分隔符的5像素宽度
                effectiveWidth = Math.Max(100, containerWidth - 305); // 确保至少有100像素显示区域
            }

            // 原始图片尺寸(缩放后) - 使用像素尺寸
            double scaledOriginalWidth = source.PixelWidth * currentZoom;
            double scaledOriginalHeight = source.PixelHeight * currentZoom;

            // 期望的中心位置
            double desiredCenterX = effectiveWidth / 2.0;
            double desiredCenterY = containerHeight / 2.0;

            // 图片元素的旋转中心在Canvas坐标系中的位置
            // 旋转中心相对于图片元素左上角的偏移是 (scaledOriginalWidth/2, scaledOriginalHeight/2)
            double rotationCenterOffsetX = scaledOriginalWidth / 2.0;
            double rotationCenterOffsetY = scaledOriginalHeight / 2.0;

            // Canvas位置 = 期望中心 - 旋转中心偏移
            imagePosition.X = Math.Round(desiredCenterX - rotationCenterOffsetX);
            imagePosition.Y = Math.Round(desiredCenterY - rotationCenterOffsetY);

            Console.WriteLine($"[CenterImage] 旋转角度={rotationAngle}, 原始尺寸缩放后=({scaledOriginalWidth},{scaledOriginalHeight}), Canvas位置=({imagePosition.X}, {imagePosition.Y})");

            UpdateImageTransform();
            UpdateZoomText();
        }

        #endregion





        /// <summary>
        /// 根据设置初始化ImageLoader
        /// </summary>
        private void InitializeImageLoader()
        {
            try
            {
                // 根据设置确定引擎类型
                ImageLoader.ImageEngine engine = ImageLoader.ImageEngine.Magick; // 默认使用Magick
                
                // 根据设置选择引擎，但会自动检测可用性
                if (appSettings != null && !string.IsNullOrEmpty(appSettings.ImageEngine))
                {
                    if (appSettings.ImageEngine.Equals("Leadtools", StringComparison.OrdinalIgnoreCase))
                    {
                        engine = ImageLoader.ImageEngine.Leadtools;
                    }
                }

                // 初始化ImageLoader（会自动检测引擎可用性并回退）
                imageLoader = new ImageLoader(0.3, engine);

                // 更新菜单状态
                UpdateEngineMenuState();

                if (statusText != null)
                {
                    string engineName = imageLoader.GetCurrentEngine() == ImageLoader.ImageEngine.Leadtools ? "LEADTOOLS" : "ImageMagick";
                    statusText.Text = $"图像引擎已初始化: {engineName}";
                }
            }
            catch (Exception ex)
            {
                // 如果初始化失败，使用默认的Magick引擎
                imageLoader = new ImageLoader(0.3, ImageLoader.ImageEngine.Magick);
                
                if (statusText != null)
                {
                    statusText.Text = $"引擎初始化失败，使用默认引擎: {ex.Message}";
                }
            }
        } 



        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 清理临时文件
            CleanupTemporaryFile();
            
            // 清理GIF/WebP播放器
            CleanupGifWebpPlayer();

            SaveAppSettings();
        }



    }
}
