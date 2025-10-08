using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PicViewEx
{
    public partial class MainWindow
    {
        #region GIF/WebP 播放器相关方法

        private void LoadGifWebpFile(string filePath)
        {
            try
            {
                // 清理之前的播放器
                CleanupGifWebpPlayer();

                // 创建新的播放器实例
                gifWebpPlayer = new GifWebpPlayer();

                // 加载文件
                if (gifWebpPlayer.LoadFile(filePath))
                {
                    isGifWebpMode = true;

                    // 显示GIF/WebP控制面板
                    if (gifWebpExpander != null)
                    {
                        gifWebpExpander.Visibility = Visibility.Visible;
                        gifWebpExpander.IsExpanded = true;
                    }

                    // 显示GIF状态栏
                    if (gifStatusItem != null)
                    {
                        gifStatusItem.Visibility = Visibility.Visible;
                    }

                    // 启用控制按钮
                    if (btnGifPlayPause != null)
                    {
                        btnGifPlayPause.IsEnabled = true;
                        // 先设置为播放状态，稍后会在开始播放后更新
                        btnGifPlayPause.Content = "▶";
                        btnGifPlayPause.ToolTip = "播放";
                    }
                    if (btnGifReset != null) btnGifReset.IsEnabled = true;
                    if (btnGifPrevFrame != null) btnGifPrevFrame.IsEnabled = true;
                    if (btnGifNextFrame != null) btnGifNextFrame.IsEnabled = true;
                    if (btnGifSeek != null) btnGifSeek.IsEnabled = true;

                    // 设置事件处理
                    gifWebpPlayer.FrameUpdated += OnGifWebpFrameUpdated;
                    gifWebpPlayer.StatusUpdated += OnGifWebpStatusUpdated;

                    // 开始播放
                    gifWebpPlayer.Play();

                    // 更新按钮状态以反映自动播放状态
                    if (btnGifPlayPause != null)
                    {
                        btnGifPlayPause.Content = "⏸";
                        btnGifPlayPause.ToolTip = "暂停";
                    }

                    // 更新状态文本
                    if (txtGifInfo != null)
                        txtGifInfo.Text = "播放中...";

                    // 更新状态
                    if (statusText != null)
                        UpdateStatusText($"已加载: {Path.GetFileName(filePath)}");

                    // 更新图片信息状态栏，显示文件大小
                    if (imageInfoText != null)
                    {
                        long fileSize = new FileInfo(filePath).Length;
                        imageInfoText.Text = $"GIF/WebP | {FormatFileSize(fileSize)}";
                    }

                    // 完全重置变换和缩放状态
                    currentTransform = Transform.Identity;
                    currentZoom = 1.0;
                    imagePosition = new Point(0, 0);
                    rotationAngle = 0.0;

                    // 重置图片显示变换，确保新GIF不继承上一个图片的缩放
                    if (mainImage != null)
                    {
                        mainImage.RenderTransform = Transform.Identity;
                        mainImage.Stretch = Stretch.Uniform;
                        mainImage.StretchDirection = StretchDirection.Both;
                    }

                    // 重置FPS计算相关字段
                    _frameCount = 0;
                    _fpsStartTime = DateTime.Now;

                    // 标记需要在第一帧加载后进行位置调整
                    _needsInitialPositioning = true;
                }
                else
                {
                    MessageBox.Show($"无法加载文件: {filePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    if (statusText != null)
                        UpdateStatusText("加载失败");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载GIF/WebP文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                if (statusText != null)
                    UpdateStatusText("加载失败");
            }
        }

        private void CleanupGifWebpPlayer()
        {
            if (gifWebpPlayer != null)
            {
                gifWebpPlayer.FrameUpdated -= OnGifWebpFrameUpdated;
                gifWebpPlayer.StatusUpdated -= OnGifWebpStatusUpdated;
                gifWebpPlayer.Dispose();
                gifWebpPlayer = null;
            }

            isGifWebpMode = false;

            // 隐藏GIF/WebP控制面板
            if (gifWebpExpander != null)
            {
                gifWebpExpander.Visibility = Visibility.Collapsed;
            }

            // 隐藏GIF状态栏
            if (gifStatusItem != null)
            {
                gifStatusItem.Visibility = Visibility.Collapsed;
            }

            // 禁用控制按钮
            if (btnGifPlayPause != null) btnGifPlayPause.IsEnabled = false;
            if (btnGifReset != null) btnGifReset.IsEnabled = false;
            if (btnGifPrevFrame != null) btnGifPrevFrame.IsEnabled = false;
            if (btnGifNextFrame != null) btnGifNextFrame.IsEnabled = false;
            if (btnGifSeek != null) btnGifSeek.IsEnabled = false;
        }

        private void OnGifWebpFrameUpdated(object sender, FrameUpdatedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (mainImage != null && e.Bitmap != null)
                {
                    mainImage.Source = e.Bitmap;

                    // 如果需要初始位置调整，在第一帧加载后执行
                    if (_needsInitialPositioning)
                    {
                        _needsInitialPositioning = false;

                        // 应用与普通图片相同的位置和缩放逻辑
                        ApplyGifInitialPositioning((int)e.Width, (int)e.Height);
                    }
                }

                // 更新状态栏信息
                if (gifFpsText != null)
                {
                    // 使用更平滑的FPS计算方法
                    _frameCount++;
                    var now = DateTime.Now;
                    var elapsed = (now - _fpsStartTime).TotalSeconds;

                    // 每秒更新一次FPS显示
                    if (elapsed >= 1.0)
                    {
                        double fps = _frameCount / elapsed;
                        gifFpsText.Text = $"FPS: {fps:F1}";
                        _frameCount = 0;
                        _fpsStartTime = now;
                    }
                }

                if (gifDelayText != null)
                {
                    gifDelayText.Text = $"延迟: {e.DelayMs}ms";
                }

                if (gifSizeText != null)
                {
                    // 使用GifWebpPlayer获取的实际尺寸
                    gifSizeText.Text = $"大小: {e.Width}x{e.Height}";
                }

                if (gifFrameText != null)
                {
                    string totalFramesText = e.TotalFrames == 0 ? "-" : e.TotalFrames.ToString();
                    gifFrameText.Text = $"帧: {e.CurrentFrame + 1}/{totalFramesText}";
                }

                // 更新工具栏的跳转编辑框和总帧数显示
                if (txtGifFrameIndex != null)
                {
                    txtGifFrameIndex.Text = (e.CurrentFrame + 1).ToString();
                }

                if (txtGifTotalFrames != null)
                {
                    txtGifTotalFrames.Text = e.TotalFrames == 0 ? "-" : e.TotalFrames.ToString();
                }

                // 显示GIF状态栏
                if (gifStatusItem != null)
                {
                    gifStatusItem.Visibility = Visibility.Visible;
                }
            });
        }

        private void OnGifWebpStatusUpdated(object sender, StatusUpdatedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (txtGifInfo != null)
                {
                    txtGifInfo.Text = e.Status;
                }
            });
        }

        // GIF/WebP 控制按钮事件处理
        private void BtnGifPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (gifWebpPlayer != null)
            {
                if (gifWebpPlayer.IsPlaying)
                {
                    // 当前正在播放，切换到暂停
                    gifWebpPlayer.Pause();

                    // 更新按钮状态
                    if (btnGifPlayPause != null)
                    {
                        btnGifPlayPause.Content = "▶";
                        btnGifPlayPause.ToolTip = "播放";
                    }

                    if (txtGifInfo != null)
                        txtGifInfo.Text = "已暂停";
                }
                else
                {
                    // 当前已暂停，切换到播放
                    gifWebpPlayer.Play();

                    // 更新按钮状态
                    if (btnGifPlayPause != null)
                    {
                        btnGifPlayPause.Content = "⏸";
                        btnGifPlayPause.ToolTip = "暂停";
                    }

                    if (txtGifInfo != null)
                        txtGifInfo.Text = "播放中...";
                }
            }
        }

        private void BtnGifReset_Click(object sender, RoutedEventArgs e)
        {
            if (gifWebpPlayer != null && gifWebpPlayer.Handle != 0)
            {
                // 调用ResetToFirstFrame方法重置到第一帧
                gifWebpPlayer.ResetToFirstFrame();

                if (txtGifInfo != null)
                    txtGifInfo.Text = "已重置到第一帧";

                // 更新按钮状态
                if (btnGifPlayPause != null)
                {
                    btnGifPlayPause.Content = "▶";
                    btnGifPlayPause.ToolTip = "播放";
                }
            }
        }

        private void BtnGifPrevFrame_Click(object sender, RoutedEventArgs e)
        {
            if (gifWebpPlayer != null)
            {
                gifWebpPlayer.PreviousFrame();

                // 暂停播放并更新按钮状态
                gifWebpPlayer.Pause();
                if (btnGifPlayPause != null)
                {
                    btnGifPlayPause.Content = "▶";
                    btnGifPlayPause.ToolTip = "播放";
                }

                if (txtGifInfo != null)
                    txtGifInfo.Text = "已暂停";
            }
        }

        private void BtnGifNextFrame_Click(object sender, RoutedEventArgs e)
        {
            if (gifWebpPlayer != null)
            {
                gifWebpPlayer.NextFrame();

                // 暂停播放并更新按钮状态
                gifWebpPlayer.Pause();
                if (btnGifPlayPause != null)
                {
                    btnGifPlayPause.Content = "▶";
                    btnGifPlayPause.ToolTip = "播放";
                }

                if (txtGifInfo != null)
                    txtGifInfo.Text = "已暂停";
            }
        }

        private void BtnGifSeek_Click(object sender, RoutedEventArgs e)
        {
            if (gifWebpPlayer != null && txtGifFrameIndex != null && gifWebpPlayer.Handle != 0)
            {
                if (uint.TryParse(txtGifFrameIndex.Text, out uint frameNumber))
                {
                    // 用户输入的是帧号（从1开始），需要转换为索引（从0开始）
                    if (frameNumber < 1 || frameNumber > gifWebpPlayer.TotalFrames)
                    {
                        MessageBox.Show($"帧号超出范围！有效范围: 1-{gifWebpPlayer.TotalFrames}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    uint frameIndex = frameNumber - 1; // 转换为从0开始的索引

                    // 暂停播放并跳转到指定帧
                    gifWebpPlayer.Pause();
                    gifWebpPlayer.SeekToFrame(frameIndex);

                    // 更新按钮状态
                    if (btnGifPlayPause != null)
                    {
                        btnGifPlayPause.Content = "▶";
                        btnGifPlayPause.ToolTip = "播放";
                    }

                    if (txtGifInfo != null)
                        txtGifInfo.Text = $"已跳转到第 {frameNumber} 帧";
                }
                else
                {
                    MessageBox.Show("请输入有效的帧号数字！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        #endregion

        #region GIF/WebP 初始位置调整

        /// <summary>
        /// 为GIF/WebP应用初始位置调整逻辑，与普通图片相同
        /// </summary>
        private void ApplyGifInitialPositioning(int imageWidth, int imageHeight)
        {
            if (imageContainer == null) return;

            var containerWidth = imageContainer.ActualWidth;
            var containerHeight = imageContainer.ActualHeight;

            if (containerWidth <= 0 || containerHeight <= 0)
            {
                // 如果容器尺寸还未确定，异步重试
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyGifInitialPositioning(imageWidth, imageHeight);
                }), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            // 计算有效显示区域宽度
            double effectiveWidth = containerWidth;
            if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
            {
                effectiveWidth = Math.Max(100, containerWidth - 305); // 确保至少有100像素显示区域
            }

            // 如果图片尺寸超过容器的80%，自动适应窗口
            if (imageWidth > effectiveWidth * 0.8 || imageHeight > containerHeight * 0.8)
            {
                FitToWindow();
                PrintImageInfo("GIF加载 - 自动适应窗口");
                if (statusText != null)
                {
                    string currentText = statusText.Text;
                    // 提取当前消息部分（去掉引擎信息）
                    string message = currentText.Contains(" | 引擎:") ? 
                        currentText.Substring(0, currentText.IndexOf(" | 引擎:")) : currentText;
                    UpdateStatusText(message + " (已自动适应窗口)");
                }
            }
            else
            {
                // 否则居中显示
                CenterImage();
                PrintImageInfo("GIF加载 - 居中显示");
            }
        }

        #endregion
    }
}
