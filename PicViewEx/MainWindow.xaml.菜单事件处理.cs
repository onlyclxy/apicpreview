using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PicViewEx
{
    public  partial class MainWindow
    {
        #region 菜单事件处理

        private void MenuOpen_Click(object sender, RoutedEventArgs e)
        {
            BtnOpen_Click(sender, e);
        }

        private void MenuSaveAs_Click(object sender, RoutedEventArgs e)
        {
            BtnSaveAs_Click(sender, e);
        }

        private void MenuPasteImage_Click(object sender, RoutedEventArgs e)
        {
            PasteImageFromClipboard();
        }

        private void MenuOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            BtnOpenLocation_Click(sender, e);
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MenuFitWindow_Click(object sender, RoutedEventArgs e)
        {
            BtnFitWindow_Click(sender, e);
        }

        private void MenuActualSize_Click(object sender, RoutedEventArgs e)
        {
            BtnActualSize_Click(sender, e);
        }

        private void MenuCenterImage_Click(object sender, RoutedEventArgs e)
        {
            BtnCenterImage_Click(sender, e);
        }

        private void MenuFullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized && this.WindowStyle == WindowStyle.None)
            {
                // 退出全屏
                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.None; // 保持无边框
            }
            else
            {
                // 进入全屏
                this.WindowState = WindowState.Maximized;
                this.WindowStyle = WindowStyle.None;
            }
        }

        private void MenuShowChannels_Click(object sender, RoutedEventArgs e)
        {
            if (menuShowChannels == null) return;

            // 根据当前显示状态决定操作，而不是菜单的IsChecked状态
            // 因为IsCheckable菜单项的IsChecked会在Click事件触发前自动切换
            bool currentlyShowing = showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible;
            bool shouldShow = !currentlyShowing; // 切换状态

            // 同步菜单状态
            menuShowChannels.IsChecked = shouldShow;

            // 同步到复选框(不会触发事件,因为我们手动执行逻辑)
            if (chkShowChannels != null)
            {
                // 临时移除事件处理器
                chkShowChannels.Checked -= ChkShowChannels_Checked;
                chkShowChannels.Unchecked -= ChkShowChannels_Unchecked;

                chkShowChannels.IsChecked = shouldShow;

                // 恢复事件处理器
                chkShowChannels.Checked += ChkShowChannels_Checked;
                chkShowChannels.Unchecked += ChkShowChannels_Unchecked;
            }

            // 执行显示/隐藏逻辑
            if (shouldShow)
            {
                RecordToolUsage("ShowChannels");
                showChannels = true;
                if (channelPanel != null && channelSplitter != null && channelColumn != null)
                {
                    channelPanel.Visibility = Visibility.Visible;
                    channelSplitter.Visibility = Visibility.Visible;

                    // 确保主图列恢复为星号宽度(自动填充)
                    if (mainImageColumn != null)
                    {
                        mainImageColumn.Width = new GridLength(1, GridUnitType.Star);
                    }

                    // 设置通道列为300像素宽度
                    channelColumn.Width = new GridLength(300);
                }

                if (!string.IsNullOrEmpty(currentImagePath))
                {
                    LoadImageChannels(currentImagePath);
                }
            }
            else
            {
                RecordToolUsage("HideChannels");
                showChannels = false;
                if (channelPanel != null && channelSplitter != null && channelColumn != null && channelStackPanel != null)
                {
                    channelPanel.Visibility = Visibility.Collapsed;
                    channelSplitter.Visibility = Visibility.Collapsed;

                    // 设置通道列宽度为0
                    channelColumn.Width = new GridLength(0);

                    // 确保主图列恢复为星号宽度(占据全部空间)
                    if (mainImageColumn != null)
                    {
                        mainImageColumn.Width = new GridLength(1, GridUnitType.Star);
                    }

                    channelStackPanel.Children.Clear();
                }
            }
        }

        private void MenuPrevious_Click(object sender, RoutedEventArgs e)
        {
            BtnPrevious_Click(sender, e);
        }

        private void MenuNext_Click(object sender, RoutedEventArgs e)
        {
            BtnNext_Click(sender, e);
        }

        private void MenuRotateLeft_Click(object sender, RoutedEventArgs e)
        {
            BtnRotateLeft_Click(sender, e);
        }

        private void MenuRotateRight_Click(object sender, RoutedEventArgs e)
        {
            BtnRotateRight_Click(sender, e);
        }

        private void MenuZoomIn_Click(object sender, RoutedEventArgs e)
        {
            BtnZoomIn_Click(sender, e);
        }

        private void MenuZoomOut_Click(object sender, RoutedEventArgs e)
        {
            BtnZoomOut_Click(sender, e);
        }

        private void MenuBgTransparent_Click(object sender, RoutedEventArgs e)
        {
            if (rbTransparent != null)
                rbTransparent.IsChecked = true;
            UpdateMenuBackgroundStates();
        }

        private void MenuBgSolid_Click(object sender, RoutedEventArgs e)
        {
            if (rbSolidColor != null)
                rbSolidColor.IsChecked = true;
            UpdateMenuBackgroundStates();
        }

        private void MenuBgImage_Click(object sender, RoutedEventArgs e)
        {
            if (rbImageBackground != null)
                rbImageBackground.IsChecked = true;
            UpdateMenuBackgroundStates();
        }

        private void MenuBgWindowTransparent_Click(object sender, RoutedEventArgs e)
        {
            if (rbWindowTransparent != null)
                rbWindowTransparent.IsChecked = true;
            UpdateMenuBackgroundStates();
        }

        private void MenuSelectBgImage_Click(object sender, RoutedEventArgs e)
        {
            BtnSelectBackgroundImage_Click(sender, e);
        }

        private void MenuSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveAppSettings();
            if (statusText != null)
                statusText.Text = "设置已手动保存";
        }

        private void MenuResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要还原到默认设置吗？这将清除所有自定义设置，并需要重启应用才能完全生效。", "还原设置",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.ResetToDefault();
                MessageBox.Show("设置已重置。请重新启动 PicViewEx。", "操作完成", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close(); // Close the application to apply changes on restart
            }
        }

        private void MenuExpandBgPanel_Click(object sender, RoutedEventArgs e)
        {
            var bgExpander = this.FindName("backgroundExpander") as Expander;
            if (bgExpander != null)
            {
                // bgExpander.IsExpanded = menuExpandBgPanel?.IsChecked ?? true;
                // SaveAppSettings();
            }
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            string controlTest = SettingsManager.TestControlSaving(this);

            MessageBox.Show("PicViewEx - Advanced Image Viewer\n\n" +
                "版本: 1.0.1\n" +
                "一个功能强大的图片查看器\n\n" +
                "功能特色:\n" +
                "• 支持多种图片格式\n" +
                "• GIF动画播放\n" +
                "• RGB/Alpha通道显示\n" +
                "• 自定义背景设置\n" +
                "• 序列帧播放器\n" +
                "• 剪贴板图片粘贴 (NEW!)\n" +
                "• 打开方式管理\n" +
                "• 命令行参数支持\n" +
                "• 设置自动保存\n\n" +
                "序列帧功能:\n" +
                "支持将网格状图片（如3×3，6×6）解析为动画序列\n" +
                "可控制播放速度，手动逐帧查看，导出为GIF\n\n" +
                "剪贴板功能 (NEW!):\n" +
                "• 支持从网页复制图片 (Ctrl+V)\n" +
                "• 支持剪贴板图片的另存为功能\n" +
                "• 支持剪贴板图片的打开方式（自动创建临时文件）\n" +
                "• 自动清理临时文件\n\n" +
                "控件状态测试:\n" + controlTest,
                "关于PicViewEx", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuKeyboardHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("快捷键帮助:\n\n" +
                "文件操作:\n" +
                "Ctrl+O - 打开文件\n" +
                "Ctrl+S - 另存为\n" +
                "Ctrl+V - 粘贴图片 (NEW!)\n" +
                "图片浏览:\n" +
                "Left/Right - 上一张/下一张\n" +
                "F - 适应窗口\n" +
                "1 - 实际大小\n" +
                "Space - 居中显示\n" +
                "F11 - 全屏\n\n" +
                "图片操作:\n" +
                "Ctrl+L - 左旋转\n" +
                "Ctrl+R - 右旋转\n" +
                "Ctrl++ - 放大\n" +
                "Ctrl+- - 缩小\n\n" +
                "序列帧播放:\n" +
                "Ctrl+P - 播放/暂停序列\n" +
                ", (逗号) - 上一帧\n" +
                ". (句号) - 下一帧\n\n" +
                "粘贴功能说明:\n" +
                "• 支持从网页复制的图片\n" +
                "• 支持从其他应用复制的图片\n" +
                "• 支持从文件管理器复制的图片文件\n" +
                "• 粘贴前会显示确认对话框\n\n" +
                "设置:\n" +
                "Ctrl+Shift+S - 保存设置\n" +
                "Alt+F4 - 退出",
                "快捷键帮助", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateMenuBackgroundStates()
        {
            if (menuBgTransparent != null)
                menuBgTransparent.IsChecked = rbTransparent?.IsChecked ?? false;
            if (menuBgSolid != null)
                menuBgSolid.IsChecked = rbSolidColor?.IsChecked ?? false;
            if (menuBgImage != null)
                menuBgImage.IsChecked = rbImageBackground?.IsChecked ?? false;
            if (menuBgWindowTransparent != null)
                menuBgWindowTransparent.IsChecked = rbWindowTransparent?.IsChecked ?? false;
        }

        #endregion

        // 添加工具使用记录方法
        private void RecordToolUsage(string toolName)
        {
            if (appSettings != null && !string.IsNullOrEmpty(toolName))
            {
                SettingsManager.AddRecentTool(appSettings, toolName);

                // 如果启用自动保存，立即保存设置
                if (appSettings.AutoSaveSettings)
                {
                    SaveAppSettings();
                }
            }
        }
    }
}
