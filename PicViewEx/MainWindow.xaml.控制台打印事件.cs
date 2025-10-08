using ImageMagick;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using PicViewEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace PicViewEx
{
    public partial class MainWindow
    {
        /// <summary>
        /// 统一的图片信息打印方法
        /// </summary>
        private void PrintImageInfo(string operation = "")
        {
            try
            {
                // 更新画布大小
                if (imageContainer != null)
                {
                    canvasSize = new Size(imageContainer.ActualWidth, imageContainer.ActualHeight);
                }

                // 更新当前图片位置
                currentImagePosition = imagePosition;

                // 更新原始图片尺寸和显示尺寸
                if (mainImage?.Source is BitmapSource source)
                {
                    originalImageSize = new Size(source.PixelWidth, source.PixelHeight);
                    displayImageSize = new Size(source.PixelWidth * currentZoom, source.PixelHeight * currentZoom);
                }

                // 更新旋转角度
                rotationAngle = GetCurrentRotationAngle();

                // 打印信息
                Console.WriteLine("=== 图片信息 ===");
                if (!string.IsNullOrEmpty(operation))
                {
                    Console.WriteLine($"操作: {operation}");
                }
                Console.WriteLine($"画布大小: {canvasSize.Width:F0} x {canvasSize.Height:F0}");
                Console.WriteLine($"当前位置: ({currentImagePosition.X:F0}, {currentImagePosition.Y:F0})");
                Console.WriteLine($"原始尺寸: {originalImageSize.Width:F0} x {originalImageSize.Height:F0}");
                Console.WriteLine($"显示尺寸: {displayImageSize.Width:F0} x {displayImageSize.Height:F0}");
                Console.WriteLine($"缩放比例: {currentZoom * 100:F1}%");
                Console.WriteLine($"旋转角度: {rotationAngle:F0}°");
                if (!string.IsNullOrEmpty(currentImagePath))
                {
                    Console.WriteLine($"文件路径: {currentImagePath}");
                }
                Console.WriteLine("================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打印图片信息时出错: {ex.Message}");
            }
        }
    }
}
