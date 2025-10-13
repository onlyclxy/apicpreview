using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Microsoft.Win32;

namespace PicViewEx.ImageSave
{
    public partial class TestWindow : Window
    {
        private BitmapSource _currentImage;
        private BitmapSource _originalImage; // 保存原始图像
        private string _currentFilePath;
        private readonly IImageSaver _imageSaver;
        private double _rotationAngle = 0;

        public TestWindow()
        {
            InitializeComponent();
            _imageSaver = new ImageSaver();

            Log("图片保存库测试工具已启动");
            Log("支持的格式: PNG, JPG, BMP, TGA, DDS");
            Log("================================");
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "选择图片文件",
                Filter = "所有支持的图片|*.png;*.jpg;*.jpeg;*.bmp;*.tga;*.dds;*.gif;*.tif;*.tiff;*.webp|" +
                         "PNG图像|*.png|" +
                         "JPEG图像|*.jpg;*.jpeg|" +
                         "BMP图像|*.bmp|" +
                         "TGA图像|*.tga|" +
                         "DDS图像|*.dds|" +
                         "所有文件|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadImage(dialog.FileName);
            }
        }

        private void LoadImage(string filePath)
        {
            try
            {
                Log($"正在加载图片: {Path.GetFileName(filePath)}");

                // 使用ImageMagick加载图片
                using (MagickImage magickImage = new MagickImage(filePath))
                {
                    // 转换为BitmapSource
                    using (var stream = new MemoryStream())
                    {
                        magickImage.Format = MagickFormat.Png;
                        magickImage.Write(stream);
                        stream.Position = 0;

                        PngBitmapDecoder decoder = new PngBitmapDecoder(stream,
                            BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.OnLoad);

                        _currentImage = decoder.Frames[0];
                        _currentImage.Freeze();
                    }

                    // 显示图片
                    DisplayImage.Source = _currentImage;
                    _originalImage = _currentImage; // 保存原始图像
                    ImageScrollViewer.Visibility = Visibility.Visible;
                    TxtPlaceholder.Visibility = Visibility.Collapsed;
                    RotationPanel.Visibility = Visibility.Visible; // 显示旋转控制面板

                    // 重置旋转
                    _rotationAngle = 0;
                    ImageRotateTransform.Angle = 0;
                    RotationAngleText.Text = "当前角度: 0°";

                    // 保存文件路径
                    _currentFilePath = filePath;

                    // 更新UI
                    BtnSave.IsEnabled = true;
                    BtnSaveAs.IsEnabled = true;

                    // 显示图片信息
                    string fileSize = FormatFileSize(new FileInfo(filePath).Length);
                    TxtImageInfo.Text = $"{Path.GetFileName(filePath)} | " +
                                       $"{magickImage.Width}x{magickImage.Height} | " +
                                       $"{magickImage.Format} | " +
                                       $"{fileSize}";

                    Log($"图片加载成功: {magickImage.Width}x{magickImage.Height} {magickImage.Format}");

                    // 如果是JPEG，显示估算的质量
                    if (Path.GetExtension(filePath).ToLower() == ".jpg" ||
                        Path.GetExtension(filePath).ToLower() == ".jpeg")
                    {
                        int quality = JpegQualityAnalyzer.EstimateQuality(filePath);
                        Log($"JPEG质量估算: {quality}");
                    }

                    // 如果是DDS，显示DDS信息
                    if (Path.GetExtension(filePath).ToLower() == ".dds")
                    {
                        var nvidiaTools = new NvidiaTextureTools();
                        if (nvidiaTools.IsAvailable)
                        {
                            var ddsInfo = nvidiaTools.GetDdsInfo(filePath);
                            if (ddsInfo != null)
                            {
                                Log($"DDS信息: 格式={ddsInfo.Format}, BC={ddsInfo.CompressionFormat}, " +
                                    $"Mipmap={ddsInfo.HasMipmaps} (级别:{ddsInfo.MipLevels})");
                            }
                        }
                        else
                        {
                            Log("警告: NVIDIA Texture Tools 不可用，无法获取DDS详细信息");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"加载图片失败: {ex.Message}");
                MessageBox.Show($"加载图片失败！\n\n{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null || string.IsNullOrEmpty(_currentFilePath))
            {
                MessageBox.Show("请先打开一张图片", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Log("执行保存操作...");
                Log($"目标文件: {_currentFilePath}");

                var result = _imageSaver.Save(_currentImage, _currentFilePath);

                if (result.Success)
                {
                    Log($"✓ 保存成功: {result.Message}");
                    MessageBox.Show("保存成功！", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Log($"✗ 保存失败: {result.Message}");
                    if (!string.IsNullOrEmpty(result.ErrorDetails))
                    {
                        Log($"  错误详情: {result.ErrorDetails}");
                    }

                    MessageBox.Show($"保存失败！\n\n{result.Message}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"✗ 保存异常: {ex.Message}");
                MessageBox.Show($"保存过程中发生异常！\n\n{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null || string.IsNullOrEmpty(_currentFilePath))
            {
                MessageBox.Show("请先打开一张图片", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Log("执行另存为操作...");

                var result = _imageSaver.SaveAs(_currentImage, _currentFilePath);

                if (result.Success)
                {
                    if (!string.IsNullOrEmpty(result.SavedPath))
                    {
                        Log($"✓ 另存为成功: {result.SavedPath}");
                    }
                    else
                    {
                        Log($"✓ {result.Message}");
                    }
                }
                else
                {
                    if (result.Message != "用户取消保存")
                    {
                        Log($"✗ 另存为失败: {result.Message}");
                        if (!string.IsNullOrEmpty(result.ErrorDetails))
                        {
                            Log($"  错误详情: {result.ErrorDetails}");
                        }
                    }
                    else
                    {
                        Log("用户取消了另存为操作");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"✗ 另存为异常: {ex.Message}");
                MessageBox.Show($"另存为过程中发生异常！\n\n{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            Log("日志已清空");
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.AppendText($"[{timestamp}] {message}\n");
            LogTextBox.ScrollToEnd();
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int order = 0;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private void BtnRotateLeft_Click(object sender, RoutedEventArgs e)
        {
            _rotationAngle -= 90;
            if (_rotationAngle <= -360)
                _rotationAngle += 360;

            ApplyRotation();
            Log($"左旋90°，当前角度: {_rotationAngle}°");
        }

        private void BtnRotateRight_Click(object sender, RoutedEventArgs e)
        {
            _rotationAngle += 90;
            if (_rotationAngle >= 360)
                _rotationAngle -= 360;

            ApplyRotation();
            Log($"右旋90°，当前角度: {_rotationAngle}°");
        }

        private void BtnResetRotation_Click(object sender, RoutedEventArgs e)
        {
            _rotationAngle = 0;
            ApplyRotation();
            Log("旋转已重置");
        }

        private void ApplyRotation()
        {
            // 更新旋转变换（仅用于显示）
            ImageRotateTransform.Angle = _rotationAngle;

            // 更新角度显示
            RotationAngleText.Text = $"当前角度: {_rotationAngle}°";

            // 应用旋转到实际图像数据
            _currentImage = RotateBitmap(_originalImage, _rotationAngle);
        }

        private BitmapSource RotateBitmap(BitmapSource source, double angle)
        {
            if (angle == 0)
                return source;

            // 只支持90度的倍数旋转
            int normalizedAngle = ((int)angle % 360 + 360) % 360;

            if (normalizedAngle == 0)
                return source;

            // 创建旋转变换
            TransformedBitmap transformedBitmap = new TransformedBitmap();
            transformedBitmap.BeginInit();
            transformedBitmap.Source = source;

            // 根据角度选择旋转
            switch (normalizedAngle)
            {
                case 90:
                    transformedBitmap.Transform = new RotateTransform(90);
                    break;
                case 180:
                    transformedBitmap.Transform = new RotateTransform(180);
                    break;
                case 270:
                    transformedBitmap.Transform = new RotateTransform(270);
                    break;
            }

            transformedBitmap.EndInit();
            transformedBitmap.Freeze();

            return transformedBitmap;
        }
    }
}
