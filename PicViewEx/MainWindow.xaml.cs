using ImageMagick;
using PicViewEx.ImageChannels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        public bool ShowText { get; set; } = false; // 改为默认false，表示默认不勾选"工具栏只显示图标"
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

        private readonly IChannelService _channels = ChannelService.Instance;

        // 记录“通道名 -> 卡片内的 Image 控件”，用于后续替换成全尺寸
        private readonly Dictionary<string, System.Windows.Controls.Image> _channelImageMap =
            new Dictionary<string, System.Windows.Controls.Image>(StringComparer.OrdinalIgnoreCase);

        private bool _updatingChannelUI = false;

        private bool ShowChannels  // “单一真相”
        {
            get => showChannels;
            set
            {
                if (showChannels == value) return;
                showChannels = value;

                ApplyShowChannelsUI(value);   // 真正改界面
                SyncChannelUI(value);         // 同步勾选，不触发二次反转

                // 如果你有配置对象，这里就顺手更新
                if (appSettings != null) appSettings.ShowChannels = value;
            }
        }

        private void ApplyShowChannelsUI(bool on)
        {
            if (channelPanel != null)
                channelPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

            if (channelSplitter != null)
                channelSplitter.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

            if (channelColumn != null)
                channelColumn.Width = on ? new GridLength(300) : new GridLength(0);

            // 首次显示或切换时，重新计算一下布局，避免宽度变化导致图片偏移
            if (mainImage?.Source != null)
            {
                // 这里用你已有的方法，二选一
                CenterImageInContainer(); // 或 FitToWindow();
            }


            // 若需要立即生成/清空通道预览：
            if (on && !string.IsNullOrEmpty(currentImagePath))
                LoadImageChannels(currentImagePath);   // ← 去掉 "_ ="
            else if (!on && channelStackPanel != null)
                channelStackPanel.Children.Clear();

        }



        private void SyncChannelUI(bool value)
        {
            _updatingChannelUI = true;
            try
            {
                if (menuShowChannels != null) menuShowChannels.IsChecked = value;
                if (chkShowChannels != null) chkShowChannels.IsChecked = value;
            }
            finally
            {
                _updatingChannelUI = false;
            }
        }


        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // 加载设置
                LoadAppSettings();

                // 根据设置初始化ImageLoader
                InitializeImageLoader();

                ImageLoader.InitializeLeadtools();



                InitializeBackgroundSettings();
            UpdateZoomText();

            // 监听窗口大小变化
            this.SizeChanged += MainWindow_SizeChanged;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
                 

            // 初始化窗口透明功能
            InitializeWindowTransparency();

            if (statusText != null)
            {
                string engineName = GetEngineDisplayName(imageLoader?.GetCurrentEngine() ?? ImageLoader.ImageEngine.Auto);
                statusText.Text = $"就绪 | 引擎: {engineName}";
            }
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
        /// 更新状态栏文本，始终包含当前引擎信息
        /// </summary>
        private void UpdateStatusText(string message)
        {
            if (statusText != null)
            {
                string engineName = GetEngineDisplayName(imageLoader?.GetCurrentEngine() ?? ImageLoader.ImageEngine.Auto);
                statusText.Text = $"{message} | 引擎: {engineName}";
            }
        }

        /// <summary>
        /// 获取引擎显示名称
        /// </summary>
        private string GetEngineDisplayName(ImageLoader.ImageEngine engine)
        {
            switch (engine)
            {
                case ImageLoader.ImageEngine.Auto:
                    // 如果是自动模式，显示实际使用的引擎
                    if (imageLoader != null)
                    {
                        var lastUsedEngine = imageLoader.GetLastUsedAutoEngine();
                        if (lastUsedEngine != ImageLoader.ImageEngine.Auto)
                        {
                            return $"自动-{GetEngineDisplayName(lastUsedEngine)}";
                        }
                    }
                    return "自动";
                case ImageLoader.ImageEngine.STBImageSharp:
                    return "STBImageSharp";
                case ImageLoader.ImageEngine.Leadtools:
                    return "LEADTOOLS";
                case ImageLoader.ImageEngine.Magick:
                    return "ImageMagick";
                default:
                    return "未知";
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
                    UpdateStatusText($"加载中: {Path.GetFileName(imagePath)}");

                string extension = Path.GetExtension(imagePath).ToLowerInvariant();
                
                // 检查是否是GIF或WebP文件，如果是则使用专用播放器
                if (extension == ".gif" || extension == ".webp")
                {
                    LoadGifWebpFile(imagePath);
                    return;
                }

                // 对于其他格式，使用原有逻辑
                BitmapSource bitmap = imageLoader.LoadImage(imagePath);
                if (bitmap != null && mainImage != null)
                {
                    // 清掉 GIF/WebP 播放器等
                    CleanupGifWebpPlayer();

                    // —— 不要立刻赋 Source；先把几何全部准备好 —— //
                    currentTransform = Transform.Identity;
                    currentZoom = 1.0;
                    imagePosition = new Point(0, 0);
                    rotationAngle = 0.0;

                    // 有效视口
                    GetEffectiveViewport(out double vw, out double vh);

                    // 决定是否自适应（保留你原来的 80% 逻辑）
                    bool autoFit = (vw > 0 && vh > 0) &&
                                   (bitmap.PixelWidth > vw * 0.8 || bitmap.PixelHeight > vh * 0.8);

                    currentZoom = autoFit ? ComputeFitZoom(bitmap, vw, vh) : 1.0;

                    // 先把图元的几何设好（像素尺寸 + 变换 + 位置）
                    mainImage.Width = bitmap.PixelWidth;
                    mainImage.Height = bitmap.PixelHeight;

                    // 构造“旋转后缩放”的变换组（首图通常未旋转，旋转中心按像素中心）
                    var tg = new TransformGroup();
                    tg.Children.Add(new RotateTransform(0, bitmap.PixelWidth / 2.0, bitmap.PixelHeight / 2.0));
                    tg.Children.Add(new ScaleTransform(currentZoom, currentZoom));
                    mainImage.LayoutTransform = Transform.Identity;
                    mainImage.RenderTransform = tg;

                    // 计算并预先写入 Canvas 位置（居中）
                    double scaledW = bitmap.PixelWidth * currentZoom;
                    double scaledH = bitmap.PixelHeight * currentZoom;
                    double x = Math.Round(vw / 2.0 - scaledW / 2.0);
                    double y = Math.Round(vh / 2.0 - scaledH / 2.0);
                    imagePosition = new Point(x, y);
                    UpdateImagePosition();     // 这里只设置 Canvas.Left/Top，不要再异步

                    // —— 到这一步，几何已就绪 —— //
                    // 最后一步才让它渲染首帧（已在正确位置/缩放）
                    mainImage.Source = bitmap;

                    // UI & 信息
                    _frameCount = 0;
                    _fpsStartTime = DateTime.Now;
                    PrintImageInfo(autoFit ? "图片加载 - 自动适应窗口" : "图片加载 - 居中显示");
                    UpdateImageInfo(bitmap);
                    if (showChannels) LoadImageChannels(imagePath);
                    if (statusText != null && !showChannels)
                        UpdateStatusText($"已加载: {Path.GetFileName(imagePath)}");
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                if (statusText != null)
                    UpdateStatusText("加载失败");
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

        private void UpdateImageInfo(BitmapSource bitmap)
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
            if (currentImageList.Count > 0)
            {
                if (currentImageIndex > 0)
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
                            UpdateStatusText("序列帧播放已停止，切换到上一张图片");
                    }

                    currentImageIndex--;
                    LoadImage(currentImageList[currentImageIndex]);
                }
                else
                {
                    // 已经是第一张图片，显示提示
                    MessageBox.Show("已经是第一张图片了！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (statusText != null)
                        UpdateStatusText("已经是第一张图片");
                }
            }
        }

        private void NavigateNext()
        {
            if (currentImageList.Count > 0)
            {
                if (currentImageIndex < currentImageList.Count - 1)
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
                            UpdateStatusText("序列帧播放已停止，切换到下一张图片");
                    }

                    currentImageIndex++;
                    LoadImage(currentImageList[currentImageIndex]);
                }
                else
                {
                    // 已经是最后一张图片，显示提示
                    MessageBox.Show("已经是最后一张图片了！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (statusText != null)
                        UpdateStatusText("已经是最后一张图片");
                }
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
                        UpdateStatusText($"已保存: {Path.GetFileName(fileName)}");
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


        private async void LoadImageChannels(string imagePath)
        {
            if (channelStackPanel == null || string.IsNullOrEmpty(imagePath)) return;

            channelStackPanel.Children.Clear();
            if (statusText != null) UpdateStatusText($"正在生成通道预览…");

            var previews = await System.Threading.Tasks.Task.Run(() =>
                _channels.GetPreviewChannels(imagePath, 300)); // <= 预览 300

            foreach (var ch in previews)
                CreateChannelControl(ch.Name, ch.Bitmap);            ; // 你已有的 UI 方法

            if (statusText != null) UpdateStatusText($"通道预览完成（{previews.Count} 个）");
        }



        private async Task OnChannelCardClicked(string channelName)
        {
            if (string.IsNullOrEmpty(currentImagePath)) return;

            var oldCursor = this.Cursor;
            try
            {
                this.Cursor = Cursors.Wait;
                if (statusText != null) UpdateStatusText($"正在生成 {channelName} 全尺寸…");

                // 后台计算全尺寸通道（ChannelService 已经 Freeze 了位图）
                var full = await Task.Run(() => _channels.GetFullResChannel(currentImagePath, channelName));

                if (full == null || full.Bitmap == null)
                {
                    if (statusText != null) UpdateStatusText($"{channelName} 全尺寸生成失败");
                    return;
                }

                // 仅打开窗口显示全尺寸（不替换卡片缩略图）
                ShowFullChannelWindow(full.Name, full.Bitmap);

                if (statusText != null) UpdateStatusText($"{full.Name} 全尺寸就绪");
            }
            catch (Exception ex)
            {
                if (statusText != null) UpdateStatusText($"生成失败：{ex.Message}");
            }
            finally
            {
                this.Cursor = oldCursor;
            }
        }

        private void ShowFullChannelWindow(string channelName, BitmapSource fullBitmap)
        {
            var window = new Window
            {
                Title = $"通道（全尺寸） - {channelName}",
                Width = 900,
                Height = 700,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var img = new System.Windows.Controls.Image
            {
                Source = fullBitmap,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true
            };

            var sv = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = img
            };

            window.Content = sv;
            window.Show();
        }

        private void CreateChannelControl(string channelName, BitmapSource channelPreview)
        {
            if (channelStackPanel == null) return;

            var border = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5),
                Tag = channelName
            };

            var stackPanel = new StackPanel();

            var label = new TextBlock
            {
                Text = channelName,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 卡片里永远放预览图
            var img = new System.Windows.Controls.Image
            {
                Source = channelPreview,
                Height = 150,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(5),
                Cursor = Cursors.Hand
            };

            // 单击仅打开“全尺寸通道”窗口；不替换卡片上的小图
            img.MouseLeftButtonDown += async (s, e) =>
            {
                e.Handled = true;
                await OnChannelCardClicked(channelName);
            };

            stackPanel.Children.Add(label);
            stackPanel.Children.Add(img);
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
                ImageLoader.ImageEngine engine = ImageLoader.ImageEngine.Auto; // 默认使用Auto
                
                // 根据设置选择引擎，但会自动检测可用性
                if (appSettings != null && !string.IsNullOrEmpty(appSettings.ImageEngine))
                {
                    if (appSettings.ImageEngine.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                    {
                        engine = ImageLoader.ImageEngine.Auto;
                    }
                    else if (appSettings.ImageEngine.Equals("STBImageSharp", StringComparison.OrdinalIgnoreCase))
                    {
                        engine = ImageLoader.ImageEngine.STBImageSharp;
                    }
                    else if (appSettings.ImageEngine.Equals("Leadtools", StringComparison.OrdinalIgnoreCase))
                    {
                        engine = ImageLoader.ImageEngine.Leadtools;
                    }
                    else if (appSettings.ImageEngine.Equals("Magick", StringComparison.OrdinalIgnoreCase))
                    {
                        engine = ImageLoader.ImageEngine.Magick;
                    }
                }

                // 初始化ImageLoader（会自动检测引擎可用性并回退）
                imageLoader = new ImageLoader(0.3, engine);

                // 更新菜单状态
                UpdateEngineMenuState();

                if (statusText != null)
                {
                    string engineName = GetEngineDisplayName(imageLoader.GetCurrentEngine());
                    UpdateStatusText($"图像引擎已初始化: {engineName}");
                }
            }
            catch (Exception ex)
            {
                // 如果初始化失败，使用默认的Magick引擎
                imageLoader = new ImageLoader(0.3, ImageLoader.ImageEngine.Magick);
                
                if (statusText != null)
                {
                    UpdateStatusText($"引擎初始化失败，使用默认引擎: {ex.Message}");
                }
            }
        } 



        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 清理临时文件
            CleanupTemporaryFile();
            
            // 清理GIF/WebP播放器
            CleanupGifWebpPlayer();

            ImageLoader.ShutdownLeadtools();

            SaveAppSettings();
        }

        private void GetEffectiveViewport(out double vw, out double vh)
        {
            vw = 0; vh = 0;
            if (imageContainer == null) return;

            var cw = imageContainer.ActualWidth;
            var ch = imageContainer.ActualHeight;
            if (cw <= 0 || ch <= 0) return;

            vw = cw;
            if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
                vw = Math.Max(100, cw - 305); // 300 + 分隔 5
            vh = ch;
        }

        private double ComputeFitZoom(BitmapSource bmp, double vw, double vh)
        {
            // 初始旋转角为 0：如需考虑旋转可用你的 GetRotatedImageBounds
            double rw = bmp.PixelWidth, rh = bmp.PixelHeight;
            double scale = Math.Min(vw / rw, vh / rh) * 0.95; // 留 5% 边
            return Math.Max(0.1, scale);
        }


    }
}
