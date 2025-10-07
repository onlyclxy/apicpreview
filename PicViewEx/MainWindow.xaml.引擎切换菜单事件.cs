using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PicViewEx
{
    public partial class MainWindow
    {
        #region 引擎切换菜单事件

        private void MenuEngineMagick_Click(object sender, RoutedEventArgs e)
        {
            if (menuEngineMagick != null && menuEngineLeadtools != null)
            {
                // 设置菜单状态
                menuEngineMagick.IsChecked = true;
                menuEngineLeadtools.IsChecked = false;

                // 切换引擎
                if (imageLoader != null)
                {
                    imageLoader.SwitchEngine(ImageLoader.ImageEngine.Magick);
                }

                // 保存设置
                if (appSettings != null)
                {
                    appSettings.ImageEngine = "Magick";
                    SettingsManager.SaveSettings(appSettings);
                }

                // 更新状态栏
                if (statusText != null)
                {
                    statusText.Text = "已切换到 ImageMagick 引擎";
                }
            }
        }

        private void MenuEngineLeadtools_Click(object sender, RoutedEventArgs e)
        {
            if (menuEngineMagick != null && menuEngineLeadtools != null)
            {
                // 设置菜单状态
                menuEngineMagick.IsChecked = false;
                menuEngineLeadtools.IsChecked = true;

                // 切换引擎
                if (imageLoader != null)
                {
                    imageLoader.SwitchEngine(ImageLoader.ImageEngine.Leadtools);
                }

                // 保存设置
                if (appSettings != null)
                {
                    appSettings.ImageEngine = "Leadtools";
                    SettingsManager.SaveSettings(appSettings);
                }

                // 更新状态栏
                if (statusText != null)
                {
                    statusText.Text = "已切换到 LEADTOOLS 引擎";
                }
            }
        }

        #endregion

        /// <summary>
        /// 更新引擎菜单状态
        /// </summary>
        private void UpdateEngineMenuState()
        {
            if (imageLoader != null && menuEngineMagick != null && menuEngineLeadtools != null)
            {
                var currentEngine = imageLoader.GetCurrentEngine();
                menuEngineMagick.IsChecked = (currentEngine == ImageLoader.ImageEngine.Magick);
                menuEngineLeadtools.IsChecked = (currentEngine == ImageLoader.ImageEngine.Leadtools);
            }
        }
    }
}
