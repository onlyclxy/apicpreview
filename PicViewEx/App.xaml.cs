using PicViewEx;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;

namespace PicViewEx
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
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
                    var supportedFormats = new[] {
                        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif",
                        ".ico", ".webp", ".tga", ".dds", ".psd"
                    };

                    if (supportedFormats.Contains(extension))
                    {
                        // 延迟加载图片，等窗口初始化完成
                        mainWindow.Loaded += (s, args) =>
                        {
                            mainWindow.LoadImageFromCommandLine(filePath);
                        };
                    }
                }
            }

            // 显示窗口
            mainWindow.Show();
        }
    }
}
