using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PicViewEx
{
    public partial class MainWindow
    {
        #region 文件操作方法

        private void OpenWithApp(int index)
        {
            if (index >= openWithApps.Count)
                return;

            try
            {
                // 获取当前图片的有效路径（包括临时文件）
                string imagePath = GetCurrentImagePath();

                var app = openWithApps[index];

                // 解析可执行文件路径（支持相对路径）
                string resolvedExecutablePath = ResolveExecutablePath(app.ExecutablePath);

                // 检查可执行文件是否存在
                if (!File.Exists(resolvedExecutablePath))
                {
                    MessageBox.Show($"找不到应用程序: {resolvedExecutablePath}\n\n原始路径: {app.ExecutablePath}",
                        "文件不存在", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 根据是否有图片路径决定参数
                string arguments;
                if (string.IsNullOrEmpty(imagePath))
                {
                    // 没有图片时，不传递任何参数
                    arguments = "";
                }
                else
                {
                    // 有图片时，格式化参数
                    arguments = string.Format(app.Arguments, imagePath);
                }

                // 创建进程启动信息
                var startInfo = new ProcessStartInfo
                {
                    FileName = resolvedExecutablePath,  // 文件路径会被系统自动处理引号
                    Arguments = arguments,              // 参数中的路径应该已经包含引号
                    UseShellExecute = true,            // 使用Shell执行，系统会自动处理路径中的空格
                    WorkingDirectory = Path.GetDirectoryName(resolvedExecutablePath) ?? ""
                };

                // 启动进程
                Process.Start(startInfo);

                if (statusText != null)
                {
                    string sourceInfo = string.IsNullOrEmpty(currentImagePath) ? "剪贴板图片" : "文件图片";
                    string pathInfo = app.ExecutablePath == resolvedExecutablePath ?
                        app.Name : $"{app.Name} (相对路径)";
                    
                    // 如果没有图片，显示不同的状态信息
                    if (string.IsNullOrEmpty(imagePath))
                    {
                        UpdateStatusText($"已启动 {pathInfo} (无参数)");
                    }
                    else
                    {
                        UpdateStatusText($"已用 {pathInfo} 打开 {sourceInfo}");
                    }
                }
            }
            catch (Exception ex)
            {
                string errorDetails = $"打开失败: {ex.Message}\n\n";
                if (index < openWithApps.Count)
                {
                    var app = openWithApps[index];
                    string resolvedPath = ResolveExecutablePath(app.ExecutablePath);
                    errorDetails += $"应用名称: {app.Name}\n";
                    errorDetails += $"原始路径: {app.ExecutablePath}\n";
                    errorDetails += $"解析路径: {resolvedPath}\n";
                    errorDetails += $"启动参数: {app.Arguments}";
                }

                MessageBox.Show(errorDetails, "打开失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFileLocation()
        {
            try
            {
                // 如果是原始文件，直接在资源管理器中显示
                if (!string.IsNullOrEmpty(currentImagePath) && File.Exists(currentImagePath))
                {
                    var argument = $"/select, \"{currentImagePath}\"";
                    Process.Start("explorer.exe", argument);

                    if (statusText != null)
                        UpdateStatusText("已在资源管理器中显示文件");
                }
                // 如果是剪贴板图片，提示用户并创建临时文件
                else if (mainImage?.Source != null)
                {
                    var result = MessageBox.Show(
                        "当前显示的是剪贴板图片，没有原始文件位置。\n\n" +
                        "是否要创建临时文件并在资源管理器中显示？\n\n" +
                        "注意：临时文件在程序关闭时会被删除。",
                        "剪贴板图片",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        string tempPath = CreateTemporaryImageFile();
                        var argument = $"/select, \"{tempPath}\"";
                        Process.Start("explorer.exe", argument);

                        if (statusText != null)
                            UpdateStatusText("已在资源管理器中显示临时文件");
                    }
                }
                else
                {
                    MessageBox.Show("当前没有打开的图片文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件位置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddOpenWithApp_Click(object sender, RoutedEventArgs e)
        {
            ShowOpenWithManager();
        }

        private void ShowOpenWithManager()
        {
            var result = MessageBox.Show(
                "打开方式管理\n\n" +
                "当前已配置的应用程序:\n" +
                GetCurrentAppsDisplay() + "\n\n" +
                "选择操作:\n" +
                "是 - 添加新应用\n" +
                "否 - 删除应用\n" +
                "取消 - 退出管理",
                "打开方式管理",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    AddNewOpenWithApp();
                    break;
                case MessageBoxResult.No:
                    RemoveOpenWithApp();
                    break;
                case MessageBoxResult.Cancel:
                    break;
            }
        }

        private string GetCurrentAppsDisplay()
        {
            if (openWithApps.Count == 0)
                return "暂无配置的应用";

            var display = "";
            for (int i = 0; i < openWithApps.Count; i++)
            {
                display += $"{i + 1}. {openWithApps[i].Name}\n";
            }
            return display.TrimEnd('\n');
        }

        private void AddNewOpenWithApp()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择应用程序",
                Filter = "可执行文件|*.exe|所有文件|*.*",
                InitialDirectory = @"C:\Program Files"
            };

            if (dialog.ShowDialog() == true)
            {
                string appName = Path.GetFileNameWithoutExtension(dialog.FileName);
                string displayName = Interaction.InputBox(
                    $"请输入显示名称:\n\n程序路径: {dialog.FileName}",
                    "添加打开方式", appName);

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    // 检查是否已存在
                    bool exists = openWithApps.Any(app =>
                        string.Equals(app.ExecutablePath, dialog.FileName, StringComparison.OrdinalIgnoreCase));

                    if (exists)
                    {
                        MessageBox.Show("该应用程序已经存在！", "添加失败",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 尝试提取图标
                    string iconPath = ExtractIconFromExecutable(dialog.FileName);

                    var newApp = new OpenWithApp
                    {
                        Name = displayName.Trim(),
                        ExecutablePath = dialog.FileName,
                        Arguments = "\"{0}\"",
                        ShowText = true,
                        IconPath = iconPath
                    };

                    openWithApps.Add(newApp);
                    UpdateOpenWithButtons();
                    UpdateOpenWithMenu();

                    if (statusText != null)
                        UpdateStatusText($"已添加打开方式: {displayName}");

                    // 询问是否继续添加
                    var continueResult = MessageBox.Show(
                        $"已成功添加 \"{displayName}\"！\n\n是否继续添加其他应用？",
                        "添加成功",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (continueResult == MessageBoxResult.Yes)
                    {
                        AddNewOpenWithApp();
                    }
                }
            }
        }

        private string ExtractIconFromExecutable(string exePath)
        {
            try
            {
                // 直接从exe提取图标，不再保存临时文件
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                {
                    // 返回exe路径，让后续处理直接从exe提取图标
                    icon.Dispose();
                    return exePath; // 返回exe路径而不是临时文件路径
                }
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    UpdateStatusText($"提取图标失败: {ex.Message}");
            }

            return string.Empty;
        }

        private void RemoveOpenWithApp()
        {
            if (openWithApps.Count == 0)
            {
                MessageBox.Show("当前没有配置的应用程序！", "删除失败",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string appsList = "";
            for (int i = 0; i < openWithApps.Count; i++)
            {
                appsList += $"{i + 1}. {openWithApps[i].Name}\n";
            }

            string input = Interaction.InputBox(
                $"请输入要删除的应用程序编号:\n\n{appsList}",
                "删除打开方式", "");

            if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input.Trim(), out int index))
            {
                index--; // 转换为0基索引
                if (index >= 0 && index < openWithApps.Count)
                {
                    var appToRemove = openWithApps[index];
                    var confirmResult = MessageBox.Show(
                        $"确定要删除 \"{appToRemove.Name}\" 吗？",
                        "确认删除",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        openWithApps.RemoveAt(index);
                        UpdateOpenWithButtons();
                        UpdateOpenWithMenu();

                        if (statusText != null)
                            UpdateStatusText($"已删除打开方式: {appToRemove.Name}");

                        MessageBox.Show($"已成功删除 \"{appToRemove.Name}\"！", "删除成功",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        // 如果还有应用，询问是否继续删除
                        if (openWithApps.Count > 0)
                        {
                            var continueResult = MessageBox.Show(
                                "是否继续删除其他应用？",
                                "继续删除",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (continueResult == MessageBoxResult.Yes)
                            {
                                RemoveOpenWithApp();
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("无效的编号！", "删除失败",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void UpdateOpenWithButtons()
        {
            var buttons = new[] { btnOpenWith1, btnOpenWith2, btnOpenWith3 };

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    if (i < openWithApps.Count)
                    {
                        buttons[i].Content = openWithApps[i].Name;
                        buttons[i].Visibility = Visibility.Visible;
                        buttons[i].ToolTip = $"用 {openWithApps[i].Name} 打开";
                    }
                    else
                    {
                        buttons[i].Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void UpdateOpenWithMenu()
        {
            if (openWithContextMenu == null) return;

            // 清除所有菜单项
            openWithContextMenu.Items.Clear();

            // 添加自定义打开方式到菜单（显示前3个）
            for (int i = 0; i < Math.Min(openWithApps.Count, 3); i++)
            {
                var app = openWithApps[i];
                var menuItem = new MenuItem
                {
                    Header = app.ShowText ? app.Name : "",
                    Tag = i
                };

                // 解析可执行文件路径并提取图标
                string resolvedExecutablePath = ResolveExecutablePath(app.ExecutablePath);

                if (!string.IsNullOrEmpty(resolvedExecutablePath) && File.Exists(resolvedExecutablePath))
                {
                    try
                    {
                        var icon = System.Drawing.Icon.ExtractAssociatedIcon(resolvedExecutablePath);
                        if (icon != null)
                        {
                            // 转换为WPF可用的BitmapImage
                            using (var bitmap = icon.ToBitmap())
                            {
                                using (var memory = new MemoryStream())
                                {
                                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                                    memory.Position = 0;

                                    var bitmapImage = new BitmapImage();
                                    bitmapImage.BeginInit();
                                    bitmapImage.StreamSource = memory;
                                    bitmapImage.DecodePixelWidth = 16;
                                    bitmapImage.DecodePixelHeight = 16;
                                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmapImage.EndInit();
                                    bitmapImage.Freeze();

                                    var image = new System.Windows.Controls.Image
                                    {
                                        Source = bitmapImage,
                                        Width = 16,
                                        Height = 16
                                    };
                                    menuItem.Icon = image;
                                }
                            }
                            icon.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"提取菜单图标失败 (路径: {resolvedExecutablePath}): {ex.Message}");
                    }
                }

                menuItem.Click += (s, e) =>
                {
                    if (s is MenuItem item && item.Tag is int index)
                        OpenWithApp(index);
                };

                openWithContextMenu.Items.Add(menuItem);
            }

            // 如果有更多应用，添加分隔符
            if (openWithApps.Count > 0)
            {
                openWithContextMenu.Items.Add(new Separator());
            }

            // 添加管理菜单项
            var manageMenuItem = new MenuItem
            {
                Header = "管理打开方式(_M)...",
                FontWeight = FontWeights.Bold
            };
            manageMenuItem.Click += ManageOpenWithApps_Click;
            openWithContextMenu.Items.Add(manageMenuItem);

            // 添加"添加打开方式"菜单项
            var addMenuItem = new MenuItem
            {
                Header = "添加打开方式(_A)..."
            };
            addMenuItem.Click += AddOpenWithApp_Click;
            openWithContextMenu.Items.Add(addMenuItem);
        }

        #endregion
    }
}
