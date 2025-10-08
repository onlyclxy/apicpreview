using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PicViewEx
{

    public partial class MainWindow
    {
       
        #region 工具栏按钮事件处理程序

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("ZoomIn");
            ZoomImage(1.2);
            PrintImageInfo("放大");
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("ZoomOut");
            ZoomImage(0.8);
            PrintImageInfo("缩小");
        }

        private void BtnFitWindow_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("FitWindow");
            FitToWindow();
            PrintImageInfo("适应窗口");
        }

        private void BtnActualSize_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("ActualSize");
            SetActualSize();
            PrintImageInfo("实际大小");
        }

        private void BtnCenterImage_Click(object sender, RoutedEventArgs e)
        {
            RecordToolUsage("CenterImage");
            CenterImage();
            PrintImageInfo("居中显示");
        }

        private void BtnOpenWith1_Click(object sender, RoutedEventArgs e)
        {
            OpenWithApp(0);
        }

        private void BtnOpenWith2_Click(object sender, RoutedEventArgs e)
        {
            OpenWithApp(1);
        }

        private void BtnOpenWith3_Click(object sender, RoutedEventArgs e)
        {
            OpenWithApp(2);
        }

        private void BtnOpenWithMenu_Click(object sender, RoutedEventArgs e)
        {
            if (btnOpenWithMenu?.ContextMenu != null)
            {
                btnOpenWithMenu.ContextMenu.IsOpen = true;
            }
        }

        private void BtnOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            OpenFileLocation();
        }

        #endregion

        /// <summary>
        /// 同步工具菜单状态 - 确保菜单勾选状态与实际工具栏显示状态一致
        /// </summary>
        private void SynchronizeToolMenuStates()
        {
            try
            {
                Console.WriteLine("--- 开始同步工具菜单状态 ---");

                // 同步背景工具栏菜单状态
                if (menuShowBgToolbar != null && backgroundExpander != null)
                {
                    // 菜单勾选状态应该反映工具栏的实际可见性
                    // 对于Expander，应该基于Visibility而不是IsExpanded
                    bool isVisible = backgroundExpander.Visibility == Visibility.Visible;
                    bool wasChecked = menuShowBgToolbar.IsChecked == true;

                    Console.WriteLine($"背景工具栏 - 实际可见: {isVisible}, 菜单勾选: {wasChecked}");

                    // 如果设置中的状态与实际状态不一致，以实际状态为准
                    if (menuShowBgToolbar.IsChecked != isVisible)
                    {
                        menuShowBgToolbar.IsChecked = isVisible;
                        Console.WriteLine($"背景工具栏菜单状态已修正: {wasChecked} -> {isVisible}");
                    }
                }

                // 同步序列帧工具栏菜单状态
                if (menuShowSequenceToolbar != null && sequenceExpander != null)
                {
                    // 菜单勾选状态应该反映工具栏的实际可见性
                    bool isVisible = sequenceExpander.Visibility == Visibility.Visible;
                    bool wasChecked = menuShowSequenceToolbar.IsChecked == true;

                    Console.WriteLine($"序列帧工具栏 - 实际可见: {isVisible}, 菜单勾选: {wasChecked}");

                    // 如果设置中的状态与实际状态不一致，以实际状态为准
                    if (menuShowSequenceToolbar.IsChecked != isVisible)
                    {
                        menuShowSequenceToolbar.IsChecked = isVisible;
                        Console.WriteLine($"序列帧工具栏菜单状态已修正: {wasChecked} -> {isVisible}");
                    }
                }

                Console.WriteLine("--- 结束同步工具菜单状态 ---");

                if (statusText != null)
                {
                    string bgStatus = menuShowBgToolbar?.IsChecked == true ? "显示" : "隐藏";
                    string seqStatus = menuShowSequenceToolbar?.IsChecked == true ? "显示" : "隐藏";
                    UpdateStatusText($"工具菜单状态已同步 - 背景工具栏: {bgStatus}, 序列帧工具栏: {seqStatus}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"同步工具菜单状态失败: {ex.Message}");
                if (statusText != null)
                    UpdateStatusText($"同步工具菜单状态失败: {ex.Message}");
            }
        }
    }
}
