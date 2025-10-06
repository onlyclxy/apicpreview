using ImageMagick;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using PicViewEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;

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
        private EverythingSearch everythingSearch;
        private readonly ImageLoader imageLoader;

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

        public MainWindow()
        {
            InitializeComponent();

            imageLoader = new ImageLoader();

            // 加载设置
            LoadAppSettings();

            InitializeBackgroundSettings();
            UpdateZoomText();

            // 监听窗口大小变化
            this.SizeChanged += MainWindow_SizeChanged;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            // 初始化Everything搜索（如果失败也不影响其他功能）
            try
            {
                everythingSearch = new EverythingSearch(supportedFormats);
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"搜索功能初始化失败: {ex.Message}";
            }

            // 初始化窗口透明功能
            InitializeWindowTransparency();

            if (statusText != null)
                statusText.Text = "就绪 - 请打开图片文件或拖拽图片到窗口";
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

        /// <summary>
        /// 同步工具菜单状态 - 确保菜单勾选状态与实际工具栏显示状态一致
        /// </summary>
        private void SynchronizeToolMenuStates()
        {
            try
            {
                Console.WriteLine("--- 开始同步工具菜单状态 ---");

                // 同步背景工具栏菜单状态
                if (menuShowBgToolbar != null && backgroundExpander != null)
                {
                    // 菜单勾选状态应该反映工具栏的实际可见性
                    // 对于Expander，应该基于Visibility而不是IsExpanded
                    bool isVisible = backgroundExpander.Visibility == Visibility.Visible;
                    bool wasChecked = menuShowBgToolbar.IsChecked == true;

                    Console.WriteLine($"背景工具栏 - 实际可见: {isVisible}, 菜单勾选: {wasChecked}");

                    // 如果设置中的状态与实际状态不一致，以实际状态为准
                    if (menuShowBgToolbar.IsChecked != isVisible)
                    {
                        menuShowBgToolbar.IsChecked = isVisible;
                        Console.WriteLine($"背景工具栏菜单状态已修正: {wasChecked} -> {isVisible}");
                    }
                }

                // 同步序列帧工具栏菜单状态
                if (menuShowSequenceToolbar != null && sequenceExpander != null)
                {
                    // 菜单勾选状态应该反映工具栏的实际可见性
                    bool isVisible = sequenceExpander.Visibility == Visibility.Visible;
                    bool wasChecked = menuShowSequenceToolbar.IsChecked == true;

                    Console.WriteLine($"序列帧工具栏 - 实际可见: {isVisible}, 菜单勾选: {wasChecked}");

                    // 如果设置中的状态与实际状态不一致，以实际状态为准
                    if (menuShowSequenceToolbar.IsChecked != isVisible)
                    {
                        menuShowSequenceToolbar.IsChecked = isVisible;
                        Console.WriteLine($"序列帧工具栏菜单状态已修正: {wasChecked} -> {isVisible}");
                    }
                }

                Console.WriteLine("--- 结束同步工具菜单状态 ---");

                if (statusText != null)
                {
                    string bgStatus = menuShowBgToolbar?.IsChecked == true ? "显示" : "隐藏";
                    string seqStatus = menuShowSequenceToolbar?.IsChecked == true ? "显示" : "隐藏";
                    statusText.Text = $"工具菜单状态已同步 - 背景工具栏: {bgStatus}, 序列帧工具栏: {seqStatus}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"同步工具菜单状态失败: {ex.Message}");
                if (statusText != null)
                    statusText.Text = $"同步工具菜单状态失败: {ex.Message}";
            }
        }

        private void EnableWindowTransparency()
        {
            try
            {
                // 设置窗口为可穿透点击（可选）
                // 这样可以让鼠标点击穿透到下面的窗口
                // 但会影响窗口的交互，所以暂时注释
                // WindowInteropHelper helper = new WindowInteropHelper(this);
                // SetWindowLong(helper.Handle, GWL_EXSTYLE, GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_TRANSPARENT);

                if (statusText != null)
                    statusText.Text = "窗口透明模式已启用 - 图片将悬浮显示";
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"启用透明模式失败: {ex.Message}";
            }
        }

        private void DisableWindowTransparency()
        {
            try
            {
                if (statusText != null)
                    statusText.Text = "窗口透明模式已禁用";
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"禁用透明模式失败: {ex.Message}";
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!isWindowInitialized || mainImage?.Source == null) return;

            var newSize = e.NewSize;

            // 检查新尺寸是否有效
            if (newSize.Width <= 0 || newSize.Height <= 0 ||
                lastWindowSize.Width <= 0 || lastWindowSize.Height <= 0)
                return;

            try
            {
                // 计算窗口大小变化的比例
                double scaleX = newSize.Width / lastWindowSize.Width;
                double scaleY = newSize.Height / lastWindowSize.Height;

                // 获取当前图片的尺寸
                var source = mainImage.Source as BitmapSource;
                if (source == null) return;

                double imageWidth = source.PixelWidth * currentZoom;
                double imageHeight = source.PixelHeight * currentZoom;

                // 计算图片在旧窗口中的中心点
                Point oldImageCenter = new Point(
                    imagePosition.X + imageWidth / 2,
                    imagePosition.Y + imageHeight / 2
                );

                // 计算旧窗口的有效显示区域中心（减去工具栏等UI元素）
                Point oldWindowCenter = new Point(
                    lastWindowSize.Width / 2,
                    (lastWindowSize.Height - 140) / 2 + 140  // 140是大概的工具栏高度
                );

                // 计算新窗口的有效显示区域中心
                Point newWindowCenter = new Point(
                    newSize.Width / 2,
                    (newSize.Height - 140) / 2 + 140
                );

                // 如果有通道面板显示，需要调整有效区域中心
                if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
                {
                    // 旧窗口的有效宽度（减去通道面板）
                    double oldEffectiveWidth = lastWindowSize.Width - 305;
                    if (oldEffectiveWidth < 100) oldEffectiveWidth = 100;

                    // 新窗口的有效宽度（减去通道面板）
                    double newEffectiveWidth = newSize.Width - 305;
                    if (newEffectiveWidth < 100) newEffectiveWidth = 100;

                    // 重新计算有效区域中心（只影响X坐标）
                    oldWindowCenter.X = oldEffectiveWidth / 2;
                    newWindowCenter.X = newEffectiveWidth / 2;
                }

                // 计算图片中心相对于窗口中心的偏移
                Vector offsetFromWindowCenter = oldImageCenter - oldWindowCenter;

                // 如果图片几乎居中（偏移很小），则保持居中
                if (Math.Abs(offsetFromWindowCenter.X) < 50 && Math.Abs(offsetFromWindowCenter.Y) < 50)
                {
                    // 保持居中
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CenterImage();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    // 按窗口缩放比例调整图片位置
                    // 这里使用较小的缩放比例来模拟对角位移效果
                    double avgScale = Math.Min(scaleX, scaleY);

                    // 计算新的图片中心位置
                    Point newImageCenter = newWindowCenter + (offsetFromWindowCenter * avgScale);

                    // 计算新的图片左上角位置
                    imagePosition.X = newImageCenter.X - imageWidth / 2;
                    imagePosition.Y = newImageCenter.Y - imageHeight / 2;

                    // 确保位置值是有效的
                    if (double.IsNaN(imagePosition.X) || double.IsInfinity(imagePosition.X))
                        imagePosition.X = 0;
                    if (double.IsNaN(imagePosition.Y) || double.IsInfinity(imagePosition.Y))
                        imagePosition.Y = 0;

                    UpdateImagePosition();
                }
            }
            catch (Exception ex)
            {
                // 如果出现任何异常，就简单地居中图片
                if (statusText != null)
                    statusText.Text = $"窗口调整时出现问题，已重置图片位置: {ex.Message}";

                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        CenterImage();
                    }
                    catch
                    {
                        // 最后的保护措施
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }

            lastWindowSize = newSize;
        }

        #region 事件处理程序

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                    NavigatePrevious();
                    break;
                case Key.Right:
                    NavigateNext();
                    break;
                case Key.F:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
                        FitToWindow();
                    else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        BtnSearch_Click(sender, e);
                    break;
                case Key.D1:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
                        SetActualSize();
                    break;
                case Key.Space:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
                        CenterImage();
                    break;
                case Key.F11:
                    MenuFullScreen_Click(sender, e);
                    break;
                case Key.Escape:
                    if (WindowState == WindowState.Maximized)
                        WindowState = WindowState.Normal;
                    break;
                case Key.O:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        BtnOpen_Click(sender, e);
                    break;
                case Key.S:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        BtnSaveAs_Click(sender, e);
                    else if (e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                        SaveAppSettings();
                    break;
                case Key.V:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        PasteImageFromClipboard();
                    break;
                case Key.P:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        // Ctrl+P 播放/暂停序列帧
                        if (hasSequenceLoaded)
                            BtnPlay_Click(sender, e);
                    }
                    break;
                case Key.OemPeriod: // . 键
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.None && hasSequenceLoaded)
                        BtnNextFrame_Click(sender, e);
                    break;
                case Key.OemComma: // , 键
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.None && hasSequenceLoaded)
                        BtnPrevFrame_Click(sender, e);
                    break;
                case Key.L:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        RotateImage(-90);
                    break;
                case Key.R:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        RotateImage(90);
                    break;
                case Key.OemPlus:
                case Key.Add:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        ZoomImage(1.2);
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        ZoomImage(0.8);
                    break;
            }
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string file = files[0];
                    string extension = Path.GetExtension(file).ToLower();

                    if (supportedFormats.Contains(extension))
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
                                statusText.Text = "序列帧播放已停止，切换到新图片";
                        }

                        LoadImage(file);
                        var directoryPath = Path.GetDirectoryName(file);
                        if (!string.IsNullOrEmpty(directoryPath))
                        {
                            LoadDirectoryImages(directoryPath);
                        }
                    }
                    else
                    {
                        MessageBox.Show("不支持的文件格式", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "支持的图片格式|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif;*.ico;*.webp;*.tga;*.dds;*.psd|所有文件|*.*";

            if (dialog.ShowDialog() == true)
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
                        statusText.Text = "序列帧播放已停止，切换到新图片";
                }

                LoadImage(dialog.FileName);
                var directoryPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    LoadDirectoryImages(directoryPath);
                }
            }
        }

        private void BtnPrevious_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("Previous");
            NavigatePrevious();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("Next");
            NavigateNext();
        }

        private void BtnRotateLeft_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("RotateLeft");
            RotateImage(-90);
        }

        private void BtnRotateRight_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("RotateRight");
            RotateImage(90);
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (mainImage?.Source == null)
            {
                MessageBox.Show("请先打开一张图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp|TIFF|*.tiff|GIF|*.gif";

            // 设置默认文件名
            if (!string.IsNullOrEmpty(currentImagePath))
            {
                // 如果有文件路径，使用原文件名
                dialog.FileName = Path.GetFileNameWithoutExtension(currentImagePath);
            }
            else
            {
                // 如果是剪贴板图片，使用时间戳作为文件名
                dialog.FileName = $"PastedImage_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            if (dialog.ShowDialog() == true)
            {
                SaveCurrentImage(dialog.FileName);
            }
        }

        /// <summary>
        /// 保存当前显示的图片（支持剪贴板图片和文件图片）
        /// </summary>
        private void SaveCurrentImage(string fileName)
        {
            try
            {
                if (mainImage?.Source == null)
                {
                    throw new InvalidOperationException("没有可保存的图片");
                }

                var source = mainImage.Source as BitmapSource;
                if (source == null)
                {
                    throw new InvalidOperationException("图片格式不支持保存");
                }

                // 如果有原始文件路径且没有旋转变换，直接使用 ImageMagick 处理原文件
                if (!string.IsNullOrEmpty(currentImagePath) && File.Exists(currentImagePath) &&
                    currentTransform == Transform.Identity)
                {
                    SaveRotatedImage(fileName);
                    return;
                }

                // 否则保存当前显示的图片（包括剪贴板图片和有变换的图片）
                SaveBitmapSource(source, fileName);

                if (statusText != null)
                    statusText.Text = $"已保存: {Path.GetFileName(fileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                if (statusText != null)
                    statusText.Text = "保存失败";
            }
        }

        /// <summary>
        /// 保存 BitmapSource 到文件
        /// </summary>
        private void SaveBitmapSource(BitmapSource source, string fileName)
        {
            try
            {
                // 应用当前的旋转变换到图片
                BitmapSource finalSource = source;

                if (currentTransform != Transform.Identity)
                {
                    // 创建一个 TransformedBitmap 来应用变换
                    var transformedBitmap = new TransformedBitmap(source, currentTransform);
                    finalSource = transformedBitmap;
                }

                // 根据文件扩展名选择编码器
                BitmapEncoder encoder;
                string extension = Path.GetExtension(fileName).ToLower();

                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                        break;
                    case ".png":
                        encoder = new PngBitmapEncoder();
                        break;
                    case ".bmp":
                        encoder = new BmpBitmapEncoder();
                        break;
                    case ".tiff":
                    case ".tif":
                        encoder = new TiffBitmapEncoder();
                        break;
                    case ".gif":
                        encoder = new GifBitmapEncoder();
                        break;
                    default:
                        encoder = new PngBitmapEncoder(); // 默认使用 PNG
                        break;
                }

                encoder.Frames.Add(BitmapFrame.Create(finalSource));

                using (var fileStream = new FileStream(fileName, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"图片编码保存失败: {ex.Message}");
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("Search");
            if (searchPanel != null && txtSearch != null)
            {
                searchPanel.Visibility = searchPanel.Visibility == Visibility.Visible ?
                    Visibility.Collapsed : Visibility.Visible;

                if (searchPanel.Visibility == Visibility.Visible)
                {
                    txtSearch.Focus();
                }
            }
        }

        private void BtnCloseSearch_Click(object sender, RoutedEventArgs e)
        {
            if (searchPanel != null)
                searchPanel.Visibility = Visibility.Collapsed;
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && txtSearch != null)
            {
                PerformEverythingSearch(txtSearch.Text);
            }
        }

        private void ChkShowChannels_Checked(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("ShowChannels");
            showChannels = true;
            if (channelPanel != null && channelSplitter != null && channelColumn != null)
            {
                channelPanel.Visibility = Visibility.Visible;
                channelSplitter.Visibility = Visibility.Visible;
                // 设置通道列为300像素宽度，而不是*，这样主图区域会相应缩小
                channelColumn.Width = new GridLength(300);
            }

            if (!string.IsNullOrEmpty(currentImagePath))
            {
                LoadImageChannels(currentImagePath);
            }

            // 同步菜单状态
            if (menuShowChannels != null)
                menuShowChannels.IsChecked = true;
        }

        private void ChkShowChannels_Unchecked(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("HideChannels");
            showChannels = false;
            if (channelPanel != null && channelSplitter != null && channelColumn != null && channelStackPanel != null)
            {
                channelPanel.Visibility = Visibility.Collapsed;
                channelSplitter.Visibility = Visibility.Collapsed;
                // 设置通道列宽度为0，主图区域恢复全宽
                channelColumn.Width = new GridLength(0);
                channelStackPanel.Children.Clear();
            }

            // 同步菜单状态
            if (menuShowChannels != null)
                menuShowChannels.IsChecked = false;
        }

        private void BackgroundType_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                string backgroundType = "";
                if (rb == rbTransparent) backgroundType = "Transparent";
                else if (rb == rbSolidColor) backgroundType = "SolidColor";
                else if (rb == rbImageBackground) backgroundType = "ImageBackground";
                else if (rb == rbWindowTransparent) backgroundType = "WindowTransparent";

                if (!string.IsNullOrEmpty(backgroundType))
                {
                    RecordToolUsage($"Background{backgroundType}");
                }
            }

            // 如果切换到图片背景，但还没有设置背景图片，则加载默认图片
            if (rbImageBackground?.IsChecked == true && backgroundImageBrush == null)
            {
                LoadDefaultBackgroundImage();
            }
            UpdateBackground();
        }

        private void LoadDefaultBackgroundImage()
        {
            try
            {
                var result = imageLoader.LoadDefaultBackgroundImage(AppDomain.CurrentDomain.BaseDirectory);
                backgroundImageBrush = result.Brush;

                if (statusText != null)
                {
                    if (result.UsedFallback)
                        statusText.Text = "默认图片不存在，使用渐变背景";
                    else if (!string.IsNullOrEmpty(result.SourcePath))
                        statusText.Text = $"已加载默认背景图片: {Path.GetFileName(result.SourcePath)}";
                }
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"加载默认背景图片失败: {ex.Message}";
            }
        }

        private void PresetColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorString)
            {
                RecordToolUsage("PresetColor");

                if (rbSolidColor != null)
                    rbSolidColor.IsChecked = true;

                Color color;
                switch (colorString)
                {
                    case "White":
                        color = Colors.White;
                        break;
                    case "Black":
                        color = Colors.Black;
                        break;
                    default:
                        var converter = new BrushConverter();
                        if (converter.ConvertFromString(colorString) is SolidColorBrush brush)
                            color = brush.Color;
                        else
                            return;
                        break;
                }

                currentBackgroundBrush = new SolidColorBrush(color);

                // 更新HSV滑块和颜色选择器
                var (h, s, v) = RgbToHsv(color);

                if (sliderColorSpectrum != null)
                {
                    sliderColorSpectrum.ValueChanged -= ColorSpectrum_ValueChanged;
                    sliderColorSpectrum.Value = h;
                    sliderColorSpectrum.ValueChanged += ColorSpectrum_ValueChanged;
                }

                if (sliderHue != null)
                {
                    sliderHue.ValueChanged -= ColorSlider_ValueChanged;
                    sliderHue.Value = h;
                    sliderHue.ValueChanged += ColorSlider_ValueChanged;
                }

                if (sliderSaturation != null)
                {
                    sliderSaturation.ValueChanged -= ColorSlider_ValueChanged;
                    sliderSaturation.Value = s * 100;
                    sliderSaturation.ValueChanged += ColorSlider_ValueChanged;
                }

                if (sliderBrightness != null)
                {
                    sliderBrightness.ValueChanged -= ColorSlider_ValueChanged;
                    sliderBrightness.Value = v * 100;
                    sliderBrightness.ValueChanged += ColorSlider_ValueChanged;
                }

                if (colorPicker != null)
                    colorPicker.SelectedColor = color;

                UpdateBackground();
            }
        }

        private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 自动切换到纯色背景类型
            if (rbSolidColor != null)
                rbSolidColor.IsChecked = true;

            if (sliderHue != null && sliderSaturation != null && sliderBrightness != null)
            {
                double hue = sliderHue.Value;
                double saturation = sliderSaturation.Value / 100.0;
                double brightness = sliderBrightness.Value / 100.0;

                Color color = HsvToRgb(hue, saturation, brightness);
                currentBackgroundBrush = new SolidColorBrush(color);

                if (colorPicker != null)
                    colorPicker.SelectedColor = color;

                UpdateBackground();
            }
        }

        private void ColorSpectrum_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 自动切换到纯色背景类型
            if (rbSolidColor != null)
                rbSolidColor.IsChecked = true;

            if (sliderColorSpectrum != null)
            {
                // 从快速选色滑块获取色相值
                double hue = sliderColorSpectrum.Value;

                // 使用饱和度100%和明度75%来生成鲜艳的颜色
                double saturation = 1.0;
                double brightness = 0.75;

                Color color = HsvToRgb(hue, saturation, brightness);
                currentBackgroundBrush = new SolidColorBrush(color);

                // 同步更新其他控件
                if (sliderHue != null)
                {
                    sliderHue.ValueChanged -= ColorSlider_ValueChanged;
                    sliderHue.Value = hue;
                    sliderHue.ValueChanged += ColorSlider_ValueChanged;
                }

                if (sliderSaturation != null)
                {
                    sliderSaturation.ValueChanged -= ColorSlider_ValueChanged;
                    sliderSaturation.Value = saturation * 100;
                    sliderSaturation.ValueChanged += ColorSlider_ValueChanged;
                }

                if (sliderBrightness != null)
                {
                    sliderBrightness.ValueChanged -= ColorSlider_ValueChanged;
                    sliderBrightness.Value = brightness * 100;
                    sliderBrightness.ValueChanged += ColorSlider_ValueChanged;
                }

                if (colorPicker != null)
                    colorPicker.SelectedColor = color;

                UpdateBackground();
            }
        }

        private void ColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue && rbSolidColor != null)
            {
                rbSolidColor.IsChecked = true;
                currentBackgroundBrush = new SolidColorBrush(e.NewValue.Value);

                // 更新HSV滑块以匹配选中的颜色
                var (h, s, v) = RgbToHsv(e.NewValue.Value);

                if (sliderColorSpectrum != null)
                {
                    sliderColorSpectrum.ValueChanged -= ColorSpectrum_ValueChanged;
                    sliderColorSpectrum.Value = h;
                    sliderColorSpectrum.ValueChanged += ColorSpectrum_ValueChanged;
                }

                if (sliderHue != null)
                {
                    sliderHue.ValueChanged -= ColorSlider_ValueChanged;
                    sliderHue.Value = h;
                    sliderHue.ValueChanged += ColorSlider_ValueChanged;
                }

                if (sliderSaturation != null)
                {
                    sliderSaturation.ValueChanged -= ColorSlider_ValueChanged;
                    sliderSaturation.Value = s * 100;
                    sliderSaturation.ValueChanged += ColorSlider_ValueChanged;
                }

                if (sliderBrightness != null)
                {
                    sliderBrightness.ValueChanged -= ColorSlider_ValueChanged;
                    sliderBrightness.Value = v * 100;
                    sliderBrightness.ValueChanged += ColorSlider_ValueChanged;
                }

                UpdateBackground();
            }
        }

        private void BtnSelectBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif|所有文件|*.*";
            dialog.Title = "选择背景图片";

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var result = imageLoader.LoadBackgroundImage(dialog.FileName);
                    backgroundImageBrush = result.Brush;

                    if (rbImageBackground != null)
                        rbImageBackground.IsChecked = true;

                    UpdateBackground();

                    if (statusText != null)
                        statusText.Text = $"背景图片已设置: {Path.GetFileName(result.SourcePath)}";
                }
                catch (Exception ex)
                {
                    // 如果加载用户选择的图片失败，尝试加载默认图片
                    if (statusText != null)
                        statusText.Text = $"加载背景图片失败，尝试使用默认图片: {ex.Message}";

                    LoadDefaultBackgroundImage();

                    if (rbImageBackground != null)
                        rbImageBackground.IsChecked = true;

                    UpdateBackground();
                }
            }
            else
            {
                // 用户取消了选择，如果当前没有背景图片，则加载默认图片
                if (backgroundImageBrush == null)
                {
                    LoadDefaultBackgroundImage();

                    if (rbImageBackground != null)
                        rbImageBackground.IsChecked = true;

                    UpdateBackground();
                }
            }
        }

        private void ImageContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (mainImage?.Source == null) return;

            double scaleFactor = e.Delta > 0 ? 1.1 : 0.9;
            double newZoom = currentZoom * scaleFactor;

            // 限制缩放范围
            var originalImage = mainImage.Source as BitmapSource;
            if (originalImage != null)
            {
                double maxZoom = Math.Max(10.0, Math.Max(
                    originalImage.PixelWidth / 50.0,
                    originalImage.PixelHeight / 50.0));
                newZoom = Math.Max(0.05, Math.Min(newZoom, maxZoom));
            }
            else
            {
                newZoom = Math.Max(0.05, Math.Min(newZoom, 20.0));
            }

            // 如果缩放没有变化，直接返回
            if (Math.Abs(newZoom - currentZoom) < 0.001) return;

            // 获取鼠标在容器中的位置
            Point mousePos = e.GetPosition(imageContainer);

            // 计算缩放前图片在鼠标位置的点
            Point mousePosInImage = new Point(
                (mousePos.X - imagePosition.X) / currentZoom,
                (mousePos.Y - imagePosition.Y) / currentZoom
            );

            // 更新缩放
            currentZoom = newZoom;

            // 计算新的图片位置，使鼠标位置在图片上的点保持不变
            imagePosition.X = mousePos.X - (mousePosInImage.X * currentZoom);
            imagePosition.Y = mousePos.Y - (mousePosInImage.Y * currentZoom);

            // 应用变换和位置更新（包含边界约束）
            UpdateImageTransform();
            UpdateZoomText();

            e.Handled = true;
        }

        private void MainImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (mainImage?.Source != null)
            {
                isDragging = true;
                lastMousePosition = e.GetPosition(imageContainer);
                mainImage.CaptureMouse();
                e.Handled = true;
            }
        }

        private void MainImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                mainImage.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void MainImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(imageContainer);
                Point delta = new Point(
                    currentPosition.X - lastMousePosition.X,
                    currentPosition.Y - lastMousePosition.Y
                );

                imagePosition.X += delta.X;
                imagePosition.Y += delta.Y;

                UpdateImagePosition();

                lastMousePosition = currentPosition;
                e.Handled = true;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击标题栏切换最大化/还原
                if (this.WindowState == WindowState.Maximized)
                    this.WindowState = WindowState.Normal;
                else
                    this.WindowState = WindowState.Maximized;
            }
            else
            {
                // 单击拖拽窗口
                this.DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                if (btnMaximize != null)
                    btnMaximize.Content = "🗖";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                if (btnMaximize != null)
                    btnMaximize.Content = "🗗";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("PasteFromToolbar");
            PasteImageFromClipboard();
        }

        #endregion

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

                BitmapImage bitmap = LoadImageWithMagick(imagePath);
                if (bitmap != null && mainImage != null)
                {
                    // 检查是否是GIF文件，如果是则启用动画
                    if (Path.GetExtension(imagePath).ToLower() == ".gif")
                    {
                        LoadGifAnimation(imagePath);
                    }
                    else
                    {
                        // 清除可能的GIF动画
                        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(mainImage, null);
                        mainImage.Source = bitmap;
                    }

                    // 重置变换和缩放
                    currentTransform = Transform.Identity;
                    currentZoom = 1.0;
                    imagePosition = new Point(0, 0);

                    // 检查图片尺寸是否超过窗口尺寸
                    if (imageContainer != null)
                    {
                        double containerWidth = imageContainer.ActualWidth;
                        double containerHeight = imageContainer.ActualHeight;

                        // 如果通道面板显示，需要考虑其占用的空间
                        if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
                        {
                            containerWidth -= 305; // 300(通道面板) + 5(分隔符)
                        }

                        // 如果图片尺寸超过容器的80%，自动适应窗口
                        if (bitmap.PixelWidth > containerWidth * 0.8 || bitmap.PixelHeight > containerHeight * 0.8)
                        {
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                FitToWindow();
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

        private void LoadGifAnimation(string gifPath)
        {
            try
            {
                var image = imageLoader.LoadGifAnimationSource(gifPath);

                // 使用WpfAnimatedGif库来播放GIF动画
                WpfAnimatedGif.ImageBehavior.SetAnimatedSource(mainImage, image);

                if (statusText != null)
                    statusText.Text = $"已加载GIF动画: {Path.GetFileName(gifPath)}";
            }
            catch (Exception ex)
            {
                // 如果GIF加载失败，尝试普通图片加载
                if (statusText != null)
                    statusText.Text = $"GIF动画加载失败，尝试静态显示: {ex.Message}";

                var bitmap = LoadImageWithMagick(gifPath);
                if (bitmap != null)
                {
                    mainImage.Source = bitmap;
                }
            }
        }

        private BitmapImage LoadImageWithMagick(string imagePath)
        {
            try
            {
                return imageLoader.LoadImage(imagePath);
            }
            catch
            {
                return null;
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
                RotateTransform rotate = new RotateTransform(angle);
                if (currentTransform == Transform.Identity)
                {
                    currentTransform = rotate;
                }
                else
                {
                    TransformGroup group = new TransformGroup();
                    group.Children.Add(currentTransform);
                    group.Children.Add(rotate);
                    currentTransform = group;
                }

                mainImage.RenderTransform = currentTransform;
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

        private void UpdateBackground()
        {
            if (imageContainer == null) return;

            if (rbTransparent?.IsChecked == true)
            {
                try
                {
                    var resource = FindResource("CheckerboardBrush");
                    if (resource is System.Windows.Media.Brush brush)
                    {
                        imageContainer.Background = brush;
                    }
                }
                catch
                {
                    imageContainer.Background = System.Windows.Media.Brushes.LightGray;
                }

                // 恢复正常窗口背景
                this.Background = System.Windows.Media.Brushes.White;
            }
            else if (rbSolidColor?.IsChecked == true)
            {
                imageContainer.Background = currentBackgroundBrush;

                // 恢复正常窗口背景
                this.Background = System.Windows.Media.Brushes.White;
            }
            else if (rbImageBackground?.IsChecked == true)
            {
                // 如果没有背景图片，先尝试加载默认图片
                if (backgroundImageBrush == null)
                {
                    LoadDefaultBackgroundImage();
                }

                // 应用背景图片
                if (backgroundImageBrush != null)
                {
                    imageContainer.Background = backgroundImageBrush;
                }
                else
                {
                    // 如果还是没有背景图片，使用浅灰色作为后备
                    imageContainer.Background = System.Windows.Media.Brushes.LightGray;
                }

                // 恢复正常窗口背景
                this.Background = System.Windows.Media.Brushes.White;
            }
            else if (rbWindowTransparent?.IsChecked == true)
            {
                // 设置画布背景为透明
                imageContainer.Background = System.Windows.Media.Brushes.Transparent;

                // 设置整个窗口背景为透明
                this.Background = System.Windows.Media.Brushes.Transparent;
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
                channelStackPanel.Children.Clear();

                // 检查是否可以使用缓存
                if (imagePath == currentChannelCachePath && channelCache.Count > 0)
                {
                    // 直接使用缓存的通道图片
                    foreach (var (name, image) in channelCache)
                    {
                        CreateChannelControl(name, image);
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

                var loadedChannels = imageLoader.LoadChannels(imagePath);

                foreach (var (name, channelImage) in loadedChannels)
                {
                    channelCache.Add(Tuple.Create(name, channelImage));
                    CreateChannelControl(name, channelImage);
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

        private void PerformEverythingSearch(string searchQuery)
        {
            try
            {
                if (statusText != null)
                    statusText.Text = "正在搜索...";

                if (everythingSearch == null)
                {
                    if (statusText != null)
                        statusText.Text = "搜索功能不可用";
                    return;
                }

                var searchResults = everythingSearch.Search(searchQuery, 500);

                if (searchResults.Count > 0)
                {
                    currentImageList = searchResults;
                    currentImageIndex = 0;
                    LoadImage(currentImageList[0]);

                    string searchMode = everythingSearch.IsEverythingAvailable ? "Everything" : "文件系统";
                    if (statusText != null)
                        statusText.Text = $"找到 {searchResults.Count} 个结果 (使用{searchMode}搜索)";
                }
                else
                {
                    if (statusText != null)
                        statusText.Text = "未找到匹配的图片";
                }
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"搜索失败: {ex.Message}";
            }
        }

        #endregion

        #region 新增按钮事件处理程序

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("ZoomIn");
            ZoomImage(1.2);
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("ZoomOut");
            ZoomImage(0.8);
        }

        private void BtnFitWindow_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("FitWindow");
            FitToWindow();
        }

        private void BtnActualSize_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("ActualSize");
            SetActualSize();
        }

        private void BtnCenterImage_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("CenterImage");
            CenterImage();
        }

        private void BtnOpenWith1_Click(object sender, RoutedEventArgs e)
        {
            OpenWithApp(0);
        }

        private void BtnOpenWith2_Click(object sender, RoutedEventArgs e)
        {
            OpenWithApp(1);
        }

        private void BtnOpenWith3_Click(object sender, RoutedEventArgs e)
        {
            OpenWithApp(2);
        }

        private void BtnOpenWithMenu_Click(object sender, RoutedEventArgs e)
        {
            if (btnOpenWithMenu?.ContextMenu != null)
            {
                btnOpenWithMenu.ContextMenu.IsOpen = true;
            }
        }

        private void BtnOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            OpenFileLocation();
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

            // 如果通道面板显示，需要减去通道面板占用的宽度
            double effectiveWidth = containerWidth;
            if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
            {
                // 通道面板宽度是300，还要减去分隔符的5像素宽度
                effectiveWidth = containerWidth - 305; // 300(通道面板) + 5(分隔符)

                // 确保有效宽度不会为负数
                if (effectiveWidth < 100) effectiveWidth = 100; // 至少保留100像素显示区域
            }

            // 计算缩放后的图片尺寸（使用source的像素尺寸和当前缩放）
            var scaledWidth = source.PixelWidth * currentZoom;
            var scaledHeight = source.PixelHeight * currentZoom;

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

        private void UpdateImageTransform()
        {
            if (mainImage?.Source == null) return;

            var source = mainImage.Source as BitmapSource;
            if (source == null) return;

            // 不要直接设置图片的Width和Height，让WPF自动处理
            // 移除这两行以避免大分辨率图片显示问题：
            // mainImage.Width = source.PixelWidth;
            // mainImage.Height = source.PixelHeight;

            // 清除之前的尺寸设置，让图片按原始尺寸显示
            mainImage.ClearValue(FrameworkElement.WidthProperty);
            mainImage.ClearValue(FrameworkElement.HeightProperty);

            // 创建变换组：缩放 + 旋转
            var transformGroup = new TransformGroup();

            // 添加缩放变换
            var scaleTransform = new ScaleTransform(currentZoom, currentZoom);
            transformGroup.Children.Add(scaleTransform);

            // 添加旋转变换（如果有的话）
            if (currentTransform != Transform.Identity)
            {
                transformGroup.Children.Add(currentTransform);
            }

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
                // 使用source的像素尺寸计算当前显示尺寸
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

            // 如果通道面板显示，需要在有效区域内适应
            double effectiveWidth = containerWidth;
            if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
            {
                // 通道面板宽度是300，还要减去分隔符的5像素宽度
                effectiveWidth = containerWidth - 305; // 300(通道面板) + 5(分隔符)

                // 确保有效宽度不会为负数
                if (effectiveWidth < 100) effectiveWidth = 100; // 至少保留100像素显示区域
            }

            // 计算适应窗口的缩放比例 - 使用有效区域
            double scaleX = effectiveWidth / source.PixelWidth;
            double scaleY = containerHeight / source.PixelHeight;
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

            // 如果通道面板显示，需要在有效区域内居中
            double effectiveWidth = containerWidth;
            if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
            {
                // 通道面板宽度是300，还要减去分隔符的5像素宽度
                effectiveWidth = containerWidth - 305; // 300(通道面板) + 5(分隔符)

                // 确保有效宽度不会为负数
                if (effectiveWidth < 100) effectiveWidth = 100; // 至少保留100像素显示区域
            }

            // 计算缩放后的图片尺寸（使用source的像素尺寸）
            var imageWidth = source.PixelWidth * currentZoom;
            var imageHeight = source.PixelHeight * currentZoom;

            // 精确计算居中位置 - 在有效区域内居中
            imagePosition.X = Math.Round((effectiveWidth - imageWidth) / 2.0);
            imagePosition.Y = Math.Round((containerHeight - imageHeight) / 2.0);

            UpdateImageTransform();
            UpdateZoomText();
        }

        #endregion

        #region 文件操作方法

        private void OpenWithApp(int index)
        {
            if (index >= openWithApps.Count)
                return;

            try
            {
                // 获取当前图片的有效路径（包括临时文件）
                string imagePath = GetCurrentImagePath();

                var app = openWithApps[index];

                // 解析可执行文件路径（支持相对路径）
                string resolvedExecutablePath = ResolveExecutablePath(app.ExecutablePath);

                // 检查可执行文件是否存在
                if (!File.Exists(resolvedExecutablePath))
                {
                    MessageBox.Show($"找不到应用程序: {resolvedExecutablePath}\n\n原始路径: {app.ExecutablePath}",
                        "文件不存在", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 格式化参数，确保图片路径被正确引用
                var arguments = string.Format(app.Arguments, imagePath);

                // 创建进程启动信息
                var startInfo = new ProcessStartInfo
                {
                    FileName = resolvedExecutablePath,  // 文件路径会被系统自动处理引号
                    Arguments = arguments,              // 参数中的路径应该已经包含引号
                    UseShellExecute = true,            // 使用Shell执行，系统会自动处理路径中的空格
                    WorkingDirectory = Path.GetDirectoryName(resolvedExecutablePath) ?? ""
                };

                // 启动进程
                Process.Start(startInfo);

                if (statusText != null)
                {
                    string sourceInfo = string.IsNullOrEmpty(currentImagePath) ? "剪贴板图片" : "文件图片";
                    string pathInfo = app.ExecutablePath == resolvedExecutablePath ?
                        app.Name : $"{app.Name} (相对路径)";
                    statusText.Text = $"已用 {pathInfo} 打开 {sourceInfo}";
                }
            }
            catch (Exception ex)
            {
                string errorDetails = $"打开失败: {ex.Message}\n\n";
                if (index < openWithApps.Count)
                {
                    var app = openWithApps[index];
                    string resolvedPath = ResolveExecutablePath(app.ExecutablePath);
                    errorDetails += $"应用名称: {app.Name}\n";
                    errorDetails += $"原始路径: {app.ExecutablePath}\n";
                    errorDetails += $"解析路径: {resolvedPath}\n";
                    errorDetails += $"启动参数: {app.Arguments}";
                }

                MessageBox.Show(errorDetails, "打开失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFileLocation()
        {
            try
            {
                // 如果是原始文件，直接在资源管理器中显示
                if (!string.IsNullOrEmpty(currentImagePath) && File.Exists(currentImagePath))
                {
                    var argument = $"/select, \"{currentImagePath}\"";
                    Process.Start("explorer.exe", argument);

                    if (statusText != null)
                        statusText.Text = "已在资源管理器中显示文件";
                }
                // 如果是剪贴板图片，提示用户并创建临时文件
                else if (mainImage?.Source != null)
                {
                    var result = MessageBox.Show(
                        "当前显示的是剪贴板图片，没有原始文件位置。\n\n" +
                        "是否要创建临时文件并在资源管理器中显示？\n\n" +
                        "注意：临时文件在程序关闭时会被删除。",
                        "剪贴板图片",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        string tempPath = CreateTemporaryImageFile();
                        var argument = $"/select, \"{tempPath}\"";
                        Process.Start("explorer.exe", argument);

                        if (statusText != null)
                            statusText.Text = "已在资源管理器中显示临时文件";
                    }
                }
                else
                {
                    MessageBox.Show("当前没有打开的图片文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件位置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddOpenWithApp_Click(object sender, RoutedEventArgs e)
        {
            ShowOpenWithManager();
        }

        private void ShowOpenWithManager()
        {
            var result = MessageBox.Show(
                "打开方式管理\n\n" +
                "当前已配置的应用程序:\n" +
                GetCurrentAppsDisplay() + "\n\n" +
                "选择操作:\n" +
                "是 - 添加新应用\n" +
                "否 - 删除应用\n" +
                "取消 - 退出管理",
                "打开方式管理",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    AddNewOpenWithApp();
                    break;
                case MessageBoxResult.No:
                    RemoveOpenWithApp();
                    break;
                case MessageBoxResult.Cancel:
                    break;
            }
        }

        private string GetCurrentAppsDisplay()
        {
            if (openWithApps.Count == 0)
                return "暂无配置的应用";

            var display = "";
            for (int i = 0; i < openWithApps.Count; i++)
            {
                display += $"{i + 1}. {openWithApps[i].Name}\n";
            }
            return display.TrimEnd('\n');
        }

        private void AddNewOpenWithApp()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择应用程序",
                Filter = "可执行文件|*.exe|所有文件|*.*",
                InitialDirectory = @"C:\Program Files"
            };

            if (dialog.ShowDialog() == true)
            {
                string appName = Path.GetFileNameWithoutExtension(dialog.FileName);
                string displayName = Interaction.InputBox(
                    $"请输入显示名称:\n\n程序路径: {dialog.FileName}",
                    "添加打开方式", appName);

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    // 检查是否已存在
                    bool exists = openWithApps.Any(app =>
                        string.Equals(app.ExecutablePath, dialog.FileName, StringComparison.OrdinalIgnoreCase));

                    if (exists)
                    {
                        MessageBox.Show("该应用程序已经存在！", "添加失败",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 尝试提取图标
                    string iconPath = ExtractIconFromExecutable(dialog.FileName);

                    var newApp = new OpenWithApp
                    {
                        Name = displayName.Trim(),
                        ExecutablePath = dialog.FileName,
                        Arguments = "\"{0}\"",
                        ShowText = true,
                        IconPath = iconPath
                    };

                    openWithApps.Add(newApp);
                    UpdateOpenWithButtons();
                    UpdateOpenWithMenu();

                    if (statusText != null)
                        statusText.Text = $"已添加打开方式: {displayName}";

                    // 询问是否继续添加
                    var continueResult = MessageBox.Show(
                        $"已成功添加 \"{displayName}\"！\n\n是否继续添加其他应用？",
                        "添加成功",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (continueResult == MessageBoxResult.Yes)
                    {
                        AddNewOpenWithApp();
                    }
                }
            }
        }

        private string ExtractIconFromExecutable(string exePath)
        {
            try
            {
                // 直接从exe提取图标，不再保存临时文件
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                {
                    // 返回exe路径，让后续处理直接从exe提取图标
                    icon.Dispose();
                    return exePath; // 返回exe路径而不是临时文件路径
                }
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"提取图标失败: {ex.Message}";
            }

            return string.Empty;
        }

        private void RemoveOpenWithApp()
        {
            if (openWithApps.Count == 0)
            {
                MessageBox.Show("当前没有配置的应用程序！", "删除失败",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string appsList = "";
            for (int i = 0; i < openWithApps.Count; i++)
            {
                appsList += $"{i + 1}. {openWithApps[i].Name}\n";
            }

            string input = Interaction.InputBox(
                $"请输入要删除的应用程序编号:\n\n{appsList}",
                "删除打开方式", "");

            if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input.Trim(), out int index))
            {
                index--; // 转换为0基索引
                if (index >= 0 && index < openWithApps.Count)
                {
                    var appToRemove = openWithApps[index];
                    var confirmResult = MessageBox.Show(
                        $"确定要删除 \"{appToRemove.Name}\" 吗？",
                        "确认删除",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        openWithApps.RemoveAt(index);
                        UpdateOpenWithButtons();
                        UpdateOpenWithMenu();

                        if (statusText != null)
                            statusText.Text = $"已删除打开方式: {appToRemove.Name}";

                        MessageBox.Show($"已成功删除 \"{appToRemove.Name}\"！", "删除成功",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        // 如果还有应用，询问是否继续删除
                        if (openWithApps.Count > 0)
                        {
                            var continueResult = MessageBox.Show(
                                "是否继续删除其他应用？",
                                "继续删除",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (continueResult == MessageBoxResult.Yes)
                            {
                                RemoveOpenWithApp();
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("无效的编号！", "删除失败",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void UpdateOpenWithButtons()
        {
            var buttons = new[] { btnOpenWith1, btnOpenWith2, btnOpenWith3 };

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    if (i < openWithApps.Count)
                    {
                        buttons[i].Content = openWithApps[i].Name;
                        buttons[i].Visibility = Visibility.Visible;
                        buttons[i].ToolTip = $"用 {openWithApps[i].Name} 打开";
                    }
                    else
                    {
                        buttons[i].Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void UpdateOpenWithMenu()
        {
            if (openWithContextMenu == null) return;

            // 清除所有菜单项
            openWithContextMenu.Items.Clear();

            // 添加自定义打开方式到菜单（显示前3个）
            for (int i = 0; i < Math.Min(openWithApps.Count, 3); i++)
            {
                var app = openWithApps[i];
                var menuItem = new MenuItem
                {
                    Header = app.ShowText ? app.Name : "",
                    Tag = i
                };

                // 解析可执行文件路径并提取图标
                string resolvedExecutablePath = ResolveExecutablePath(app.ExecutablePath);

                if (!string.IsNullOrEmpty(resolvedExecutablePath) && File.Exists(resolvedExecutablePath))
                {
                    try
                    {
                        var icon = System.Drawing.Icon.ExtractAssociatedIcon(resolvedExecutablePath);
                        if (icon != null)
                        {
                            // 转换为WPF可用的BitmapImage
                            using (var bitmap = icon.ToBitmap())
                            {
                                using (var memory = new MemoryStream())
                                {
                                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                                    memory.Position = 0;

                                    var bitmapImage = new BitmapImage();
                                    bitmapImage.BeginInit();
                                    bitmapImage.StreamSource = memory;
                                    bitmapImage.DecodePixelWidth = 16;
                                    bitmapImage.DecodePixelHeight = 16;
                                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmapImage.EndInit();
                                    bitmapImage.Freeze();

                                    var image = new System.Windows.Controls.Image
                                    {
                                        Source = bitmapImage,
                                        Width = 16,
                                        Height = 16
                                    };
                                    menuItem.Icon = image;
                                }
                            }
                            icon.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"提取菜单图标失败 (路径: {resolvedExecutablePath}): {ex.Message}");
                    }
                }

                menuItem.Click += (s, e) =>
                {
                    if (s is MenuItem item && item.Tag is int index)
                        OpenWithApp(index);
                };

                openWithContextMenu.Items.Add(menuItem);
            }

            // 如果有更多应用，添加分隔符
            if (openWithApps.Count > 0)
            {
                openWithContextMenu.Items.Add(new Separator());
            }

            // 添加管理菜单项
            var manageMenuItem = new MenuItem
            {
                Header = "管理打开方式(_M)...",
                FontWeight = FontWeights.Bold
            };
            manageMenuItem.Click += ManageOpenWithApps_Click;
            openWithContextMenu.Items.Add(manageMenuItem);

            // 添加"添加打开方式"菜单项
            var addMenuItem = new MenuItem
            {
                Header = "添加打开方式(_A)..."
            };
            addMenuItem.Click += AddOpenWithApp_Click;
            openWithContextMenu.Items.Add(addMenuItem);
        }

        #endregion

        private void LoadAppSettings()
        {
            try
            {
                isLoadingSettings = true;
                appSettings = SettingsManager.LoadSettings();

                // 应用窗口设置
                if (appSettings.WindowWidth > 0 && appSettings.WindowHeight > 0)
                {
                    this.Width = appSettings.WindowWidth;
                    this.Height = appSettings.WindowHeight;
                }

                if (appSettings.WindowLeft > 0 && appSettings.WindowTop > 0)
                {
                    this.Left = appSettings.WindowLeft;
                    this.Top = appSettings.WindowTop;
                }

                if (appSettings.IsMaximized)
                    this.WindowState = WindowState.Maximized;

                // 延迟恢复控件状态，确保所有控件都已初始化
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 先恢复非背景控件状态
                    SettingsManager.RestoreControlStates(this, appSettings);

                    // 特殊处理背景设置 - 按正确的优先级顺序
                    RestoreBackgroundSettingsWithPriority();

                    // 恢复图像查看状态
                    if (appSettings.LastZoomLevel > 0)
                    {
                        currentZoom = appSettings.LastZoomLevel;
                        UpdateZoomText();
                    }

                    if (appSettings.RememberImagePosition)
                    {
                        imagePosition.X = appSettings.LastImageX;
                        imagePosition.Y = appSettings.LastImageY;
                    }

                    // 恢复打开方式应用
                    openWithApps.Clear();
                    foreach (var appData in appSettings.OpenWithApps)
                    {
                        openWithApps.Add(new OpenWithApp
                        {
                            Name = appData.Name,
                            ExecutablePath = appData.ExecutablePath,
                            Arguments = appData.Arguments,
                            ShowText = appData.ShowText,
                            IconPath = appData.IconPath
                        });
                    }
                    UpdateOpenWithButtons();
                    UpdateOpenWithMenu();

                    isLoadingSettings = false;

                    // 同步工具菜单状态 - 确保设置恢复后菜单状态正确
                    SynchronizeToolMenuStates();

                    if (statusText != null)
                        statusText.Text = $"设置已加载 - 控件状态: {appSettings.ControlStates.Count} 项";

                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                isLoadingSettings = false;
                appSettings = new AppSettings();
                if (statusText != null)
                    statusText.Text = $"加载设置失败，使用默认设置: {ex.Message}";
            }
        }

        // 按正确优先级恢复背景设置
        private void RestoreBackgroundSettingsWithPriority()
        {
            try
            {
                // 第一步：恢复背景图片路径（如果有的话）
                if (!string.IsNullOrEmpty(appSettings.BackgroundImagePath) && File.Exists(appSettings.BackgroundImagePath))
                {
                    LoadBackgroundImageFromPath(appSettings.BackgroundImagePath);
                }

                // 第二步：恢复颜色值（禁用事件处理器以防止自动切换背景类型）
                RestoreColorValues();

                // 第三步：根据颜色值更新派生控件
                UpdateDerivedColorControls();

                // 第四步：最后恢复背景类型选择（这是最高优先级）
                RestoreBackgroundType();

                // 第五步：应用最终的背景设置
                UpdateBackground();
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"恢复背景设置失败: {ex.Message}";
            }
        }

        // 恢复颜色值（不触发事件）
        private void RestoreColorValues()
        {
            // 临时移除事件处理器
            if (sliderHue != null)
            {
                sliderHue.ValueChanged -= ColorSlider_ValueChanged;
                if (appSettings.ControlStates.ContainsKey("sliderHue") &&
                    appSettings.ControlStates["sliderHue"].ContainsKey("Value"))
                {
                    var value = Convert.ToDouble(appSettings.ControlStates["sliderHue"]["Value"]);
                    sliderHue.Value = value;
                }
                sliderHue.ValueChanged += ColorSlider_ValueChanged;
            }

            if (sliderSaturation != null)
            {
                sliderSaturation.ValueChanged -= ColorSlider_ValueChanged;
                if (appSettings.ControlStates.ContainsKey("sliderSaturation") &&
                    appSettings.ControlStates["sliderSaturation"].ContainsKey("Value"))
                {
                    var value = Convert.ToDouble(appSettings.ControlStates["sliderSaturation"]["Value"]);
                    sliderSaturation.Value = value;
                }
                sliderSaturation.ValueChanged += ColorSlider_ValueChanged;
            }

            if (sliderBrightness != null)
            {
                sliderBrightness.ValueChanged -= ColorSlider_ValueChanged;
                if (appSettings.ControlStates.ContainsKey("sliderBrightness") &&
                    appSettings.ControlStates["sliderBrightness"].ContainsKey("Value"))
                {
                    var value = Convert.ToDouble(appSettings.ControlStates["sliderBrightness"]["Value"]);
                    sliderBrightness.Value = value;
                }
                sliderBrightness.ValueChanged += ColorSlider_ValueChanged;
            }

            // 根据HSV值重建当前背景画刷
            if (sliderHue != null && sliderSaturation != null && sliderBrightness != null)
            {
                double hue = sliderHue.Value;
                double saturation = sliderSaturation.Value / 100.0;
                double brightness = sliderBrightness.Value / 100.0;

                Color color = HsvToRgb(hue, saturation, brightness);
                currentBackgroundBrush = new SolidColorBrush(color);
            }
        }

        // 更新派生颜色控件
        private void UpdateDerivedColorControls()
        {
            if (sliderHue != null && sliderSaturation != null && sliderBrightness != null)
            {
                double hue = sliderHue.Value;

                // 更新快速选色滑块
                if (sliderColorSpectrum != null)
                {
                    sliderColorSpectrum.ValueChanged -= ColorSpectrum_ValueChanged;
                    if (appSettings.ControlStates.ContainsKey("sliderColorSpectrum") &&
                        appSettings.ControlStates["sliderColorSpectrum"].ContainsKey("Value"))
                    {
                        var value = Convert.ToDouble(appSettings.ControlStates["sliderColorSpectrum"]["Value"]);
                        sliderColorSpectrum.Value = value;
                    }
                    else
                    {
                        sliderColorSpectrum.Value = hue;
                    }
                    sliderColorSpectrum.ValueChanged += ColorSpectrum_ValueChanged;
                }

                // 更新颜色选择器
                if (colorPicker != null)
                {
                    colorPicker.SelectedColorChanged -= ColorPicker_SelectedColorChanged;
                    if (appSettings.ControlStates.ContainsKey("colorPicker") &&
                        appSettings.ControlStates["colorPicker"].ContainsKey("SelectedColor"))
                    {
                        var colorString = appSettings.ControlStates["colorPicker"]["SelectedColor"].ToString();
                        if (!string.IsNullOrEmpty(colorString))
                        {
                            try
                            {
                                var color = (Color)ColorConverter.ConvertFromString(colorString);
                                colorPicker.SelectedColor = color;
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        // 如果没有保存的颜色选择器值，使用当前HSV值
                        double saturation = sliderSaturation.Value / 100.0;
                        double brightness = sliderBrightness.Value / 100.0;
                        Color color = HsvToRgb(hue, saturation, brightness);
                        colorPicker.SelectedColor = color;
                    }
                    colorPicker.SelectedColorChanged += ColorPicker_SelectedColorChanged;
                }
            }
        }

        // 恢复背景类型（最后执行，覆盖之前的任何自动切换）
        private void RestoreBackgroundType()
        {
            // 临时移除事件处理器以防止触发更新
            if (rbTransparent != null) rbTransparent.Checked -= BackgroundType_Changed;
            if (rbSolidColor != null) rbSolidColor.Checked -= BackgroundType_Changed;
            if (rbImageBackground != null) rbImageBackground.Checked -= BackgroundType_Changed;
            if (rbWindowTransparent != null) rbWindowTransparent.Checked -= BackgroundType_Changed;

            // 恢复RadioButton状态
            if (appSettings.ControlStates.ContainsKey("rbTransparent"))
            {
                var isChecked = Convert.ToBoolean(appSettings.ControlStates["rbTransparent"]["IsChecked"]);
                if (rbTransparent != null) rbTransparent.IsChecked = isChecked;
            }
            if (appSettings.ControlStates.ContainsKey("rbSolidColor"))
            {
                var isChecked = Convert.ToBoolean(appSettings.ControlStates["rbSolidColor"]["IsChecked"]);
                if (rbSolidColor != null) rbSolidColor.IsChecked = isChecked;
            }
            if (appSettings.ControlStates.ContainsKey("rbImageBackground"))
            {
                var isChecked = Convert.ToBoolean(appSettings.ControlStates["rbImageBackground"]["IsChecked"]);
                if (rbImageBackground != null) rbImageBackground.IsChecked = isChecked;
            }
            if (appSettings.ControlStates.ContainsKey("rbWindowTransparent"))
            {
                var isChecked = Convert.ToBoolean(appSettings.ControlStates["rbWindowTransparent"]["IsChecked"]);
                if (rbWindowTransparent != null) rbWindowTransparent.IsChecked = isChecked;
            }

            // 恢复事件处理器
            if (rbTransparent != null) rbTransparent.Checked += BackgroundType_Changed;
            if (rbSolidColor != null) rbSolidColor.Checked += BackgroundType_Changed;
            if (rbImageBackground != null) rbImageBackground.Checked += BackgroundType_Changed;
            if (rbWindowTransparent != null) rbWindowTransparent.Checked += BackgroundType_Changed;
        }

        private void ApplyBackgroundSettings()
        {
            // 解析背景颜色
            try
            {
                var converter = new BrushConverter();
                if (converter.ConvertFromString(appSettings.BackgroundColor) is SolidColorBrush brush)
                {
                    currentBackgroundBrush = brush;
                }
            }
            catch
            {
                currentBackgroundBrush = new SolidColorBrush(Colors.Gray);
            }

            // 设置滑块值
            if (sliderHue != null)
                sliderHue.Value = appSettings.BackgroundHue;
            if (sliderSaturation != null)
                sliderSaturation.Value = appSettings.BackgroundSaturation;
            if (sliderBrightness != null)
                sliderBrightness.Value = appSettings.BackgroundBrightness;

            // 记录最后使用的背景预设
            if (!string.IsNullOrEmpty(appSettings.LastBackgroundPreset))
            {
                // 根据预设名称恢复背景
                switch (appSettings.LastBackgroundPreset)
                {
                    case "White":
                        currentBackgroundBrush = new SolidColorBrush(Colors.White);
                        break;
                    case "Black":
                        currentBackgroundBrush = new SolidColorBrush(Colors.Black);
                        break;
                    case "#808080":
                        currentBackgroundBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));
                        break;
                    case "#C0C0C0":
                        currentBackgroundBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192));
                        break;
                    case "#404040":
                        currentBackgroundBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64));
                        break;
                }
            }

            // 设置背景类型
            switch (appSettings.BackgroundType)
            {
                case "Transparent":
                    if (rbTransparent != null) rbTransparent.IsChecked = true;
                    break;
                case "SolidColor":
                    if (rbSolidColor != null) rbSolidColor.IsChecked = true;
                    break;
                case "Image":
                    if (rbImageBackground != null) rbImageBackground.IsChecked = true;
                    if (!string.IsNullOrEmpty(appSettings.BackgroundImagePath) && File.Exists(appSettings.BackgroundImagePath))
                    {
                        LoadBackgroundImageFromPath(appSettings.BackgroundImagePath);
                    }
                    break;
                case "WindowTransparent":
                    if (rbWindowTransparent != null) rbWindowTransparent.IsChecked = true;
                    break;
            }

            // 应用背景更新
            UpdateBackground();
        }

        private void ApplyUISettings()
        {
            // 设置通道显示状态
            if (chkShowChannels != null)
                chkShowChannels.IsChecked = appSettings.ShowChannels;
            if (menuShowChannels != null)
                menuShowChannels.IsChecked = appSettings.ShowChannels;
            showChannels = appSettings.ShowChannels;

            // 根据showChannels状态显示或隐藏通道面板
            if (showChannels)
            {
                if (channelPanel != null) channelPanel.Visibility = Visibility.Visible;
                if (channelSplitter != null) channelSplitter.Visibility = Visibility.Visible;
                if (channelColumn != null) channelColumn.Width = new GridLength(300);
            }
            else
            {
                if (channelPanel != null) channelPanel.Visibility = Visibility.Collapsed;
                if (channelSplitter != null) channelSplitter.Visibility = Visibility.Collapsed;
                if (channelColumn != null) channelColumn.Width = new GridLength(0);
            }

            // 设置背景面板展开状态
            var bgExpander = this.FindName("backgroundExpander") as Expander;
            if (bgExpander != null)
            {
                bgExpander.IsExpanded = appSettings.BackgroundPanelExpanded;
                // 工具菜单项控制的是工具栏的可见性，默认情况下工具栏应该显示
                // 只有用户通过菜单明确隐藏时才不显示
                bgExpander.Visibility = Visibility.Visible;
            }
            if (menuShowBgToolbar != null)
                menuShowBgToolbar.IsChecked = true; // 默认显示

            // 设置序列帧面板展开状态
            if (sequenceExpander != null)
            {
                sequenceExpander.IsExpanded = appSettings.SequencePlayerExpanded;
                // 序列帧工具栏默认显示
                sequenceExpander.Visibility = Visibility.Visible;
            }
            if (menuShowSequenceToolbar != null)
                menuShowSequenceToolbar.IsChecked = true; // 默认显示

            // 恢复序列帧设置
            if (txtGridWidth != null)
                txtGridWidth.Text = appSettings.LastGridWidth.ToString();
            if (txtGridHeight != null)
                txtGridHeight.Text = appSettings.LastGridHeight.ToString();
            if (txtFPS != null)
                txtFPS.Text = appSettings.LastSequenceFPS.ToString();

            // 设置搜索面板可见性
            if (searchPanel != null)
                searchPanel.Visibility = appSettings.SearchPanelVisible ? Visibility.Visible : Visibility.Collapsed;

            // 从设置中恢复打开方式应用
            openWithApps.Clear();
            foreach (var appData in appSettings.OpenWithApps)
            {
                openWithApps.Add(new OpenWithApp
                {
                    Name = appData.Name,
                    ExecutablePath = appData.ExecutablePath,
                    Arguments = appData.Arguments,
                    ShowText = appData.ShowText,
                    IconPath = appData.IconPath
                });
            }
            UpdateOpenWithButtons();
            UpdateOpenWithMenu();

            // 恢复图像查看设置
            if (appSettings.LastZoomLevel > 0)
            {
                currentZoom = appSettings.LastZoomLevel;
                UpdateZoomText();
            }

            // 恢复图像位置
            if (appSettings.RememberImagePosition)
            {
                imagePosition.X = appSettings.LastImageX;
                imagePosition.Y = appSettings.LastImageY;
            }
        }

        private void LoadBackgroundImageFromPath(string imagePath)
        {
            try
            {
                var result = imageLoader.LoadBackgroundImage(imagePath);
                backgroundImageBrush = result.Brush;
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"加载背景图片失败: {ex.Message}";
            }
        }

        private void SaveAppSettings()
        {
            if (isLoadingSettings || appSettings == null) return;

            try
            {
                // 保存窗口状态
                if (this.WindowState == WindowState.Normal)
                {
                    appSettings.WindowWidth = this.Width;
                    appSettings.WindowHeight = this.Height;
                    appSettings.WindowLeft = this.Left;
                    appSettings.WindowTop = this.Top;
                }
                appSettings.IsMaximized = this.WindowState == WindowState.Maximized;

                // 保存图像查看状态
                appSettings.LastZoomLevel = currentZoom;
                if (appSettings.RememberImagePosition)
                {
                    appSettings.LastImageX = imagePosition.X;
                    appSettings.LastImageY = imagePosition.Y;
                }

                // 保存当前文件到最近文件列表
                if (!string.IsNullOrEmpty(currentImagePath))
                {
                    SettingsManager.AddRecentFile(appSettings, currentImagePath);
                }

                // 保存打开方式应用
                appSettings.OpenWithApps.Clear();
                foreach (var app in openWithApps)
                {
                    appSettings.OpenWithApps.Add(new OpenWithAppData
                    {
                        Name = app.Name,
                        ExecutablePath = app.ExecutablePath,
                        Arguments = app.Arguments,
                        ShowText = app.ShowText,
                        IconPath = app.IconPath
                    });
                }

                // 统一保存所有控件状态 - 这是新的核心功能
                SettingsManager.SaveControlStates(this, appSettings);

                // 保存设置到文件
                SettingsManager.SaveSettings(appSettings);

                if (statusText != null)
                    statusText.Text = $"设置已保存 - 控件状态: {appSettings.ControlStates.Count} 项";
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"保存设置失败: {ex.Message}";
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 清理临时文件
            CleanupTemporaryFile();

            SaveAppSettings();
        }

        #region 菜单事件处理

        private void MenuOpen_Click(object sender, RoutedEventArgs e)
        {
            BtnOpen_Click(sender, e);
        }

        private void MenuSaveAs_Click(object sender, RoutedEventArgs e)
        {
            BtnSaveAs_Click(sender, e);
        }

        private void MenuPasteImage_Click(object sender, RoutedEventArgs e)
        {
            PasteImageFromClipboard();
        }

        private void MenuOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            BtnOpenLocation_Click(sender, e);
        }

        private void MenuSearch_Click(object sender, RoutedEventArgs e)
        {
            BtnSearch_Click(sender, e);
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MenuFitWindow_Click(object sender, RoutedEventArgs e)
        {
            BtnFitWindow_Click(sender, e);
        }

        private void MenuActualSize_Click(object sender, RoutedEventArgs e)
        {
            BtnActualSize_Click(sender, e);
        }

        private void MenuCenterImage_Click(object sender, RoutedEventArgs e)
        {
            BtnCenterImage_Click(sender, e);
        }

        private void MenuFullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized && this.WindowStyle == WindowStyle.None)
            {
                // 退出全屏
                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.None; // 保持无边框
            }
            else
            {
                // 进入全屏
                this.WindowState = WindowState.Maximized;
                this.WindowStyle = WindowStyle.None;
            }
        }

        private void MenuShowChannels_Click(object sender, RoutedEventArgs e)
        {
            if (chkShowChannels != null)
                chkShowChannels.IsChecked = menuShowChannels?.IsChecked ?? false;
        }

        private void MenuPrevious_Click(object sender, RoutedEventArgs e)
        {
            BtnPrevious_Click(sender, e);
        }

        private void MenuNext_Click(object sender, RoutedEventArgs e)
        {
            BtnNext_Click(sender, e);
        }

        private void MenuRotateLeft_Click(object sender, RoutedEventArgs e)
        {
            BtnRotateLeft_Click(sender, e);
        }

        private void MenuRotateRight_Click(object sender, RoutedEventArgs e)
        {
            BtnRotateRight_Click(sender, e);
        }

        private void MenuZoomIn_Click(object sender, RoutedEventArgs e)
        {
            BtnZoomIn_Click(sender, e);
        }

        private void MenuZoomOut_Click(object sender, RoutedEventArgs e)
        {
            BtnZoomOut_Click(sender, e);
        }

        private void MenuBgTransparent_Click(object sender, RoutedEventArgs e)
        {
            if (rbTransparent != null)
                rbTransparent.IsChecked = true;
            UpdateMenuBackgroundStates();
        }

        private void MenuBgSolid_Click(object sender, RoutedEventArgs e)
        {
            if (rbSolidColor != null)
                rbSolidColor.IsChecked = true;
            UpdateMenuBackgroundStates();
        }

        private void MenuBgImage_Click(object sender, RoutedEventArgs e)
        {
            if (rbImageBackground != null)
                rbImageBackground.IsChecked = true;
            UpdateMenuBackgroundStates();
        }

        private void MenuBgWindowTransparent_Click(object sender, RoutedEventArgs e)
        {
            if (rbWindowTransparent != null)
                rbWindowTransparent.IsChecked = true;
            UpdateMenuBackgroundStates();
        }

        private void MenuSelectBgImage_Click(object sender, RoutedEventArgs e)
        {
            BtnSelectBackgroundImage_Click(sender, e);
        }

        private void MenuSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveAppSettings();
            if (statusText != null)
                statusText.Text = "设置已手动保存";
        }

        private void MenuResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要还原到默认设置吗？这将清除所有自定义设置，并需要重启应用才能完全生效。", "还原设置",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.ResetToDefault();
                MessageBox.Show("设置已重置。请重新启动 PicViewEx。", "操作完成", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close(); // Close the application to apply changes on restart
            }
        }

        private void MenuExpandBgPanel_Click(object sender, RoutedEventArgs e)
        {
            var bgExpander = this.FindName("backgroundExpander") as Expander;
            if (bgExpander != null)
            {
                // bgExpander.IsExpanded = menuExpandBgPanel?.IsChecked ?? true;
                // SaveAppSettings();
            }
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            string controlTest = SettingsManager.TestControlSaving(this);

            MessageBox.Show("PicViewEx - Advanced Image Viewer\n\n" +
                "版本: 1.0.1\n" +
                "一个功能强大的图片查看器\n\n" +
                "功能特色:\n" +
                "• 支持多种图片格式\n" +
                "• GIF动画播放\n" +
                "• RGB/Alpha通道显示\n" +
                "• 自定义背景设置\n" +
                "• 序列帧播放器\n" +
                "• 剪贴板图片粘贴 (NEW!)\n" +
                "• 打开方式管理\n" +
                "• 命令行参数支持\n" +
                "• 设置自动保存\n\n" +
                "序列帧功能:\n" +
                "支持将网格状图片（如3×3，6×6）解析为动画序列\n" +
                "可控制播放速度，手动逐帧查看，导出为GIF\n\n" +
                "剪贴板功能 (NEW!):\n" +
                "• 支持从网页复制图片 (Ctrl+V)\n" +
                "• 支持剪贴板图片的另存为功能\n" +
                "• 支持剪贴板图片的打开方式（自动创建临时文件）\n" +
                "• 自动清理临时文件\n\n" +
                "控件状态测试:\n" + controlTest,
                "关于PicViewEx", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuKeyboardHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("快捷键帮助:\n\n" +
                "文件操作:\n" +
                "Ctrl+O - 打开文件\n" +
                "Ctrl+S - 另存为\n" +
                "Ctrl+V - 粘贴图片 (NEW!)\n" +
                "Ctrl+F - 搜索图片\n\n" +
                "图片浏览:\n" +
                "Left/Right - 上一张/下一张\n" +
                "F - 适应窗口\n" +
                "1 - 实际大小\n" +
                "Space - 居中显示\n" +
                "F11 - 全屏\n\n" +
                "图片操作:\n" +
                "Ctrl+L - 左旋转\n" +
                "Ctrl+R - 右旋转\n" +
                "Ctrl++ - 放大\n" +
                "Ctrl+- - 缩小\n\n" +
                "序列帧播放:\n" +
                "Ctrl+P - 播放/暂停序列\n" +
                ", (逗号) - 上一帧\n" +
                ". (句号) - 下一帧\n\n" +
                "粘贴功能说明:\n" +
                "• 支持从网页复制的图片\n" +
                "• 支持从其他应用复制的图片\n" +
                "• 支持从文件管理器复制的图片文件\n" +
                "• 粘贴前会显示确认对话框\n\n" +
                "设置:\n" +
                "Ctrl+Shift+S - 保存设置\n" +
                "Alt+F4 - 退出",
                "快捷键帮助", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateMenuBackgroundStates()
        {
            if (menuBgTransparent != null)
                menuBgTransparent.IsChecked = rbTransparent?.IsChecked ?? false;
            if (menuBgSolid != null)
                menuBgSolid.IsChecked = rbSolidColor?.IsChecked ?? false;
            if (menuBgImage != null)
                menuBgImage.IsChecked = rbImageBackground?.IsChecked ?? false;
            if (menuBgWindowTransparent != null)
                menuBgWindowTransparent.IsChecked = rbWindowTransparent?.IsChecked ?? false;
        }

        #endregion

        // 添加工具使用记录方法
        private void RecordToolUsage(string toolName)
        {
            if (appSettings != null && !string.IsNullOrEmpty(toolName))
            {
                SettingsManager.AddRecentTool(appSettings, toolName);

                // 如果启用自动保存，立即保存设置
                if (appSettings.AutoSaveSettings)
                {
                    SaveAppSettings();
                }
            }
        }

        #region 序列帧播放功能

        // 序列帧播放相关变量
        private List<BitmapSource> sequenceFrames = new List<BitmapSource>();
        private int currentFrameIndex = 0;
        private System.Windows.Threading.DispatcherTimer sequenceTimer;
        private bool isSequencePlaying = false;
        private int gridWidth = 3;
        private int gridHeight = 3;
        private bool hasSequenceLoaded = false;
        private BitmapSource originalImage;

        private void InitializeSequencePlayer()
        {
            // 初始化序列帧播放定时器
            sequenceTimer = new System.Windows.Threading.DispatcherTimer();
            sequenceTimer.Tick += SequenceTimer_Tick;
            UpdateSequenceTimerInterval();
        }

        private void UpdateSequenceTimerInterval()
        {
            if (sequenceTimer != null && txtFPS != null)
            {
                try
                {
                    int fps = int.Parse(txtFPS.Text);
                    if (fps > 0 && fps <= 120)
                    {
                        sequenceTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
                    }
                    else
                    {
                        txtFPS.Text = "10";
                        sequenceTimer.Interval = TimeSpan.FromMilliseconds(100); // 10 FPS
                    }
                }
                catch
                {
                    txtFPS.Text = "10";
                    sequenceTimer.Interval = TimeSpan.FromMilliseconds(100); // 10 FPS
                }
            }
        }

        // 解析网格按钮事件
        private void BtnParseGrid_Click(object sender, RoutedEventArgs e)
        {
            if (mainImage?.Source == null)
            {
                MessageBox.Show("请先打开一张图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 获取网格设置
                gridWidth = int.Parse(txtGridWidth.Text);
                gridHeight = int.Parse(txtGridHeight.Text);

                if (gridWidth <= 0 || gridHeight <= 0 || gridWidth > 20 || gridHeight > 20)
                {
                    MessageBox.Show("网格尺寸必须在1-20之间", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ParseImageToSequence();
                RecordToolUsage("ParseSequence");

                if (statusText != null)
                    statusText.Text = $"已解析为 {sequenceFrames.Count} 帧序列 ({gridWidth}×{gridHeight})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解析失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 将图片解析为序列帧
        private void ParseImageToSequence()
        {
            if (mainImage?.Source == null) return;

            try
            {
                var source = mainImage.Source as BitmapSource;
                if (source == null) return;

                originalImage = source;
                sequenceFrames.Clear();

                int frameWidth = source.PixelWidth / gridWidth;
                int frameHeight = source.PixelHeight / gridHeight;

                if (frameWidth <= 0 || frameHeight <= 0)
                {
                    MessageBox.Show("图片尺寸太小，无法按指定网格分割", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (statusText != null)
                    statusText.Text = $"正在解析 {gridWidth}×{gridHeight} 网格...";

                // 按网格切分图片
                int totalFrames = gridWidth * gridHeight;
                for (int row = 0; row < gridHeight; row++)
                {
                    for (int col = 0; col < gridWidth; col++)
                    {
                        int x = col * frameWidth;
                        int y = row * frameHeight;

                        // 确保不会超出图片边界
                        int actualWidth = Math.Min(frameWidth, source.PixelWidth - x);
                        int actualHeight = Math.Min(frameHeight, source.PixelHeight - y);

                        if (actualWidth > 0 && actualHeight > 0)
                        {
                            // 创建裁剪区域
                            var cropRect = new Int32Rect(x, y, actualWidth, actualHeight);

                            // 裁剪帧
                            var frame = new CroppedBitmap(source, cropRect);
                            frame.Freeze();

                            sequenceFrames.Add(frame);
                        }

                        // 更新进度
                        int currentFrame = row * gridWidth + col + 1;
                        if (statusText != null)
                            statusText.Text = $"正在解析帧 {currentFrame}/{totalFrames}...";
                    }
                }

                currentFrameIndex = 0;
                hasSequenceLoaded = true;

                // 启用控件
                EnableSequenceControls(true);

                // 显示第一帧
                ShowCurrentFrame();
                UpdateFrameDisplay();

                // 解析完成后自动居中显示第一帧，提供更好的用户体验
                CenterImage();
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"序列解析失败: {ex.Message}";
                MessageBox.Show($"序列解析失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 显示当前帧
        private void ShowCurrentFrame()
        {
            if (hasSequenceLoaded && currentFrameIndex >= 0 && currentFrameIndex < sequenceFrames.Count)
            {
                mainImage.Source = sequenceFrames[currentFrameIndex];

                // 保持当前的缩放和位置状态，让序列帧像正常图片一样可以拖动和缩放
                // 移除强制重置，这样更人性化
                // currentZoom = 1.0;
                // currentTransform = Transform.Identity;
                // imagePosition = new Point(0, 0);

                // 只更新图片变换，保持当前状态
                UpdateImageTransform();
                UpdateZoomText();
            }
        }

        // 更新帧显示信息
        private void UpdateFrameDisplay()
        {
            if (txtCurrentFrame != null)
            {
                if (hasSequenceLoaded && sequenceFrames.Count > 0)
                {
                    txtCurrentFrame.Text = $"{currentFrameIndex + 1} / {sequenceFrames.Count}";
                }
                else
                {
                    txtCurrentFrame.Text = "- / -";
                }
            }
        }

        // 启用/禁用序列控件
        private void EnableSequenceControls(bool enabled)
        {
            if (btnPlay != null) btnPlay.IsEnabled = enabled;
            if (btnStop != null) btnStop.IsEnabled = enabled;
            if (btnFirstFrame != null) btnFirstFrame.IsEnabled = enabled;
            if (btnPrevFrame != null) btnPrevFrame.IsEnabled = enabled;
            if (btnNextFrame != null) btnNextFrame.IsEnabled = enabled;
            if (btnLastFrame != null) btnLastFrame.IsEnabled = enabled;
            if (btnSaveAsGif != null) btnSaveAsGif.IsEnabled = enabled;
            if (btnResetSequence != null) btnResetSequence.IsEnabled = enabled;
        }

        // 播放/暂停按钮事件
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded) return;

            if (isSequencePlaying)
            {
                PauseSequence();
            }
            else
            {
                PlaySequence();
            }

            RecordToolUsage("SequencePlay");
        }

        // 开始播放
        private void PlaySequence()
        {
            if (sequenceTimer == null || !hasSequenceLoaded) return;

            UpdateSequenceTimerInterval();
            isSequencePlaying = true;
            sequenceTimer.Start();

            if (btnPlay != null)
                btnPlay.Content = "⏸ 暂停";

            if (statusText != null)
                statusText.Text = "序列播放中...";
        }

        // 暂停播放
        private void PauseSequence()
        {
            if (sequenceTimer == null) return;

            isSequencePlaying = false;
            sequenceTimer.Stop();

            if (btnPlay != null)
                btnPlay.Content = "▶ 播放";

            if (statusText != null)
                statusText.Text = "序列播放已暂停";
        }

        // 停止播放按钮事件
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopSequence();
            RecordToolUsage("SequenceStop");
        }

        // 停止播放并重置
        private void StopSequence()
        {
            if (sequenceTimer == null) return;

            isSequencePlaying = false;
            sequenceTimer.Stop();
            currentFrameIndex = 0;

            if (btnPlay != null)
                btnPlay.Content = "▶ 播放";

            ShowCurrentFrame();
            UpdateFrameDisplay();

            if (statusText != null)
                statusText.Text = "序列播放已停止并重置";
        }

        // 序列定时器事件
        private void SequenceTimer_Tick(object sender, EventArgs e)
        {
            if (!hasSequenceLoaded || sequenceFrames.Count == 0) return;

            currentFrameIndex++;
            if (currentFrameIndex >= sequenceFrames.Count)
            {
                currentFrameIndex = 0; // 循环播放
            }

            ShowCurrentFrame();
            UpdateFrameDisplay();
        }

        // 第一帧按钮事件
        private void BtnFirstFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded) return;

            currentFrameIndex = 0;
            ShowCurrentFrame();
            UpdateFrameDisplay();
            RecordToolUsage("SequenceFirstFrame");
        }

        // 上一帧按钮事件
        private void BtnPrevFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded) return;

            currentFrameIndex--;
            if (currentFrameIndex < 0)
                currentFrameIndex = sequenceFrames.Count - 1;

            ShowCurrentFrame();
            UpdateFrameDisplay();
            RecordToolUsage("SequencePrevFrame");
        }

        // 下一帧按钮事件
        private void BtnNextFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded) return;

            currentFrameIndex++;
            if (currentFrameIndex >= sequenceFrames.Count)
                currentFrameIndex = 0;

            ShowCurrentFrame();
            UpdateFrameDisplay();
            RecordToolUsage("SequenceNextFrame");
        }

        // 最后一帧按钮事件
        private void BtnLastFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded) return;

            currentFrameIndex = sequenceFrames.Count - 1;
            ShowCurrentFrame();
            UpdateFrameDisplay();
            RecordToolUsage("SequenceLastFrame");
        }

        // 网格预设选择事件
        private void CbGridPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbGridPresets?.SelectedItem is ComboBoxItem selected)
            {
                string preset = selected.Content.ToString() ?? "";

                switch (preset)
                {
                    case "3×3":
                        SetGridSize(3, 3);
                        break;
                    case "4×4":
                        SetGridSize(4, 4);
                        break;
                    case "5×5":
                        SetGridSize(5, 5);
                        break;
                    case "6×6":
                        SetGridSize(6, 6);
                        break;
                    case "8×8":
                        SetGridSize(8, 8);
                        break;
                    case "2×4":
                        SetGridSize(2, 4);
                        break;
                    case "4×2":
                        SetGridSize(4, 2);
                        break;
                    case "自定义":
                        // 不改变当前值，让用户手动输入
                        break;
                }
            }
        }

        // 设置网格尺寸
        private void SetGridSize(int width, int height)
        {
            if (txtGridWidth != null) txtGridWidth.Text = width.ToString();
            if (txtGridHeight != null) txtGridHeight.Text = height.ToString();
        }

        // 保存为GIF按钮事件
        private void BtnSaveAsGif_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded || sequenceFrames.Count == 0)
            {
                MessageBox.Show("请先解析序列帧", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "GIF动画|*.gif";
                dialog.FileName = Path.GetFileNameWithoutExtension(currentImagePath) + "_sequence";

                if (dialog.ShowDialog() == true)
                {
                    SaveSequenceAsGif(dialog.FileName);
                    RecordToolUsage("SaveAsGif");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存GIF失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 保存序列为GIF动画
        private void SaveSequenceAsGif(string fileName)
        {
            try
            {
                using (var gifImage = new MagickImageCollection())
                {
                    int fps = 10;
                    try
                    {
                        fps = int.Parse(txtFPS.Text);
                        if (fps <= 0 || fps > 120) fps = 10;
                    }
                    catch
                    {
                        fps = 10;
                    }

                    int delay = Math.Max(1, 100 / fps); // GIF delay in 1/100s

                    foreach (var frame in sequenceFrames)
                    {
                        // 将WPF BitmapSource转换为ImageMagick可用的格式
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(frame));

                        using (var stream = new MemoryStream())
                        {
                            encoder.Save(stream);
                            stream.Position = 0;

                            var magickFrame = new MagickImage(stream);
                            magickFrame.AnimationDelay = (uint)delay;
                            magickFrame.GifDisposeMethod = GifDisposeMethod.Background;

                            gifImage.Add(magickFrame);
                        }
                    }

                    // 设置GIF格式和选项
                    foreach (var image in gifImage)
                    {
                        image.Format = MagickFormat.Gif;
                    }

                    // 保存GIF
                    gifImage.Write(fileName);

                    if (statusText != null)
                        statusText.Text = $"GIF动画已保存: {Path.GetFileName(fileName)} ({sequenceFrames.Count}帧, {fps}FPS)";
                }
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"保存GIF失败: {ex.Message}";
                throw;
            }
        }

        // 重置序列播放器到原始图片
        private void ResetToOriginalImage()
        {
            if (originalImage != null && mainImage != null)
            {
                // 停止播放
                if (isSequencePlaying)
                {
                    StopSequence();
                }

                // 恢复原始图片
                mainImage.Source = originalImage;

                // 重置序列状态
                hasSequenceLoaded = false;
                sequenceFrames.Clear();
                currentFrameIndex = 0;

                // 禁用序列控件
                EnableSequenceControls(false);
                UpdateFrameDisplay();

                // 重置缩放和变换（不影响背景设置）
                currentZoom = 1.0;
                currentTransform = Transform.Identity;
                imagePosition = new Point(0, 0);

                // 更新图片显示
                UpdateImageTransform();
                UpdateZoomText();

                if (statusText != null)
                    statusText.Text = "已恢复到原始图片";
            }
        }

        #endregion

        private void MenuShowSequencePlayer_Click(object sender, RoutedEventArgs e)
        {
            // 这个方法已被弃用 - 新的工具菜单使用MenuShowSequenceToolbar_Click
            // if (sequenceExpander != null && menuShowSequencePlayer != null)
            // {
            //     sequenceExpander.IsExpanded = menuShowSequencePlayer.IsChecked == true;
            //     SaveAppSettings();
            // }
        }

        // 重置序列按钮事件
        private void BtnResetSequence_Click(object sender, RoutedEventArgs e)
        {
            ResetToOriginalImage();
            RecordToolUsage("ResetSequence");
        }

        private void ManageOpenWithApps_Click(object sender, RoutedEventArgs e)
        {
            var manageWindow = new OpenWithManagerWindow(openWithApps);
            manageWindow.Owner = this;
            manageWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (manageWindow.ShowDialog() == true)
            {
                // 用户确认了更改，更新应用列表
                openWithApps.Clear();
                foreach (var viewModel in manageWindow.OpenWithApps)
                {
                    openWithApps.Add(viewModel.ToOpenWithApp());
                }

                UpdateOpenWithButtons();
                UpdateOpenWithMenu();
                SaveAppSettings(); // 立即保存设置

                if (statusText != null)
                    statusText.Text = $"打开方式设置已更新，共 {openWithApps.Count} 个应用";
            }
        }

        private void ImageContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (mainImage?.Source == null)
            {
                // 如果没有图片，直接拖动窗口
                this.DragMove();
                e.Handled = true;
                return;
            }

            // 获取鼠标在容器中的位置
            Point mousePos = e.GetPosition(imageContainer);

            // 计算图片的显示区域
            var source = mainImage.Source as BitmapSource;
            if (source != null)
            {
                // 计算图片的实际显示区域（考虑缩放和位置）
                double imageWidth = source.PixelWidth * currentZoom;
                double imageHeight = source.PixelHeight * currentZoom;

                // 图片的边界框
                double imageLeft = imagePosition.X;
                double imageTop = imagePosition.Y;
                double imageRight = imageLeft + imageWidth;
                double imageBottom = imageTop + imageHeight;

                // 检查鼠标是否在图片区域内
                bool isInImageArea = mousePos.X >= imageLeft && mousePos.X <= imageRight &&
                                   mousePos.Y >= imageTop && mousePos.Y <= imageBottom;

                if (isInImageArea)
                {
                    // 在图片区域内，传递给原有的图片拖动处理
                    MainImage_MouseLeftButtonDown(sender, e);
                }
                else
                {
                    // 在空白区域，拖动整个窗口
                    try
                    {
                        this.DragMove();
                    }
                    catch (InvalidOperationException)
                    {
                        // 处理可能的拖动异常（比如快速点击时）
                    }
                    e.Handled = true;
                }
            }
            else
            {
                // 如果无法获取图片信息，默认拖动窗口
                this.DragMove();
                e.Handled = true;
            }
        }

        #region 剪贴板图片粘贴功能

        /// <summary>
        /// 从剪贴板粘贴图片
        /// </summary>
        private void PasteImageFromClipboard()
        {
            try
            {
                // 检查剪贴板是否包含图像数据
                if (!Clipboard.ContainsImage() && !Clipboard.ContainsFileDropList())
                {
                    if (statusText != null)
                        statusText.Text = "剪贴板中没有检测到图像数据";
                    return;
                }

                BitmapSource clipboardImage = null;
                string sourceInfo = "";

                // 优先尝试获取图像数据
                if (Clipboard.ContainsImage())
                {
                    clipboardImage = Clipboard.GetImage();
                    sourceInfo = "剪贴板图像";
                }
                // 如果没有直接的图像，尝试从文件列表获取
                else if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    foreach (string file in files)
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        if (supportedFormats.Contains(extension))
                        {
                            // 找到第一个支持的图片文件
                            clipboardImage = LoadImageWithMagick(file);
                            sourceInfo = $"剪贴板文件: {Path.GetFileName(file)}";
                            break;
                        }
                    }
                }

                if (clipboardImage == null)
                {
                    if (statusText != null)
                        statusText.Text = "无法从剪贴板获取有效的图像数据";
                    return;
                }

                // 显示确认对话框
                var result = ShowPasteConfirmDialog(sourceInfo, clipboardImage);

                if (result == MessageBoxResult.Yes)
                {
                    LoadImageFromClipboard(clipboardImage, sourceInfo);
                    RecordToolUsage("PasteFromClipboard");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴图片失败: {ex.Message}", "粘贴错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (statusText != null)
                    statusText.Text = $"粘贴失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 显示粘贴确认对话框
        /// </summary>
        private MessageBoxResult ShowPasteConfirmDialog(string sourceInfo, BitmapSource image)
        {
            string message = $"检测到图像数据！\n\n" +
                           $"来源: {sourceInfo}\n" +
                           $"尺寸: {image.PixelWidth} × {image.PixelHeight}\n" +
                           $"格式: {image.Format}\n\n" +
                           $"是否将当前图片更新为粘贴的图像？\n\n" +
                           $"注意: 这将替换当前显示的图片";

            return MessageBox.Show(message, "发现剪贴板图像",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        }

        /// <summary>
        /// 从剪贴板加载图像到查看器
        /// </summary>
        private void LoadImageFromClipboard(BitmapSource clipboardImage, string sourceInfo)
        {
            try
            {
                // 如果当前有序列帧在播放，停止并重置
                if (hasSequenceLoaded)
                {
                    if (isSequencePlaying)
                    {
                        PauseSequence();
                    }

                    hasSequenceLoaded = false;
                    sequenceFrames.Clear();
                    currentFrameIndex = 0;
                    originalImage = null;

                    EnableSequenceControls(false);
                    UpdateFrameDisplay();
                }

                // 清除之前的图片路径信息，因为这是从剪贴板来的
                currentImagePath = "";
                currentImageList.Clear();
                currentImageIndex = -1;

                // 清除可能的GIF动画
                WpfAnimatedGif.ImageBehavior.SetAnimatedSource(mainImage, null);

                // 设置图片源
                mainImage.Source = clipboardImage;

                // 重置变换和缩放
                currentTransform = Transform.Identity;
                currentZoom = 1.0;
                imagePosition = new Point(0, 0);

                // 检查图片尺寸是否超过窗口尺寸，决定是否自动适应
                if (imageContainer != null)
                {
                    double containerWidth = imageContainer.ActualWidth;
                    double containerHeight = imageContainer.ActualHeight;

                    // 如果通道面板显示，需要考虑其占用的空间
                    if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
                    {
                        containerWidth -= 305;
                    }

                    // 如果图片尺寸超过容器的80%，自动适应窗口
                    if (clipboardImage.PixelWidth > containerWidth * 0.8 ||
                        clipboardImage.PixelHeight > containerHeight * 0.8)
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            FitToWindow();
                            if (statusText != null)
                                statusText.Text = $"已粘贴并自动适应窗口: {sourceInfo}";
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                    else
                    {
                        // 否则居中显示
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            CenterImage();
                            if (statusText != null)
                                statusText.Text = $"已粘贴: {sourceInfo}";
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }

                // 更新图片信息显示
                UpdateImageInfoForClipboard(clipboardImage, sourceInfo);

                // 如果显示通道面板，尝试生成通道（但可能会失败，因为没有文件路径）
                if (showChannels)
                {
                    LoadClipboardImageChannels(clipboardImage);
                }

                if (statusText != null && !showChannels)
                    statusText.Text = $"已从剪贴板粘贴: {sourceInfo}";

            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载剪贴板图片失败: {ex.Message}", "加载错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (statusText != null)
                    statusText.Text = "剪贴板图片加载失败";
            }
        }

        /// <summary>
        /// 更新剪贴板图片的信息显示
        /// </summary>
        private void UpdateImageInfoForClipboard(BitmapSource image, string sourceInfo)
        {
            if (imageInfoText != null)
            {
                // 由于是剪贴板图片，无法获取文件大小，只显示尺寸和来源
                imageInfoText.Text = $"{image.PixelWidth} × {image.PixelHeight} | {sourceInfo}";
            }
        }

        /// <summary>
        /// 为剪贴板图片加载通道信息
        /// </summary>
        private void LoadClipboardImageChannels(BitmapSource image)
        {
            try
            {
                if (channelStackPanel == null) return;
                channelStackPanel.Children.Clear();

                // 清除之前的缓存，因为这是新的剪贴板图片
                channelCache.Clear();
                currentChannelCachePath = null;

                if (statusText != null)
                    statusText.Text = "正在为剪贴板图片生成通道...";

                var loadedChannels = imageLoader.LoadChannels(image);

                foreach (var (name, channelImage) in loadedChannels)
                {
                    channelCache.Add(Tuple.Create(name, channelImage));
                    CreateChannelControl(name, channelImage);
                }

                if (statusText != null)
                    statusText.Text = $"剪贴板图片通道加载完成 ({channelStackPanel.Children.Count}个)";
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"剪贴板图片通道生成失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 为剪贴板图片创建临时文件（用于打开方式功能）
        /// </summary>
        private string CreateTemporaryImageFile()
        {
            try
            {
                if (mainImage?.Source == null)
                    throw new InvalidOperationException("没有可用的图片");

                var source = mainImage.Source as BitmapSource;
                if (source == null)
                    throw new InvalidOperationException("图片格式不支持");

                // 清理旧的临时文件
                CleanupTemporaryFile();

                // 创建临时文件路径
                string tempDir = Path.GetTempPath();
                string guidPart = Guid.NewGuid().ToString("N").Substring(0, 8);                // 取前8位
                string tempFileName = $"PicViewEx_Temp_{DateTime.Now:yyyyMMdd_HHmmss}_{guidPart}.png";
                temporaryImagePath = Path.Combine(tempDir, tempFileName);

                // 保存图片到临时文件
                SaveBitmapSource(source, temporaryImagePath);

                if (statusText != null)
                    statusText.Text = $"已创建临时文件用于打开方式: {Path.GetFileName(temporaryImagePath)}";

                return temporaryImagePath;
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"创建临时文件失败: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// 清理临时文件
        /// </summary>
        private void CleanupTemporaryFile()
        {
            if (!string.IsNullOrEmpty(temporaryImagePath) && File.Exists(temporaryImagePath))
            {
                try
                {
                    File.Delete(temporaryImagePath);
                    if (statusText != null)
                        statusText.Text = "已清理临时文件";
                }
                catch (Exception ex)
                {
                    // 临时文件清理失败不应该影响主要功能
                    System.Diagnostics.Debug.WriteLine($"清理临时文件失败: {ex.Message}");
                }
            }
            temporaryImagePath = null;
        }

        /// <summary>
        /// 获取当前图片的有效路径（包括临时文件）
        /// </summary>
        private string GetCurrentImagePath()
        {
            // 如果有原始文件路径，直接返回
            if (!string.IsNullOrEmpty(currentImagePath) && File.Exists(currentImagePath))
            {
                return currentImagePath;
            }

            // 如果是剪贴板图片，创建临时文件
            if (mainImage?.Source != null)
            {
                return CreateTemporaryImageFile();
            }

            throw new InvalidOperationException("没有可用的图片文件");
        }

        #endregion

        /// <summary>
        /// 解析相对路径为绝对路径（相对于程序exe所在目录）
        /// </summary>
        /// <param name="path">可能是相对路径或绝对路径的路径</param>
        /// <returns>解析后的绝对路径</returns>
        public static string ResolveExecutablePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // 如果已经是绝对路径，直接返回
            if (Path.IsPathRooted(path))
                return path;

            // 如果是相对路径，基于实际exe所在目录解析
            try
            {
                // 获取当前可执行文件的完整路径
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                // 获取可执行文件所在的目录
                string exeDirectory = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;

                string resolvedPath = Path.Combine(exeDirectory, path);
                return Path.GetFullPath(resolvedPath); // 规范化路径
            }
            catch
            {
                // 如果解析失败，返回原路径
                return path;
            }
        }

        private void MenuShowBgToolbar_Click(object sender, RoutedEventArgs e)
        {
            if (backgroundExpander != null && menuShowBgToolbar != null)
            {
                backgroundExpander.Visibility = menuShowBgToolbar.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void MenuShowSequenceToolbar_Click(object sender, RoutedEventArgs e)
        {
            if (sequenceExpander != null && menuShowSequenceToolbar != null)
            {
                sequenceExpander.Visibility = menuShowSequenceToolbar.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
