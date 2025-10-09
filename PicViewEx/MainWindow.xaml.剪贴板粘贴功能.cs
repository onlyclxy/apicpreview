using ImageMagick;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PicViewEx
{
    public partial class MainWindow
    {
        #region 剪贴板图片粘贴功能（仅使用 C++ DLL）

        // ========= C++ DLL ===============
        private static class ClipboardBridgeNative
        {
            [DllImport("ClipboardBridge.dll", CallingConvention = CallingConvention.StdCall)]
            public static extern int GetClipboardImageBGRA32(
                out IntPtr data, out int width, out int height, out int stride);

            [DllImport("ClipboardBridge.dll", CallingConvention = CallingConvention.StdCall)]
            public static extern void FreeBuffer(IntPtr data);
        }

        /// <summary>
        /// 通过 C++ DLL 获取剪贴板图像（BGRA32）并转为 BitmapSource
        /// </summary>
        private BitmapSource GetClipboardImageNative()
        {
            // 必须在 UI(STA) 线程调用
            IntPtr ptr;
            int w, h, stride;
            int ok = ClipboardBridgeNative.GetClipboardImageBGRA32(out ptr, out w, out h, out stride);
            if (ok == 0 || ptr == IntPtr.Zero) return null;

            try
            {
                int total = stride * h;
                byte[] bytes = new byte[total];
                Marshal.Copy(ptr, bytes, 0, total);

                var bs = BitmapSource.Create(
                    w, h, 96, 96, PixelFormats.Bgra32, null, bytes, stride);
                bs.Freeze();
                return bs;
            }
            finally
            {
                ClipboardBridgeNative.FreeBuffer(ptr);
            }
        }

        /// <summary>
        /// 为防止 WPF 的隐式格式/插值导致发糊，转为 Pbgra32 并设置清晰显示
        /// </summary>
        private BitmapSource ToPbgra32(BitmapSource src)
        {
            if (src == null) return null;
            if (src.Format == PixelFormats.Pbgra32) return src;
            var conv = new FormatConvertedBitmap(src, PixelFormats.Pbgra32, null, 0);
            conv.Freeze();
            return conv;
        }

        private void SetCrispRendering()
        {
            try
            {
                // 1:1 显示，不拉伸；使用最近邻缩放；对齐设备像素
                if (mainImage != null)
                {
                    mainImage.Stretch = Stretch.None;
                    mainImage.SnapsToDevicePixels = true;
                    RenderOptions.SetBitmapScalingMode(mainImage, BitmapScalingMode.NearestNeighbor);
                    RenderOptions.SetEdgeMode(mainImage, EdgeMode.Aliased);
                }
                if (imageContainer is FrameworkElement fe)
                {
                    fe.UseLayoutRounding = true;
                    fe.SnapsToDevicePixels = true;
                    RenderOptions.SetBitmapScalingMode(fe, BitmapScalingMode.NearestNeighbor);
                }
                this.UseLayoutRounding = true;
                this.SnapsToDevicePixels = true;
                RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            }
            catch { /* 安全兜底 */ }
        }

        /// <summary>
        /// 从剪贴板粘贴图片（仅走 C++ DLL）
        /// </summary>
        private void PasteImageFromClipboard()
        {
            try
            {
                // 通过原生 DLL 拿 BGRA32
                var native = GetClipboardImageNative();
                if (native == null)
                {
                    if (statusText != null)
                        UpdateStatusText("剪贴板中没有检测到图像数据");
                    return;
                }

                // 转为 Pbgra32（WPF 首选）并设置清晰显示
                var clipboardImage = ToPbgra32(native);
                SetCrispRendering();

                // 确认框
                var result = ShowPasteConfirmDialog("剪贴板图像 (Native BGRA32)", clipboardImage);
                if (result == MessageBoxResult.Yes)
                {
                    LoadImageFromClipboard(clipboardImage, "剪贴板图像 (Native BGRA32)");
                    RecordToolUsage("PasteFromClipboard");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴图片失败: {ex.Message}", "粘贴错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (statusText != null)
                    UpdateStatusText($"粘贴失败: {ex.Message}");
            }
        }

        #endregion

        // ====== 以下保留你原有的其它方法（未更动）======

        /// <summary>显示粘贴确认对话框</summary>
        private MessageBoxResult ShowPasteConfirmDialog(string sourceInfo, BitmapSource image)
        {
            string message = $"检测到图像数据！\n\n" +
                           $"来源: {sourceInfo}\n" +
                           $"尺寸: {image.PixelWidth} × {image.PixelHeight}\n" +
                           $"格式: {image.Format}\n\n" +
                           $"是否将当前图片更新为粘贴的图像？\n\n" +
                           $"注意: 这将替换当前显示的图片";

            return MessageBox.Show(message, "发现剪贴板图像",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        }

        /// <summary>从剪贴板加载图像到查看器（保持你的原逻辑）</summary>
        private void LoadImageFromClipboard(BitmapSource clipboardImage, string sourceInfo)
        {
            try
            {
                // 如果当前有序列帧在播放，停止并重置
                if (hasSequenceLoaded)
                {
                    if (isSequencePlaying)
                    {
                        PauseSequence();
                    }

                    hasSequenceLoaded = false;
                    sequenceFrames.Clear();
                    currentFrameIndex = 0;
                    originalImage = null;

                    EnableSequenceControls(false);
                    UpdateFrameDisplay();
                }

                // 清除之前的图片路径信息，因为这是从剪贴板来的
                currentImagePath = "";
                currentImageList.Clear();
                currentImageIndex = -1;

                // 设置图片源
                mainImage.Source = clipboardImage;

                // 重置变换和缩放
                currentTransform = Transform.Identity;
                currentZoom = 1.0;
                imagePosition = new Point(0, 0);
                rotationAngle = 0.0;

                // 检查图片尺寸是否超过窗口尺寸，决定是否自动适应
                if (imageContainer != null)
                {
                    double containerWidth = imageContainer.ActualWidth;
                    double containerHeight = imageContainer.ActualHeight;

                    double effectiveWidth = containerWidth;

                    if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
                    {
                        effectiveWidth = Math.Max(100, containerWidth - 305);
                    }

                    if (clipboardImage.PixelWidth > effectiveWidth * 0.8 ||
                        clipboardImage.PixelHeight > containerHeight * 0.8)
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            FitToWindow();
                            PrintImageInfo("剪贴板图片加载 - 自动适应窗口");
                            if (statusText != null)
                                UpdateStatusText($"已粘贴并自动适应窗口: {sourceInfo}");
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                    else
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            CenterImage();
                            PrintImageInfo("剪贴板图片加载 - 居中显示");
                            if (statusText != null)
                                UpdateStatusText($"已粘贴: {sourceInfo}");
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }

                // 更新图片信息显示
                UpdateImageInfoForClipboard(clipboardImage, sourceInfo);

                // 如果显示通道面板，尝试生成通道
                if (showChannels)
                {
                    LoadClipboardImageChannels(clipboardImage);
                }

                if (statusText != null && !showChannels)
                    UpdateStatusText($"已从剪贴板粘贴: {sourceInfo}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载剪贴板图片失败: {ex.Message}", "加载错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (statusText != null)
                    UpdateStatusText("剪贴板图片加载失败");
            }
        }

        private void UpdateImageInfoForClipboard(BitmapSource image, string sourceInfo)
        {
            if (imageInfoText != null)
            {
                imageInfoText.Text = $"{image.PixelWidth} × {image.PixelHeight} | {sourceInfo}";
            }
        }

        private void LoadClipboardImageChannels(BitmapSource image)
        {
            try
            {
                if (channelStackPanel == null) return;
                channelStackPanel.Children.Clear();

                channelCache.Clear();
                currentChannelCachePath = null;

                if (statusText != null)
                    UpdateStatusText("正在为剪贴板图片生成通道...");

                var loadedChannels = imageLoader.LoadChannels(image);

                foreach (var (name, channelImage) in loadedChannels)
                {
                    channelCache.Add(Tuple.Create(name, channelImage));
                    CreateChannelControl(name, channelImage);
                }

                if (statusText != null)
                    UpdateStatusText($"剪贴板图片通道加载完成 ({channelStackPanel.Children.Count}个)");
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    UpdateStatusText($"剪贴板图片通道生成失败: {ex.Message}");
            }
        }

        private string CreateTemporaryImageFile()
        {
            try
            {
                if (mainImage?.Source == null)
                    throw new InvalidOperationException("没有可用的图片");

                var source = mainImage.Source as BitmapSource;
                if (source == null)
                    throw new InvalidOperationException("图片格式不支持");

                CleanupTemporaryFile();

                string tempDir = Path.GetTempPath();
                string guidPart = Guid.NewGuid().ToString("N").Substring(0, 8);
                string tempFileName = $"PicViewEx_Temp_{DateTime.Now:yyyyMMdd_HHmmss}_{guidPart}.png";
                temporaryImagePath = Path.Combine(tempDir, tempFileName);

                SaveBitmapSource(source, temporaryImagePath);

                if (statusText != null)
                    UpdateStatusText($"已创建临时文件用于打开方式: {System.IO.Path.GetFileName(temporaryImagePath)}");

                return temporaryImagePath;
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    UpdateStatusText($"创建临时文件失败: {ex.Message}");
                throw;
            }
        }

        private void CleanupTemporaryFile()
        {
            if (!string.IsNullOrEmpty(temporaryImagePath) && File.Exists(temporaryImagePath))
            {
                try
                {
                    File.Delete(temporaryImagePath);
                    if (statusText != null)
                        UpdateStatusText("已清理临时文件");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"清理临时文件失败: {ex.Message}");
                }
            }
            temporaryImagePath = null;
        }

        private string GetCurrentImagePath()
        {
            if (!string.IsNullOrEmpty(currentImagePath) && File.Exists(currentImagePath))
            {
                return currentImagePath;
            }

            if (mainImage?.Source != null)
            {
                return CreateTemporaryImageFile();
            }

            return string.Empty;
        }

        // 仍保留：GDI Bitmap -> BitmapSource 的过渡（别的方法可能会用到）
        private BitmapSource TryDecodeObjectAsEncodedImage(object obj)
        {
            try
            {
                var bmp = obj as System.Drawing.Bitmap;
                if (bmp == null) return null;
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    return dec.Frames.FirstOrDefault();
                }
            }
            catch { return null; }
        }

    }
}
