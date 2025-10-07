using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PicViewEx
{
    /// <summary>
    /// NVIDIA Texture Tools 配置管理器
    /// 负责读取TOML配置文件并转换为nvtt_export.exe的命令行参数
    /// </summary>
    public class NvttConfigManager
    {
        private const string CONFIG_FILE_NAME = "nvtt_config.toml";
        
        /// <summary>
        /// NVTT配置数据结构
        /// </summary>
        public class NvttConfig
        {
            public int SelectedPreset { get; set; } = 0;
            public Dictionary<string, PresetConfig> Presets { get; set; } = new Dictionary<string, PresetConfig>();
        }

        /// <summary>
        /// 预设配置
        /// </summary>
        public class PresetConfig
        {
            public string Format { get; set; } = "";
            public string Quality { get; set; } = "";
            public bool GenerateMipmaps { get; set; } = true;
            public string MipFilter { get; set; } = "";
            public bool UseDx10 { get; set; } = true;
            public bool NoCuda { get; set; } = false;
            public string TextureType { get; set; } = "2d";
            public bool ToNormal { get; set; } = false;
            public float NormalScale { get; set; } = 1.0f;
            public string HeightSource { get; set; } = "average";
            public bool Normalize { get; set; } = true;
            public int SuperRes { get; set; } = 0;
            public bool SaveFlipY { get; set; } = false;
            public bool ExportPreAlpha { get; set; } = false;
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        public static string GetConfigFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILE_NAME);
        }

        /// <summary>
        /// 创建默认配置文件
        /// </summary>
        public static void CreateDefaultConfig()
        {
            var config = new NvttConfig
            {
                SelectedPreset = 0,
                Presets = new Dictionary<string, PresetConfig>
                {
                    ["default"] = new PresetConfig(),
                    ["bc3_highest"] = new PresetConfig
                    {
                        Format = "bc3",
                        Quality = "highest",
                        GenerateMipmaps = false
                    }
                }
            };

            SaveConfig(config);
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        public static NvttConfig LoadConfig()
        {
            string configPath = GetConfigFilePath();
            
            if (!File.Exists(configPath))
            {
                CreateDefaultConfig();
            }

            try
            {
                string content = File.ReadAllText(configPath, Encoding.UTF8);
                return ParseTomlConfig(content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载NVTT配置失败: {ex.Message}，使用默认配置");
                CreateDefaultConfig();
                return LoadConfig();
            }
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        public static void SaveConfig(NvttConfig config)
        {
            string configPath = GetConfigFilePath();
            string tomlContent = GenerateTomlContent(config);
            File.WriteAllText(configPath, tomlContent, Encoding.UTF8);
        }

        /// <summary>
        /// 解析TOML配置内容（简单实现）
        /// </summary>
        private static NvttConfig ParseTomlConfig(string content)
        {
            var config = new NvttConfig();
            var lines = content.Split('\n');
            string currentSection = "";
            PresetConfig currentPreset = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                // 解析节
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    if (currentSection.StartsWith("presets."))
                    {
                        string presetName = currentSection.Substring(8);
                        currentPreset = new PresetConfig();
                        config.Presets[presetName] = currentPreset;
                    }
                    continue;
                }

                // 解析键值对
                var equalIndex = trimmedLine.IndexOf('=');
                if (equalIndex > 0)
                {
                    string key = trimmedLine.Substring(0, equalIndex).Trim();
                    string value = trimmedLine.Substring(equalIndex + 1).Trim().Trim('"');

                    if (currentSection == "" && key == "selected_preset")
                    {
                        if (int.TryParse(value, out int preset))
                            config.SelectedPreset = preset;
                    }
                    else if (currentPreset != null)
                    {
                        SetPresetProperty(currentPreset, key, value);
                    }
                }
            }

            return config;
        }

        /// <summary>
        /// 设置预设属性
        /// </summary>
        private static void SetPresetProperty(PresetConfig preset, string key, string value)
        {
            switch (key.ToLower())
            {
                case "format":
                    preset.Format = value;
                    break;
                case "quality":
                    preset.Quality = value;
                    break;
                case "generate_mipmaps":
                    preset.GenerateMipmaps = value.ToLower() == "true";
                    break;
                case "mip_filter":
                    preset.MipFilter = value;
                    break;
                case "use_dx10":
                    preset.UseDx10 = value.ToLower() == "true";
                    break;
                case "no_cuda":
                    preset.NoCuda = value.ToLower() == "true";
                    break;
                case "texture_type":
                    preset.TextureType = value;
                    break;
                case "to_normal":
                    preset.ToNormal = value.ToLower() == "true";
                    break;
                case "normal_scale":
                    if (float.TryParse(value, out float scale))
                        preset.NormalScale = scale;
                    break;
                case "height_source":
                    preset.HeightSource = value;
                    break;
                case "normalize":
                    preset.Normalize = value.ToLower() == "true";
                    break;
                case "super_res":
                    if (int.TryParse(value, out int res))
                        preset.SuperRes = res;
                    break;
                case "save_flip_y":
                    preset.SaveFlipY = value.ToLower() == "true";
                    break;
                case "export_pre_alpha":
                    preset.ExportPreAlpha = value.ToLower() == "true";
                    break;
            }
        }

        /// <summary>
        /// 生成TOML配置内容
        /// </summary>
        private static string GenerateTomlContent(NvttConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# NVIDIA Texture Tools 配置文件");
            sb.AppendLine("# 选择使用的预设索引（0=default, 1=bc3_highest, ...）");
            sb.AppendLine($"selected_preset = {config.SelectedPreset}");
            sb.AppendLine();

            foreach (var preset in config.Presets)
            {
                sb.AppendLine($"[presets.{preset.Key}]");
                var p = preset.Value;
                
                if (!string.IsNullOrEmpty(p.Format))
                    sb.AppendLine($"format = \"{p.Format}\"");
                if (!string.IsNullOrEmpty(p.Quality))
                    sb.AppendLine($"quality = \"{p.Quality}\"");
                
                sb.AppendLine($"generate_mipmaps = {p.GenerateMipmaps.ToString().ToLower()}");
                
                if (!string.IsNullOrEmpty(p.MipFilter))
                    sb.AppendLine($"mip_filter = \"{p.MipFilter}\"");
                
                sb.AppendLine($"use_dx10 = {p.UseDx10.ToString().ToLower()}");
                sb.AppendLine($"no_cuda = {p.NoCuda.ToString().ToLower()}");
                sb.AppendLine($"texture_type = \"{p.TextureType}\"");
                sb.AppendLine($"to_normal = {p.ToNormal.ToString().ToLower()}");
                sb.AppendLine($"normal_scale = {p.NormalScale}");
                sb.AppendLine($"height_source = \"{p.HeightSource}\"");
                sb.AppendLine($"normalize = {p.Normalize.ToString().ToLower()}");
                
                if (p.SuperRes > 0)
                    sb.AppendLine($"super_res = {p.SuperRes}");
                
                sb.AppendLine($"save_flip_y = {p.SaveFlipY.ToString().ToLower()}");
                sb.AppendLine($"export_pre_alpha = {p.ExportPreAlpha.ToString().ToLower()}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// 根据配置生成nvtt_export.exe的命令行参数
        /// </summary>
        public static string GenerateCommandLineArgs(string inputFile, string outputFile, NvttConfig config = null)
        {
            if (config == null)
                config = LoadConfig();

            var args = new List<string>();
            
            // 获取当前选择的预设
            string selectedPresetName = GetSelectedPresetName(config);
            if (!config.Presets.ContainsKey(selectedPresetName))
            {
                // 如果选择的预设不存在，使用default
                selectedPresetName = "default";
            }

            var preset = config.Presets[selectedPresetName];

            // 如果是default预设且所有值都是默认值，则不添加任何参数
            if (selectedPresetName == "default" && IsDefaultPreset(preset))
            {
                args.Add($"\"{inputFile}\"");
                args.Add("-o");
                args.Add($"\"{outputFile}\"");
                return string.Join(" ", args);
            }

            // 添加输入文件
            args.Add($"\"{inputFile}\"");

            // 添加输出文件
            args.Add("-o");
            args.Add($"\"{outputFile}\"");

            // 添加格式参数
            if (!string.IsNullOrEmpty(preset.Format))
            {
                args.Add("-f");
                args.Add(preset.Format);
            }

            // 添加质量参数
            if (!string.IsNullOrEmpty(preset.Quality))
            {
                args.Add("-q");
                args.Add(preset.Quality);
            }

            // 添加纹理类型
            if (!string.IsNullOrEmpty(preset.TextureType) && preset.TextureType != "2d")
            {
                args.Add("-t");
                args.Add(preset.TextureType);
            }

            // Mipmap相关
            if (!preset.GenerateMipmaps)
                args.Add("--no-mips");

            if (!string.IsNullOrEmpty(preset.MipFilter))
            {
                args.Add("--mip-filter");
                args.Add(preset.MipFilter);
            }

            // DX10头
            if (preset.UseDx10)
                args.Add("--dx10");

            // CUDA
            if (preset.NoCuda)
                args.Add("--no-cuda");

            // 法线贴图相关
            if (preset.ToNormal)
            {
                args.Add("--to-normal");
                
                if (preset.NormalScale != 1.0f)
                {
                    args.Add("--normal-scale");
                    args.Add(preset.NormalScale.ToString());
                }

                if (!string.IsNullOrEmpty(preset.HeightSource) && preset.HeightSource != "average")
                {
                    args.Add("--height");
                    args.Add(preset.HeightSource);
                }

                if (!preset.Normalize)
                    args.Add("--no-normalize");
            }

            // 超分辨率
            if (preset.SuperRes > 0)
            {
                args.Add("--super-res");
                args.Add(preset.SuperRes.ToString());
            }

            // 其他选项
            if (preset.SaveFlipY)
                args.Add("--save-flip-y");

            if (preset.ExportPreAlpha)
                args.Add("--export-pre-alpha");

            return string.Join(" ", args);
        }

        /// <summary>
        /// 获取当前选择的预设名称
        /// </summary>
        public static string GetSelectedPresetName(NvttConfig config)
        {
            var presetNames = new List<string>(config.Presets.Keys);
            if (config.SelectedPreset >= 0 && config.SelectedPreset < presetNames.Count)
            {
                return presetNames[config.SelectedPreset];
            }
            return "default";
        }

        /// <summary>
        /// 检查是否为默认预设（所有值都是默认值）
        /// </summary>
        private static bool IsDefaultPreset(PresetConfig preset)
        {
            return string.IsNullOrEmpty(preset.Format) &&
                   string.IsNullOrEmpty(preset.Quality) &&
                   preset.GenerateMipmaps == true &&
                   string.IsNullOrEmpty(preset.MipFilter) &&
                   preset.UseDx10 == true &&
                   preset.NoCuda == false &&
                   preset.TextureType == "2d" &&
                   preset.ToNormal == false &&
                   preset.NormalScale == 1.0f &&
                   preset.HeightSource == "average" &&
                   preset.Normalize == true &&
                   preset.SuperRes == 0 &&
                   preset.SaveFlipY == false &&
                   preset.ExportPreAlpha == false;
        }
    }
}