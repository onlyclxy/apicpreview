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
    }
}
