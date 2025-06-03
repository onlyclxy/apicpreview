using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ImageMagick;
using System.Threading.Tasks;
using System.Diagnostics;
using WpfAnimatedGif;

namespace PicView
{
    public class OpenWithApp
    {
        public string Name { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public string Arguments { get; set; } = "\"{0}\""; // {0} 将被替换为文件路径
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
        private ImageBrush? backgroundImageBrush;
        private EverythingSearch? everythingSearch;

        // 拖拽相关
        private bool isDragging = false;
        private Point lastMousePosition;
        private Point imagePosition = new Point(0, 0);

        // 打开方式配置
        private List<OpenWithApp> openWithApps = new List<OpenWithApp>();

        // 窗口大小变化时的智能缩放
        private bool isWindowInitialized = false;
        private Size lastWindowSize;

        // 设置管理
        private AppSettings appSettings;
        private bool isLoadingSettings = false;

        public MainWindow()
        {
            InitializeComponent();
            
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
            dialog.FileName = Path.GetFileNameWithoutExtension(currentImagePath);
            
            if (dialog.ShowDialog() == true)
            {
                SaveRotatedImage(dialog.FileName);
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
                // 获取exe所在目录
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string defaultImagePath = Path.Combine(exeDirectory, "res", "01.jpg");
                
                if (File.Exists(defaultImagePath))
                {
                    BitmapImage bgImage = new BitmapImage();
                    bgImage.BeginInit();
                    bgImage.UriSource = new Uri(defaultImagePath);
                    bgImage.CacheOption = BitmapCacheOption.OnLoad;
                    bgImage.EndInit();
                    bgImage.Freeze();
                    
                    backgroundImageBrush = new ImageBrush(bgImage)
                    {
                        Stretch = Stretch.UniformToFill,
                        TileMode = TileMode.Tile,
                        Opacity = 0.3
                    };
                    
                    if (statusText != null)
                        statusText.Text = "已加载默认背景图片: 01.jpg";
                }
                else
                {
                    // 如果默认图片不存在，创建一个简单的渐变背景
                    var gradientBrush = new LinearGradientBrush();
                    gradientBrush.StartPoint = new Point(0, 0);
                    gradientBrush.EndPoint = new Point(1, 1);
                    gradientBrush.GradientStops.Add(new GradientStop(Colors.LightBlue, 0.0));
                    gradientBrush.GradientStops.Add(new GradientStop(Colors.LightGray, 1.0));
                    
                    backgroundImageBrush = new ImageBrush
                    {
                        ImageSource = CreateGradientImage(),
                        Stretch = Stretch.UniformToFill,
                        TileMode = TileMode.Tile,
                        Opacity = 0.3
                    };
                    
                    if (statusText != null)
                        statusText.Text = "默认图片不存在，使用渐变背景";
                }
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"加载默认背景图片失败: {ex.Message}";
            }
        }

        private BitmapSource CreateGradientImage()
        {
            // 创建一个简单的渐变图像作为后备
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
            return renderBitmap;
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
                    BitmapImage bgImage = new BitmapImage();
                    bgImage.BeginInit();
                    bgImage.UriSource = new Uri(dialog.FileName);
                    bgImage.CacheOption = BitmapCacheOption.OnLoad;
                    bgImage.EndInit();
                    bgImage.Freeze();
                    
                    backgroundImageBrush = new ImageBrush(bgImage)
                    {
                        Stretch = Stretch.UniformToFill,
                        TileMode = TileMode.Tile,
                        Opacity = 0.3
                    };
                    
                    if (rbImageBackground != null)
                        rbImageBackground.IsChecked = true;
                        
                    UpdateBackground();
                    
                    if (statusText != null)
                        statusText.Text = $"背景图片已设置: {Path.GetFileName(dialog.FileName)}";
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

        #endregion

        #region 核心功能实现

        private void LoadImage(string imagePath)
        {
            try
            {
                currentImagePath = imagePath;
                if (statusText != null)
                    statusText.Text = $"加载中: {Path.GetFileName(imagePath)}";
                
                BitmapImage? bitmap = LoadImageWithMagick(imagePath);
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
                    
                    // 立即居中显示图片
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CenterImage();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                    
                    UpdateImageInfo(bitmap);
                    
                    if (showChannels)
                    {
                        LoadImageChannels(imagePath);
                    }
                    
                    if (statusText != null)
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
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(gifPath);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                
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

        private BitmapImage? LoadImageWithMagick(string imagePath)
        {
            try
            {
                using (var magickImage = new MagickImage(imagePath))
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
            }
            catch
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
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
                currentImageIndex--;
                LoadImage(currentImageList[currentImageIndex]);
            }
        }

        private void NavigateNext()
        {
            if (currentImageList.Count > 0 && currentImageIndex < currentImageList.Count - 1)
            {
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
                
                using (var magickImage = new MagickImage(imagePath))
                {
                    // 简单的RGB通道分离
                    CreateSimpleRGBChannels(magickImage);
                    
                    // 检查Alpha通道（支持所有格式）
                    if (magickImage.HasAlpha)
                    {
                        CreateAlphaChannel(magickImage);
                    }
                    
                    if (statusText != null)
                        statusText.Text = $"通道加载完成 - {Path.GetFileName(imagePath)}";
                }
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"通道加载失败: {ex.Message}";
            }
        }

        private void CreateSimpleRGBChannels(MagickImage originalImage)
        {
            try
            {
                // R通道 - 保留红色，其他为0
                var redImage = new MagickImage(originalImage);
                redImage.Evaluate(Channels.Green, EvaluateOperator.Set, 0);
                redImage.Evaluate(Channels.Blue, EvaluateOperator.Set, 0);
                var redBitmap = CreateBitmapFromMagickImage(redImage);
                if (redBitmap != null)
                    CreateChannelControl("红色 (R)", redBitmap);
                redImage.Dispose();

                // G通道 - 保留绿色，其他为0
                var greenImage = new MagickImage(originalImage);
                greenImage.Evaluate(Channels.Red, EvaluateOperator.Set, 0);
                greenImage.Evaluate(Channels.Blue, EvaluateOperator.Set, 0);
                var greenBitmap = CreateBitmapFromMagickImage(greenImage);
                if (greenBitmap != null)
                    CreateChannelControl("绿色 (G)", greenBitmap);
                greenImage.Dispose();

                // B通道 - 保留蓝色，其他为0
                var blueImage = new MagickImage(originalImage);
                blueImage.Evaluate(Channels.Red, EvaluateOperator.Set, 0);
                blueImage.Evaluate(Channels.Green, EvaluateOperator.Set, 0);
                var blueBitmap = CreateBitmapFromMagickImage(blueImage);
                if (blueBitmap != null)
                    CreateChannelControl("蓝色 (B)", blueBitmap);
                blueImage.Dispose();
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"RGB通道分离失败: {ex.Message}";
            }
        }

        private void CreateAlphaChannel(MagickImage originalImage)
        {
            try
            {
                // 提取Alpha通道
                var alphaImage = new MagickImage(originalImage);
                alphaImage.Alpha(AlphaOption.Extract);
                alphaImage.Format = MagickFormat.Png;
                
                var alphaBitmap = CreateBitmapFromMagickImage(alphaImage);
                if (alphaBitmap != null)
                    CreateChannelControl("透明 (Alpha)", alphaBitmap);
                    
                alphaImage.Dispose();
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"Alpha通道提取失败: {ex.Message}";
            }
        }

        private BitmapImage? CreateBitmapFromMagickImage(MagickImage magickImage)
        {
            try
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
            catch
            {
                return null;
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
                Margin = new Thickness(5)
            };
            
            image.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
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
                }
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

            // 计算缩放后的图片尺寸
            var scaledWidth = source.PixelWidth * currentZoom;
            var scaledHeight = source.PixelHeight * currentZoom;

            // 定义最小可见区域（图片必须至少有这么多像素在屏幕内）
            var minVisibleWidth = Math.Min(scaledWidth * 0.3, 200); // 至少30%或200像素可见
            var minVisibleHeight = Math.Min(scaledHeight * 0.3, 200);

            // 计算位置约束
            var maxX = containerWidth - minVisibleWidth;
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

            // 重置图片尺寸为原始尺寸
            mainImage.Width = source.PixelWidth;
            mainImage.Height = source.PixelHeight;

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

            // 计算适应窗口的缩放比例
            double scaleX = containerWidth / source.PixelWidth;
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

            // 等待布局更新完成
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                var containerWidth = imageContainer.ActualWidth;
                var containerHeight = imageContainer.ActualHeight;
                
                if (containerWidth <= 0 || containerHeight <= 0) return;
                
                // 计算缩放后的图片尺寸
                var imageWidth = source.PixelWidth * currentZoom;
                var imageHeight = source.PixelHeight * currentZoom;
                
                imagePosition.X = (containerWidth - imageWidth) / 2;
                imagePosition.Y = (containerHeight - imageHeight) / 2;
                
                UpdateImageTransform();
                UpdateZoomText();
                
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        #endregion

        #region 文件操作方法

        private void OpenWithApp(int index)
        {
            if (string.IsNullOrEmpty(currentImagePath) || index >= openWithApps.Count)
                return;

            try
            {
                var app = openWithApps[index];
                var arguments = string.Format(app.Arguments, currentImagePath);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = app.ExecutablePath,
                    Arguments = arguments,
                    UseShellExecute = true
                };
                
                Process.Start(startInfo);
                
                if (statusText != null)
                    statusText.Text = $"已用 {app.Name} 打开图片";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFileLocation()
        {
            if (string.IsNullOrEmpty(currentImagePath) || !File.Exists(currentImagePath))
            {
                MessageBox.Show("当前没有打开的图片文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 在资源管理器中选中文件
                var argument = $"/select, \"{currentImagePath}\"";
                Process.Start("explorer.exe", argument);
                
                if (statusText != null)
                    statusText.Text = "已在资源管理器中显示文件";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件位置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddOpenWithApp_Click(object sender, RoutedEventArgs e)
        {
            AddOpenWithApplication();
        }

        private void AddOpenWithApplication()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择应用程序",
                Filter = "可执行文件|*.exe|所有文件|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                string appName = Path.GetFileNameWithoutExtension(dialog.FileName);
                string displayName = Microsoft.VisualBasic.Interaction.InputBox(
                    "请输入显示名称:", "添加打开方式", appName);
                
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    var newApp = new OpenWithApp
                    {
                        Name = displayName,
                        ExecutablePath = dialog.FileName,
                        Arguments = "\"{0}\""
                    };
                    
                    openWithApps.Add(newApp);
                    UpdateOpenWithButtons();
                    UpdateOpenWithMenu();
                    
                    if (statusText != null)
                        statusText.Text = $"已添加打开方式: {displayName}";
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

            // 清除除了"添加打开方式..."之外的所有菜单项
            var itemsToRemove = openWithContextMenu.Items.Cast<object>()
                .Where(item => item is MenuItem menuItem && 
                       menuItem.Header.ToString() != "添加打开方式...")
                .ToList();
            
            foreach (var item in itemsToRemove)
            {
                openWithContextMenu.Items.Remove(item);
            }

            // 如果有自定义打开方式，显示分隔符
            if (openWithApps.Count > 0)
            {
                if (openWithSeparator != null)
                    openWithSeparator.Visibility = Visibility.Visible;
                
                // 添加所有打开方式到菜单
                for (int i = 0; i < openWithApps.Count; i++)
                {
                    var app = openWithApps[i];
                    var menuItem = new MenuItem
                    {
                        Header = app.Name,
                        Tag = i
                    };
                    menuItem.Click += (s, e) =>
                    {
                        if (s is MenuItem item && item.Tag is int index)
                            OpenWithApp(index);
                    };
                    
                    // 插入到分隔符之前
                    var separatorIndex = openWithContextMenu.Items.IndexOf(openWithSeparator);
                    openWithContextMenu.Items.Insert(separatorIndex, menuItem);
                }
            }
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
                            Arguments = appData.Arguments
                        });
                    }
                    UpdateOpenWithButtons();
                    UpdateOpenWithMenu();
                    
                    isLoadingSettings = false;
                    
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
                bgExpander.IsExpanded = appSettings.BackgroundPanelExpanded;
            if (menuExpandBgPanel != null)
                menuExpandBgPanel.IsChecked = appSettings.BackgroundPanelExpanded;

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
                    Arguments = appData.Arguments
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
                if (File.Exists(imagePath))
                {
                    BitmapImage bgImage = new BitmapImage();
                    bgImage.BeginInit();
                    bgImage.UriSource = new Uri(imagePath);
                    bgImage.CacheOption = BitmapCacheOption.OnLoad;
                    bgImage.EndInit();
                    bgImage.Freeze();
                    
                    backgroundImageBrush = new ImageBrush(bgImage)
                    {
                        Stretch = Stretch.UniformToFill,
                        TileMode = TileMode.Tile,
                        Opacity = 0.3
                    };
                }
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
                        Arguments = app.Arguments
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
            var result = MessageBox.Show("确定要还原到默认设置吗？这将清除所有自定义设置。", "还原设置", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.ResetToDefault();
                appSettings = new AppSettings();
                
                // 重新应用默认设置
                ApplyBackgroundSettings();
                ApplyUISettings();
                
                if (statusText != null)
                    statusText.Text = "设置已还原到默认状态";
            }
        }

        private void MenuExpandBgPanel_Click(object sender, RoutedEventArgs e)
        {
            var bgExpander = this.FindName("backgroundExpander") as Expander;
            if (bgExpander != null)
            {
                bgExpander.IsExpanded = menuExpandBgPanel?.IsChecked ?? true;
                SaveAppSettings();
            }
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            string controlTest = SettingsManager.TestControlSaving(this);
            
            MessageBox.Show("PicView - Advanced Image Viewer\n\n" +
                "版本: 1.0.0\n" +
                "一个功能强大的图片查看器\n\n" +
                "功能特色:\n" +
                "• 支持多种图片格式\n" +
                "• GIF动画播放\n" +
                "• RGB/Alpha通道显示\n" +
                "• 自定义背景设置\n" +
                "• 命令行参数支持\n" +
                "• 设置自动保存\n\n" +
                "控件状态测试:\n" + controlTest, 
                "关于PicView", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuKeyboardHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("快捷键帮助:\n\n" +
                "文件操作:\n" +
                "Ctrl+O - 打开文件\n" +
                "Ctrl+S - 另存为\n" +
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
    }
} 