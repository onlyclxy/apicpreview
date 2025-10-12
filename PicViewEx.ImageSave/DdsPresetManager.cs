using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace PicViewEx.ImageSave
{
    /// <summary>
    /// DDS预设管理器
    /// </summary>
    public class DdsPresetManager
    {
        private readonly string _presetsFolder;
        private readonly string _historyConfigPath;
        private const int MaxHistoryCount = 3;

        public DdsPresetManager()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            _presetsFolder = Path.Combine(exeDir, "DDS presets");
            _historyConfigPath = Path.Combine(exeDir, "PicViewEx.ImageSave.DdsPresetHistory.json");

            // 确保预设文件夹存在
            if (!Directory.Exists(_presetsFolder))
            {
                Directory.CreateDirectory(_presetsFolder);
            }
        }

        /// <summary>
        /// 获取所有可用的预设文件
        /// </summary>
        public List<DdsPresetInfo> GetAllPresets()
        {
            try
            {
                if (!Directory.Exists(_presetsFolder))
                    return new List<DdsPresetInfo>();

                var presets = Directory.GetFiles(_presetsFolder, "*.dpf")
                    .Select(f => new DdsPresetInfo
                    {
                        FilePath = f,
                        FileName = Path.GetFileNameWithoutExtension(f),
                        LastModified = File.GetLastWriteTime(f)
                    })
                    .OrderByDescending(p => p.LastModified)
                    .ToList();

                return presets;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取DDS预设失败: {ex.Message}");
                return new List<DdsPresetInfo>();
            }
        }

        /// <summary>
        /// 获取历史记录中的预设
        /// </summary>
        public List<DdsPresetInfo> GetHistoryPresets()
        {
            try
            {
                if (!File.Exists(_historyConfigPath))
                    return new List<DdsPresetInfo>();

                string json = File.ReadAllText(_historyConfigPath);
                var history = JsonConvert.DeserializeObject<List<string>>(json);

                if (history == null || history.Count == 0)
                    return new List<DdsPresetInfo>();

                // 只返回仍然存在的预设文件
                return history
                    .Where(File.Exists)
                    .Select(f => new DdsPresetInfo
                    {
                        FilePath = f,
                        FileName = Path.GetFileNameWithoutExtension(f),
                        LastModified = File.GetLastWriteTime(f)
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取历史预设失败: {ex.Message}");
                return new List<DdsPresetInfo>();
            }
        }

        /// <summary>
        /// 添加预设到历史记录
        /// </summary>
        public void AddToHistory(string presetFilePath)
        {
            try
            {
                if (!File.Exists(presetFilePath))
                    return;

                List<string> history = new List<string>();

                // 读取现有历史
                if (File.Exists(_historyConfigPath))
                {
                    string json = File.ReadAllText(_historyConfigPath);
                    history = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                }

                // 移除已存在的相同项
                history.Remove(presetFilePath);

                // 添加到最前面
                history.Insert(0, presetFilePath);

                // 只保留最新的N个
                if (history.Count > MaxHistoryCount)
                {
                    history = history.Take(MaxHistoryCount).ToList();
                }

                // 保存历史
                string newJson = JsonConvert.SerializeObject(history, Formatting.Indented);
                File.WriteAllText(_historyConfigPath, newJson);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存历史预设失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取预设文件夹路径
        /// </summary>
        public string GetPresetsFolder()
        {
            return _presetsFolder;
        }
    }

    /// <summary>
    /// DDS预设信息
    /// </summary>
    public class DdsPresetInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public DateTime LastModified { get; set; }
    }
}
