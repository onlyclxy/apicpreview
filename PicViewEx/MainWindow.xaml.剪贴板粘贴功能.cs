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
        #region 剪贴板图片粘贴功能

        /// <summary>
        /// 从剪贴板粘贴图片
        /// </summary>
        private void PasteImageFromClipboard()
        {
            try
            {
                // 检查剪贴板是否包含图像数据
                if (!Clipboard.ContainsImage() && !Clipboard.ContainsFileDropList())
                {
                    if (statusText != null)
                        UpdateStatusText("剪贴板中没有检测到图像数据");
                    return;
                }

                BitmapSource clipboardImage = null;
                string sourceInfo = "";

                // 优先尝试获取图像数据
                if (Clipboard.ContainsImage())
                {
                    clipboardImage = Clipboard.GetImage();
                    sourceInfo = "剪贴板图像";
                }
                // 如果没有直接的图像，尝试从文件列表获取
                else if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    foreach (string file in files)
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        if (supportedFormats.Contains(extension))
                        {
                            // 找到第一个支持的图片文件
                            clipboardImage = imageLoader.LoadImage(file);
                            sourceInfo = $"剪贴板文件: {Path.GetFileName(file)}";
                            break;
                        }
                    }
                }

                if (clipboardImage == null)
                {
                    if (statusText != null)
                        UpdateStatusText("无法从剪贴板获取有效的图像数据");
                    return;
                }

                // 显示确认对话框
                var result = ShowPasteConfirmDialog(sourceInfo, clipboardImage);

                if (result == MessageBoxResult.Yes)
                {
                    LoadImageFromClipboard(clipboardImage, sourceInfo);
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

        /// <summary>
        /// 显示粘贴确认对话框
        /// </summary>
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

        /// <summary>
        /// 从剪贴板加载图像到查看器
        /// </summary>
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

                // 清除可能的GIF动画


                // 设置图片源
                mainImage.Source = clipboardImage;

                // 重置变换和缩放
                currentTransform = Transform.Identity;
                currentZoom = 1.0;
                imagePosition = new Point(0, 0);
                rotationAngle = 0.0; // 重置旋转角度

                // 检查图片尺寸是否超过窗口尺寸，决定是否自动适应
                if (imageContainer != null)
                {
                    double containerWidth = imageContainer.ActualWidth;
                    double containerHeight = imageContainer.ActualHeight;

                    // 计算有效显示区域宽度
                    double effectiveWidth = containerWidth;

                    // 只有当通道面板真正显示时才减去其宽度
                    if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
                    {
                        effectiveWidth = Math.Max(100, containerWidth - 305); // 确保至少有100像素显示区域
                    }

                    // 如果图片尺寸超过容器的80%，自动适应窗口
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
                        // 否则居中显示
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

                // 如果显示通道面板，尝试生成通道（但可能会失败，因为没有文件路径）
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

        /// <summary>
        /// 更新剪贴板图片的信息显示
        /// </summary>
        private void UpdateImageInfoForClipboard(BitmapSource image, string sourceInfo)
        {
            if (imageInfoText != null)
            {
                // 由于是剪贴板图片，无法获取文件大小，只显示尺寸和来源
                imageInfoText.Text = $"{image.PixelWidth} × {image.PixelHeight} | {sourceInfo}";
            }
        }

        /// <summary>
        /// 为剪贴板图片加载通道信息
        /// </summary>
        private void LoadClipboardImageChannels(BitmapSource image)
        {
            try
            {
                if (channelStackPanel == null) return;
                channelStackPanel.Children.Clear();

                // 清除之前的缓存，因为这是新的剪贴板图片
                channelCache.Clear();
                currentChannelCachePath = null;

                if (statusText != null)
                    UpdateStatusText("正在为剪贴板图片生成通道...");
                var channels = imageLoader.LoadChannels(image);

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

        /// <summary>
        /// 为剪贴板图片创建临时文件（用于打开方式功能）
        /// </summary>
        private string CreateTemporaryImageFile()
        {
            try
            {
                if (mainImage?.Source == null)
                    throw new InvalidOperationException("没有可用的图片");

                var source = mainImage.Source as BitmapSource;
                if (source == null)
                    throw new InvalidOperationException("图片格式不支持");

                // 清理旧的临时文件
                CleanupTemporaryFile();

                // 创建临时文件路径
                string tempDir = Path.GetTempPath();
                string guidPart = Guid.NewGuid().ToString("N").Substring(0, 8);                // 取前8位
                string tempFileName = $"PicViewEx_Temp_{DateTime.Now:yyyyMMdd_HHmmss}_{guidPart}.png";
                temporaryImagePath = Path.Combine(tempDir, tempFileName);

                // 保存图片到临时文件
                SaveBitmapSource(source, temporaryImagePath);

                if (statusText != null)
                    UpdateStatusText($"已创建临时文件用于打开方式: {Path.GetFileName(temporaryImagePath)}");

                return temporaryImagePath;
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    UpdateStatusText($"创建临时文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 清理临时文件
        /// </summary>
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
                    // 临时文件清理失败不应该影响主要功能
                    System.Diagnostics.Debug.WriteLine($"清理临时文件失败: {ex.Message}");
                }
            }
            temporaryImagePath = null;
        }

        /// <summary>
        /// 获取当前图片的有效路径（包括临时文件）
        /// </summary>
        private string GetCurrentImagePath()
        {
            // 如果有原始文件路径，直接返回
            if (!string.IsNullOrEmpty(currentImagePath) && File.Exists(currentImagePath))
            {
                return currentImagePath;
            }

            // 如果是剪贴板图片，创建临时文件
            if (mainImage?.Source != null)
            {
                return CreateTemporaryImageFile();
            }

            // 没有可用的图片文件时，返回空字符串而不是抛出异常
            return string.Empty;
        }

        #endregion

    }
}
