using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
		#region 事件处理程序

		private void MainWindow_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Left:
					NavigatePrevious();
					break;
				case Key.Right:
					NavigateNext();
					break;
				case Key.F:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
					{
						FitToWindow();
						PrintImageInfo("适应窗口 (快捷键F)");
					}
					break;
				case Key.D1:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
					{
						SetActualSize();
						PrintImageInfo("实际大小 (快捷键1)");
					}
					break;
				case Key.Space:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
					{
						CenterImage();
						PrintImageInfo("居中显示 (快捷键空格)");
					}
					break;
				case Key.F11:
					MenuFullScreen_Click(sender, e);
					break;
				case Key.Escape:
					if (WindowState == WindowState.Maximized)
						WindowState = WindowState.Normal;
					break;
				case Key.O:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
						BtnOpen_Click(sender, e);
					break;
				case Key.S:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
						BtnSaveAs_Click(sender, e);
					else if (e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
						SaveAppSettings();
					break;
				case Key.V:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
						PasteImageFromClipboard();
					break;
				case Key.P:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
					{
						// Ctrl+P 播放/暂停序列帧
						if (hasSequenceLoaded)
							BtnPlay_Click(sender, e);
					}
					break;
				case Key.OemPeriod: // . 键
					if (e.KeyboardDevice.Modifiers == ModifierKeys.None && hasSequenceLoaded)
						BtnNextFrame_Click(sender, e);
					break;
				case Key.OemComma: // , 键
					if (e.KeyboardDevice.Modifiers == ModifierKeys.None && hasSequenceLoaded)
						BtnPrevFrame_Click(sender, e);
					break;
				case Key.L:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
						RotateImage(-90);
					break;
				case Key.R:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
						RotateImage(90);
					break;
				case Key.OemPlus:
				case Key.Add:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
						ZoomImage(1.2);
					break;
				case Key.OemMinus:
				case Key.Subtract:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
						ZoomImage(0.8);
					break;
			}
		}

		private void MainWindow_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files.Length > 0)
				{
					string file = files[0];
					string extension = Path.GetExtension(file).ToLower();

					if (supportedFormats.Contains(extension))
					{
						// 如果当前有序列帧在播放，自动停止并重置到正常图片模式
						if (hasSequenceLoaded)
						{
							// 停止播放
							if (isSequencePlaying)
							{
								PauseSequence();
							}

							// 重置序列帧状态
							hasSequenceLoaded = false;
							sequenceFrames.Clear();
							currentFrameIndex = 0;
							originalImage = null;

							// 禁用序列控件
							EnableSequenceControls(false);
							UpdateFrameDisplay();

							if (statusText != null)
								UpdateStatusText("序列帧播放已停止，切换到新图片");
						}

						LoadImage(file);
						var directoryPath = Path.GetDirectoryName(file);
						if (!string.IsNullOrEmpty(directoryPath))
						{
							LoadDirectoryImages(directoryPath);
						}
					}
					else
					{
						MessageBox.Show("不支持的文件格式", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
					}
				}
			}
		}

		private void BtnOpen_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dialog = new OpenFileDialog();
			dialog.Filter = "支持的图片格式|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif;*.ico;*.webp;*.tga;*.dds;*.psd|所有文件|*.*";

			if (dialog.ShowDialog() == true)
			{
				// 如果当前有序列帧在播放，自动停止并重置到正常图片模式
				if (hasSequenceLoaded)
				{
					// 停止播放
					if (isSequencePlaying)
					{
						PauseSequence();
					}

					// 重置序列帧状态
					hasSequenceLoaded = false;
					sequenceFrames.Clear();
					currentFrameIndex = 0;
					originalImage = null;

					// 禁用序列控件
					EnableSequenceControls(false);
					UpdateFrameDisplay();

					if (statusText != null)
						UpdateStatusText("序列帧播放已停止，切换到新图片");
				}

				LoadImage(dialog.FileName);
				var directoryPath = Path.GetDirectoryName(dialog.FileName);
				if (!string.IsNullOrEmpty(directoryPath))
				{
					LoadDirectoryImages(directoryPath);
				}
			}
		}

		private void BtnPrevious_Click(object sender, RoutedEventArgs e)
		{
			RecordToolUsage("Previous");
			NavigatePrevious();
		}

		private void BtnNext_Click(object sender, RoutedEventArgs e)
		{
			RecordToolUsage("Next");
			NavigateNext();
		}

		private void BtnRotateLeft_Click(object sender, RoutedEventArgs e)
		{
			RecordToolUsage("RotateLeft");
			RotateImage(-90);
			PrintImageInfo("左旋转");
		}

		private void BtnRotateRight_Click(object sender, RoutedEventArgs e)
		{
			RecordToolUsage("RotateRight");
			RotateImage(90);
			PrintImageInfo("右旋转");
		}

		private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
		{
			if (mainImage?.Source == null)
			{
				MessageBox.Show("请先打开一张图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			SaveFileDialog dialog = new SaveFileDialog();
			dialog.Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp|TIFF|*.tiff|GIF|*.gif|DDS|*.dds";

			// 设置默认文件名
			if (!string.IsNullOrEmpty(currentImagePath))
			{
				// 如果有文件路径，使用原文件名
				dialog.FileName = Path.GetFileNameWithoutExtension(currentImagePath);
			}
			else
			{
				// 如果是剪贴板图片，使用时间戳作为文件名
				dialog.FileName = $"PastedImage_{DateTime.Now:yyyyMMdd_HHmmss}";
			}

			if (dialog.ShowDialog() == true)
			{
				SaveCurrentImage(dialog.FileName);
			}
		}

		/// <summary>
		/// 保存当前显示的图片（支持剪贴板图片和文件图片）
		/// </summary>
		private void SaveCurrentImage(string fileName)
		{
			try
			{
				if (mainImage?.Source == null)
				{
					throw new InvalidOperationException("没有可保存的图片");
				}

				var source = mainImage.Source as BitmapSource;
				if (source == null)
				{
					throw new InvalidOperationException("图片格式不支持保存");
				}

				// 检查是否为DDS格式
				string extension = Path.GetExtension(fileName).ToLower();
				if (extension == ".dds")
				{
					SaveAsDds(source, fileName);
					return;
				}

				// 如果有原始文件路径且没有旋转变换，直接使用 ImageMagick 处理原文件
				if (!string.IsNullOrEmpty(currentImagePath) && File.Exists(currentImagePath) &&
					currentTransform == Transform.Identity)
				{
					SaveRotatedImage(fileName);
					return;
				}

				// 否则保存当前显示的图片（包括剪贴板图片和有变换的图片）
				SaveBitmapSource(source, fileName);

				if (statusText != null)
					UpdateStatusText($"已保存: {Path.GetFileName(fileName)}");
			}
			catch (Exception ex)
			{
				MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
				if (statusText != null)
					UpdateStatusText("保存失败");
			}
		}

		/// <summary>
		/// 保存 BitmapSource 到文件
		/// </summary>
		private void SaveBitmapSource(BitmapSource source, string fileName)
		{
			try
			{
				// 应用当前的旋转变换到图片
				BitmapSource finalSource = source;

				if (currentTransform != Transform.Identity)
				{
					// 创建一个 TransformedBitmap 来应用变换
					var transformedBitmap = new TransformedBitmap(source, currentTransform);
					finalSource = transformedBitmap;
				}

				// 根据文件扩展名选择编码器
				BitmapEncoder encoder;
				string extension = Path.GetExtension(fileName).ToLower();

				switch (extension)
				{
					case ".jpg":
					case ".jpeg":
						encoder = new JpegBitmapEncoder { QualityLevel = 95 };
						break;
					case ".png":
						encoder = new PngBitmapEncoder();
						break;
					case ".bmp":
						encoder = new BmpBitmapEncoder();
						break;
					case ".tiff":
					case ".tif":
						encoder = new TiffBitmapEncoder();
						break;
					case ".gif":
						encoder = new GifBitmapEncoder();
						break;
					default:
						encoder = new PngBitmapEncoder(); // 默认使用 PNG
						break;
				}

				encoder.Frames.Add(BitmapFrame.Create(finalSource));

				using (var fileStream = new FileStream(fileName, FileMode.Create))
				{
					encoder.Save(fileStream);
				}
			}
			catch (Exception ex)
			{
				throw new Exception($"图片编码保存失败: {ex.Message}");
			}
		}


		private void ChkShowChannels_Checked(object sender, RoutedEventArgs e)
		{
			Console.WriteLine("显示通道面板");
			RecordToolUsage("ShowChannels");
			if (chkShowChannels != null) chkShowChannels.Content = "关闭通道";

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

			// 同步菜单状态
			if (menuShowChannels != null)
				menuShowChannels.IsChecked = true;
		}

		private void ChkShowChannels_Unchecked(object sender, RoutedEventArgs e)
		{
			Console.WriteLine("隐藏通道面板");
			RecordToolUsage("HideChannels");
			if (chkShowChannels != null) chkShowChannels.Content = "显示通道";

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

			// 同步菜单状态
			if (menuShowChannels != null)
				menuShowChannels.IsChecked = false;
		}

		private void BackgroundType_Changed(object sender, RoutedEventArgs e)
		{
			if (sender is RadioButton rb)
			{
				string backgroundType = "";
				if (rb == rbTransparent) backgroundType = "Transparent";
				else if (rb == rbSolidColor) backgroundType = "SolidColor";
				else if (rb == rbImageBackground) backgroundType = "ImageBackground";
				else if (rb == rbWindowTransparent) backgroundType = "WindowTransparent";

				if (!string.IsNullOrEmpty(backgroundType))
				{
					RecordToolUsage($"Background{backgroundType}");
				}
			}

			// 如果切换到图片背景，但还没有设置背景图片，则加载默认图片
			if (rbImageBackground?.IsChecked == true && backgroundImageBrush == null)
			{
				ApplyDefaultBackgroundImage(updateBackground: false, updateStatus: true);
			}

			UpdateBackground();
		}

		private void ApplyDefaultBackgroundImage(bool updateBackground = true, bool updateStatus = true)
		{
			try
			{
				var result = imageLoader.LoadDefaultBackgroundImage(AppDomain.CurrentDomain.BaseDirectory);
				backgroundImageBrush = result.Brush;

				if (statusText != null)
				{
					if (result.UsedFallback)
						UpdateStatusText("默认图片不存在，使用渐变背景");
					else if (!string.IsNullOrEmpty(result.SourcePath))
						UpdateStatusText($"已加载默认背景图片: {Path.GetFileName(result.SourcePath)}");
				}
			}
			catch (Exception ex)
			{
				if (updateStatus && statusText != null)
					UpdateStatusText($"加载默认背景图片失败: {ex.Message}");
			}
		}

		private void PresetColor_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is string colorString)
			{
				RecordToolUsage("PresetColor");

				if (rbSolidColor != null)
					rbSolidColor.IsChecked = true;

				Color color;
				switch (colorString)
				{
					case "White":
						color = Colors.White;
						break;
					case "Black":
						color = Colors.Black;
						break;
					default:
						var converter = new BrushConverter();
						if (converter.ConvertFromString(colorString) is SolidColorBrush brush)
							color = brush.Color;
						else
							return;
						break;
				}

				currentBackgroundBrush = new SolidColorBrush(color);

				// 更新HSV滑块和颜色选择器
				var (h, s, v) = RgbToHsv(color);

				if (sliderColorSpectrum != null)
				{
					sliderColorSpectrum.ValueChanged -= ColorSpectrum_ValueChanged;
					sliderColorSpectrum.Value = h;
					sliderColorSpectrum.ValueChanged += ColorSpectrum_ValueChanged;
				}

				if (sliderHue != null)
				{
					sliderHue.ValueChanged -= ColorSlider_ValueChanged;
					sliderHue.Value = h;
					sliderHue.ValueChanged += ColorSlider_ValueChanged;
				}

				if (sliderSaturation != null)
				{
					sliderSaturation.ValueChanged -= ColorSlider_ValueChanged;
					sliderSaturation.Value = s * 100;
					sliderSaturation.ValueChanged += ColorSlider_ValueChanged;
				}

				if (sliderBrightness != null)
				{
					sliderBrightness.ValueChanged -= ColorSlider_ValueChanged;
					sliderBrightness.Value = v * 100;
					sliderBrightness.ValueChanged += ColorSlider_ValueChanged;
				}

				if (colorPicker != null)
					colorPicker.SelectedColor = color;

				UpdateBackground();
			}
		}

		private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			// 自动切换到纯色背景类型
			if (rbSolidColor != null)
				rbSolidColor.IsChecked = true;

			if (sliderHue != null && sliderSaturation != null && sliderBrightness != null)
			{
				double hue = sliderHue.Value;
				double saturation = sliderSaturation.Value / 100.0;
				double brightness = sliderBrightness.Value / 100.0;

				Color color = HsvToRgb(hue, saturation, brightness);
				currentBackgroundBrush = new SolidColorBrush(color);

				if (colorPicker != null)
					colorPicker.SelectedColor = color;

				UpdateBackground();
			}
		}

		private void ColorSpectrum_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			// 自动切换到纯色背景类型
			if (rbSolidColor != null)
				rbSolidColor.IsChecked = true;

			if (sliderColorSpectrum != null)
			{
				// 从快速选色滑块获取色相值
				double hue = sliderColorSpectrum.Value;

				// 使用饱和度100%和明度75%来生成鲜艳的颜色
				double saturation = 1.0;
				double brightness = 0.75;

				Color color = HsvToRgb(hue, saturation, brightness);
				currentBackgroundBrush = new SolidColorBrush(color);

				// 同步更新其他控件
				if (sliderHue != null)
				{
					sliderHue.ValueChanged -= ColorSlider_ValueChanged;
					sliderHue.Value = hue;
					sliderHue.ValueChanged += ColorSlider_ValueChanged;
				}

				if (sliderSaturation != null)
				{
					sliderSaturation.ValueChanged -= ColorSlider_ValueChanged;
					sliderSaturation.Value = saturation * 100;
					sliderSaturation.ValueChanged += ColorSlider_ValueChanged;
				}

				if (sliderBrightness != null)
				{
					sliderBrightness.ValueChanged -= ColorSlider_ValueChanged;
					sliderBrightness.Value = brightness * 100;
					sliderBrightness.ValueChanged += ColorSlider_ValueChanged;
				}

				if (colorPicker != null)
					colorPicker.SelectedColor = color;

				UpdateBackground();
			}
		}

		private void ColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
		{
			if (e.NewValue.HasValue && rbSolidColor != null)
			{
				rbSolidColor.IsChecked = true;
				currentBackgroundBrush = new SolidColorBrush(e.NewValue.Value);

				// 更新HSV滑块以匹配选中的颜色
				var (h, s, v) = RgbToHsv(e.NewValue.Value);

				if (sliderColorSpectrum != null)
				{
					sliderColorSpectrum.ValueChanged -= ColorSpectrum_ValueChanged;
					sliderColorSpectrum.Value = h;
					sliderColorSpectrum.ValueChanged += ColorSpectrum_ValueChanged;
				}

				if (sliderHue != null)
				{
					sliderHue.ValueChanged -= ColorSlider_ValueChanged;
					sliderHue.Value = h;
					sliderHue.ValueChanged += ColorSlider_ValueChanged;
				}

				if (sliderSaturation != null)
				{
					sliderSaturation.ValueChanged -= ColorSlider_ValueChanged;
					sliderSaturation.Value = s * 100;
					sliderSaturation.ValueChanged += ColorSlider_ValueChanged;
				}

				if (sliderBrightness != null)
				{
					sliderBrightness.ValueChanged -= ColorSlider_ValueChanged;
					sliderBrightness.Value = v * 100;
					sliderBrightness.ValueChanged += ColorSlider_ValueChanged;
				}

				UpdateBackground();
			}
		}

		private void BtnSelectBackgroundImage_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dialog = new OpenFileDialog();
			dialog.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif|所有文件|*.*";
			dialog.Title = "选择背景图片";

			if (dialog.ShowDialog() == true)
			{
				ApplyBackgroundImageFromPath(dialog.FileName);
			}
			else
			{
				// 用户取消了选择，如果当前没有背景图片，则加载默认图片
				if (backgroundImageBrush == null)
				{
					ApplyDefaultBackgroundImage(updateBackground: true, updateStatus: true);
					if (rbImageBackground != null)
						rbImageBackground.IsChecked = true;
				}
			}
		}

		private void ImageContainer_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (mainImage?.Source == null) return;

			double scaleFactor = e.Delta > 0 ? 1.1 : 0.9;
			double newZoom = currentZoom * scaleFactor;

			// 限制缩放范围
			var originalImage = mainImage.Source as BitmapSource;
			if (originalImage != null)
			{
				double maxZoom = Math.Max(10.0, Math.Max(
					originalImage.PixelWidth / 50.0,
					originalImage.PixelHeight / 50.0));
				newZoom = Math.Max(0.05, Math.Min(newZoom, maxZoom));
			}
			else
			{
				newZoom = Math.Max(0.05, Math.Min(newZoom, 20.0));
			}

			// 如果缩放没有变化，直接返回
			if (Math.Abs(newZoom - currentZoom) < 0.001) return;

			// 获取鼠标在容器中的位置
			Point mousePos = e.GetPosition(imageContainer);

			// 计算缩放前图片在鼠标位置的点
			Point mousePosInImage = new Point(
				(mousePos.X - imagePosition.X) / currentZoom,
				(mousePos.Y - imagePosition.Y) / currentZoom
			);

			// 更新缩放
			currentZoom = newZoom;

			// 计算新的图片位置，使鼠标位置在图片上的点保持不变
			imagePosition.X = mousePos.X - (mousePosInImage.X * currentZoom);
			imagePosition.Y = mousePos.Y - (mousePosInImage.Y * currentZoom);

			// 应用变换和位置更新（包含边界约束）
			UpdateImageTransform();
			UpdateZoomText();

			// 添加信息打印
			string zoomAction = e.Delta > 0 ? "鼠标滚轮放大" : "鼠标滚轮缩小";
			PrintImageInfo(zoomAction);

			e.Handled = true;
		}

		private void MainImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (mainImage?.Source != null)
			{
				isDragging = true;
				lastMousePosition = e.GetPosition(imageContainer);
				mainImage.CaptureMouse();
				e.Handled = true;
			}
		}

		private void MainImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (isDragging)
			{
				isDragging = false;
				mainImage.ReleaseMouseCapture();

				// 拖拽完成后打印图片信息
				PrintImageInfo("拖拽");

				e.Handled = true;
			}
		}

		private void MainImage_MouseMove(object sender, MouseEventArgs e)
		{
			if (isDragging && e.LeftButton == MouseButtonState.Pressed)
			{
				Point currentPosition = e.GetPosition(imageContainer);
				Point delta = new Point(
					currentPosition.X - lastMousePosition.X,
					currentPosition.Y - lastMousePosition.Y
				);

				imagePosition.X += delta.X;
				imagePosition.Y += delta.Y;

				UpdateImagePosition();

				lastMousePosition = currentPosition;
				e.Handled = true;
			}
		}

		private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				// 双击标题栏切换最大化/还原
				if (this.WindowState == WindowState.Maximized)
					this.WindowState = WindowState.Normal;
				else
					this.WindowState = WindowState.Maximized;
			}
			else
			{
				// 单击拖拽窗口
				this.DragMove();
			}
		}

		private void BtnMinimize_Click(object sender, RoutedEventArgs e)
		{
			this.WindowState = WindowState.Minimized;
		}

		private void BtnMaximize_Click(object sender, RoutedEventArgs e)
		{
			if (this.WindowState == WindowState.Maximized)
			{
				this.WindowState = WindowState.Normal;
				if (btnMaximize != null)
					btnMaximize.Content = "🗖";
			}
			else
			{
				this.WindowState = WindowState.Maximized;
				if (btnMaximize != null)
					btnMaximize.Content = "🗗";
			}
		}

		private void BtnClose_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		private void BtnPaste_Click(object sender, RoutedEventArgs e)
		{
			RecordToolUsage("PasteFromToolbar");
			PasteImageFromClipboard();
		}

        // 重置序列按钮事件
        private void BtnResetSequence_Click(object sender, RoutedEventArgs e)
        {
            ResetToOriginalImage();
            RecordToolUsage("ResetSequence");
        }

        /// <summary>
        /// 显示DDS保存成功对话框并提供打开选项
        /// </summary>
        private void ShowDdsSaveSuccessDialog(string ddsFilePath)
        {
            try
            {
                var result = MessageBox.Show(
                    $"DDS文件保存成功！\n\n文件路径：{ddsFilePath}\n\n是否要打开文件所在位置？",
                    "DDS保存成功",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information
                );

                if (result == MessageBoxResult.Yes)
                {
                    // 打开文件所在文件夹并选中文件
                    string argument = $"/select, \"{ddsFilePath}\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示DDS成功对话框时发生错误: {ex.Message}");
            }
        }

        private void ManageOpenWithApps_Click(object sender, RoutedEventArgs e)
        {
            var manageWindow = new OpenWithManagerWindow(openWithApps);
            manageWindow.Owner = this;
            manageWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (manageWindow.ShowDialog() == true)
            {
                // 用户确认了更改，更新应用列表
                openWithApps.Clear();
                foreach (var viewModel in manageWindow.OpenWithApps)
                {
                    openWithApps.Add(viewModel.ToOpenWithApp());
                }

                UpdateOpenWithButtons();
                UpdateOpenWithMenu();
                SaveAppSettings(); // 立即保存设置

                if (statusText != null)
                    UpdateStatusText($"打开方式设置已更新，共 {openWithApps.Count} 个应用");
            }
        }

        private void ImageContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (mainImage?.Source == null)
            {
                // 如果没有图片，直接拖动窗口
                this.DragMove();
                e.Handled = true;
                return;
            }

            // 获取鼠标在容器中的位置
            Point mousePos = e.GetPosition(imageContainer);

            // 计算图片的显示区域
            var source = mainImage.Source as BitmapSource;
            if (source != null)
            {
                // 计算图片的实际显示区域（考虑缩放和位置）
                double imageWidth = source.PixelWidth * currentZoom;
                double imageHeight = source.PixelHeight * currentZoom;

                // 图片的边界框
                double imageLeft = imagePosition.X;
                double imageTop = imagePosition.Y;
                double imageRight = imageLeft + imageWidth;
                double imageBottom = imageTop + imageHeight;

                // 检查鼠标是否在图片区域内
                bool isInImageArea = mousePos.X >= imageLeft && mousePos.X <= imageRight &&
                                   mousePos.Y >= imageTop && mousePos.Y <= imageBottom;

                if (isInImageArea)
                {
                    // 在图片区域内，传递给原有的图片拖动处理
                    MainImage_MouseLeftButtonDown(sender, e);
                }
                else
                {
                    // 在空白区域，拖动整个窗口
                    try
                    {
                        this.DragMove();
                    }
                    catch (InvalidOperationException)
                    {
                        // 处理可能的拖动异常（比如快速点击时）
                    }
                    e.Handled = true;
                }
            }
            else
            {
                // 如果无法获取图片信息，默认拖动窗口
                this.DragMove();
                e.Handled = true;
            }
        }


        /// <summary>
        /// 保存为DDS格式
        /// </summary>
        private void SaveAsDds(BitmapSource source, string fileName)
        {
            string tempPngFile = null;
            try
            {
                Console.WriteLine("=== 开始DDS保存流程 ===");
                Console.WriteLine($"目标DDS文件: {fileName}");
                
                // 应用当前的旋转变换到图片
                BitmapSource finalSource = source;
                if (currentTransform != Transform.Identity)
                {
                    Console.WriteLine("应用图片旋转变换");
                    var transformedBitmap = new TransformedBitmap(source, currentTransform);
                    finalSource = transformedBitmap;
                }

                // 生成临时PNG文件名（使用不常见的扩展名防止用户误用）
                string tempDir = Path.GetTempPath();
                string tempFileName = $"nvtt_temp_{Guid.NewGuid():N}.tmp_png";
                tempPngFile = Path.Combine(tempDir, tempFileName);
                
                Console.WriteLine($"临时PNG文件路径: {tempPngFile}");

                // 保存为临时PNG文件
                Console.WriteLine("开始保存临时PNG文件...");
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(finalSource));
                using (var fileStream = new FileStream(tempPngFile, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
                
                Console.WriteLine($"临时PNG文件保存成功，大小: {new FileInfo(tempPngFile).Length} 字节");

                // 显示DDS预设选择对话框
                Console.WriteLine("显示DDS预设选择对话框...");
                var presetDialog = new DdsPresetDialog();
                var result = presetDialog.ShowDialog();
                
                if (result == true)
                {
                    if (presetDialog.IsCustomPanelSelected)
                    {
                        // 用户选择自定义面板
                        Console.WriteLine("用户选择自定义面板");
                        Console.WriteLine("注意: 临时文件将保留，供NVIDIA Texture Tools使用");
                        Console.WriteLine($"临时文件位置: {tempPngFile}");
                        Console.WriteLine("请在NVIDIA Texture Tools中完成转换后手动清理临时文件（如需要）");
                        
                        OpenNvttCustomPanel(tempPngFile);
                        
                        // 对于自定义面板，我们不删除临时文件，因为用户可能还在使用
                        // 显示提示信息
                        MessageBox.Show($"NVIDIA Texture Tools已启动。\n\n临时文件位置：\n{tempPngFile}\n\n请在完成转换后关闭工具。", 
                                      "自定义面板已启动", MessageBoxButton.OK, MessageBoxImage.Information);
                        return; // 直接返回，不删除临时文件
                    }
                    else if (presetDialog.SelectedPreset != null)
                    {
                        // 用户选择了预设
                        Console.WriteLine($"用户选择了预设: {presetDialog.SelectedPreset.Name}");
                        
						if (string.IsNullOrEmpty(fileName))
						{
                            // 生成默认输出文件名（与原图同目录，扩展名改为.dds）
                            fileName = Path.ChangeExtension(currentImagePath, ".dds");
                            

                        }
                        Console.WriteLine($"默认输出路径: {fileName}");

                        // 直接使用预设进行静默转换，不弹出保存对话框
                        ConvertToDdsWithPreset(tempPngFile, fileName, presetDialog.SelectedPreset.FilePath);



						ShowDdsSaveSuccessDialog(fileName);


                    }
                }
                else
                {
                    Console.WriteLine("用户取消了DDS保存操作");
                    return; // 用户取消，直接返回
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DDS保存过程中发生错误: {ex.Message}");
                Console.WriteLine($"错误堆栈: {ex.StackTrace}");
                throw new Exception($"DDS保存失败: {ex.Message}");
            }
            finally
            {
                // 清理临时文件（仅在非自定义面板模式下）
                // 注意：如果用户选择了自定义面板，临时文件已经在上面的代码中通过return跳过了这里
                if (!string.IsNullOrEmpty(tempPngFile) && File.Exists(tempPngFile))
                {
                    try
                    {
                        Console.WriteLine($"清理临时文件: {tempPngFile}");
                        File.Delete(tempPngFile);
                        Console.WriteLine("临时文件清理完成");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"删除临时文件失败: {ex.Message}");
                    }
                }
                Console.WriteLine("=== DDS保存流程结束 ===");
            }
        }

        /// <summary>
        /// 使用nvtt_export.exe转换PNG为DDS
        private void OpenNvttCustomPanel(string inputPngFile)
        {
            try
            {
                Console.WriteLine("--- OpenNvttCustomPanel 开始 ---");
                Console.WriteLine($"输入PNG文件: {inputPngFile}");
                
                // 获取nvtt_export.exe的路径
                string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) 
                    ?? AppDomain.CurrentDomain.BaseDirectory;
                string nvttPath = Path.Combine(exeDirectory, "NVIDIA Texture Tools", "nvtt_export.exe");
                
                Console.WriteLine($"nvtt_export.exe路径: {nvttPath}");

                if (!File.Exists(nvttPath))
                {
                    Console.WriteLine($"错误: nvtt_export.exe文件不存在!");
                    throw new FileNotFoundException($"找不到NVIDIA Texture Tools: {nvttPath}");
                }

                // 直接启动nvtt_export.exe并传入图片文件，让用户自定义设置
                Console.WriteLine("启动NVIDIA Texture Tools自定义面板...");
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = nvttPath,
                    Arguments = $"\"{inputPngFile}\"",  // 只传入图片文件，让用户自己设置
                    UseShellExecute = true,  // 使用Shell执行，这样可以正常显示GUI
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(nvttPath)
                };

                System.Diagnostics.Process.Start(processInfo);
                Console.WriteLine("NVIDIA Texture Tools自定义面板已启动");
                Console.WriteLine("--- OpenNvttCustomPanel 完成 ---");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OpenNvttCustomPanel发生错误: {ex.Message}");
                Console.WriteLine($"错误堆栈: {ex.StackTrace}");
                MessageBox.Show($"打开NVIDIA Texture Tools自定义面板时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 使用nvtt_export.exe转换PNG为DDS
        /// </summary>
        private void ConvertToDds(string inputPngFile, string outputDdsFile)
        {
            try
            {
                Console.WriteLine("--- ConvertToDds 开始 ---");
                Console.WriteLine($"输入PNG文件: {inputPngFile}");
                Console.WriteLine($"输出DDS文件: {outputDdsFile}");
                
                // 获取nvtt_export.exe的路径
                string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) 
                    ?? AppDomain.CurrentDomain.BaseDirectory;
                string nvttPath = Path.Combine(exeDirectory, "NVIDIA Texture Tools", "nvtt_export.exe");
                
                Console.WriteLine($"应用程序目录: {exeDirectory}");
                Console.WriteLine($"nvtt_export.exe路径: {nvttPath}");

                if (!File.Exists(nvttPath))
                {
                    Console.WriteLine($"错误: nvtt_export.exe文件不存在!");
                    throw new FileNotFoundException($"找不到NVIDIA Texture Tools: {nvttPath}");
                }
                
                Console.WriteLine("nvtt_export.exe文件存在，继续处理...");

                // 构建基本命令行参数：输入PNG文件 -> 输出DDS文件
                var args = new List<string>();
                args.Add($"\"{inputPngFile}\"");  // 输入的临时PNG文件
                args.Add("-o");
                args.Add($"\"{outputDdsFile}\""); // 输出的DDS文件
                
                Console.WriteLine("开始加载TOML配置...");
                
                // 加载TOML配置并添加额外参数
                var config = NvttConfigManager.LoadConfig();
                string selectedPresetName = NvttConfigManager.GetSelectedPresetName(config);
                
                Console.WriteLine($"选择的预设: {selectedPresetName}");
                
                if (config.Presets.ContainsKey(selectedPresetName))
                {
                    var preset = config.Presets[selectedPresetName];
                    Console.WriteLine("找到预设配置，添加参数:");
                    
                    // 添加格式参数
                    if (!string.IsNullOrEmpty(preset.Format))
                    {
                        args.Add("-f");
                        args.Add(preset.Format);
                        Console.WriteLine($"  格式: {preset.Format}");
                    }
                    
                    // 添加质量参数
                    if (!string.IsNullOrEmpty(preset.Quality))
                    {
                        args.Add("-q");
                        args.Add(preset.Quality);
                        Console.WriteLine($"  质量: {preset.Quality}");
                    }
                    
                    // 添加mipmap参数
                    if (preset.GenerateMipmaps)
                    {
                        args.Add("--mips");
                        Console.WriteLine("  启用mipmap生成");
                    }
                }
                else
                {
                    Console.WriteLine($"警告: 未找到预设 '{selectedPresetName}'，使用默认参数");
                }

                string commandArgs = string.Join(" ", args);
                Console.WriteLine($"完整命令行: \"{nvttPath}\" {commandArgs}");

                // 启动进程
                Console.WriteLine("启动nvtt_export.exe进程...");
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"\"{nvttPath}\"",  // 给exe路径加引号，防止空格问题
                    Arguments = commandArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(nvttPath)
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        Console.WriteLine("错误: 无法启动进程!");
                        throw new Exception("无法启动nvtt_export.exe进程");
                    }

                    Console.WriteLine($"进程已启动，PID: {process.Id}");
                    Console.WriteLine("等待进程完成...");

                    // 等待进程完成
                    process.WaitForExit();

                    Console.WriteLine($"进程已退出，退出代码: {process.ExitCode}");

                    // 读取输出
                    string standardOutput = process.StandardOutput.ReadToEnd();
                    string errorOutput = process.StandardError.ReadToEnd();
                    
                    if (!string.IsNullOrEmpty(standardOutput))
                    {
                        Console.WriteLine($"标准输出:\n{standardOutput}");
                    }
                    
                    if (!string.IsNullOrEmpty(errorOutput))
                    {
                        Console.WriteLine($"错误输出:\n{errorOutput}");
                    }

                    // 检查退出代码
                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"nvtt_export.exe执行失败 (退出代码: {process.ExitCode})\n错误输出: {errorOutput}\n标准输出: {standardOutput}");
                    }

                    // 验证输出文件是否生成
                    Console.WriteLine("检查输出文件是否生成...");
                    if (!File.Exists(outputDdsFile))
                    {
                        Console.WriteLine("错误: DDS文件未生成!");
                        throw new Exception("DDS文件未成功生成");
                    }
                    
                    var outputFileInfo = new FileInfo(outputDdsFile);
                    Console.WriteLine($"DDS文件生成成功! 大小: {outputFileInfo.Length} 字节");
                }
                
                Console.WriteLine("--- ConvertToDds 完成 ---");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConvertToDds发生错误: {ex.Message}");
                Console.WriteLine($"错误堆栈: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 解析相对路径为绝对路径（相对于程序exe所在目录）
        /// </summary>
        /// <param name="path">可能是相对路径或绝对路径的路径</param>
        /// <returns>解析后的绝对路径</returns>
        public static string ResolveExecutablePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // 如果已经是绝对路径，直接返回
            if (Path.IsPathRooted(path))
                return path;

            // 如果是相对路径，基于实际exe所在目录解析
            try
            {
                // 获取当前可执行文件的完整路径
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                // 获取可执行文件所在的目录
                string exeDirectory = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;

                string resolvedPath = Path.Combine(exeDirectory, path);
                return Path.GetFullPath(resolvedPath); // 规范化路径
            }
            catch
            {
                // 如果解析失败，返回原路径
                return path;
            }
        }

        private void MenuShowBgToolbar_Click(object sender, RoutedEventArgs e)
        {
            if (backgroundExpander != null && menuShowBgToolbar != null)
            {
                backgroundExpander.Visibility = menuShowBgToolbar.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void MenuShowSequenceToolbar_Click(object sender, RoutedEventArgs e)
        {
            if (sequenceExpander != null && menuShowSequenceToolbar != null)
            {
                sequenceExpander.Visibility = menuShowSequenceToolbar.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }


        private string ExecuteCommand(string command, string arguments, string workingDirectory = null)
        {
            try
            {
                // 检查 command 是否包含空格且没有明显路径分离
                string fileName = command;
                string args = arguments ?? string.Empty;

                // 如果 arguments 为空且 command 本身看起来像一整串命令，则拆分
                if (string.IsNullOrWhiteSpace(arguments) && command.Contains(" "))
                {
                    int firstSpace = command.IndexOf(' ');
                    fileName = command.Substring(0, firstSpace).Trim('"');
                    args = command.Substring(firstSpace + 1);
                }

                Console.WriteLine($"执行程序: {fileName}");
                Console.WriteLine($"参数: {args}");

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    if (process == null)
                        throw new Exception("无法启动进程");

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    Console.WriteLine($"退出代码: {process.ExitCode}");
                    if (!string.IsNullOrEmpty(output))
                        Console.WriteLine($"标准输出:\n{output}");
                    if (!string.IsNullOrEmpty(error))
                        Console.WriteLine($"错误输出:\n{error}");

                    return output + (string.IsNullOrEmpty(error) ? "" : "\nERROR:\n" + error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行命令时发生错误: {ex.Message}");
                throw;
            }
        }



        /// <summary>
        /// 执行CMD命令并获取实时输出
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <param name="arguments">命令参数</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <returns>命令输出结果</returns>
        private string ExecuteCommand2(string command, string arguments, string workingDirectory = null)
        {
            try
            {
                Console.WriteLine($"执行命令: {command} {arguments}");
                
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{command}\" {arguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        throw new Exception("无法启动命令进程");
                    }

                    // 发送回车键（如果需要的话）
                    //process.StandardInput.WriteLine();
                    //process.StandardInput.Close();

                    // 读取所有输出
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    
                    process.WaitForExit();
                    
                    Console.WriteLine($"命令执行完成，退出代码: {process.ExitCode}");
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        Console.WriteLine($"标准输出:\n{output}");
                    }
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"错误输出:\n{error}");
                    }

                    // 返回合并的输出
                    return output + (string.IsNullOrEmpty(error) ? "" : "\nERROR:\n" + error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行命令时发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 使用预设文件将PNG转换为DDS格式
        /// </summary>
        /// <param name="inputPngFile">输入的PNG文件路径</param>
        /// <param name="outputDdsFile">输出的DDS文件路径（可选，如果为空则让用户选择）</param>
        /// <param name="presetPath">预设文件路径</param>
        private void ConvertToDdsWithPreset(string inputPngFile, string outputDdsFile, string presetPath)
        {
            try
            {
                Console.WriteLine("--- ConvertToDdsWithPreset 开始 ---");
                Console.WriteLine($"输入PNG文件: {inputPngFile}");
                Console.WriteLine($"输出DDS文件: {outputDdsFile}");
                Console.WriteLine($"预设文件: {presetPath}");

                // nvtt_export.exe 路径
                string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                                      ?? AppDomain.CurrentDomain.BaseDirectory;
                string nvttPath = Path.Combine(exeDirectory, "NVIDIA Texture Tools", "nvtt_export.exe");

                Console.WriteLine($"应用程序目录: {exeDirectory}");
                Console.WriteLine($"nvtt_export.exe路径: {nvttPath}");

                if (!File.Exists(nvttPath))
                    throw new FileNotFoundException($"找不到 NVIDIA Texture Tools: {nvttPath}");

                // 若未指定输出路径，让用户选择一次
                if (string.IsNullOrWhiteSpace(outputDdsFile))
                {
                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "保存DDS文件",
                        Filter = "DDS文件 (*.dds)|*.dds",
                        DefaultExt = ".dds"
                    };
                    if (saveDialog.ShowDialog() != true)
                    {
                        Console.WriteLine("用户取消了保存操作");
                        return;
                    }
                    outputDdsFile = saveDialog.FileName; //这个是保存对话框
                }

                // 确保目标目录存在

                Directory.CreateDirectory(Path.GetDirectoryName(outputDdsFile));

                // 临时目录（规避中文/空格）
                string tempDir = Path.Combine(Path.GetTempPath(), "nvtt_temp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(tempDir);
                Console.WriteLine($"创建临时目录: {tempDir}");

                // 复制 preset / 输入 到临时目录（只为输入稳定性；输出直写到最终路径）
                string tempPresetPath = Path.Combine(tempDir, Path.GetFileName(presetPath));
                File.Copy(presetPath, tempPresetPath, true);
                Console.WriteLine($"预设文件复制到: {tempPresetPath}");

                // 直接把 --output 指向最终输出
                string arguments = $"\"{inputPngFile}\" --preset \"{tempPresetPath}\" --output \"{outputDdsFile}\"";
                Console.WriteLine($"完整命令: {nvttPath} {arguments}");

                // 不用 cmd，直接启动；注意 FileName 不要加引号
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = nvttPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = tempDir,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                string stdOut, stdErr;
                int exitCode;
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null) throw new InvalidOperationException("无法启动 nvtt_export 进程。");
                    stdOut = p.StandardOutput.ReadToEnd();
                    stdErr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                }

                Console.WriteLine($"退出代码: {exitCode}");
                if (!string.IsNullOrEmpty(stdOut)) Console.WriteLine("[STDOUT]\n" + stdOut);
                if (!string.IsNullOrEmpty(stdErr)) Console.WriteLine("[STDERR]\n" + stdErr);

                // 成功条件：退出码==0 且 目标文件存在
                if (exitCode != 0 || !File.Exists(outputDdsFile))
                    throw new Exception($"nvtt_export 失败或未生成输出文件：{outputDdsFile}");

                var fi = new FileInfo(outputDdsFile);
                Console.WriteLine($"DDS生成成功：{fi.FullName}（{fi.Length} 字节）");

                // 清理临时目录
                try { Directory.Delete(tempDir, true); Console.WriteLine("临时目录清理完成"); } catch { /* 忽略 */ }

                Console.WriteLine("--- ConvertToDdsWithPreset 完成 ---");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConvertToDdsWithPreset发生错误: {ex.Message}");
                Console.WriteLine($"错误堆栈: {ex.StackTrace}");
                throw;
            }
        }


        #endregion
    }
}
