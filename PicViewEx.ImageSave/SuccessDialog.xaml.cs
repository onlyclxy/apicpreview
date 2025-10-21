using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace PicViewEx.ImageSave
{
    public partial class SuccessDialog : Window
    {
        private string _filePath;

        private SuccessDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 显示保存成功对话框
        /// </summary>
        /// <param name="filePath">保存的文件路径</param>
        /// <param name="owner">父窗口</param>
        public static void Show(string filePath, Window owner = null)
        {
            var dialog = new SuccessDialog();
            
            if (owner != null)
            {
                dialog.Owner = owner;
            }

            dialog._filePath = filePath;
            dialog.PathText.Text = filePath;

            // 显示文件信息
            if (File.Exists(filePath))
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    
                    // 文件大小
                    string sizeStr = FormatFileSize(fileInfo.Length);
                    dialog.FileSizeText.Text = $"大小: {sizeStr}";

                    // 文件格式
                    string extension = Path.GetExtension(filePath).ToUpper().TrimStart('.');
                    dialog.FileFormatText.Text = $"格式: {extension}";
                }
                catch
                {
                    dialog.InfoPanel.Visibility = Visibility.Collapsed;
                }
            }

            dialog.ShowDialog();
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 打开文件夹并选中文件
        /// </summary>
        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                CustomMessageBox.Show(
                    "文件不存在或路径无效！", 
                    "错误", 
                    CustomMessageBox.MessageBoxButtons.OK, 
                    CustomMessageBox.MessageBoxType.Error,
                    this);
                return;
            }

            try
            {
                // 使用 explorer.exe 打开文件夹并选中文件
                Process.Start("explorer.exe", $"/select,\"{_filePath}\"");
                
                // 关闭对话框
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"无法打开文件夹！\n\n错误详情: {ex.Message}", 
                    "错误", 
                    CustomMessageBox.MessageBoxButtons.OK, 
                    CustomMessageBox.MessageBoxType.Error,
                    this);
            }
        }

        /// <summary>
        /// 复制路径到剪贴板
        /// </summary>
        private void BtnCopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_filePath))
            {
                return;
            }

            try
            {
                Clipboard.SetText(_filePath);
                
                // 显示复制成功提示
                BtnCopyPath.Content = "✔️ 已复制";
                
                // 2秒后恢复按钮文本
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (s, args) =>
                {
                    BtnCopyPath.Content = "📋 复制路径";
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"复制路径失败！\n\n错误详情: {ex.Message}", 
                    "错误", 
                    CustomMessageBox.MessageBoxButtons.OK, 
                    CustomMessageBox.MessageBoxType.Error,
                    this);
            }
        }

        /// <summary>
        /// 关闭对话框
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}

