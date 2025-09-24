using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

// 导入 LEADTOOLS 命名空间
using Leadtools;
using Leadtools.Codecs;

namespace LeadtoolsImageBrowser.Net8
{
    public partial class MainWindow : Window
    {
        private RasterCodecs? _codecs;
        // 用于存储加载的多页图像对象
        private RasterImage? _rasterImage;
        // 用于跟踪当前显示的页码
        private int _currentPage = 1;
        // 用于存储当前打开文件的路径
        private string? _currentFilePath;


        public MainWindow()
        {
            InitializeComponent();
            InitializeLeadtools();
            UpdateNavigationControls(); // 初始时禁用按钮
        }

        private void InitializeLeadtools()
        {
            Console.WriteLine("--- InitializeLeadtools: Starting LEADTOOLS initialization ---");
            try
            {
                // 检查许可证文件是否存在
                string keyPath = "full_license.key";
                string licPath = "full_license.lic";
                
                if (!File.Exists(keyPath) || !File.Exists(licPath))
                {
                    Console.WriteLine("InitializeLeadtools: License files not found, attempting to initialize without license...");
                    StatusTextBlock.Text = "License files not found - running in evaluation mode";
                    
                    // 尝试不使用许可证初始化（评估模式）
                    _codecs = new RasterCodecs();
                    
                    // 启用所有可用的编解码器
                    _codecs.Options.Load.AllPages = true;
                    
                    // 为了更好地处理PDF等矢量格式，建议设置光栅化选项
                    _codecs.Options.RasterizeDocument.Load.XResolution = 300;
                    _codecs.Options.RasterizeDocument.Load.YResolution = 300;
                    
                    StatusTextBlock.Text = "LEADTOOLS initialization successful (evaluation mode)";
                    Console.WriteLine("InitializeLeadtools: LEADTOOLS initialization successful (evaluation mode).");
                    return;
                }
                
                // 请确保许可证文件路径正确
                var key = File.ReadAllText(keyPath);
                var lic = File.ReadAllBytes(licPath);
                RasterSupport.SetLicense(lic, key);
                Console.WriteLine("InitializeLeadtools: License set.");

                _codecs = new RasterCodecs();

                // 启用所有可用的编解码器
                _codecs.Options.Load.AllPages = true;
                
                // 为了更好地处理PDF等矢量格式，建议设置光栅化选项
                _codecs.Options.RasterizeDocument.Load.XResolution = 300;
                _codecs.Options.RasterizeDocument.Load.YResolution = 300;

                StatusTextBlock.Text = "LEADTOOLS initialization successful (license loaded)";
                Console.WriteLine("InitializeLeadtools: LEADTOOLS initialization successful.");
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"LEADTOOLS initialization failed: {ex.Message}";
                Console.WriteLine($"InitializeLeadtools: LEADTOOLS initialization failed! Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"LEADTOOLS initialization failed! Please check license settings and DLL references.\nError: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            if (_codecs == null)
            {
                StatusTextBlock.Text = "LEADTOOLS not initialized.";
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "All Supported Files|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.gif;*.pdf|PDF Files|*.pdf|Image Files|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.gif|All Files|*.*",
                Title = "Select File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _currentFilePath = openFileDialog.FileName;
                StatusTextBlock.Text = $"Loading: {Path.GetFileName(_currentFilePath)}...";
                ImageViewer.Source = null;

                if (_rasterImage != null)
                {
                    _rasterImage.Dispose();
                    _rasterImage = null;
                }

                try
                {
                    Console.WriteLine($"OpenImage_Click: Attempting to load all pages from '{_currentFilePath}'...");
                    
                    // 首先检查文件信息
                    var info = await Task.Run(() => _codecs.GetInformation(_currentFilePath, true));
                    Console.WriteLine($"File info: Format={info.Format}, Pages={info.TotalPages}, Size={info.Width}x{info.Height}");
                    
                    // 使用 Task.Run 在后台线程上调用同步的 Load 方法，并传入参数以加载所有页面 (1, -1)
                    _rasterImage = await Task.Run(() => _codecs.Load(_currentFilePath, 0, CodecsLoadByteOrder.BgrOrGray, 1, -1));
                    Console.WriteLine($"OpenImage_Click: File loaded. Page Count: {_rasterImage.PageCount}");

                    _currentPage = 1;
                    await DisplayPageAsync(_currentPage);
                }
                catch (RasterException rex)
                {
                    StatusTextBlock.Text = $"Load failed: LEADTOOLS Error Code {rex.Code} - {rex.Message}";
                    Console.WriteLine($"RasterException: Code={rex.Code}, Message={rex.Message}");
                    Console.WriteLine($"Stack Trace: {rex.StackTrace}");
                    MessageBox.Show($"Failed to load file!\nLEADTOOLS Error Code: {rex.Code}\nError: {rex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = $"Load failed: {ex.Message}";
                    Console.WriteLine($"General Exception: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    MessageBox.Show($"Failed to load file!\nError: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 异步显示 RasterImage 中的指定页面
        /// </summary>
        /// <param name="pageNumber">要显示的页码 (从1开始)</param>
        private async Task DisplayPageAsync(int pageNumber)
        {
            if (_rasterImage == null || _codecs == null || pageNumber < 1 || pageNumber > _rasterImage.PageCount)
            {
                return;
            }

            StatusTextBlock.Text = $"Loading page {pageNumber}...";

            try
            {
                // 设置当前活动页面
                _rasterImage.Page = pageNumber;

                // 使用更兼容的方式转换图像
                BitmapSource bitmapSource = await Task.Run(() => {
                    try
                    {
                        // 方法1: 直接转换为 BMP 格式
                        using (var ms = new MemoryStream())
                        {
                            // 使用标准 BMP 格式保存
                            _codecs.Save(_rasterImage, ms, RasterImageFormat.Bmp, 24);
                            ms.Position = 0;

                            // 从内存流创建 BitmapImage
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = ms;
                            bmp.EndInit();
                            bmp.Freeze(); // 允许在非UI线程上访问
                            return bmp;
                        }
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Method 1 failed: {ex1.Message}");
                        
                        // 方法2: 尝试 PNG 格式
                        try
                        {
                            using (var ms = new MemoryStream())
                            {
                                _codecs.Save(_rasterImage, ms, RasterImageFormat.Png, 0);
                                ms.Position = 0;

                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.StreamSource = ms;
                                bmp.EndInit();
                                bmp.Freeze();
                                return bmp;
                            }
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine($"Method 2 failed: {ex2.Message}");
                            
                            // 方法3: 使用系统默认格式
                            using (var ms = new MemoryStream())
                            {
                                _codecs.Save(_rasterImage, ms, RasterImageFormat.RasPdf, 0);
                                ms.Position = 0;

                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.StreamSource = ms;
                                bmp.EndInit();
                                bmp.Freeze();
                                return bmp;
                            }
                        }
                    }
                });

                ImageViewer.Source = bitmapSource;

                // 更新状态信息和导航控件
                UpdateNavigationControls();
                StatusTextBlock.Text = $"File: {Path.GetFileName(_currentFilePath)}, Page {pageNumber} of {_rasterImage.PageCount}, {_rasterImage.Width}x{_rasterImage.Height}";

            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Failed to display page {pageNumber}. Error: {ex.Message}";
                Console.WriteLine($"DisplayPageAsync Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private async void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await DisplayPageAsync(_currentPage);
            }
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_rasterImage != null && _currentPage < _rasterImage.PageCount)
            {
                _currentPage++;
                await DisplayPageAsync(_currentPage);
            }
        }

        /// <summary>
        /// 更新导航按钮和页码文本的状态
        /// </summary>
        private void UpdateNavigationControls()
        {
            if (_rasterImage == null || _rasterImage.PageCount <= 1)
            {
                PageNav.Visibility = Visibility.Collapsed;
            }
            else
            {
                PageNav.Visibility = Visibility.Visible;
                PrevButton.IsEnabled = _currentPage > 1;
                NextButton.IsEnabled = _currentPage < _rasterImage.PageCount;
                PageInfo.Text = $"{_currentPage} / {_rasterImage.PageCount}";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            try
            {
                if (_codecs != null)
                {
                    _codecs.Dispose();
                    _codecs = null;
                }
                if (_rasterImage != null)
                {
                    _rasterImage.Dispose();
                    _rasterImage = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error: {ex.Message}");
            }
        }
    }
}
