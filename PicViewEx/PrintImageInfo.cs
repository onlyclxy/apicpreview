using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PicViewEx
{
    public partial class MainWindow
    {
        private void PrintImageInfo(string action)
        {
            if (mainImage?.Source == null || imageContainer == null) return;

            var source = mainImage.Source as BitmapSource;
            if (source == null) return;

            var containerWidth = imageContainer.ActualWidth;
            var containerHeight = imageContainer.ActualHeight;

            if (containerWidth <= 0 || containerHeight <= 0) return;

            Console.WriteLine($"=== {action}完成后的图片信息 ===");
            Console.WriteLine($"容器尺寸: {containerWidth} x {containerHeight}");

            // 计算有效显示区域宽度
            double effectiveWidth = containerWidth;
            
            if (showChannels && channelPanel != null && channelPanel.Visibility == System.Windows.Visibility.Visible)
            {
                effectiveWidth = Math.Max(100, containerWidth - 305);
                Console.WriteLine($"通道面板可见，有效宽度: {effectiveWidth}");
            }
            else
            {
                Console.WriteLine($"通道面板不可见，有效宽度: {effectiveWidth}");
            }

            // 计算旋转后的实际边界框尺寸
            var (actualWidth, actualHeight) = GetRotatedImageBounds(source.PixelWidth, source.PixelHeight);
            var scaledWidth = actualWidth * currentZoom;
            var scaledHeight = actualHeight * currentZoom;
            
            Console.WriteLine($"原始图片尺寸: {source.PixelWidth} x {source.PixelHeight}");
            Console.WriteLine($"旋转角度: {GetCurrentRotationAngle()}°");
            Console.WriteLine($"旋转后边界框: {actualWidth} x {actualHeight}");
            Console.WriteLine($"当前缩放: {currentZoom:F2}");
            Console.WriteLine($"缩放后图片尺寸: {scaledWidth} x {scaledHeight}");
            Console.WriteLine($"当前图片位置: ({imagePosition.X:F1}, {imagePosition.Y:F1})");
            Console.WriteLine($"=== {action}信息结束 ===");
        }
    }
}