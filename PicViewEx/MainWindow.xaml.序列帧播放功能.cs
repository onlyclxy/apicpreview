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
using WpfAnimatedGif;

namespace PicViewEx
{
    public partial class MainWindow
    {
        #region 序列帧播放功能

        // 序列帧播放相关变量
        private List<BitmapSource> sequenceFrames = new List<BitmapSource>();
        private int currentFrameIndex = 0;
        private System.Windows.Threading.DispatcherTimer sequenceTimer;
        private bool isSequencePlaying = false;
        private int gridWidth = 3;
        private int gridHeight = 3;
        private bool hasSequenceLoaded = false;
        private BitmapSource originalImage;

        private void InitializeSequencePlayer()
        {
            // 初始化序列帧播放定时器
            sequenceTimer = new System.Windows.Threading.DispatcherTimer();
            sequenceTimer.Tick += SequenceTimer_Tick;
            UpdateSequenceTimerInterval();
        }

        private void UpdateSequenceTimerInterval()
        {
            if (sequenceTimer != null && txtFPS != null)
            {
                try
                {
                    int fps = int.Parse(txtFPS.Text);
                    if (fps > 0 && fps <= 120)
                    {
                        sequenceTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
                    }
                    else
                    {
                        txtFPS.Text = "10";
                        sequenceTimer.Interval = TimeSpan.FromMilliseconds(100); // 10 FPS
                    }
                }
                catch
                {
                    txtFPS.Text = "10";
                    sequenceTimer.Interval = TimeSpan.FromMilliseconds(100); // 10 FPS
                }
            }
        }

        // 解析网格按钮事件
        private void BtnParseGrid_Click(object sender, RoutedEventArgs e)
        {
            if (mainImage?.Source == null)
            {
                MessageBox.Show("请先打开一张图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 获取网格设置
                gridWidth = int.Parse(txtGridWidth.Text);
                gridHeight = int.Parse(txtGridHeight.Text);

                if (gridWidth <= 0 || gridHeight <= 0 || gridWidth > 20 || gridHeight > 20)
                {
                    MessageBox.Show("网格尺寸必须在1-20之间", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ParseImageToSequence();
                RecordToolUsage("ParseSequence");

                if (statusText != null)
                    statusText.Text = $"已解析为 {sequenceFrames.Count} 帧序列 ({gridWidth}×{gridHeight})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解析失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 将图片解析为序列帧
        private void ParseImageToSequence()
        {
            if (mainImage?.Source == null) return;

            try
            {
                var source = mainImage.Source as BitmapSource;
                if (source == null) return;

                originalImage = source;
                sequenceFrames.Clear();

                int frameWidth = source.PixelWidth / gridWidth;
                int frameHeight = source.PixelHeight / gridHeight;

                if (frameWidth <= 0 || frameHeight <= 0)
                {
                    MessageBox.Show("图片尺寸太小，无法按指定网格分割", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (statusText != null)
                    statusText.Text = $"正在解析 {gridWidth}×{gridHeight} 网格...";

                // 按网格切分图片
                int totalFrames = gridWidth * gridHeight;
                for (int row = 0; row < gridHeight; row++)
                {
                    for (int col = 0; col < gridWidth; col++)
                    {
                        int x = col * frameWidth;
                        int y = row * frameHeight;

                        // 确保不会超出图片边界
                        int actualWidth = Math.Min(frameWidth, source.PixelWidth - x);
                        int actualHeight = Math.Min(frameHeight, source.PixelHeight - y);

                        if (actualWidth > 0 && actualHeight > 0)
                        {
                            // 创建裁剪区域
                            var cropRect = new Int32Rect(x, y, actualWidth, actualHeight);

                            // 裁剪帧
                            var frame = new CroppedBitmap(source, cropRect);
                            frame.Freeze();

                            sequenceFrames.Add(frame);
                        }

                        // 更新进度
                        int currentFrame = row * gridWidth + col + 1;
                        if (statusText != null)
                            statusText.Text = $"正在解析帧 {currentFrame}/{totalFrames}...";
                    }
                }

                currentFrameIndex = 0;
                hasSequenceLoaded = true;

                // 启用控件
                EnableSequenceControls(true);

                // 显示第一帧
                ShowCurrentFrame();
                UpdateFrameDisplay();

                // 解析完成后自动居中显示第一帧，提供更好的用户体验
                CenterImage();
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"序列解析失败: {ex.Message}";
                MessageBox.Show($"序列解析失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 显示当前帧
        private void ShowCurrentFrame()
        {
            if (hasSequenceLoaded && currentFrameIndex >= 0 && currentFrameIndex < sequenceFrames.Count)
            {
                mainImage.Source = sequenceFrames[currentFrameIndex];

                // 保持当前的缩放和位置状态，让序列帧像正常图片一样可以拖动和缩放
                // 移除强制重置，这样更人性化
                // currentZoom = 1.0;
                // currentTransform = Transform.Identity;
                // imagePosition = new Point(0, 0);

                // 只更新图片变换，保持当前状态
                UpdateImageTransform();
                UpdateZoomText();
            }
        }

        // 更新帧显示信息
        private void UpdateFrameDisplay()
        {
            if (txtCurrentFrame != null)
            {
                if (hasSequenceLoaded && sequenceFrames.Count > 0)
                {
                    txtCurrentFrame.Text = $"{currentFrameIndex + 1} / {sequenceFrames.Count}";
                }
                else
                {
                    txtCurrentFrame.Text = "- / -";
                }
            }
        }

        // 启用/禁用序列控件
        private void EnableSequenceControls(bool enabled)
        {
            if (btnPlay != null) btnPlay.IsEnabled = enabled;
            if (btnStop != null) btnStop.IsEnabled = enabled;
            if (btnFirstFrame != null) btnFirstFrame.IsEnabled = enabled;
            if (btnPrevFrame != null) btnPrevFrame.IsEnabled = enabled;
            if (btnNextFrame != null) btnNextFrame.IsEnabled = enabled;
            if (btnLastFrame != null) btnLastFrame.IsEnabled = enabled;
            if (btnSaveAsGif != null) btnSaveAsGif.IsEnabled = enabled;
            if (btnResetSequence != null) btnResetSequence.IsEnabled = enabled;
        }

        // 播放/暂停按钮事件
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded) return;

            if (isSequencePlaying)
            {
                PauseSequence();
            }
            else
            {
                PlaySequence();
            }

            RecordToolUsage("SequencePlay");
        }

        // 开始播放
        private void PlaySequence()
        {
            if (sequenceTimer == null || !hasSequenceLoaded) return;

            UpdateSequenceTimerInterval();
            isSequencePlaying = true;
            sequenceTimer.Start();

            if (btnPlay != null)
                btnPlay.Content = "⏸ 暂停";

            if (statusText != null)
                statusText.Text = "序列播放中...";
        }

        // 暂停播放
        private void PauseSequence()
        {
            if (sequenceTimer == null) return;

            isSequencePlaying = false;
            sequenceTimer.Stop();

            if (btnPlay != null)
                btnPlay.Content = "▶ 播放";

            if (statusText != null)
                statusText.Text = "序列播放已暂停";
        }

        // 停止播放按钮事件
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopSequence();
            RecordToolUsage("SequenceStop");
        }

        // 停止播放并重置
        private void StopSequence()
        {
            if (sequenceTimer == null) return;

            isSequencePlaying = false;
            sequenceTimer.Stop();
            currentFrameIndex = 0;

            if (btnPlay != null)
                btnPlay.Content = "▶ 播放";

            ShowCurrentFrame();
            UpdateFrameDisplay();

            if (statusText != null)
                statusText.Text = "序列播放已停止并重置";
        }

        // 序列定时器事件
        private void SequenceTimer_Tick(object sender, EventArgs e)
        {
            if (!hasSequenceLoaded || sequenceFrames.Count == 0) return;

            currentFrameIndex++;
            if (currentFrameIndex >= sequenceFrames.Count)
            {
                currentFrameIndex = 0; // 循环播放
            }

            ShowCurrentFrame();
            UpdateFrameDisplay();
        }

        // 第一帧按钮事件
        private void BtnFirstFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded) return;

            currentFrameIndex = 0;
            ShowCurrentFrame();
            UpdateFrameDisplay();
            RecordToolUsage("SequenceFirstFrame");
        }

        // 上一帧按钮事件
        private void BtnPrevFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded) return;

            currentFrameIndex--;
            if (currentFrameIndex < 0)
                currentFrameIndex = sequenceFrames.Count - 1;

            ShowCurrentFrame();
            UpdateFrameDisplay();
            RecordToolUsage("SequencePrevFrame");
        }

        // 下一帧按钮事件
        private void BtnNextFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded) return;

            currentFrameIndex++;
            if (currentFrameIndex >= sequenceFrames.Count)
                currentFrameIndex = 0;

            ShowCurrentFrame();
            UpdateFrameDisplay();
            RecordToolUsage("SequenceNextFrame");
        }

        // 最后一帧按钮事件
        private void BtnLastFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded) return;

            currentFrameIndex = sequenceFrames.Count - 1;
            ShowCurrentFrame();
            UpdateFrameDisplay();
            RecordToolUsage("SequenceLastFrame");
        }

        // 网格预设选择事件
        private void CbGridPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbGridPresets?.SelectedItem is ComboBoxItem selected)
            {
                string preset = selected.Content.ToString() ?? "";

                switch (preset)
                {
                    case "3×3":
                        SetGridSize(3, 3);
                        break;
                    case "4×4":
                        SetGridSize(4, 4);
                        break;
                    case "5×5":
                        SetGridSize(5, 5);
                        break;
                    case "6×6":
                        SetGridSize(6, 6);
                        break;
                    case "8×8":
                        SetGridSize(8, 8);
                        break;
                    case "2×4":
                        SetGridSize(2, 4);
                        break;
                    case "4×2":
                        SetGridSize(4, 2);
                        break;
                    case "自定义":
                        // 不改变当前值，让用户手动输入
                        break;
                }
            }
        }

        // 设置网格尺寸
        private void SetGridSize(int width, int height)
        {
            if (txtGridWidth != null) txtGridWidth.Text = width.ToString();
            if (txtGridHeight != null) txtGridHeight.Text = height.ToString();
        }

        // 保存为GIF按钮事件
        private void BtnSaveAsGif_Click(object sender, RoutedEventArgs e)
        {
            if (!hasSequenceLoaded || sequenceFrames.Count == 0)
            {
                MessageBox.Show("请先解析序列帧", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "GIF动画|*.gif";
                dialog.FileName = Path.GetFileNameWithoutExtension(currentImagePath) + "_sequence";

                if (dialog.ShowDialog() == true)
                {
                    SaveSequenceAsGif(dialog.FileName);
                    RecordToolUsage("SaveAsGif");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存GIF失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 保存序列为GIF动画
        private void SaveSequenceAsGif(string fileName)
        {
            try
            {
                using (var gifImage = new MagickImageCollection())
                {
                    int fps = 10;
                    try
                    {
                        fps = int.Parse(txtFPS.Text);
                        if (fps <= 0 || fps > 120) fps = 10;
                    }
                    catch
                    {
                        fps = 10;
                    }

                    int delay = Math.Max(1, 100 / fps); // GIF delay in 1/100s

                    foreach (var frame in sequenceFrames)
                    {
                        // 将WPF BitmapSource转换为ImageMagick可用的格式
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(frame));

                        using (var stream = new MemoryStream())
                        {
                            encoder.Save(stream);
                            stream.Position = 0;

                            var magickFrame = new MagickImage(stream);
                            magickFrame.AnimationDelay = (uint)delay;
                            magickFrame.GifDisposeMethod = GifDisposeMethod.Background;

                            gifImage.Add(magickFrame);
                        }
                    }

                    // 设置GIF格式和选项
                    foreach (var image in gifImage)
                    {
                        image.Format = MagickFormat.Gif;
                    }

                    // 保存GIF
                    gifImage.Write(fileName);

                    if (statusText != null)
                        statusText.Text = $"GIF动画已保存: {Path.GetFileName(fileName)} ({sequenceFrames.Count}帧, {fps}FPS)";
                }
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"保存GIF失败: {ex.Message}";
                throw;
            }
        }

        // 重置序列播放器到原始图片
        private void ResetToOriginalImage()
        {
            if (originalImage != null && mainImage != null)
            {
                // 停止播放
                if (isSequencePlaying)
                {
                    StopSequence();
                }

                // 恢复原始图片
                mainImage.Source = originalImage;

                // 重置序列状态
                hasSequenceLoaded = false;
                sequenceFrames.Clear();
                currentFrameIndex = 0;

                // 禁用序列控件
                EnableSequenceControls(false);
                UpdateFrameDisplay();

                // 重置缩放和变换（不影响背景设置）
                currentZoom = 1.0;
                currentTransform = Transform.Identity;
                imagePosition = new Point(0, 0);

                // 更新图片显示
                UpdateImageTransform();
                UpdateZoomText();

                if (statusText != null)
                    statusText.Text = "已恢复到原始图片";
            }
        }

        #endregion
    }
}
