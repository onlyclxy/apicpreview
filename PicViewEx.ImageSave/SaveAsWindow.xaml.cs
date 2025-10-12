using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        public string SavedFilePath { get; private set; }

        public SaveAsWindow(BitmapSource source, string originalFilePath, NvidiaTextureTools nvidiaTools)
        {
            InitializeComponent();

            _source = source;
            _originalFilePath = originalFilePath;
            _nvidiaTools = nvidiaTools;
            _presetManager = new DdsPresetManager();

            // 设置JPG质量滑块和文本框的双向绑定
            JpgQualitySlider.ValueChanged += JpgQualitySlider_ValueChanged;
            JpgQualityTextBox.TextChanged += JpgQualityTextBox_TextChanged;

            // 设置DDS模式切换
            RbDdsPreset.Checked += (s, e) => PresetListPanel.Visibility = Visibility.Visible;
            RbDdsCustom.Checked += (s, e) => PresetListPanel.Visibility = Visibility.Collapsed;
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

        private void LoadDdsPresets()
        {
            // 加载历史预设
            var historyPresets = _presetManager.GetHistoryPresets();
            if (historyPresets.Count > 0)
            {
                HistoryHeader.Visibility = Visibility.Visible;
                HistoryPresetsList.Items.Clear();

                foreach (var preset in historyPresets)
                {
                    var button = CreatePresetButton(preset, true);
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

            var stackPanel = new StackPanel();

            var nameText = new TextBlock
            {
                Text = preset.FileName,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };

            var dateText = new TextBlock
            {
                Text = $"修改时间: {preset.LastModified:yyyy-MM-dd HH:mm}",
                FontSize = 11,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 2, 0, 0)
            };

            stackPanel.Children.Add(nameText);
            stackPanel.Children.Add(dateText);

            if (isHistory)
            {
                var historyTag = new TextBlock
                {
                    Text = "常用",
                    FontSize = 10,
                    Foreground = System.Windows.Media.Brushes.LightGreen,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                stackPanel.Children.Add(historyTag);
            }

            button.Content = stackPanel;
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
            else if (_selectedFormat == "DDS")
            {
                if (RbDdsCustom.IsChecked == true)
                {
                    // 使用NVIDIA UI
                    string tempPng = _nvidiaTools.CreateTempPngForDds(_source);
                    if (!string.IsNullOrEmpty(tempPng))
                    {
                        _nvidiaTools.ExportWithUI(tempPng);
                        MessageBox.Show("已启动 NVIDIA Texture Tools 界面，请在其中完成DDS保存。",
                            "信息", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                        Close();
                    }
                }
                else
                {
                    MessageBox.Show("请选择一个预设文件。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
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
                return saver.SaveTo(_source, path, options);
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
                string tempPng = _nvidiaTools.CreateTempPngForDds(_source);
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
    }
}
