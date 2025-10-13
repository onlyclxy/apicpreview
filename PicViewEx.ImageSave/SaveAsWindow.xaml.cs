using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace PicViewEx.ImageSave
{
    public partial class SaveAsWindow : Window
    {
        private readonly BitmapSource _source;
        private readonly string _originalFilePath;
        private readonly NvidiaTextureTools _nvidiaTools;
        private readonly DdsPresetManager _presetManager;
        private string _selectedFormat = "";
        private string _selectedPresetPath = null;
        private double _rotationAngle = 0;
        private BitmapSource _currentSource;

        public string SavedFilePath { get; private set; }

        public SaveAsWindow(BitmapSource source, string originalFilePath, NvidiaTextureTools nvidiaTools)
        {
            InitializeComponent();

            _source = source;
            _currentSource = source;
            _originalFilePath = originalFilePath;
            _nvidiaTools = nvidiaTools;
            _presetManager = new DdsPresetManager();

            // 设置预览图片
            PreviewImage.Source = _currentSource;

            // 设置JPG质量滑块和文本框的双向绑定
            JpgQualitySlider.ValueChanged += JpgQualitySlider_ValueChanged;
            JpgQualityTextBox.TextChanged += JpgQualityTextBox_TextChanged;

            // 添加窗口级别的鼠标点击事件来处理高级设置面板的收起
            this.PreviewMouseLeftButtonDown += Window_PreviewMouseLeftButtonDown;
        }

        // 窗口级别的鼠标点击事件,用于关闭高级设置面板
        private void Window_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (OptionsPanel.Visibility == Visibility.Visible)
            {
                // 检查点击位置是否在OptionsPanel内部
                var position = e.GetPosition(OptionsPanel);
                if (position.X < 0 || position.Y < 0 ||
                    position.X > OptionsPanel.ActualWidth ||
                    position.Y > OptionsPanel.ActualHeight)
                {
                    // 点击在OptionsPanel外部,关闭面板
                    OptionsPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        // 主Grid点击事件(保留作为后备)
        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 这个方法现在作为后备,主要逻辑在Window_PreviewMouseLeftButtonDown中
        }

        // 移除这两个不再需要的方法
        private void WrapPanel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 不再需要阻止事件冒泡
        }

        private void OptionsPanel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 不再需要阻止事件冒泡
        }

        private void BtnPng_Click(object sender, RoutedEventArgs e)
        {
            _selectedFormat = "PNG";
            ShowSaveDialog("PNG图像 (*.png)|*.png");
        }

        private void BtnJpg_Click(object sender, RoutedEventArgs e)
        {
            _selectedFormat = "JPG";
            OptionsPanel.Visibility = Visibility.Visible;
            JpgQualityPanel.Visibility = Visibility.Visible;
            DdsPresetPanel.Visibility = Visibility.Collapsed;

            // 如果原文件是JPG，尝试获取其质量
            if (_originalFilePath != null && Path.GetExtension(_originalFilePath).ToLower() == ".jpg" ||
                Path.GetExtension(_originalFilePath).ToLower() == ".jpeg")
            {
                int quality = JpegQualityAnalyzer.EstimateQuality(_originalFilePath);
                JpgQualitySlider.Value = quality;
            }
        }

        private void BtnBmp_Click(object sender, RoutedEventArgs e)
        {
            _selectedFormat = "BMP";
            ShowSaveDialog("BMP图像 (*.bmp)|*.bmp");
        }

        private void BtnTga_Click(object sender, RoutedEventArgs e)
        {
            _selectedFormat = "TGA";
            ShowSaveDialog("TGA图像 (*.tga)|*.tga");
        }

        private void BtnDds_Click(object sender, RoutedEventArgs e)
        {
            _selectedFormat = "DDS";

            if (!_nvidiaTools.IsAvailable)
            {
                MessageBox.Show("NVIDIA Texture Tools 不可用！\n\n请确保 NVIDIA Texture Tools 文件夹存在于程序根目录。",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OptionsPanel.Visibility = Visibility.Visible;
            JpgQualityPanel.Visibility = Visibility.Collapsed;
            DdsPresetPanel.Visibility = Visibility.Visible;

            LoadDdsPresets();
        }

        // DDS自定义按钮点击事件
        private void BtnDdsCustom_Click(object sender, RoutedEventArgs e)
        {
            // 检查图像尺寸是否为2的幂次方
            if (!IsPowerOfTwo(_currentSource.PixelWidth) || !IsPowerOfTwo(_currentSource.PixelHeight))
            {
                string message = $"警告:当前图像尺寸为 {_currentSource.PixelWidth} x {_currentSource.PixelHeight}\n\n" +
                               "DDS格式建议使用2的幂次方尺寸(如256x256, 512x512, 1024x1024等)。\n\n" +
                               "非2的幂次方尺寸的DDS图像可能会:\n" +
                               "• 在某些游戏引擎中无法正确显示\n" +
                               "• 无法生成完整的mipmap链\n" +
                               "• 在某些硬件上加载失败\n\n" +
                               "建议先将图像缩放到2的幂次方尺寸后再保存为DDS格式。\n\n" +
                               "是否仍要继续?";

                var result = MessageBox.Show(message, "DDS尺寸警告",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            // 使用NVIDIA UI - 不等待,不提示成功,使用旋转后的图像
            string tempPng = _nvidiaTools.CreateTempPngForDds(_currentSource);
            if (!string.IsNullOrEmpty(tempPng))
            {
                bool launched = _nvidiaTools.ExportWithUI(tempPng);
                if (launched)
                {
                    // UI模式:只通知已启动,不等待完成
                    MessageBox.Show("已启动 NVIDIA Texture Tools 界面。\n\n请在界面中完成DDS保存操作。",
                        "信息", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 关闭另存为窗口,但不算"成功"(因为用户还没保存)
                    DialogResult = false;
                    Close();
                }
                else
                {
                    MessageBox.Show("启动 NVIDIA Texture Tools 失败！",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("创建临时PNG文件失败！",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDdsPresets()
        {
            // 加载历史预设(最多3个)
            var historyPresets = _presetManager.GetHistoryPresets();
            if (historyPresets.Count > 0)
            {
                HistoryHeader.Visibility = Visibility.Visible;
                HistoryPresetsList.Items.Clear();

                // 最多显示3个常用预设
                int count = Math.Min(historyPresets.Count, 3);
                for (int i = 0; i < count; i++)
                {
                    var button = CreatePresetButton(historyPresets[i], true);
                    HistoryPresetsList.Items.Add(button);
                }
            }
            else
            {
                HistoryHeader.Visibility = Visibility.Collapsed;
            }

            // 加载所有预设
            var allPresets = _presetManager.GetAllPresets();
            AllPresetsList.Items.Clear();

            if (allPresets.Count == 0)
            {
                var noPresetsText = new TextBlock
                {
                    Text = "没有找到预设文件。\n请在 'DDS presets' 文件夹中添加 .dpf 预设文件。",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontSize = 12,
                    Margin = new Thickness(5)
                };
                AllPresetsList.Items.Add(noPresetsText);
            }
            else
            {
                foreach (var preset in allPresets)
                {
                    var button = CreatePresetButton(preset, false);
                    AllPresetsList.Items.Add(button);
                }
            }
        }

        private Button CreatePresetButton(DdsPresetInfo preset, bool isHistory)
        {
            var button = new Button
            {
                Style = (Style)FindResource("PresetButtonStyle"),
                Tag = preset.FilePath
            };

            // 使用Grid来水平排列内容,确保在一行内显示
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 文件名
            var nameText = new TextBlock
            {
                Text = preset.FileName,
                FontSize = 12,
                FontWeight = FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 0);

            // 常用标签(如果是历史记录)
            if (isHistory)
            {
                var historyTag = new TextBlock
                {
                    Text = "[常用]",
                    FontSize = 10,
                    Foreground = System.Windows.Media.Brushes.LightGreen,
                    Margin = new Thickness(5, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(historyTag, 1);
                grid.Children.Add(historyTag);
            }

            grid.Children.Add(nameText);
            button.Content = grid;
            button.Click += PresetButton_Click;

            return button;
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                _selectedPresetPath = button.Tag as string;

                // 添加到历史
                if (!string.IsNullOrEmpty(_selectedPresetPath))
                {
                    _presetManager.AddToHistory(_selectedPresetPath);
                }

                // 直接打开保存对话框
                ShowSaveDialog("DDS图像 (*.dds)|*.dds");
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFormat == "JPG")
            {
                ShowSaveDialog("JPEG图像 (*.jpg)|*.jpg|所有文件 (*.*)|*.*");
            }
        }

        private void ShowSaveDialog(string filter)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = filter,
                FilterIndex = 1
            };

            // 设置默认文件名
            if (!string.IsNullOrEmpty(_originalFilePath))
            {
                string originalName = Path.GetFileNameWithoutExtension(_originalFilePath);
                string originalExt = Path.GetExtension(_originalFilePath).ToLower();

                // 如果原格式和目标格式不同，使用新扩展名
                string targetExt = _selectedFormat.ToLower();
                if (originalExt.TrimStart('.') == targetExt)
                {
                    dialog.FileName = Path.GetFileName(_originalFilePath);
                }
                else
                {
                    dialog.FileName = $"{originalName}.{targetExt}";
                }

                dialog.InitialDirectory = Path.GetDirectoryName(_originalFilePath);
            }

            if (dialog.ShowDialog() == true)
            {
                SaveResult result = null;

                if (_selectedFormat == "JPG")
                {
                    var options = new JpegSaveOptions
                    {
                        Quality = (int)JpgQualitySlider.Value
                    };
                    result = SaveImage(dialog.FileName, options);
                }
                else if (_selectedFormat == "PNG")
                {
                    result = SaveImage(dialog.FileName, new PngSaveOptions());
                }
                else if (_selectedFormat == "BMP")
                {
                    result = SaveImage(dialog.FileName, new BmpSaveOptions());
                }
                else if (_selectedFormat == "TGA")
                {
                    result = SaveImage(dialog.FileName, new TgaSaveOptions());
                }
                else if (_selectedFormat == "DDS")
                {
                    var options = new DdsSaveOptions
                    {
                        PresetPath = _selectedPresetPath
                    };
                    result = SaveDdsImage(dialog.FileName, options);
                }

                if (result != null)
                {
                    if (result.Success)
                    {
                        SavedFilePath = result.SavedPath;
                        MessageBox.Show($"保存成功！\n\n文件路径: {result.SavedPath}",
                            "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show($"保存失败！\n\n{result.Message}\n{result.ErrorDetails}",
                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private SaveResult SaveImage(string path, SaveOptions options)
        {
            try
            {
                var saver = new ImageSaver();
                // 使用旋转后的图像源
                return saver.SaveTo(_currentSource, path, options);
            }
            catch (Exception ex)
            {
                return new SaveResult
                {
                    Success = false,
                    Message = "保存失败",
                    ErrorDetails = ex.Message
                };
            }
        }

        private SaveResult SaveDdsImage(string path, DdsSaveOptions options)
        {
            try
            {
                // 检查图像尺寸是否为2的幂次方
                if (!IsPowerOfTwo(_currentSource.PixelWidth) || !IsPowerOfTwo(_currentSource.PixelHeight))
                {
                    string message = $"警告:当前图像尺寸为 {_currentSource.PixelWidth} x {_currentSource.PixelHeight}\n\n" +
                                   "DDS格式建议使用2的幂次方尺寸(如256x256, 512x512, 1024x1024等)。\n\n" +
                                   "非2的幂次方尺寸的DDS图像可能会:\n" +
                                   "• 在某些游戏引擎中无法正确显示\n" +
                                   "• 无法生成完整的mipmap链\n" +
                                   "• 在某些硬件上加载失败\n\n" +
                                   "建议先将图像缩放到2的幂次方尺寸后再保存为DDS格式。\n\n" +
                                   "是否仍要继续保存?";

                    var result = MessageBox.Show(message, "DDS尺寸警告",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

                    if (result != MessageBoxResult.Yes)
                    {
                        return new SaveResult
                        {
                            Success = false,
                            Message = "用户取消保存"
                        };
                    }
                }

                // 使用旋转后的图像源
                string tempPng = _nvidiaTools.CreateTempPngForDds(_currentSource);
                if (string.IsNullOrEmpty(tempPng))
                {
                    return new SaveResult
                    {
                        Success = false,
                        Message = "创建临时PNG失败"
                    };
                }

                try
                {
                    bool success = _nvidiaTools.ExportWithPreset(tempPng, options.PresetPath, path);

                    return new SaveResult
                    {
                        Success = success,
                        Message = success ? "保存成功" : "保存失败",
                        SavedPath = success ? path : null
                    };
                }
                finally
                {
                    if (File.Exists(tempPng))
                    {
                        try { File.Delete(tempPng); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                return new SaveResult
                {
                    Success = false,
                    Message = "保存失败",
                    ErrorDetails = ex.Message
                };
            }
        }

        // 检查一个数是否为2的幂次方
        private bool IsPowerOfTwo(int n)
        {
            return n > 0 && (n & (n - 1)) == 0;
        }

        private void JpgQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (JpgQualityTextBox != null)
            {
                JpgQualityTextBox.Text = ((int)e.NewValue).ToString();
            }
        }

        private void JpgQualityTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(JpgQualityTextBox.Text, out int value))
            {
                if (value >= 1 && value <= 100)
                {
                    JpgQualitySlider.Value = value;
                }
            }
        }

        private void BtnRotateLeft_Click(object sender, RoutedEventArgs e)
        {
            _rotationAngle -= 90;
            if (_rotationAngle <= -360)
                _rotationAngle += 360;

            ApplyRotation();
        }

        private void BtnRotateRight_Click(object sender, RoutedEventArgs e)
        {
            _rotationAngle += 90;
            if (_rotationAngle >= 360)
                _rotationAngle -= 360;

            ApplyRotation();
        }

        private void ApplyRotation()
        {
            // 更新旋转变换
            ImageRotateTransform.Angle = _rotationAngle;

            // 更新角度显示
            RotationAngleText.Text = $"当前角度: {_rotationAngle}°";

            // 应用旋转到实际图像数据
            _currentSource = RotateBitmap(_source, _rotationAngle);

            // 更新预览
            PreviewImage.Source = _source;
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
