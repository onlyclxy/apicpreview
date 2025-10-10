using System;
using System.Windows;


namespace PicViewEx
{
    public partial class MainWindow
    {

        private void LoadAppSettings()
        {
            try
            {
                isLoadingSettings = true;
                appSettings = SettingsManager.LoadSettings();

                // 应用窗口设置
                if (appSettings.WindowWidth > 0 && appSettings.WindowHeight > 0)
                {
                    this.Width = appSettings.WindowWidth;
                    this.Height = appSettings.WindowHeight;
                }

                if (appSettings.WindowLeft > 0 && appSettings.WindowTop > 0)
                {
                    this.Left = appSettings.WindowLeft;
                    this.Top = appSettings.WindowTop;
                }

                if (appSettings.IsMaximized)
                    this.WindowState = WindowState.Maximized;

                //// 恢复显示通道状态
                //if (appSettings.ShowChannels)
                //{
                //    chkShowChannels.IsChecked = true;
                //}

                // 延迟恢复控件状态，确保所有控件都已初始化
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 先恢复非背景控件状态
                    SettingsManager.RestoreControlStates(this, appSettings);

                    // 特殊处理背景设置 - 按正确的优先级顺序
                    RestoreBackgroundSettingsWithPriority();

                    // 恢复图像查看状态
                    if (appSettings.LastZoomLevel > 0)
                    {
                        currentZoom = appSettings.LastZoomLevel;
                        UpdateZoomText();
                    }

                    if (appSettings.RememberImagePosition)
                    {
                        imagePosition.X = appSettings.LastImageX;
                        imagePosition.Y = appSettings.LastImageY;
                    }

                    // 恢复打开方式应用
                    openWithApps.Clear();
                    foreach (var appData in appSettings.OpenWithApps)
                    {
                        openWithApps.Add(new OpenWithApp
                        {
                            Name = appData.Name,
                            ExecutablePath = appData.ExecutablePath,
                            Arguments = appData.Arguments,
                            ShowText = appData.ShowText,
                            IconPath = appData.IconPath
                        });
                    }
                    UpdateOpenWithButtons();
                    UpdateOpenWithMenu();

                    // ✅ 只在这里由“单一真相”属性驱动 UI（内部会调用 SyncChannelUI 抑制事件重入）
                    ShowChannels = (appSettings?.ShowChannels ?? false);

                    // 如该方法会同步其它菜单项，保留；不要在里面反向写回 ShowChannels
                    SynchronizeToolMenuStates();



                    isLoadingSettings = false;
                  



                    if (statusText != null)
                        UpdateStatusText($"设置已加载 - 控件状态: {appSettings.ControlStates.Count} 项");

                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                isLoadingSettings = false;
                appSettings = new AppSettings();
                if (statusText != null)
                    UpdateStatusText($"加载设置失败，使用默认设置: {ex.Message}");
            }
        }

        private void SaveAppSettings()
        {
            if (isLoadingSettings || appSettings == null) return;

            try
            {
                // 保存窗口状态
                if (this.WindowState == WindowState.Normal)
                {
                    appSettings.WindowWidth = this.Width;
                    appSettings.WindowHeight = this.Height;
                    appSettings.WindowLeft = this.Left;
                    appSettings.WindowTop = this.Top;
                }
                appSettings.IsMaximized = this.WindowState == WindowState.Maximized;

                // 保存图像查看状态
                appSettings.LastZoomLevel = currentZoom;
                if (appSettings.RememberImagePosition)
                {
                    appSettings.LastImageX = imagePosition.X;
                    appSettings.LastImageY = imagePosition.Y;
                }

                // 保存当前文件到最近文件列表
                if (!string.IsNullOrEmpty(currentImagePath))
                {
                    SettingsManager.AddRecentFile(appSettings, currentImagePath);
                }

                // 保存打开方式应用
                appSettings.OpenWithApps.Clear();
                foreach (var app in openWithApps)
                {
                    appSettings.OpenWithApps.Add(new OpenWithAppData
                    {
                        Name = app.Name,
                        ExecutablePath = app.ExecutablePath,
                        Arguments = app.Arguments,
                        ShowText = app.ShowText,
                        IconPath = app.IconPath
                    });
                }

                // 统一保存所有控件状态 - 这是新的核心功能
                SettingsManager.SaveControlStates(this, appSettings);

                // 保存设置到文件
                SettingsManager.SaveSettings(appSettings);

                if (statusText != null)
                    UpdateStatusText($"设置已保存 - 控件状态: {appSettings.ControlStates.Count} 项");
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    UpdateStatusText($"保存设置失败: {ex.Message}");
            }
        }

    }
}
