using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using Xceed.Wpf.Toolkit;

namespace ImageViewerWPF
{
    public partial class MainWindow : Window
    {
        private string currentFilePath = "";
        private List<string> currentDirectoryImages = new List<string>();
        private int currentImageIndex = -1;
        private double currentZoom = 1.0;
        private bool isChannelViewEnabled = false;
        private BitmapSource? currentImageSource;
        private System.Drawing.Bitmap currentChannelBitmap;
        private ImageBackground currentBackground = ImageBackground.SolidColor;
        private System.Drawing.Color currentBackgroundColor = System.Drawing.Color.Gray;
        private BitmapSource? backgroundImageSource;
        private bool isDragging = false;
        private System.Windows.Point lastMousePosition;

        public enum ImageBackground
        {
            Checkerboard,
            SolidColor,
            ImageFile,
            Transparent
        }

        public MainWindow()
        {
            InitializeComponent();
            SetupInitialBackground();
            UpdateUI();
            
            // 添加键盘事件处理
            this.KeyDown += MainWindow_KeyDown;
            this.Focusable = true;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            bool ctrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            
            switch (e.Key)
            {
                case Key.O when ctrlPressed:
                    OpenFile_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.S when ctrlPressed:
                    SaveAsFile_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Add when ctrlPressed:
                case Key.OemPlus when ctrlPressed:
                    ZoomIn_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Subtract when ctrlPressed:
                case Key.OemMinus when ctrlPressed:
                    ZoomOut_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D0 when ctrlPressed:
                    FitToWindow_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D1 when ctrlPressed:
                    ActualSize_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.R when ctrlPressed:
                    RotateRight_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.L when ctrlPressed:
                    RotateLeft_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.F11:
                    ToggleFullScreen_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Left:
                    PreviousImage_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Right:
                    NextImage_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Escape:
                    if (WindowStyle == WindowStyle.None)
                    {
                        ToggleFullScreen_Click(sender, new RoutedEventArgs());
                    }
                    else
                    {
                        Close();
                    }
                    e.Handled = true;
                    break;
            }
        }

        private void SetupInitialBackground()
        {
            currentBackground = ImageBackground.SolidColor;
            currentBackgroundColor = System.Drawing.Color.Gray;
            UpdateImageBackground();
        }

        #region 文件操作

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = GetFileFilter(),
                Title = "选择图片文件"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadImage(dialog.FileName);
            }
        }

        private void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            if (currentImageSource == null)
            {
                System.Windows.MessageBox.Show("没有图片可以保存！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PNG 文件|*.png|JPEG 文件|*.jpg|BMP 文件|*.bmp|TIFF 文件|*.tiff|所有文件|*.*",
                DefaultExt = ".png",
                Title = "另存为"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    SaveImageToFile(currentImageSource, dialog.FileName);
                    StatusText.Text = $"图片已保存: {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void LoadImage(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    System.Windows.MessageBox.Show("文件不存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                currentImageSource = ImageLoader.LoadImage(filePath);
                if (currentImageSource != null)
                {
                    RefreshChannelBitmap();
                    MainImage.Source = currentImageSource;
                    currentFilePath = filePath;

                    // 更新当前目录的图片列表
                    UpdateCurrentDirectoryImages();

                    // 重置缩放
                    currentZoom = 1.0;
                    ApplyZoom();

                    // 更新通道显示
                    if (isChannelViewEnabled)
                    {
                        UpdateChannelDisplay();
                    }

                    UpdateUI();
                    StatusText.Text = $"已加载: {Path.GetFileName(filePath)}";
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"加载图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshChannelBitmap()
        {
            currentChannelBitmap?.Dispose();
            currentChannelBitmap = null;

            if (currentImageSource == null)
            {
                return;
            }

            try
            {
                currentChannelBitmap = ImageLoader.CreateChannelBitmap(currentImageSource);
            }
            catch (Exception ex)
            {
                currentChannelBitmap = null;
                StatusText.Text = $"通道缓存创建失败: {ex.Message}";
            }
        }

        #region 背景控制

        private void SetCheckerboardBackground_Click(object sender, RoutedEventArgs e)
        {
            currentBackground = ImageBackground.Checkerboard;
            UpdateImageBackground();
        }

        private void SetSolidBackground_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorName)
            {
                currentBackground = ImageBackground.SolidColor;
                currentBackgroundColor = System.Drawing.Color.FromName(colorName);
                UpdateImageBackground();
                UpdateSlidersFromColor(currentBackgroundColor);
            }
        }

        private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LightnessSlider != null)
            {
                UpdateBackgroundFromSliders();
            }
        }

        private void LightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (HueSlider != null)
            {
                UpdateBackgroundFromSliders();
            }
        }

        private void BackgroundColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                var wpfColor = e.NewValue.Value;
                currentBackgroundColor = System.Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
                currentBackground = ImageBackground.SolidColor;
                UpdateImageBackground();
                UpdateSlidersFromColor(currentBackgroundColor);
            }
        }

        private void SelectImageBackground_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff|所有文件|*.*",
                Title = "选择背景图片"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    backgroundImageSource = ImageLoader.LoadBackgroundImage(dialog.FileName);
                    currentBackground = ImageBackground.ImageFile;
                    UpdateImageBackground();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"无法加载背景图片: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UseDefaultImageBackground_Click(object sender, RoutedEventArgs e)
        {
            string bgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bg.png");
            if (File.Exists(bgPath))
            {
                try
                {
                    backgroundImageSource = ImageLoader.LoadBackgroundImage(bgPath);
                    currentBackground = ImageBackground.ImageFile;
                    UpdateImageBackground();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"无法加载bg.png: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("bg.png 文件不存在！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SetTransparentBackground_Click(object sender, RoutedEventArgs e)
        {
            currentBackground = ImageBackground.Transparent;
            UpdateImageBackground();
        }

        private void UpdateImageBackground()
        {
            switch (currentBackground)
            {
                case ImageBackground.Checkerboard:
                    ImageBorder.Background = (System.Windows.Media.Brush)FindResource("CheckerboardBrush");
                    break;
                case ImageBackground.SolidColor:
                    var wpfColor = System.Windows.Media.Color.FromArgb(
                        currentBackgroundColor.A, 
                        currentBackgroundColor.R, 
                        currentBackgroundColor.G, 
                        currentBackgroundColor.B);
                    ImageBorder.Background = new SolidColorBrush(wpfColor);
                    break;
                case ImageBackground.ImageFile:
                    if (backgroundImageSource != null)
                    {
                        ImageBorder.Background = new ImageBrush(backgroundImageSource)
                        {
                            TileMode = TileMode.Tile,
                            ViewportUnits = BrushMappingMode.Absolute,
                            Viewport = new Rect(0, 0, backgroundImageSource.PixelWidth, backgroundImageSource.PixelHeight)
                        };
                    }
                    break;
                case ImageBackground.Transparent:
                    ImageBorder.Background = System.Windows.Media.Brushes.Transparent;
                    this.AllowsTransparency = true;
                    this.WindowStyle = WindowStyle.None;
                    break;
            }
        }

        private void UpdateBackgroundFromSliders()
        {
            double hue = HueSlider.Value;
            double lightness = LightnessSlider.Value / 100.0;
            
            var hslColor = HslToRgb(hue, 1.0, lightness);
            currentBackgroundColor = System.Drawing.Color.FromArgb(255, hslColor.R, hslColor.G, hslColor.B);
            currentBackground = ImageBackground.SolidColor;
            
            UpdateImageBackground();
            
            // 更新颜色选择器（避免循环事件）
            BackgroundColorPicker.SelectedColorChanged -= BackgroundColorPicker_SelectedColorChanged;
            BackgroundColorPicker.SelectedColor = System.Windows.Media.Color.FromRgb(hslColor.R, hslColor.G, hslColor.B);
            BackgroundColorPicker.SelectedColorChanged += BackgroundColorPicker_SelectedColorChanged;
        }

        private void UpdateSlidersFromColor(System.Drawing.Color color)
        {
            var hsl = RgbToHsl(color);
            
            HueSlider.ValueChanged -= HueSlider_ValueChanged;
            LightnessSlider.ValueChanged -= LightnessSlider_ValueChanged;
            
            HueSlider.Value = hsl.H;
            LightnessSlider.Value = hsl.L * 100;
            
            HueSlider.ValueChanged += HueSlider_ValueChanged;
            LightnessSlider.ValueChanged += LightnessSlider_ValueChanged;
        }

        #endregion

        #region 图片浏览

        private void PreviousImage_Click(object sender, RoutedEventArgs e)
        {
            if (currentDirectoryImages.Count > 0 && currentImageIndex > 0)
            {
                currentImageIndex--;
                LoadImage(currentDirectoryImages[currentImageIndex]);
            }
        }

        private void NextImage_Click(object sender, RoutedEventArgs e)
        {
            if (currentDirectoryImages.Count > 0 && currentImageIndex < currentDirectoryImages.Count - 1)
            {
                currentImageIndex++;
                LoadImage(currentDirectoryImages[currentImageIndex]);
            }
        }

        private void UpdateCurrentDirectoryImages()
        {
            if (string.IsNullOrEmpty(currentFilePath))
                return;

            string directory = Path.GetDirectoryName(currentFilePath);
            if (Directory.Exists(directory))
            {
                currentDirectoryImages = Directory.GetFiles(directory)
                    .Where(f => ImageLoader.IsSupportedExtension(Path.GetExtension(f)))
                    .OrderBy(f => f)
                    .ToList();

                currentImageIndex = currentDirectoryImages.IndexOf(currentFilePath);
            }
        }

        #endregion

        #region 缩放和视图控制

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            currentZoom = Math.Min(currentZoom * 1.25, 10.0);
            ApplyZoom();
            UpdateUI();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            currentZoom = Math.Max(currentZoom * 0.8, 0.05);
            ApplyZoom();
            UpdateUI();
        }

        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            if (currentImageSource != null)
            {
                double scaleX = ImageScrollViewer.ActualWidth / currentImageSource.PixelWidth;
                double scaleY = ImageScrollViewer.ActualHeight / currentImageSource.PixelHeight;
                currentZoom = Math.Min(scaleX, scaleY);
                ApplyZoom();
                UpdateUI();
            }
        }

        private void ActualSize_Click(object sender, RoutedEventArgs e)
        {
            currentZoom = 1.0;
            ApplyZoom();
            UpdateUI();
        }

        private void ApplyZoom()
        {
            if (currentImageSource != null)
            {
                // 使用ScaleTransform实现缩放
                var scaleTransform = new ScaleTransform(currentZoom, currentZoom);
                MainImage.RenderTransform = scaleTransform;
                
                // 更新Canvas大小以适应缩放后的图片
                ImageCanvas.Width = currentImageSource.PixelWidth * currentZoom;
                ImageCanvas.Height = currentImageSource.PixelHeight * currentZoom;
            }
        }

        #endregion

        #region 旋转功能

        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            RotateImage(-90);
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            RotateImage(90);
        }

        private void RotateImage(double angle)
        {
            if (currentImageSource == null) return;

            try
            {
                var rotatedBitmap = new TransformedBitmap(currentImageSource, new RotateTransform(angle));
                currentImageSource = rotatedBitmap;
                MainImage.Source = currentImageSource;
                RefreshChannelBitmap();

                // 更新通道显示
                if (isChannelViewEnabled)
                {
                    UpdateChannelDisplay();
                }
                
                UpdateUI();
                StatusText.Text = $"图片已旋转 {angle}°";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"旋转失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 通道显示

        private void ShowChannels_Checked(object sender, RoutedEventArgs e)
        {
            isChannelViewEnabled = true;
            ChannelPanel.Visibility = Visibility.Visible;
            ChannelSplitter.Visibility = Visibility.Visible;
            ChannelColumn.Width = new GridLength(1, GridUnitType.Star);
            
            if (currentImageSource != null)
            {
                UpdateChannelDisplay();
            }
        }

        private void ShowChannels_Unchecked(object sender, RoutedEventArgs e)
        {
            isChannelViewEnabled = false;
            ChannelPanel.Visibility = Visibility.Collapsed;
            ChannelSplitter.Visibility = Visibility.Collapsed;
            ChannelColumn.Width = new GridLength(0);
        }

        private void ChannelSelection_Changed(object sender, RoutedEventArgs e)
        {
            if (isChannelViewEnabled && currentImageSource != null)
            {
                UpdateChannelDisplay();
            }
        }

        private void UpdateChannelDisplay()
        {
            ChannelGrid.Children.Clear();
            
            if (currentImageSource == null) return;

            try
            {
                // 转换为Bitmap以便处理通道
                if (currentChannelBitmap == null)
                {
                    RefreshChannelBitmap();
                }

                var bitmap = currentChannelBitmap;
                if (bitmap == null)
                {
                    return;
                }

                // 添加主图
                var mainImageControl = new System.Windows.Controls.Image
                {
                    Source = currentImageSource,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(2)
                };
                mainImageControl.MouseLeftButtonDown += (s, e) => 
                {
                    if (e.ClickCount == 2)
                    {
                        OpenImageInNewWindow(currentImageSource, "主图");
                    }
                };
                ChannelGrid.Children.Add(mainImageControl);

                // 添加通道
                if (ShowRedChannel.IsChecked == true)
                {
                    var redChannel = ExtractChannel(bitmap, ColorChannel.Red);
                    var redImageControl = CreateChannelImageControl(redChannel, "红色通道");
                    ChannelGrid.Children.Add(redImageControl);
                }

                if (ShowGreenChannel.IsChecked == true)
                {
                    var greenChannel = ExtractChannel(bitmap, ColorChannel.Green);
                    var greenImageControl = CreateChannelImageControl(greenChannel, "绿色通道");
                    ChannelGrid.Children.Add(greenImageControl);
                }

                if (ShowBlueChannel.IsChecked == true)
                {
                    var blueChannel = ExtractChannel(bitmap, ColorChannel.Blue);
                    var blueImageControl = CreateChannelImageControl(blueChannel, "蓝色通道");
                    ChannelGrid.Children.Add(blueImageControl);
                }

                if (ShowAlphaChannel.IsChecked == true && HasAlphaChannel(bitmap))
                {
                    var alphaChannel = ExtractChannel(bitmap, ColorChannel.Alpha);
                    var alphaImageControl = CreateChannelImageControl(alphaChannel, "透明通道");
                    ChannelGrid.Children.Add(alphaImageControl);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"通道显示错误: {ex.Message}";
            }
        }

        private System.Windows.Controls.Image CreateChannelImageControl(BitmapSource channelImage, string channelName)
        {
            var imageControl = new System.Windows.Controls.Image
            {
                Source = channelImage,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(2),
                ToolTip = channelName
            };
            
            imageControl.MouseLeftButtonDown += (s, e) => 
            {
                if (e.ClickCount == 2)
                {
                    OpenImageInNewWindow(channelImage, channelName);
                }
            };
            
            return imageControl;
        }

        private void OpenImageInNewWindow(BitmapSource imageSource, string title)
        {
            var newWindow = new MainWindow();
            newWindow.Title = $"高级图片预览器 - {title}";
            newWindow.MainImage.Source = imageSource;
            newWindow.currentImageSource = imageSource;
            newWindow.RefreshChannelBitmap();
            newWindow.UpdateUI();
            newWindow.Show();
        }

        #endregion

        #region 拖拽和鼠标操作

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    LoadImage(files[0]);
                }
            }
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                isDragging = true;
                lastMousePosition = e.GetPosition(ImageScrollViewer);
                ((System.Windows.Controls.Image)sender).CaptureMouse();
            }
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPosition = e.GetPosition(ImageScrollViewer);
                var delta = currentPosition - lastMousePosition;
                
                ImageScrollViewer.ScrollToHorizontalOffset(ImageScrollViewer.HorizontalOffset - delta.X);
                ImageScrollViewer.ScrollToVerticalOffset(ImageScrollViewer.VerticalOffset - delta.Y);
                
                lastMousePosition = currentPosition;
            }
        }

        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            ((System.Windows.Controls.Image)sender).ReleaseMouseCapture();
        }

        private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            currentZoom = Math.Max(0.05, Math.Min(10.0, currentZoom * zoomFactor));
            
            ApplyZoom();
            UpdateUI();
        }

        #endregion

        #region 其他功能

        private void ToggleFullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Normal)
            {
                this.WindowStyle = System.Windows.WindowStyle.None;
                this.WindowState = System.Windows.WindowState.Maximized;
            }
            else
            {
                this.WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
                this.WindowState = System.Windows.WindowState.Normal;
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "高级图片预览器 v2.0\n\n" +
                "特色功能：\n" +
                "• 多种背景模式（透明方格、纯色、图片、透明）\n" +
                "• 通道显示和分析\n" +
                "• 图片旋转和格式转换\n" +
                "• 支持多种图片格式\n" +
                "• 高级颜色控制\n\n" +
                "快捷键：\n" +
                "• Ctrl+O: 打开文件\n" +
                "• Ctrl+S: 另存为\n" +
                "• ←/→: 上一张/下一张\n" +
                "• Ctrl+R/L: 旋转\n" +
                "• F11: 全屏\n" +
                "• 鼠标滚轮: 缩放",
                "关于",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region 辅助方法

        private void UpdateUI()
        {
            if (currentImageSource != null)
            {
                ZoomTextBox.Text = $"{currentZoom * 100:F0}%";
                ZoomInfoText.Text = $"{currentZoom * 100:F0}%";
                ImageInfoText.Text = $"{currentImageSource.PixelWidth} × {currentImageSource.PixelHeight}";
                FileInfoText.Text = Path.GetFileName(currentFilePath);
                Title = $"高级图片预览器 - {Path.GetFileName(currentFilePath)}";
            }
            else
            {
                ZoomTextBox.Text = "100%";
                ZoomInfoText.Text = "100%";
                ImageInfoText.Text = "";
                FileInfoText.Text = "";
                Title = "高级图片预览器";
            }
        }

        private string GetFileFilter()
        {
            return "所有支持的图片|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.tiff;*.tif;*.ico;*.tga;*.dds;*.psd;*.webp;*.a|" +
                   "JPEG|*.jpg;*.jpeg|PNG|*.png|GIF|*.gif|BMP|*.bmp|TIFF|*.tiff;*.tif|ICO|*.ico|" +
                   "TGA|*.tga|DDS|*.dds|PSD|*.psd|WebP|*.webp|A文件|*.a|所有文件|*.*";
        }

        private void SaveImageToFile(BitmapSource source, string filePath)
        {
            BitmapEncoder encoder;
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            switch (extension)
            {
                case ".png":
                    encoder = new PngBitmapEncoder();
                    break;
                case ".jpg":
                case ".jpeg":
                    encoder = new JpegBitmapEncoder();
                    break;
                case ".bmp":
                    encoder = new BmpBitmapEncoder();
                    break;
                case ".tiff":
                case ".tif":
                    encoder = new TiffBitmapEncoder();
                    break;
                default:
                    encoder = new PngBitmapEncoder();
                    break;
            }

            encoder.Frames.Add(BitmapFrame.Create(source));
            
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }

        // 颜色转换辅助方法
        private (double H, double S, double L) RgbToHsl(System.Drawing.Color rgb)
        {
            double r = rgb.R / 255.0;
            double g = rgb.G / 255.0;
            double b = rgb.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double diff = max - min;

            double h = 0, s = 0, l = (max + min) / 2;

            if (diff != 0)
            {
                s = l > 0.5 ? diff / (2 - max - min) : diff / (max + min);

                if (max == r)
                    h = (g - b) / diff + (g < b ? 6 : 0);
                else if (max == g)
                    h = (b - r) / diff + 2;
                else
                    h = (r - g) / diff + 4;

                h /= 6;
            }

            return (h * 360, s, l);
        }

        private (byte R, byte G, byte B) HslToRgb(double h, double s, double l)
        {
            h /= 360;
            
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                double hue2rgb(double p, double q, double t)
                {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1.0/6) return p + (q - p) * 6 * t;
                    if (t < 1.0/2) return q;
                    if (t < 2.0/3) return p + (q - p) * (2.0/3 - t) * 6;
                    return p;
                }

                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = hue2rgb(p, q, h + 1.0/3);
                g = hue2rgb(p, q, h);
                b = hue2rgb(p, q, h - 1.0/3);
            }

            return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private enum ColorChannel { Red, Green, Blue, Alpha }

        private BitmapSource ExtractChannel(System.Drawing.Bitmap bitmap, ColorChannel channel)
        {
            var result = new System.Drawing.Bitmap(bitmap.Width, bitmap.Height);
            
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    byte value = channel switch
                    {
                        ColorChannel.Red => pixel.R,
                        ColorChannel.Green => pixel.G,
                        ColorChannel.Blue => pixel.B,
                        ColorChannel.Alpha => pixel.A,
                        _ => 0
                    };
                    result.SetPixel(x, y, System.Drawing.Color.FromArgb(255, value, value, value));
                }
            }

            return BitmapToImageSource(result);
        }

        private bool HasAlphaChannel(System.Drawing.Bitmap bitmap)
        {
            return System.Drawing.Image.IsAlphaPixelFormat(bitmap.PixelFormat);
        }

        private BitmapSource BitmapToImageSource(System.Drawing.Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                var bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                return bitmapimage;
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            currentChannelBitmap?.Dispose();
            currentChannelBitmap = null;
            base.OnClosed(e);
        }
    }
}