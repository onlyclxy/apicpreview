using System;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;

namespace PicView
{
    public class ControlState
    {
        public string ControlName { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public object? Value { get; set; }
        public bool ShouldSave { get; set; } = true;
    }

    public class AppSettings
    {
        // 背景设置
        public string BackgroundType { get; set; } = "Transparent"; // Transparent, SolidColor, Image, WindowTransparent
        public string BackgroundColor { get; set; } = "#808080"; // 默认中性灰
        public double BackgroundHue { get; set; } = 0;
        public double BackgroundSaturation { get; set; } = 0;
        public double BackgroundBrightness { get; set; } = 50;
        public string BackgroundImagePath { get; set; } = "";
        
        // UI面板状态
        public bool ShowChannels { get; set; } = false;
        public bool BackgroundPanelExpanded { get; set; } = true;
        public bool SearchPanelVisible { get; set; } = false;
        
        // 窗口状态
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public bool IsMaximized { get; set; } = false;
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        
        // 图像查看设置
        public double LastZoomLevel { get; set; } = 1.0;
        public string LastViewMode { get; set; } = "FitWindow"; // FitWindow, ActualSize, Custom
        public bool RememberImagePosition { get; set; } = true;
        public double LastImageX { get; set; } = 0;
        public double LastImageY { get; set; } = 0;
        
        // 最近使用的工具和功能
        public string LastUsedTool { get; set; } = ""; // 记录最后点击的工具按钮
        public string LastBackgroundPreset { get; set; } = ""; // 最后使用的背景预设
        public bool LastFullScreenState { get; set; } = false;
        public List<string> RecentlyUsedTools { get; set; } = new();
        public List<string> RecentFiles { get; set; } = new();
        
        // 打开方式应用列表
        public List<OpenWithAppData> OpenWithApps { get; set; } = new();
        
        // 界面偏好设置
        public bool AutoSaveSettings { get; set; } = true; // 自动保存设置
        public int MaxRecentFiles { get; set; } = 10; // 最大最近文件数
        public int MaxRecentTools { get; set; } = 5; // 最大最近工具数
        
        // 搜索设置
        public string LastSearchQuery { get; set; } = "";
        public bool RememberLastSearch { get; set; } = true;
        
        // 序列帧播放设置
        public bool SequencePlayerExpanded { get; set; } = false;
        public int LastGridWidth { get; set; } = 3;
        public int LastGridHeight { get; set; } = 3;
        public int LastSequenceFPS { get; set; } = 10;
        public string LastGridPreset { get; set; } = "3×3";
        
        // 控件状态字典 - 存储所有控件的状态
        public Dictionary<string, Dictionary<string, object?>> ControlStates { get; set; } = new();
    }

    public class OpenWithAppData
    {
        public string Name { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public string Arguments { get; set; } = "\"{0}\"";
        public bool ShowText { get; set; } = true;
        public string IconPath { get; set; } = "";
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFileName = "PicViewSettings.json";
        
        // 控件保存配置 - 可以配置哪些控件的哪些属性要保存
        private static readonly Dictionary<string, List<string>> ControlSaveConfig = new()
        {
            // 复选框
            ["chkShowChannels"] = new() { "IsChecked" },
            ["menuShowChannels"] = new() { "IsChecked" },
            
            // 面板状态
            ["backgroundExpander"] = new() { "IsExpanded" },
            ["sequenceExpander"] = new() { "IsExpanded" },
            ["searchPanel"] = new() { "Visibility" },
            ["channelPanel"] = new() { "Visibility" },
            ["channelColumn"] = new() { "Width" },
            
            // 搜索框
            ["txtSearch"] = new() { "Text" },
            
            // 序列帧控件
            ["txtGridWidth"] = new() { "Text" },
            ["txtGridHeight"] = new() { "Text" },
            ["txtFPS"] = new() { "Text" },
            ["cbGridPresets"] = new() { "SelectedIndex" },
            
            // 打开方式按钮
            ["btnOpenWith1"] = new() { "Content", "Visibility" },
            ["btnOpenWith2"] = new() { "Content", "Visibility" },
            ["btnOpenWith3"] = new() { "Content", "Visibility" },
            
            // 菜单项
            ["menuExpandBgPanel"] = new() { "IsChecked" },
            ["menuShowSequencePlayer"] = new() { "IsChecked" },
        };

        // 背景设置的优先级恢复配置
        private static readonly Dictionary<string, List<string>> BackgroundControlsConfig = new()
        {
            // 第一优先级：基础颜色值（这些不会触发自动切换）
            ["sliderHue"] = new() { "Value" },
            ["sliderSaturation"] = new() { "Value" },
            ["sliderBrightness"] = new() { "Value" },
            
            // 第二优先级：派生控件（这些会根据基础值自动更新）
            ["sliderColorSpectrum"] = new() { "Value" },
            ["colorPicker"] = new() { "SelectedColor" },
            
            // 第三优先级：背景类型（最后恢复，覆盖任何自动切换）
            ["rbTransparent"] = new() { "IsChecked" },
            ["rbSolidColor"] = new() { "IsChecked" },
            ["rbImageBackground"] = new() { "IsChecked" },
            ["rbWindowTransparent"] = new() { "IsChecked" },
            
            // 菜单同步
            ["menuBgTransparent"] = new() { "IsChecked" },
            ["menuBgSolid"] = new() { "IsChecked" },
            ["menuBgImage"] = new() { "IsChecked" },
            ["menuBgWindowTransparent"] = new() { "IsChecked" },
        };
        
        // 获取exe所在目录的设置文件路径
        private static string GetSettingsPath()
        {
            try
            {
                string exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? 
                                     AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(exeDirectory, SettingsFileName);
            }
            catch
            {
                string fallbackDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "PicView");
                return Path.Combine(fallbackDirectory, SettingsFileName);
            }
        }

        public static AppSettings LoadSettings()
        {
            try
            {
                string settingsPath = GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        ValidateAndCleanSettings(settings);
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
            }
            
            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                string settingsPath = GetSettingsPath();
                string directory = Path.GetDirectoryName(settingsPath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                ValidateAndCleanSettings(settings);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(settingsPath, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }

        // 保存窗口中所有配置的控件状态
        public static void SaveControlStates(Window window, AppSettings settings)
        {
            try
            {
                // 清空现有的控件状态
                settings.ControlStates.Clear();
                
                foreach (var kvp in ControlSaveConfig)
                {
                    string controlName = kvp.Key;
                    var properties = kvp.Value;
                    
                    // 通过名称查找控件
                    var control = window.FindName(controlName);
                    if (control != null)
                    {
                        var controlStates = new Dictionary<string, object?>();
                        
                        foreach (string propertyName in properties)
                        {
                            try
                            {
                                var property = control.GetType().GetProperty(propertyName);
                                if (property != null && property.CanRead)
                                {
                                    object? value = property.GetValue(control);
                                    
                                    // 转换特殊类型为可序列化的类型
                                    if (value is Color color)
                                    {
                                        value = color.ToString();
                                    }
                                    else if (value is GridLength gridLength)
                                    {
                                        value = gridLength.Value;
                                    }
                                    else if (value is Visibility visibility)
                                    {
                                        value = visibility.ToString();
                                    }
                                    
                                    controlStates[propertyName] = value;
                                    System.Diagnostics.Debug.WriteLine($"已保存普通控件 {controlName}.{propertyName} = {value}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"属性不存在或不可读: {controlName}.{propertyName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"保存控件 {controlName}.{propertyName} 失败: {ex.Message}");
                            }
                        }
                        
                        if (controlStates.Count > 0)
                        {
                            settings.ControlStates[controlName] = controlStates;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"控件未找到: {controlName}");
                    }
                }

                // 保存背景控件
                foreach (var kvp in BackgroundControlsConfig)
                {
                    string controlName = kvp.Key;
                    var properties = kvp.Value;
                    
                    // 通过名称查找控件
                    var control = window.FindName(controlName);
                    if (control != null)
                    {
                        var controlStates = new Dictionary<string, object?>();
                        
                        foreach (string propertyName in properties)
                        {
                            try
                            {
                                var property = control.GetType().GetProperty(propertyName);
                                if (property != null && property.CanRead)
                                {
                                    object? value = property.GetValue(control);
                                    
                                    // 转换特殊类型为可序列化的类型
                                    if (value is Color color)
                                    {
                                        value = color.ToString();
                                    }
                                    else if (value is GridLength gridLength)
                                    {
                                        value = gridLength.Value;
                                    }
                                    else if (value is Visibility visibility)
                                    {
                                        value = visibility.ToString();
                                    }
                                    
                                    controlStates[propertyName] = value;
                                    System.Diagnostics.Debug.WriteLine($"已保存背景控件 {controlName}.{propertyName} = {value}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"属性不存在或不可读: {controlName}.{propertyName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"保存控件 {controlName}.{propertyName} 失败: {ex.Message}");
                            }
                        }
                        
                        if (controlStates.Count > 0)
                        {
                            settings.ControlStates[controlName] = controlStates;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"背景控件未找到: {controlName}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"保存了 {settings.ControlStates.Count} 个控件的状态");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存控件状态失败: {ex.Message}");
            }
        }

        // 恢复窗口中所有配置的控件状态
        public static void RestoreControlStates(Window window, AppSettings settings)
        {
            try
            {
                int restoredCount = 0;
                
                foreach (var kvp in settings.ControlStates)
                {
                    string controlName = kvp.Key;
                    var controlStates = kvp.Value;
                    
                    // 通过名称查找控件
                    var control = window.FindName(controlName);
                    if (control != null)
                    {
                        foreach (var stateKvp in controlStates)
                        {
                            string propertyName = stateKvp.Key;
                            object? value = stateKvp.Value;
                            
                            try
                            {
                                var property = control.GetType().GetProperty(propertyName);
                                if (property != null && property.CanWrite && value != null)
                                {
                                    // 类型转换
                                    object? convertedValue = ConvertValue(value, property.PropertyType);
                                    if (convertedValue != null)
                                    {
                                        property.SetValue(control, convertedValue);
                                        restoredCount++;
                                        System.Diagnostics.Debug.WriteLine($"已恢复 {controlName}.{propertyName} = {convertedValue}");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"类型转换失败: {controlName}.{propertyName} value={value} targetType={property.PropertyType}");
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"属性不可写或值为空: {controlName}.{propertyName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"恢复控件 {controlName}.{propertyName} 失败: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"恢复时控件未找到: {controlName}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"恢复了 {restoredCount} 个控件属性");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"恢复控件状态失败: {ex.Message}");
            }
        }

        // 类型转换辅助方法
        private static object? ConvertValue(object value, Type targetType)
        {
            try
            {
                if (targetType == typeof(Color) || targetType == typeof(Color?))
                {
                    if (value is string colorString)
                    {
                        return (Color)ColorConverter.ConvertFromString(colorString);
                    }
                }
                else if (targetType == typeof(GridLength))
                {
                    if (value is double doubleValue)
                    {
                        return new GridLength(doubleValue);
                    }
                    else if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
                    {
                        return new GridLength(jsonElement.GetDouble());
                    }
                }
                else if (targetType == typeof(Visibility))
                {
                    if (value is string visibilityString)
                    {
                        return Enum.Parse<Visibility>(visibilityString);
                    }
                }
                else if (targetType == typeof(bool) || targetType == typeof(bool?))
                {
                    if (value is JsonElement jsonElement)
                    {
                        if (jsonElement.ValueKind == JsonValueKind.True) return true;
                        if (jsonElement.ValueKind == JsonValueKind.False) return false;
                        if (jsonElement.ValueKind == JsonValueKind.Null) return null;
                    }
                    else if (value is bool boolValue)
                    {
                        return boolValue;
                    }
                    else if (value == null && targetType == typeof(bool?))
                    {
                        return null;
                    }
                    
                    return Convert.ToBoolean(value);
                }
                else if (targetType == typeof(double) || targetType == typeof(double?))
                {
                    if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
                    {
                        return jsonElement.GetDouble();
                    }
                    return Convert.ToDouble(value);
                }
                else if (targetType == typeof(string))
                {
                    return value?.ToString();
                }
                else
                {
                    return Convert.ChangeType(value, targetType);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"类型转换异常: {value} -> {targetType}: {ex.Message}");
                return null;
            }
            
            return null; // 默认返回值
        }

        // 添加或移除要保存的控件
        public static void SetControlSaveState(string controlName, string propertyName, bool shouldSave)
        {
            if (shouldSave)
            {
                if (!ControlSaveConfig.ContainsKey(controlName))
                {
                    ControlSaveConfig[controlName] = new List<string>();
                }
                if (!ControlSaveConfig[controlName].Contains(propertyName))
                {
                    ControlSaveConfig[controlName].Add(propertyName);
                }
            }
            else
            {
                if (ControlSaveConfig.ContainsKey(controlName))
                {
                    ControlSaveConfig[controlName].Remove(propertyName);
                    if (ControlSaveConfig[controlName].Count == 0)
                    {
                        ControlSaveConfig.Remove(controlName);
                    }
                }
            }
        }

        private static void ValidateAndCleanSettings(AppSettings settings)
        {
            // 限制最近文件和工具的数量
            if (settings.RecentFiles.Count > settings.MaxRecentFiles)
            {
                settings.RecentFiles = settings.RecentFiles.GetRange(0, settings.MaxRecentFiles);
            }
            
            if (settings.RecentlyUsedTools.Count > settings.MaxRecentTools)
            {
                settings.RecentlyUsedTools = settings.RecentlyUsedTools.GetRange(0, settings.MaxRecentTools);
            }

            // 验证窗口尺寸
            if (settings.WindowWidth < 400) settings.WindowWidth = 1200;
            if (settings.WindowHeight < 300) settings.WindowHeight = 800;
            
            // 验证缩放级别
            if (settings.LastZoomLevel < 0.1) settings.LastZoomLevel = 1.0;
            if (settings.LastZoomLevel > 10.0) settings.LastZoomLevel = 1.0;

            // 清理不存在的背景图片路径
            if (!string.IsNullOrEmpty(settings.BackgroundImagePath) && 
                !File.Exists(settings.BackgroundImagePath))
            {
                settings.BackgroundImagePath = "";
            }

            // 清理不存在的最近文件
            for (int i = settings.RecentFiles.Count - 1; i >= 0; i--)
            {
                if (!File.Exists(settings.RecentFiles[i]))
                {
                    settings.RecentFiles.RemoveAt(i);
                }
            }
        }

        public static AppSettings GetDefaultSettings()
        {
            return new AppSettings();
        }

        public static void ResetToDefault()
        {
            try
            {
                string settingsPath = GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    File.Delete(settingsPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"重置设置失败: {ex.Message}");
            }
        }

        // 添加工具使用记录
        public static void AddRecentTool(AppSettings settings, string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return;

            settings.RecentlyUsedTools.Remove(toolName);
            settings.RecentlyUsedTools.Insert(0, toolName);
            settings.LastUsedTool = toolName;
            
            if (settings.RecentlyUsedTools.Count > settings.MaxRecentTools)
            {
                settings.RecentlyUsedTools.RemoveAt(settings.RecentlyUsedTools.Count - 1);
            }
        }

        // 添加最近文件记录
        public static void AddRecentFile(AppSettings settings, string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            settings.RecentFiles.Remove(filePath);
            settings.RecentFiles.Insert(0, filePath);
            
            if (settings.RecentFiles.Count > settings.MaxRecentFiles)
            {
                settings.RecentFiles.RemoveAt(settings.RecentFiles.Count - 1);
            }
        }

        // 获取设置文件信息
        public static string GetSettingsInfo()
        {
            string settingsPath = GetSettingsPath();
            if (File.Exists(settingsPath))
            {
                var fileInfo = new FileInfo(settingsPath);
                return $"设置文件位置: {settingsPath}\n最后修改时间: {fileInfo.LastWriteTime}";
            }
            return $"设置文件将保存到: {settingsPath}";
        }

        // 测试方法：手动检查控件状态
        public static string TestControlSaving(Window window)
        {
            var results = new List<string>();
            
            results.Add("=== 普通控件 ===");
            foreach (var kvp in ControlSaveConfig)
            {
                string controlName = kvp.Key;
                var properties = kvp.Value;
                
                var control = window.FindName(controlName);
                if (control != null)
                {
                    results.Add($"✓ 找到控件: {controlName}");
                    
                    foreach (string propertyName in properties)
                    {
                        var property = control.GetType().GetProperty(propertyName);
                        if (property != null && property.CanRead)
                        {
                            object? value = property.GetValue(control);
                            results.Add($"  - {propertyName}: {value} ({value?.GetType().Name})");
                        }
                        else
                        {
                            results.Add($"  ✗ 属性不存在: {propertyName}");
                        }
                    }
                }
                else
                {
                    results.Add($"✗ 控件未找到: {controlName}");
                }
            }

            results.Add("\n=== 背景控件 ===");
            foreach (var kvp in BackgroundControlsConfig)
            {
                string controlName = kvp.Key;
                var properties = kvp.Value;
                
                var control = window.FindName(controlName);
                if (control != null)
                {
                    results.Add($"✓ 找到控件: {controlName}");
                    
                    foreach (string propertyName in properties)
                    {
                        var property = control.GetType().GetProperty(propertyName);
                        if (property != null && property.CanRead)
                        {
                            object? value = property.GetValue(control);
                            results.Add($"  - {propertyName}: {value} ({value?.GetType().Name})");
                        }
                        else
                        {
                            results.Add($"  ✗ 属性不存在: {propertyName}");
                        }
                    }
                }
                else
                {
                    results.Add($"✗ 控件未找到: {controlName}");
                }
            }
            
            return string.Join("\n", results);
        }
    } 
} 