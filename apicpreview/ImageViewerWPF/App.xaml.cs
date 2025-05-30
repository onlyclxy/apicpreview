using System.Windows;

namespace ImageViewerWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 创建主窗口
            var mainWindow = new MainWindow();
            
            // 如果有命令行参数，加载第一个参数作为图片
            if (e.Args.Length > 0)
            {
                mainWindow.LoadImage(e.Args[0]);
            }
            
            mainWindow.Show();
        }
    }
} 