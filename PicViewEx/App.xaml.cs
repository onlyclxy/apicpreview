using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PicViewEx
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // 1) UI线程未处理异常
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // 2) 非UI线程/域级未处理异常（尽量弹窗提示，无法标记为Handled）
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                ShowErrorDialog($"发生未处理异常（非UI线程）：\n{ex?.Message ?? e.ExceptionObject?.ToString()}");
            };

            // 3) 异步任务未观察异常
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                ShowErrorDialog($"发生未观察的任务异常：\n{e.Exception?.Flatten().Message}");
                e.SetObserved();
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 创建主窗口
            var mainWindow = new MainWindow();

            // 检查命令行参数
            if (e.Args.Length > 0)
            {
                string filePath = e.Args[0];

                // 检查文件是否存在并且是支持的图片格式
                if (File.Exists(filePath))
                {
                    string extension = Path.GetExtension(filePath).ToLower();
                    var supportedFormats = new[]
                    {
                        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif",
                        ".ico", ".webp", ".tga", ".dds", ".psd"
                    };

                    if (supportedFormats.Contains(extension))
                    {
                        // 等窗口初始化完成再加载，避免初始化期间阻塞UI
                        mainWindow.Loaded += (s, args) =>
                        {
                            try
                            {
                                mainWindow.LoadImageFromCommandLine(filePath);
                            }
                            catch (Exception exLoad)
                            {
                                ShowErrorDialog($"启动时加载图片失败：\n{exLoad.Message}");
                            }
                        };
                    }
                }
            }

            // 显示窗口
            mainWindow.Show();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // UI线程异常：弹窗 + 标记已处理，避免直接崩溃
            ShowErrorDialog($"发生未处理异常：\n{e.Exception.Message}");
            e.Handled = true;
        }

        private static void ShowErrorDialog(string message)
        {
            // 保证在UI线程弹窗
            if (Current?.Dispatcher != null && !Current.Dispatcher.CheckAccess())
            {
                Current.Dispatcher.Invoke(() =>
                    MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            else
            {
                MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
