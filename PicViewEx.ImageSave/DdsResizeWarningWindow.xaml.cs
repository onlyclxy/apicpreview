using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PicViewEx.ImageSave
{
    /// <summary>
    /// DDS尺寸警告窗口，提供缩放到2的幂次方的功能
    /// </summary>
    public partial class DdsResizeWarningWindow : Window
    {
        private int _currentWidth;
        private int _currentHeight;
        private BitmapSource _sourceImage;

        public bool ShouldResize { get; private set; }
        public bool ContinueAnyway { get; private set; }
        public int TargetWidth { get; private set; }
        public int TargetHeight { get; private set; }
        public BitmapSource ResizedImage { get; private set; }

        // 游戏行业常用的正方形纹理尺寸（推荐用）
        private static readonly int[] GameTextureSizes = { 16, 32, 64, 128, 256, 512, 1024, 2048 };

        // 所有可用的分辨率（包括非正方形，最大4096）- 从大到小排序
        private static readonly List<(int width, int height, string description)> AllResolutions = new List<(int, int, string)>
        {
            // 正方形分辨率（从大到小）
            (4096, 4096, "4096 × 4096 (超大)"),
            (2048, 2048, "2048 × 2048 (很大)"),
            (1024, 1024, "1024 × 1024 (大)"),
            (512, 512, "512 × 512 (中)"),
            (256, 256, "256 × 256 (中)"),
            (128, 128, "128 × 128 (小)"),
            (64, 64, "64 × 64 (小)"),
            (32, 32, "32 × 32 (极小)"),
            (16, 16, "16 × 16 (极小)"),
            
            // 常用非正方形分辨率（从大到小）
            (4096, 2048, "4096 × 2048 (宽)"),
            (2048, 1024, "2048 × 1024 (宽)"),
            (1024, 512, "1024 × 512 (宽)"),
            (512, 256, "512 × 256 (宽)"),
            (256, 128, "256 × 128 (宽)"),
            
            (2048, 4096, "2048 × 4096 (高)"),
            (1024, 2048, "1024 × 2048 (高)"),
            (512, 1024, "512 × 1024 (高)"),
            (256, 512, "256 × 512 (高)"),
            (128, 256, "128 × 256 (高)"),
            
            (4096, 1024, "4096 × 1024 (超宽)"),
            (2048, 512, "2048 × 512 (超宽)"),
            (1024, 256, "1024 × 256 (超宽)"),
            (512, 128, "512 × 128 (超宽)"),
            
            (1024, 4096, "1024 × 4096 (超高)"),
            (512, 2048, "512 × 2048 (超高)"),
            (256, 1024, "256 × 1024 (超高)"),
            (128, 512, "128 × 512 (超高)")
        };

        public DdsResizeWarningWindow(BitmapSource source, int currentWidth, int currentHeight)
        {
            InitializeComponent();

            _sourceImage = source;
            _currentWidth = currentWidth;
            _currentHeight = currentHeight;

            ShouldResize = false;
            ContinueAnyway = false;

            InitializeUI();
        }

        private void InitializeUI()
        {
            // 显示当前尺寸
            TxtCurrentSize.Text = $"当前图像尺寸：{_currentWidth} × {_currentHeight}";

            // 计算并显示长宽比（以1为基准）
            string aspectRatio = CalculateAspectRatioString(_currentWidth, _currentHeight);
            TxtAspectRatio.Text = $"长宽比：{aspectRatio}";

            // 计算推荐的分辨率（正方形和非正方形各一个）
            var squareRecommended = CalculateRecommendedSquareResolution(_currentWidth, _currentHeight);
            var rectangleRecommended = CalculateRecommendedRectangleResolution(_currentWidth, _currentHeight);

            // 显示推荐提示
            TxtRecommendedHint.Text = $"智能推荐：正方形 {squareRecommended.width}×{squareRecommended.height}  |  " +
                                      $"非正方形 {rectangleRecommended.width}×{rectangleRecommended.height}";

            // 构建分辨率列表：推荐的在最上方，然后是其他选项
            BuildResolutionList(squareRecommended, rectangleRecommended);

            // 默认选中第一个推荐项
            if (ResolutionListBox.Items.Count > 0)
            {
                ResolutionListBox.SelectedIndex = 0;
            }

            // 初始化预览
            UpdatePreview();
        }

        /// <summary>
        /// 计算长宽比字符串（以1为基准）
        /// 例如：1920×1080 → "1.78:1"（横向）
        ///       1080×1920 → "1:1.78"（纵向）
        ///       2048×2048 → "1:1"（正方形）
        /// </summary>
        private string CalculateAspectRatioString(int width, int height)
        {
            if (width == height)
            {
                return "1:1";
            }
            else if (width > height)
            {
                // 横向：x:1
                double ratio = (double)width / height;
                return $"{ratio:F2}:1";
            }
            else
            {
                // 纵向：1:x
                double ratio = (double)height / width;
                return $"1:{ratio:F2}";
            }
        }

        /// <summary>
        /// 计算推荐的正方形分辨率
        /// 规则：
        /// 1. 基于游戏行业标准尺寸（正方形）
        /// 2. 找最长边，然后找最接近的2的幂次方
        /// 3. 一般放大不缩小，除非超过2048
        /// 4. 最大推荐2048×2048
        /// </summary>
        private (int width, int height) CalculateRecommendedSquareResolution(int width, int height)
        {
            int maxDimension = Math.Max(width, height);

            // 如果超过2048，推荐2048×2048
            if (maxDimension > 2048)
            {
                return (2048, 2048);
            }

            // 找到最接近的游戏纹理尺寸（向上取整）
            int recommendedSize = 2048; // 默认最大值
            foreach (int size in GameTextureSizes)
            {
                if (size >= maxDimension)
                {
                    recommendedSize = size;
                    break;
                }
            }

            return (recommendedSize, recommendedSize);
        }

        /// <summary>
        /// 计算推荐的非正方形分辨率
        /// 规则：
        /// 1. 分别找长边和短边最接近的2的幂次方
        /// 2. 保持原始宽高比的大致方向
        /// 3. 向上取整，最大边不超过2048
        /// </summary>
        private (int width, int height) CalculateRecommendedRectangleResolution(int width, int height)
        {
            int[] powerOfTwoSizes = { 16, 32, 64, 128, 256, 512, 1024, 2048 };

            // 找到长边和短边
            int longSide = Math.Max(width, height);
            int shortSide = Math.Min(width, height);
            bool isWidthLonger = width > height;

            // 为长边找到最接近的2的幂次方（向上取整），最大2048
            int recommendedLong = 2048;
            foreach (int size in powerOfTwoSizes)
            {
                if (size >= longSide)
                {
                    recommendedLong = size;
                    break;
                }
            }

            // 为短边找到最接近的2的幂次方（向上取整），最大2048
            int recommendedShort = 2048;
            foreach (int size in powerOfTwoSizes)
            {
                if (size >= shortSide)
                {
                    recommendedShort = size;
                    break;
                }
            }

            // 如果推荐出来的长边和短边相同，则短边降一级（除非已经是最小的16）
            if (recommendedLong == recommendedShort && recommendedShort > 16)
            {
                int shortIndex = Array.IndexOf(powerOfTwoSizes, recommendedShort);
                if (shortIndex > 0)
                {
                    recommendedShort = powerOfTwoSizes[shortIndex - 1];
                }
            }

            // 根据原始图片方向返回
            if (isWidthLonger)
            {
                return (recommendedLong, recommendedShort);
            }
            else
            {
                return (recommendedShort, recommendedLong);
            }
        }

        /// <summary>
        /// 构建分辨率列表
        /// </summary>
        private void BuildResolutionList((int width, int height) squareRecommended, (int width, int height) rectangleRecommended)
        {
            ResolutionListBox.Items.Clear();

            // 首先添加正方形推荐项
            var squareItem = CreateResolutionItem(squareRecommended.width, squareRecommended.height, true, "正方形");
            ResolutionListBox.Items.Add(squareItem);

            // 如果非正方形推荐与正方形推荐不同，则添加非正方形推荐
            if (rectangleRecommended.width != squareRecommended.width || 
                rectangleRecommended.height != squareRecommended.height)
            {
                var rectangleItem = CreateResolutionItem(rectangleRecommended.width, rectangleRecommended.height, true, "非正方形");
                ResolutionListBox.Items.Add(rectangleItem);
            }

            // 添加分隔线
            var separator = new Border
            {
                Height = 1,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 5, 0, 5)
            };
            ResolutionListBox.Items.Add(separator);

            // 添加所有其他分辨率（排除推荐项）
            foreach (var resolution in AllResolutions)
            {
                // 跳过与推荐项相同的分辨率
                if ((resolution.width == squareRecommended.width && resolution.height == squareRecommended.height) ||
                    (resolution.width == rectangleRecommended.width && resolution.height == rectangleRecommended.height))
                    continue;

                var item = CreateResolutionItem(resolution.width, resolution.height, false, resolution.description);
                ResolutionListBox.Items.Add(item);
            }
        }

        /// <summary>
        /// 创建分辨率列表项
        /// </summary>
        private ListBoxItem CreateResolutionItem(int width, int height, bool isRecommended, string description = null)
        {
            var item = new ListBoxItem();
            item.Tag = new ResolutionInfo { Width = width, Height = height };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            // 分辨率文本
            var resolutionText = new TextBlock
            {
                Text = $"{width} × {height}",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 140
            };
            panel.Children.Add(resolutionText);

            // 如果是推荐项，添加标签
            if (isRecommended)
            {
                var recommendedTag = new TextBlock
                {
                    Text = $"【推荐 - {description}】",
                    FontSize = 12,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0, 200, 83)),
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                panel.Children.Add(recommendedTag);
            }
            else if (!string.IsNullOrEmpty(description))
            {
                // 添加描述
                var descText = new TextBlock
                {
                    Text = description,
                    FontSize = 11,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(170, 170, 170)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                panel.Children.Add(descText);
            }

            // 显示与原始尺寸的关系
            double scaleRatio = Math.Max((double)width / _currentWidth, (double)height / _currentHeight);
            string scaleInfo = "";
            if (scaleRatio > 1.0)
            {
                scaleInfo = $"放大 ↑{scaleRatio:F2}x";
            }
            else if (scaleRatio < 1.0)
            {
                scaleInfo = $"缩小 ↓{scaleRatio:F2}x";
            }
            else
            {
                scaleInfo = "原尺寸 1.00x";
            }

            var scaleText = new TextBlock
            {
                Text = scaleInfo,
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(170, 170, 170)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            
            // 使用Grid来让缩放信息右对齐
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            Grid.SetColumn(panel, 0);
            Grid.SetColumn(scaleText, 1);
            
            grid.Children.Add(panel);
            grid.Children.Add(scaleText);

            item.Content = grid;
            return item;
        }

        private void ResolutionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 更新选中的目标分辨率
            if (ResolutionListBox.SelectedItem is ListBoxItem item && item.Tag is ResolutionInfo info)
            {
                TargetWidth = info.Width;
                TargetHeight = info.Height;
            }

            // 更新预览
            UpdatePreview();
        }

        private void BtnResize_Click(object sender, RoutedEventArgs e)
        {
            if (ResolutionListBox.SelectedItem == null)
            {
                CustomMessageBox.Show(
                    "请选择目标分辨率！",
                    "提示",
                    CustomMessageBox.MessageBoxButtons.OK,
                    CustomMessageBox.MessageBoxType.Information,
                    this);
                return;
            }

            if (ResolutionListBox.SelectedItem is ListBoxItem item && item.Tag is ResolutionInfo info)
            {
                TargetWidth = info.Width;
                TargetHeight = info.Height;

                // 执行缩放
                ResizedImage = ResizeImage(_sourceImage, TargetWidth, TargetHeight);
                if (ResizedImage == null)
                {
                    CustomMessageBox.Show(
                        "图像缩放失败！",
                        "错误",
                        CustomMessageBox.MessageBoxButtons.OK,
                        CustomMessageBox.MessageBoxType.Error,
                        this);
                    return;
                }

                ShouldResize = true;
                ContinueAnyway = false;
                DialogResult = true;
                Close();
            }
        }

        private void BtnContinueAnyway_Click(object sender, RoutedEventArgs e)
        {
            ShouldResize = false;
            ContinueAnyway = true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ShouldResize = false;
            ContinueAnyway = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 更新预览图和信息
        /// </summary>
        private void UpdatePreview()
        {
            // 首先设置原图参考框（始终显示）
            SetOriginalReferenceBox();

            if (ResolutionListBox.SelectedItem is ListBoxItem item && item.Tag is ResolutionInfo info)
            {
                // 设置预览图源
                PreviewImage.Source = _sourceImage;

                // 根据目标分辨率的宽高比动态调整预览框大小
                // 最大100×100，按比例缩放
                double targetRatio = (double)info.Width / info.Height;
                double maxSize = 100;

                double previewWidth, previewHeight;
                if (targetRatio > 1)
                {
                    // 横向图片：宽度优先
                    previewWidth = maxSize;
                    previewHeight = maxSize / targetRatio;
                }
                else
                {
                    // 纵向或正方形：高度优先
                    previewHeight = maxSize;
                    previewWidth = maxSize * targetRatio;
                }

                // 应用计算出的宽高
                PreviewBorder.Width = previewWidth;
                PreviewBorder.Height = previewHeight;

                // 更新选中的分辨率信息
                TxtSelectedResolution.Text = $"{info.Width} × {info.Height}";

                // 计算并显示缩放信息（去掉汉字）
                double widthScale = (double)info.Width / _currentWidth;
                double heightScale = (double)info.Height / _currentHeight;

                string scaleText;
                if (Math.Abs(widthScale - heightScale) < 0.01)
                {
                    // 等比缩放
                    scaleText = $"缩放：{widthScale:F2}x";
                }
                else
                {
                    // 非等比缩放
                    scaleText = $"缩放：W{widthScale:F2}x H{heightScale:F2}x";
                    
                    // 计算形变程度
                    double distortion = Math.Abs(widthScale - heightScale) / Math.Max(widthScale, heightScale) * 100;
                    if (distortion > 5)
                    {
                        scaleText += $"\n形变：{distortion:F0}%";
                    }
                }

                TxtScaleInfo.Text = scaleText;
            }
            else
            {
                // 没有选中项
                PreviewImage.Source = null;
                PreviewBorder.Width = 100;
                PreviewBorder.Height = 100;
                TxtSelectedResolution.Text = "-";
                TxtScaleInfo.Text = "-";
            }
        }

        /// <summary>
        /// 设置原图分辨率参考框
        /// </summary>
        private void SetOriginalReferenceBox()
        {
            // 根据原图分辨率的宽高比设置参考框大小
            double originalRatio = (double)_currentWidth / _currentHeight;
            double maxSize = 100;

            double originalWidth, originalHeight;
            if (originalRatio > 1)
            {
                // 横向图片：宽度优先
                originalWidth = maxSize;
                originalHeight = maxSize / originalRatio;
            }
            else
            {
                // 纵向或正方形：高度优先
                originalHeight = maxSize;
                originalWidth = maxSize * originalRatio;
            }

            // 应用计算出的宽高
            OriginalBorder.Width = originalWidth;
            OriginalBorder.Height = originalHeight;
        }

        /// <summary>
        /// 缩放图像到指定尺寸
        /// </summary>
        private BitmapSource ResizeImage(BitmapSource source, int targetWidth, int targetHeight)
        {
            try
            {
                var scaledBitmap = new TransformedBitmap(source,
                    new System.Windows.Media.ScaleTransform(
                        (double)targetWidth / source.PixelWidth,
                        (double)targetHeight / source.PixelHeight));
                
                scaledBitmap.Freeze();
                return scaledBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"图像缩放失败: {ex.Message}");
                return null;
            }
        }

        private class ResolutionInfo
        {
            public int Width { get; set; }
            public int Height { get; set; }
        }
    }
}

