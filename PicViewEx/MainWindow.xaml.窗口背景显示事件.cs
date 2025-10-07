using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PicViewEx
{
    public partial class MainWindow
    {      // 按正确优先级恢复背景设置
        private void RestoreBackgroundSettingsWithPriority()
        {
            try
            {
                // 第一步：恢复背景图片路径（如果有的话）
                if (!string.IsNullOrEmpty(appSettings.BackgroundImagePath) && File.Exists(appSettings.BackgroundImagePath))
                {
                    ApplyBackgroundImageFromPath(appSettings.BackgroundImagePath);
                }

                // 第二步：恢复颜色值（禁用事件处理器以防止自动切换背景类型）
                RestoreColorValues();

                // 第三步：根据颜色值更新派生控件
                UpdateDerivedColorControls();

                // 第四步：最后恢复背景类型选择（这是最高优先级）
                RestoreBackgroundType();

                // 第五步：应用最终的背景设置
                UpdateBackground();
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    statusText.Text = $"恢复背景设置失败: {ex.Message}";
            }
        }

        // 恢复颜色值（不触发事件）
        private void RestoreColorValues()
        {
            // 临时移除事件处理器
            if (sliderHue != null)
            {
                sliderHue.ValueChanged -= ColorSlider_ValueChanged;
                if (appSettings.ControlStates.ContainsKey("sliderHue") &&
                    appSettings.ControlStates["sliderHue"].ContainsKey("Value"))
                {
                    var value = Convert.ToDouble(appSettings.ControlStates["sliderHue"]["Value"]);
                    sliderHue.Value = value;
                }
                sliderHue.ValueChanged += ColorSlider_ValueChanged;
            }

            if (sliderSaturation != null)
            {
                sliderSaturation.ValueChanged -= ColorSlider_ValueChanged;
                if (appSettings.ControlStates.ContainsKey("sliderSaturation") &&
                    appSettings.ControlStates["sliderSaturation"].ContainsKey("Value"))
                {
                    var value = Convert.ToDouble(appSettings.ControlStates["sliderSaturation"]["Value"]);
                    sliderSaturation.Value = value;
                }
                sliderSaturation.ValueChanged += ColorSlider_ValueChanged;
            }

            if (sliderBrightness != null)
            {
                sliderBrightness.ValueChanged -= ColorSlider_ValueChanged;
                if (appSettings.ControlStates.ContainsKey("sliderBrightness") &&
                    appSettings.ControlStates["sliderBrightness"].ContainsKey("Value"))
                {
                    var value = Convert.ToDouble(appSettings.ControlStates["sliderBrightness"]["Value"]);
                    sliderBrightness.Value = value;
                }
                sliderBrightness.ValueChanged += ColorSlider_ValueChanged;
            }

            // 根据HSV值重建当前背景画刷
            if (sliderHue != null && sliderSaturation != null && sliderBrightness != null)
            {
                double hue = sliderHue.Value;
                double saturation = sliderSaturation.Value / 100.0;
                double brightness = sliderBrightness.Value / 100.0;

                Color color = HsvToRgb(hue, saturation, brightness);
                currentBackgroundBrush = new SolidColorBrush(color);
            }
        }

        // 更新派生颜色控件
        private void UpdateDerivedColorControls()
        {
            if (sliderHue != null && sliderSaturation != null && sliderBrightness != null)
            {
                double hue = sliderHue.Value;

                // 更新快速选色滑块
                if (sliderColorSpectrum != null)
                {
                    sliderColorSpectrum.ValueChanged -= ColorSpectrum_ValueChanged;
                    if (appSettings.ControlStates.ContainsKey("sliderColorSpectrum") &&
                        appSettings.ControlStates["sliderColorSpectrum"].ContainsKey("Value"))
                    {
                        var value = Convert.ToDouble(appSettings.ControlStates["sliderColorSpectrum"]["Value"]);
                        sliderColorSpectrum.Value = value;
                    }
                    else
                    {
                        sliderColorSpectrum.Value = hue;
                    }
                    sliderColorSpectrum.ValueChanged += ColorSpectrum_ValueChanged;
                }

                // 更新颜色选择器
                if (colorPicker != null)
                {
                    colorPicker.SelectedColorChanged -= ColorPicker_SelectedColorChanged;
                    if (appSettings.ControlStates.ContainsKey("colorPicker") &&
                        appSettings.ControlStates["colorPicker"].ContainsKey("SelectedColor"))
                    {
                        var colorString = appSettings.ControlStates["colorPicker"]["SelectedColor"].ToString();
                        if (!string.IsNullOrEmpty(colorString))
                        {
                            try
                            {
                                var color = (Color)ColorConverter.ConvertFromString(colorString);
                                colorPicker.SelectedColor = color;
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        // 如果没有保存的颜色选择器值，使用当前HSV值
                        double saturation = sliderSaturation.Value / 100.0;
                        double brightness = sliderBrightness.Value / 100.0;
                        Color color = HsvToRgb(hue, saturation, brightness);
                        colorPicker.SelectedColor = color;
                    }
                    colorPicker.SelectedColorChanged += ColorPicker_SelectedColorChanged;
                }
            }
        }

        // 恢复背景类型（最后执行，覆盖之前的任何自动切换）
        private void RestoreBackgroundType()
        {
            // 临时移除事件处理器以防止触发更新
            if (rbTransparent != null) rbTransparent.Checked -= BackgroundType_Changed;
            if (rbSolidColor != null) rbSolidColor.Checked -= BackgroundType_Changed;
            if (rbImageBackground != null) rbImageBackground.Checked -= BackgroundType_Changed;
            if (rbWindowTransparent != null) rbWindowTransparent.Checked -= BackgroundType_Changed;

            // 恢复RadioButton状态
            if (appSettings.ControlStates.ContainsKey("rbTransparent"))
            {
                var isChecked = Convert.ToBoolean(appSettings.ControlStates["rbTransparent"]["IsChecked"]);
                if (rbTransparent != null) rbTransparent.IsChecked = isChecked;
            }
            if (appSettings.ControlStates.ContainsKey("rbSolidColor"))
            {
                var isChecked = Convert.ToBoolean(appSettings.ControlStates["rbSolidColor"]["IsChecked"]);
                if (rbSolidColor != null) rbSolidColor.IsChecked = isChecked;
            }
            if (appSettings.ControlStates.ContainsKey("rbImageBackground"))
            {
                var isChecked = Convert.ToBoolean(appSettings.ControlStates["rbImageBackground"]["IsChecked"]);
                if (rbImageBackground != null) rbImageBackground.IsChecked = isChecked;
            }
            if (appSettings.ControlStates.ContainsKey("rbWindowTransparent"))
            {
                var isChecked = Convert.ToBoolean(appSettings.ControlStates["rbWindowTransparent"]["IsChecked"]);
                if (rbWindowTransparent != null) rbWindowTransparent.IsChecked = isChecked;
            }

            // 恢复事件处理器
            if (rbTransparent != null) rbTransparent.Checked += BackgroundType_Changed;
            if (rbSolidColor != null) rbSolidColor.Checked += BackgroundType_Changed;
            if (rbImageBackground != null) rbImageBackground.Checked += BackgroundType_Changed;
            if (rbWindowTransparent != null) rbWindowTransparent.Checked += BackgroundType_Changed;
        }


        private void ApplyBackgroundImageFromPath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                if (appSettings != null)
                    appSettings.BackgroundImagePath = "";

                if (statusText != null)
                    statusText.Text = "背景图片路径无效，已恢复默认背景";

                ApplyDefaultBackgroundImage();

                if (rbImageBackground != null)
                    rbImageBackground.IsChecked = true;

                UpdateBackground();
                return;
            }

            try
            {
                var result = imageLoader.LoadBackgroundImage(imagePath);
                backgroundImageBrush = result.Brush;

                if (appSettings != null)
                    appSettings.BackgroundImagePath = imagePath;

                if (rbImageBackground != null)
                    rbImageBackground.IsChecked = true;

                UpdateBackground();

                if (statusText != null)
                    statusText.Text = $"背景图片已设置: {Path.GetFileName(result.SourcePath ?? imagePath)}";
            }
            catch (Exception ex)
            {
                if (appSettings != null)
                    appSettings.BackgroundImagePath = "";

                if (statusText != null)
                    statusText.Text = $"加载背景图片失败，尝试使用默认图片: {ex.Message}";

                ApplyDefaultBackgroundImage();

                if (rbImageBackground != null)
                    rbImageBackground.IsChecked = true;

                UpdateBackground();
            }
        }

        private void UpdateBackground()
        {
            if (imageContainer == null) return;

            if (rbTransparent?.IsChecked == true)
            {
                try
                {
                    var resource = FindResource("CheckerboardBrush");
                    if (resource is System.Windows.Media.Brush brush)
                    {
                        imageContainer.Background = brush;
                    }
                }
                catch
                {
                    imageContainer.Background = System.Windows.Media.Brushes.LightGray;
                }

                // 恢复正常窗口背景
                this.Background = System.Windows.Media.Brushes.White;
            }
            else if (rbSolidColor?.IsChecked == true)
            {
                imageContainer.Background = currentBackgroundBrush;

                // 恢复正常窗口背景
                this.Background = System.Windows.Media.Brushes.White;
            }
            else if (rbImageBackground?.IsChecked == true)
            {
                // 如果没有背景图片，先尝试加载默认图片
                if (backgroundImageBrush == null)
                {
                    ApplyDefaultBackgroundImage(updateBackground: false, updateStatus: false);
                }

                // 应用背景图片
                if (backgroundImageBrush != null)
                {
                    imageContainer.Background = backgroundImageBrush;
                }
                else
                {
                    // 如果还是没有背景图片，使用浅灰色作为后备
                    imageContainer.Background = System.Windows.Media.Brushes.LightGray;
                }

                // 恢复正常窗口背景
                this.Background = System.Windows.Media.Brushes.White;
            }
            else if (rbWindowTransparent?.IsChecked == true)
            {
                // 设置画布背景为透明
                imageContainer.Background = System.Windows.Media.Brushes.Transparent;

                // 设置整个窗口背景为透明
                this.Background = System.Windows.Media.Brushes.Transparent;
            }
        }

    }
}
