using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace PicViewEx
{
    public class ControlState
    {
        public string ControlName { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public object Value { get; set; }
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
        public List<string> RecentlyUsedTools { get; set; } = new List<string>();
        public List<string> RecentFiles { get; set; } = new List<string>();

        // 打开方式应用列表
        public List<OpenWithAppData> OpenWithApps { get; set; } = new List<OpenWithAppData>();

        // 界面偏好设置
        public bool AutoSaveSettings { get; set; } = true; // 自动保存设置
        public int MaxRecentFiles { get; set; } = 10; // 最大最近文件数
        public int MaxRecentTools { get; set; } = 5; // 最大最近工具数


        // 序列帧播放设置
        public bool SequencePlayerExpanded { get; set; } = false;
        public int LastGridWidth { get; set; } = 3;
        public int LastGridHeight { get; set; } = 3;
        public int LastSequenceFPS { get; set; } = 10;
        public string LastGridPreset { get; set; } = "3×3";

        // 图像引擎设置
        public string ImageEngine { get; set; } = "Auto"; // Auto, STBImageSharp, Leadtools, Magick

        // 控件状态字典 - 存储所有控件的状态
        public Dictionary<string, Dictionary<string, object>> ControlStates { get; set; }
    = new Dictionary<string, Dictionary<string, object>>();

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
        private static readonly string SettingsFileName = "PicViewExSettings.json";
        private static string settingsPathCache; // 缓存路径
        private static bool isSaving = false; // 添加一个静态锁标志
        private static readonly object saveLock = new object(); // 锁对象

        // 控件保存配置 - 可以配置哪些控件的哪些属性要保存
        private static readonly Dictionary<string, List<string>> ControlSaveConfig =
            new Dictionary<string, List<string>>()
        {
    // 复选框
    { "chkShowChannels", new List<string> { "IsChecked" } },
    { "menuShowChannels", new List<string> { "IsChecked" } },

    // 面板状态
    { "backgroundExpander", new List<string> { "IsExpanded" } },
    { "sequenceExpander", new List<string> { "IsExpanded" } },
    { "searchPanel", new List<string> { "Visibility" } },
    { "channelPanel", new List<string> { "Visibility" } },
    { "channelColumn", new List<string> { "Width" } },


    // 序列帧控件
    { "txtGridWidth", new List<string> { "Text" } },
    { "txtGridHeight", new List<string> { "Text" } },
    { "txtFPS", new List<string> { "Text" } },
    { "cbGridPresets", new List<string> { "SelectedIndex" } },

    // 打开方式按钮 - 只保存可见性，不保存Content避免循环引用
    { "btnOpenWith1", new List<string> { "Visibility" } },
    { "btnOpenWith2", new List<string> { "Visibility" } },
    { "btnOpenWith3", new List<string> { "Visibility" } },

    // 菜单项
    { "menuShowBgToolbar", new List<string> { "IsChecked" } },
    { "menuShowSequenceToolbar", new List<string> { "IsChecked" } },
        };

        // 背景设置的优先级恢复配置
        private static readonly Dictionary<string, List<string>> BackgroundControlsConfig =
            new Dictionary<string, List<string>>()
        {
    // 第一优先级：基础颜色值（这些不会触发自动切换）
    { "sliderHue", new List<string> { "Value" } },
    { "sliderSaturation", new List<string> { "Value" } },
    { "sliderBrightness", new List<string> { "Value" } },

    // 第二优先级：派生控件（这些会根据基础值自动更新）
    { "sliderColorSpectrum", new List<string> { "Value" } },
    { "colorPicker", new List<string> { "SelectedColor" } },

    // 第三优先级：背景类型（最后恢复，覆盖任何自动切换）
    { "rbTransparent", new List<string> { "IsChecked" } },
    { "rbSolidColor", new List<string> { "IsChecked" } },
    { "rbImageBackground", new List<string> { "IsChecked" } },
    { "rbWindowTransparent", new List<string> { "IsChecked" } },

    // 菜单同步
    { "menuBgTransparent", new List<string> { "IsChecked" } },
    { "menuBgSolid", new List<string> { "IsChecked" } },
    { "menuBgImage", new List<string> { "IsChecked" } },
    { "menuBgWindowTransparent", new List<string> { "IsChecked" } },
        };


        // 获取设置文件路径 (增加权限检查和缓存)
        public static string GetSettingsPath()
        {
            // 如果已经计算过，直接返回缓存的路径
            if (!string.IsNullOrEmpty(settingsPathCache))
            {
                return settingsPathCache;
            }

            // 优先尝试exe所在目录
            try
            {
                string exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                                     ?? AppDomain.CurrentDomain.BaseDirectory;
                string localPath = Path.Combine(exeDirectory, SettingsFileName);

                // 检查写入权限
                if (HasWriteAccess(exeDirectory))
                {
                    System.Diagnostics.Debug.WriteLine($"使用本地设置文件路径: {localPath}");
                    settingsPathCache = localPath;
                    return localPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查本地路径时发生错误: {ex.Message}");
            }

            // 如果没有写入权限或发生错误，则使用APPDATA目录
            string appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PicViewEx");

            // 确保APPDATA目录存在
            if (!Directory.Exists(appDataDirectory))
            {
                try
                {
                    Directory.CreateDirectory(appDataDirectory);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"创建APPDATA目录失败: {ex.Message}");
                    // 如果连APPDATA都创建失败，就只能返回一个临时路径了
                    return Path.Combine(Path.GetTempPath(), SettingsFileName);
                }
            }

            string appDataPath = Path.Combine(appDataDirectory, SettingsFileName);
            System.Diagnostics.Debug.WriteLine($"使用APPDATA设置文件路径: {appDataPath}");
            settingsPathCache = appDataPath;
            return appDataPath;
        }

        // 检查目录是否有写入权限
        private static bool HasWriteAccess(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return false;
            }

            try
            {
                // 尝试在目录中创建一个临时文件
                string testFile = Path.Combine(directoryPath, Guid.NewGuid().ToString() + ".tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"对目录 '{directoryPath}' 没有写入权限");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查写入权限时发生未知错误: {ex.Message}");
                return false;
            }
        }

        public static AppSettings LoadSettings()
        {
            string settingsPath = GetSettingsPath();
            Console.WriteLine($"--- 开始加载设置 ---");
            Console.WriteLine($"路径: {settingsPath}");

            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    //Console.WriteLine("加载的 JSON 内容:");
                    //Console.WriteLine(json); // 输出加载的原始JSON

                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);

                    if (settings != null)
                    {
                        ValidateAndCleanSettings(settings);
                        Console.WriteLine("设置解析成功。");
                        Console.WriteLine($"--- 结束加载设置 ---");
                        return settings;
                    }
                }
                else
                {
                    Console.WriteLine("设置文件不存在，返回默认设置。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!! 加载设置失败: {ex.Message}");
            }

            Console.WriteLine($"--- 结束加载设置 (失败或默认) ---");
            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            // 使用锁来防止并发写入
            lock (saveLock)
            {
                if (isSaving)
                {
                    Console.WriteLine("--- 保存操作已被锁定，跳过当前请求 ---");
                    return; // 如果正在保存，则跳过
                }
                isSaving = true;
            }

            try
            {
                string settingsPath = GetSettingsPath();
                Console.WriteLine($"--- 开始保存设置 ---");
                Console.WriteLine($"路径: {settingsPath}");

                // 在序列化之前验证和清理数据
                ValidateAndCleanSettings(settings);

                // Newtonsoft.Json 的序列化配置
                var jsonSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented, // 等价于 WriteIndented = true
                    StringEscapeHandling = StringEscapeHandling.Default // 最接近 UnsafeRelaxedJsonEscaping
                                                                        // 如果你希望完全保留原始字符，不转义，可以改成：
                                                                        // StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
                };

                // 序列化
                string json = JsonConvert.SerializeObject(settings, jsonSettings);

                //Console.WriteLine("设置暂停保存");
                //Console.WriteLine("保存的 JSON 内容:");
                //Console.WriteLine(json); // 输出将要保存的JSON

                File.WriteAllText(settingsPath, json, System.Text.Encoding.UTF8);
                Console.WriteLine("设置保存成功。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!! 保存设置失败: {ex.Message}");
            }
            finally
            {
                // 确保在操作完成后释放锁
                isSaving = false;
            }
            Console.WriteLine($"--- 结束保存设置 ---");
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
                        var controlStates = new Dictionary<string, object>();

                        foreach (string propertyName in properties)
                        {
                            try
                            {
                                var property = control.GetType().GetProperty(propertyName);
                                if (property != null && property.CanRead)
                                {
                                    object value = property.GetValue(control);

                                    // 跳过可能导致循环引用的Content属性（如果包含UI元素）
                                    if (propertyName == "Content" && IsUIElement(value))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"跳过UI元素Content: {controlName}.{propertyName}");
                                        continue;
                                    }

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
                                    // 对于Content属性，只保存简单的字符串值
                                    else if (propertyName == "Content" && value != null)
                                    {
                                        value = value.ToString();
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
                        var controlStates = new Dictionary<string, object>();

                        foreach (string propertyName in properties)
                        {
                            try
                            {
                                var property = control.GetType().GetProperty(propertyName);
                                if (property != null && property.CanRead)
                                {
                                    object value = property.GetValue(control);

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
            if (settings?.ControlStates == null) return;

            int restoredCount = 0;

            foreach (var kvp in settings.ControlStates)
            {
                string controlName = kvp.Key;
                var controlStates = kvp.Value;
                var control = window.FindName(controlName);

                if (control == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Restore] Control not found: {controlName}");
                    continue;
                }

                foreach (var stateKvp in controlStates)
                {
                    string propertyName = stateKvp.Key;
                    object savedValue = stateKvp.Value;

                    try
                    {
                        var property = control.GetType().GetProperty(propertyName);
                        if (property != null && property.CanWrite)
                        {
                            object convertedValue = ConvertValue(savedValue, property.PropertyType);

                            var isNullable = Nullable.GetUnderlyingType(property.PropertyType) != null || !property.PropertyType.IsValueType;

                            if (convertedValue != null || isNullable)
                            {
                                property.SetValue(control, convertedValue);
                                restoredCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Restore] Failed to restore {controlName}.{propertyName}: {ex.Message}");
                    }
                }
            }
            System.Diagnostics.Debug.WriteLine($"[Restore] Restored {restoredCount} control properties.");
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            // Handle JToken conversions
            if (value is JToken token)
            {
                if (token.Type == JTokenType.Boolean)
                {
                    return (bool)token;
                }
                else if (token.Type == JTokenType.Integer)
                {
                    if (targetType == typeof(int)) return (int)token;
                    if (targetType == typeof(double)) return (double)token;
                    return (long)token; // fallback
                }
                else if (token.Type == JTokenType.Float)
                {
                    if (targetType == typeof(double)) return (double)token;
                    return (float)token;
                }
                else if (token.Type == JTokenType.String)
                {
                    value = (string)token;
                }
                else if (token.Type == JTokenType.Null)
                {
                    return null;
                }
            }


            if (value == null) return null;

            try
            {
                if (targetType.IsEnum)
                {
                    return Enum.Parse(targetType, value.ToString(), true);

                }

                if (targetType == typeof(Color))
                {
                    return (Color)ColorConverter.ConvertFromString(value.ToString());
                }

                if (targetType == typeof(GridLength))
                {
                    return new GridLength(Convert.ToDouble(value), GridUnitType.Star);
                }

                return Convert.ChangeType(value, targetType);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConvertValue] Failed to convert '{value}' to '{targetType.Name}': {ex.Message}");
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }

        // 辅助方法：检查对象是否为UI元素
        private static bool IsUIElement(object value)
        {
            if (value == null) return false;
            
            // 检查是否为WPF UI元素类型
            return value is System.Windows.UIElement || 
                   value is System.Windows.FrameworkElement ||
                   value is System.Windows.Controls.Control ||
                   value is System.Windows.Controls.Image ||
                   value is System.Windows.Controls.TextBlock ||
                   value is System.Windows.Controls.Panel;
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

            // 验证并修正无效的数值
            settings.WindowWidth = ValidateDouble(settings.WindowWidth, 1200);
            settings.WindowHeight = ValidateDouble(settings.WindowHeight, 800);
            settings.WindowLeft = ValidateDouble(settings.WindowLeft, 100);
            settings.WindowTop = ValidateDouble(settings.WindowTop, 100);
            settings.LastZoomLevel = ValidateDouble(settings.LastZoomLevel, 1.0, 0.1, 20.0);
            settings.LastImageX = ValidateDouble(settings.LastImageX, 0);
            settings.LastImageY = ValidateDouble(settings.LastImageY, 0);

            // 验证窗口尺寸
            if (settings.WindowWidth < 400) settings.WindowWidth = 1200;
            if (settings.WindowHeight < 300) settings.WindowHeight = 800;

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

        // 新增一个辅助方法来验证double值
        private static double ValidateDouble(double value, double defaultValue, double min = -10000, double max = 10000)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
            {
                return defaultValue;
            }
            return value;
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
                            object value = property.GetValue(control);
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
                            object value = property.GetValue(control);
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