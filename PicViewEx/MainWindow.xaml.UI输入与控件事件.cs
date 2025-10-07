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
					else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
						BtnSearch_Click(sender, e);
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
								statusText.Text = "序列帧播放已停止，切换到新图片";
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
						statusText.Text = "序列帧播放已停止，切换到新图片";
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
			dialog.Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp|TIFF|*.tiff|GIF|*.gif";

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
					statusText.Text = $"已保存: {Path.GetFileName(fileName)}";
			}
			catch (Exception ex)
			{
				MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
				if (statusText != null)
					statusText.Text = "保存失败";
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

		private void BtnSearch_Click(object sender, RoutedEventArgs e)
		{
			RecordToolUsage("Search");
			if (searchPanel != null && txtSearch != null)
			{
				searchPanel.Visibility = searchPanel.Visibility == Visibility.Visible ?
					Visibility.Collapsed : Visibility.Visible;

				if (searchPanel.Visibility == Visibility.Visible)
				{
					txtSearch.Focus();
				}
			}
		}

		private void BtnCloseSearch_Click(object sender, RoutedEventArgs e)
		{
			if (searchPanel != null)
				searchPanel.Visibility = Visibility.Collapsed;
		}

		private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && txtSearch != null)
			{
				PerformEverythingSearch(txtSearch.Text);
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
						statusText.Text = "默认图片不存在，使用渐变背景";
					else if (!string.IsNullOrEmpty(result.SourcePath))
						statusText.Text = $"已加载默认背景图片: {Path.GetFileName(result.SourcePath)}";
				}
			}
			catch (Exception ex)
			{
				if (updateStatus && statusText != null)
					statusText.Text = $"加载默认背景图片失败: {ex.Message}";
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
                    statusText.Text = $"打开方式设置已更新，共 {openWithApps.Count} 个应用";
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


        #endregion
    }
}
