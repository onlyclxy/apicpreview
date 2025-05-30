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

namespace PicView
{
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
        private SolidColorBrush currentBackgroundBrush = new SolidColorBrush(Colors.White);
        private ImageBrush? backgroundImageBrush;
        private EverythingSearch? everythingSearch;

        public MainWindow()
        {
            InitializeComponent();
            InitializeBackgroundSettings();
            UpdateZoomText();
            
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
            
            if (statusText != null)
                statusText.Text = "就绪 - 请打开图片文件或拖拽图片到窗口";
        }

        private void InitializeBackgroundSettings()
        {
            if (rbTransparent != null)
                rbTransparent.IsChecked = true;
            UpdateBackground();
        }

        private void UpdateZoomText()
        {
            if (zoomText != null)
            {
                zoomText.Text = $"{(currentZoom * 100):F0}%";
            }
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
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    break;
                case Key.Escape:
                    if (WindowState == WindowState.Maximized)
                        WindowState = WindowState.Normal;
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
            NavigatePrevious();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            NavigateNext();
        }

        private void BtnRotateLeft_Click(object sender, RoutedEventArgs e)
        {
            RotateImage(-90);
        }

        private void BtnRotateRight_Click(object sender, RoutedEventArgs e)
        {
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
        }

        private void ChkShowChannels_Unchecked(object sender, RoutedEventArgs e)
        {
            showChannels = false;
            if (channelPanel != null && channelSplitter != null && channelColumn != null && channelStackPanel != null)
            {
                channelPanel.Visibility = Visibility.Collapsed;
                channelSplitter.Visibility = Visibility.Collapsed;
                channelColumn.Width = new GridLength(0);
                channelStackPanel.Children.Clear();
            }
        }

        private void BackgroundType_Changed(object sender, RoutedEventArgs e)
        {
            UpdateBackground();
        }

        private void PresetColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorString)
            {
                if (rbSolidColor != null)
                    rbSolidColor.IsChecked = true;
                
                switch (colorString)
                {
                    case "White":
                        currentBackgroundBrush = new SolidColorBrush(Colors.White);
                        break;
                    case "Black":
                        currentBackgroundBrush = new SolidColorBrush(Colors.Black);
                        break;
                    default:
                        var converter = new BrushConverter();
                        if (converter.ConvertFromString(colorString) is SolidColorBrush brush)
                            currentBackgroundBrush = brush;
                        break;
                }
                
                UpdateBackground();
            }
        }

        private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (rbSolidColor?.IsChecked == true && sliderHue != null && sliderBrightness != null)
            {
                double hue = sliderHue.Value;
                double brightness = sliderBrightness.Value / 100.0;
                
                Color color = HslToRgb(hue, 1.0, brightness);
                currentBackgroundBrush = new SolidColorBrush(color);
                
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
                    MessageBox.Show($"加载背景图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MainImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && mainImage != null)
            {
                double scaleFactor = e.Delta > 0 ? 1.1 : 0.9;
                currentZoom *= scaleFactor;
                
                ScaleTransform scale = new ScaleTransform(currentZoom, currentZoom);
                mainImage.RenderTransform = new TransformGroup
                {
                    Children = { currentTransform, scale }
                };
                
                UpdateZoomText();
                e.Handled = true;
            }
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
                    mainImage.Source = bitmap;
                    mainImage.RenderTransform = currentTransform;
                    
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
            }
            else if (rbSolidColor?.IsChecked == true)
            {
                imageContainer.Background = currentBackgroundBrush;
            }
            else if (rbImageBackground?.IsChecked == true && backgroundImageBrush != null)
            {
                imageContainer.Background = backgroundImageBrush;
            }
            else if (rbWindowTransparent?.IsChecked == true)
            {
                imageContainer.Background = System.Windows.Media.Brushes.Transparent;
            }
        }

        private Color HslToRgb(double h, double s, double l)
        {
            h /= 360.0;
            
            double r, g, b;
            
            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                var hue2rgb = new Func<double, double, double, double>((p, q, t) =>
                {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1.0 / 6) return p + (q - p) * 6 * t;
                    if (t < 1.0 / 2) return q;
                    if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
                    return p;
                });

                var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                var p = 2 * l - q;
                r = hue2rgb(p, q, h + 1.0 / 3);
                g = hue2rgb(p, q, h);
                b = hue2rgb(p, q, h - 1.0 / 3);
            }

            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private void LoadImageChannels(string imagePath)
        {
            try
            {
                if (channelStackPanel == null) return;
                channelStackPanel.Children.Clear();
                
                using (var magickImage = new MagickImage(imagePath))
                {
                    var rgbImage = magickImage.Clone();
                    rgbImage.Format = MagickFormat.Png;
                    byte[] rgbBytes = rgbImage.ToByteArray();
                    
                    BitmapImage rgbBitmap = new BitmapImage();
                    rgbBitmap.BeginInit();
                    rgbBitmap.StreamSource = new MemoryStream(rgbBytes);
                    rgbBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    rgbBitmap.EndInit();
                    rgbBitmap.Freeze();
                    
                    CreateChannelControl("RGB 综合", rgbBitmap);
                    
                    if (Path.GetExtension(imagePath).ToLower() == ".tga" && magickImage.HasAlpha)
                    {
                        var alphaImage = magickImage.Clone();
                        alphaImage.Alpha(AlphaOption.Extract);
                        alphaImage.Format = MagickFormat.Png;
                        byte[] alphaBytes = alphaImage.ToByteArray();
                        
                        BitmapImage alphaBitmap = new BitmapImage();
                        alphaBitmap.BeginInit();
                        alphaBitmap.StreamSource = new MemoryStream(alphaBytes);
                        alphaBitmap.CacheOption = BitmapCacheOption.OnLoad;
                        alphaBitmap.EndInit();
                        alphaBitmap.Freeze();
                        
                        CreateChannelControl("Alpha 透明", alphaBitmap);
                    }
                }
            }
            catch (Exception ex)
            {
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
    }
} 