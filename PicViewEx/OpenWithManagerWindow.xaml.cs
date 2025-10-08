using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace PicViewEx
{
    public partial class OpenWithManagerWindow : Window
    {
        public ObservableCollection<OpenWithAppViewModel> OpenWithApps { get; private set; }
        private OpenWithAppViewModel selectedApp;

        public OpenWithManagerWindow(List<OpenWithApp> apps)
        {
            InitializeComponent();

            // 初始化数据
            OpenWithApps = new ObservableCollection<OpenWithAppViewModel>();
            foreach (var app in apps)
            {
                OpenWithApps.Add(new OpenWithAppViewModel(app));
            }

            // 设置数据绑定
            dgOpenWithApps.ItemsSource = OpenWithApps;

            // 更新行号
            UpdateRowNumbers();

            // 如果有数据，选中第一行
            if (OpenWithApps.Count > 0)
            {
                dgOpenWithApps.SelectedIndex = 0;
            }
        }

        private void UpdateRowNumbers()
        {
            for (int i = 0; i < OpenWithApps.Count; i++)
            {
                if (dgOpenWithApps.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
                {
                    row.Header = (i + 1).ToString();
                }
            }
        }

        private void DgOpenWithApps_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedApp = dgOpenWithApps.SelectedItem as OpenWithAppViewModel;
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = selectedApp != null;
            int selectedIndex = dgOpenWithApps.SelectedIndex;

            btnRemove.IsEnabled = hasSelection;
            btnMoveUp.IsEnabled = hasSelection && selectedIndex > 0;
            btnMoveDown.IsEnabled = hasSelection && selectedIndex < OpenWithApps.Count - 1;
            btnSetIcon.IsEnabled = hasSelection;
            btnTestRun.IsEnabled = hasSelection && !string.IsNullOrEmpty(selectedApp?.ExecutablePath)
                                   && File.Exists(selectedApp?.ExecutablePath);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择应用程序",
                Filter = "可执行文件|*.exe|所有文件|*.*",
                InitialDirectory = GetProgramDirectory()
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string selectedPath = dialog.FileName;
                    string exeDirectory = GetProgramDirectory();

                    // 询问用户是否使用相对路径
                    bool useRelativePath = false;
                    if (selectedPath.StartsWith(exeDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        var result = MessageBox.Show(
                            $"检测到选择的程序在PicViewEx目录内。\n\n" +
                            $"完整路径: {selectedPath}\n" +
                            $"程序目录: {exeDirectory}\n\n" +
                            $"是否使用相对路径存储？\n" +
                            $"相对路径便于程序移动，但需要确保目标程序始终在相对位置。\n\n" +
                            $"是 - 使用相对路径\n" +
                            $"否 - 使用完整路径",
                            "路径选择",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        useRelativePath = (result == MessageBoxResult.Yes);
                    }

                    string finalExecutablePath;
                    if (useRelativePath)
                    {
                        // 转换为相对路径（替代 Path.GetRelativePath）
                        finalExecutablePath = GetRelativePath(exeDirectory, selectedPath);

                        // 确保使用正斜杠分隔符以保持一致性
                        finalExecutablePath = finalExecutablePath.Replace(Path.DirectorySeparatorChar, '/');
                    }
                    else
                    {
                        finalExecutablePath = selectedPath;
                    }


                    string appName = Path.GetFileNameWithoutExtension(selectedPath);

                    // 创建新的应用配置
                    var newApp = new OpenWithAppViewModel(new OpenWithApp
                    {
                        Name = appName,
                        ExecutablePath = finalExecutablePath,
                        Arguments = "\"{0}\"",
                        ShowText = false, // 改为默认false
                        IconPath = finalExecutablePath // 图标路径默认使用相同的路径
                    });

                    // 检查是否已存在相同的应用
                    bool exists = OpenWithApps.Any(app =>
                        string.Equals(app.ExecutablePath, finalExecutablePath, StringComparison.OrdinalIgnoreCase));

                    if (exists)
                    {
                        MessageBox.Show($"应用程序已经存在！\n路径: {finalExecutablePath}",
                            "添加失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    OpenWithApps.Add(newApp);
                    dgOpenWithApps.SelectedItem = newApp;
                    UpdateRowNumbers();

                    string pathTypeInfo = useRelativePath ? " (相对路径)" : " (完整路径)";
                    MessageBox.Show($"已成功添加应用程序！\n\n" +
                                  $"名称: {newApp.Name}\n" +
                                  $"路径: {finalExecutablePath}{pathTypeInfo}",
                                  "添加成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"添加应用程序失败: {ex.Message}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }



        public static string GetRelativePath(string basePath, string targetPath)
        {
            Uri baseUri = new Uri(AppendDirectorySeparatorChar(basePath));
            Uri targetUri = new Uri(targetPath);

            Uri relativeUri = baseUri.MakeRelativeUri(targetUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            // Windows 下用正斜杠转为反斜杠
            return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparatorChar(string path)
        {
            // 如果 basePath 不是目录，加上目录分隔符
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }
            return path;
        }



        /// <summary>
        /// 获取程序目录，与MainWindow.ResolveExecutablePath使用相同的逻辑
        /// </summary>
        private string GetProgramDirectory()
        {
            try
            {
                // 获取当前可执行文件的完整路径
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                // 获取可执行文件所在的目录
                return Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (selectedApp == null) return;

            var result = MessageBox.Show(
                $"确定要删除应用程序 \"{selectedApp.Name}\" 吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                int selectedIndex = dgOpenWithApps.SelectedIndex;
                OpenWithApps.Remove(selectedApp);

                // 选中下一个或上一个项
                if (OpenWithApps.Count > 0)
                {
                    int newIndex = Math.Min(selectedIndex, OpenWithApps.Count - 1);
                    dgOpenWithApps.SelectedIndex = newIndex;
                }

                UpdateRowNumbers();
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (selectedApp == null) return;

            int currentIndex = dgOpenWithApps.SelectedIndex;
            if (currentIndex > 0)
            {
                OpenWithApps.Move(currentIndex, currentIndex - 1);
                dgOpenWithApps.SelectedIndex = currentIndex - 1;
                UpdateRowNumbers();
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (selectedApp == null) return;

            int currentIndex = dgOpenWithApps.SelectedIndex;
            if (currentIndex < OpenWithApps.Count - 1)
            {
                OpenWithApps.Move(currentIndex, currentIndex + 1);
                dgOpenWithApps.SelectedIndex = currentIndex + 1;
                UpdateRowNumbers();
            }
        }

        private void BtnSetIcon_Click(object sender, RoutedEventArgs e)
        {
            if (selectedApp == null) return;

            var dialog = new OpenFileDialog
            {
                Title = "选择图标文件或程序文件",
                Filter = "所有支持的文件|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.exe|图标文件|*.ico|图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|程序文件|*.exe|所有文件|*.*",
                InitialDirectory = Path.GetDirectoryName(selectedApp.ExecutablePath) ?? @"C:\"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedApp.IconPath = dialog.FileName;
                selectedApp.RefreshIcon();

                // 提示用户选择的文件类型
                string extension = Path.GetExtension(dialog.FileName).ToLower();
                if (extension == ".exe")
                {
                    MessageBox.Show($"已从程序文件提取图标：\n{Path.GetFileName(dialog.FileName)}",
                        "图标设置", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"已设置图标文件：\n{Path.GetFileName(dialog.FileName)}",
                        "图标设置", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnTestRun_Click(object sender, RoutedEventArgs e)
        {
            if (selectedApp == null) return;

            try
            {
                // 解析可执行文件路径（支持相对路径）
                string resolvedExecutablePath = MainWindow.ResolveExecutablePath(selectedApp.ExecutablePath);

                // 检查可执行文件是否存在
                if (!File.Exists(resolvedExecutablePath))
                {
                    MessageBox.Show($"找不到应用程序！\n\n" +
                                  $"原始路径: {selectedApp.ExecutablePath}\n" +
                                  $"解析路径: {resolvedExecutablePath}\n\n" +
                                  $"请检查应用程序路径是否正确。",
                                  "文件不存在", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 创建一个测试图片文件路径（可能不存在，仅用于测试命令行参数）
                string testImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_image.png");
                string testArguments = string.Format(selectedApp.Arguments, testImagePath);

                var result = MessageBox.Show(
                    $"即将测试运行以下应用程序：\n\n" +
                    $"应用名称: {selectedApp.Name}\n" +
                    $"原始路径: {selectedApp.ExecutablePath}\n" +
                    $"解析路径: {resolvedExecutablePath}\n" +
                    $"启动参数: {testArguments}\n\n" +
                    $"注意：测试将使用虚拟图片路径，应用程序可能会显示文件未找到的错误。\n\n" +
                    $"是否继续测试？",
                    "测试运行确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = resolvedExecutablePath,
                        Arguments = testArguments,
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);

                    MessageBox.Show($"测试运行命令已发送！\n\n" +
                                  $"如果应用程序没有启动，请检查：\n" +
                                  $"1. 路径是否正确\n" +
                                  $"2. 应用程序是否可执行\n" +
                                  $"3. 启动参数格式是否正确",
                                  "测试完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试运行失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    public class OpenWithAppViewModel : INotifyPropertyChanged
    {
        private string _name = "";
        private string _executablePath = "";
        private string _arguments = "";
        private bool _showText = false; // 改为默认false
        private string _iconPath = "";
        private BitmapImage _iconSource;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string ExecutablePath
        {
            get => _executablePath;
            set
            {
                _executablePath = value;
                OnPropertyChanged(nameof(ExecutablePath));
                OnPropertyChanged(nameof(FileName));
                RefreshIcon(); // 当路径改变时自动刷新图标
            }
        }

        public string FileName => string.IsNullOrEmpty(ExecutablePath) ? "" : Path.GetFileName(ExecutablePath);

        public string Arguments
        {
            get => _arguments;
            set
            {
                _arguments = value;
                OnPropertyChanged(nameof(Arguments));
            }
        }

        public bool ShowText
        {
            get => _showText;
            set
            {
                _showText = value;
                OnPropertyChanged(nameof(ShowText));
            }
        }

        public string IconPath
        {
            get => _iconPath;
            set
            {
                _iconPath = value;
                OnPropertyChanged(nameof(IconPath));
                RefreshIcon();
            }
        }

        public BitmapImage IconSource
        {
            get => _iconSource;
            private set
            {
                _iconSource = value;
                OnPropertyChanged(nameof(IconSource));
            }
        }

        public OpenWithAppViewModel(OpenWithApp app)
        {
            _name = app.Name;
            _executablePath = app.ExecutablePath;
            _arguments = app.Arguments;
            _showText = app.ShowText;
            _iconPath = app.IconPath;

            RefreshIcon();
        }

        public OpenWithApp ToOpenWithApp()
        {
            return new OpenWithApp
            {
                Name = this.Name,
                ExecutablePath = this.ExecutablePath,
                Arguments = this.Arguments,
                ShowText = this.ShowText,
                IconPath = this.IconPath
            };
        }

        public void RefreshIcon()
        {
            _iconSource = null;

            if (!string.IsNullOrEmpty(_iconPath))
            {
                // 解析图标路径（支持相对路径）
                string resolvedIconPath = MainWindow.ResolveExecutablePath(_iconPath);

                // 首先尝试直接加载图片文件
                if (File.Exists(resolvedIconPath))
                {
                    string extension = Path.GetExtension(resolvedIconPath).ToLower();
                    if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" ||
                        extension == ".bmp" || extension == ".ico")
                    {
                        _iconSource = LoadImageFile(resolvedIconPath);
                        if (_iconSource != null)
                        {
                            OnPropertyChanged(nameof(IconSource));
                            return;
                        }
                    }
                }

                // 如果图标路径是exe文件，从exe提取图标
                if (resolvedIconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(resolvedIconPath))
                {
                    _iconSource = ExtractIconFromExeDirect(resolvedIconPath);
                    OnPropertyChanged(nameof(IconSource));
                    return;
                }
            }

            // 如果图标路径为空或无效，尝试从可执行文件路径提取图标
            if (!string.IsNullOrEmpty(_executablePath))
            {
                string resolvedExecutablePath = MainWindow.ResolveExecutablePath(_executablePath);
                if (File.Exists(resolvedExecutablePath))
                {
                    _iconSource = ExtractIconFromExeDirect(resolvedExecutablePath);
                }
            }

            OnPropertyChanged(nameof(IconSource));
        }

        // 直接从exe文件提取图标并转换为BitmapImage
        private BitmapImage ExtractIconFromExeDirect(string exePath)
        {
            try
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                {
                    using (icon)
                    using (var bitmap = icon.ToBitmap())
                    using (var memory = new MemoryStream())
                    {
                        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                        memory.Position = 0;

                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = memory;
                        bitmapImage.DecodePixelWidth = 32;
                        bitmapImage.DecodePixelHeight = 32;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        return bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从exe提取图标失败 ({exePath}): {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 加载图片文件为BitmapImage
        /// </summary>
        private BitmapImage LoadImageFile(string imagePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.DecodePixelWidth = 32;
                bitmap.DecodePixelHeight = 32;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载图片文件失败 ({imagePath}): {ex.Message}");
                return null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}