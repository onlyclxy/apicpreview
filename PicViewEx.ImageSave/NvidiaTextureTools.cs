using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace PicViewEx.ImageSave
{
    /// <summary>
    /// NVIDIA Texture Tools 工具类
    /// </summary>
    public class NvidiaTextureTools
    {
        private readonly string _toolsPath;
        private readonly string _nvddsInfoPath;
        private readonly string _nvttExportPath;

        public bool IsAvailable { get; private set; }

        public NvidiaTextureTools()
        {
            // 检查exe根目录下的NVIDIA Texture Tools文件夹
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            _toolsPath = Path.Combine(exeDir, "NVIDIA Texture Tools");

            _nvddsInfoPath = Path.Combine(_toolsPath, "nvddsinfo.exe");
            _nvttExportPath = Path.Combine(_toolsPath, "nvtt_export.exe");

            IsAvailable = CheckToolsAvailability();
        }

        /// <summary>
        /// 检查NVIDIA工具是否可用
        /// </summary>
        private bool CheckToolsAvailability()
        {
            if (!Directory.Exists(_toolsPath))
                return false;

            if (!File.Exists(_nvddsInfoPath) || !File.Exists(_nvttExportPath))
                return false;

            return true;
        }

        /// <summary>
        /// 获取DDS文件信息
        /// </summary>
        public DdsFileInfo GetDdsInfo(string ddsFilePath)
        {
            if (!IsAvailable || !File.Exists(ddsFilePath))
                return null;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = _nvddsInfoPath,
                    Arguments = $"\"{ddsFilePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        return ParseDdsInfo(output);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取DDS信息失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 解析DDS信息
        /// </summary>
        private DdsFileInfo ParseDdsInfo(string output)
        {
            DdsFileInfo info = new DdsFileInfo();

            try
            {
                // 解析FourCC格式 (如 'DXT1', 'DXT5')
                Match fourccMatch = Regex.Match(output, @"FourCC:\s*'([^']+)'", RegexOptions.IgnoreCase);
                if (fourccMatch.Success)
                {
                    string fourcc = fourccMatch.Groups[1].Value.ToUpper();
                    info.Format = fourcc;

                    // 将DXT格式转换为BC格式
                    info.CompressionFormat = ConvertFourCCToBC(fourcc);
                }

                // 如果没有FourCC，尝试解析其他格式信息
                if (string.IsNullOrEmpty(info.Format))
                {
                    Match formatMatch = Regex.Match(output, @"Format:\s*(\S+)", RegexOptions.IgnoreCase);
                    if (formatMatch.Success)
                    {
                        info.Format = formatMatch.Groups[1].Value;
                    }
                }

                // 解析Mipmap数量
                Match mipMatch = Regex.Match(output, @"Mipmap count:\s*(\d+)", RegexOptions.IgnoreCase);
                if (mipMatch.Success)
                {
                    info.MipLevels = int.Parse(mipMatch.Groups[1].Value);
                    info.HasMipmaps = info.MipLevels > 1;
                }

                // 解析分辨率
                Match widthMatch = Regex.Match(output, @"Width:\s*(\d+)", RegexOptions.IgnoreCase);
                Match heightMatch = Regex.Match(output, @"Height:\s*(\d+)", RegexOptions.IgnoreCase);

                if (widthMatch.Success && heightMatch.Success)
                {
                    info.Width = int.Parse(widthMatch.Groups[1].Value);
                    info.Height = int.Parse(heightMatch.Groups[1].Value);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析DDS信息失败: {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// 将FourCC格式转换为BC格式
        /// DXT1 = BC1, DXT2/DXT3 = BC2, DXT4/DXT5 = BC3
        /// ATI1/BC4U = BC4, ATI2/BC5U = BC5
        /// BC6H, BC7 直接对应
        /// </summary>
        private string ConvertFourCCToBC(string fourcc)
        {
            switch (fourcc.ToUpper())
            {
                case "DXT1":
                    return "BC1";
                case "DXT2":
                case "DXT3":
                    return "BC2";
                case "DXT4":
                case "DXT5":
                    return "BC3";
                case "ATI1":
                case "BC4U":
                case "BC4S":
                    return "BC4";
                case "ATI2":
                case "BC5U":
                case "BC5S":
                    return "BC5";
                case "BC6H":
                    return "BC6H";
                case "BC7":
                case "BC7L":
                    return "BC7";
                default:
                    // 如果无法识别，返回原始值
                    return fourcc;
            }
        }

        /// <summary>
        /// 使用NVIDIA UI导出DDS（让用户手动选择参数）
        /// </summary>
        public bool ExportWithUI(string inputImagePath)
        {
            if (!IsAvailable)
                return false;

            // 验证输入文件存在
            if (!File.Exists(inputImagePath))
            {
                System.Diagnostics.Debug.WriteLine($"输入文件不存在: {inputImagePath}");
                return false;
            }

            try
            {
                // 给文件系统一点时间确保文件完全可访问
                System.Threading.Thread.Sleep(100);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = _nvttExportPath,
                    Arguments = $"\"{inputImagePath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动NVIDIA UI失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 使用预设文件导出DDS
        /// </summary>
        public bool ExportWithPreset(string inputImagePath, string presetPath, string outputPath)
        {
            if (!IsAvailable || !File.Exists(presetPath))
                return false;

            // 验证输入文件存在
            if (!File.Exists(inputImagePath))
            {
                System.Diagnostics.Debug.WriteLine($"输入文件不存在: {inputImagePath}");
                return false;
            }

            try
            {
                // 给文件系统一点时间确保文件完全可访问
                System.Threading.Thread.Sleep(100);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = _nvttExportPath,
                    Arguments = $"\"{inputImagePath}\" --preset \"{presetPath}\" --output \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit(30000); // 30秒超时

                    // 检查输出中是否包含 "Total processing time"，这表示转换完成
                    bool hasCompletionMessage = output.Contains("Total processing time");

                    if (!string.IsNullOrEmpty(error))
                    {
                        System.Diagnostics.Debug.WriteLine($"NVIDIA导出错误: {error}");
                    }

                    // 必须满足：退出码为0 AND 包含完成消息 AND 输出文件存在
                    if (process.ExitCode == 0 && hasCompletionMessage && File.Exists(outputPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"DDS导出成功: {outputPath}");
                        System.Diagnostics.Debug.WriteLine($"NVIDIA输出: {output}");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"NVIDIA导出失败，退出码: {process.ExitCode}");
                        System.Diagnostics.Debug.WriteLine($"包含完成消息: {hasCompletionMessage}");
                        System.Diagnostics.Debug.WriteLine($"文件存在: {File.Exists(outputPath)}");
                        System.Diagnostics.Debug.WriteLine($"输出: {output}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"使用预设导出DDS失败: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 使用命令行参数导出DDS（用于直接保存）
        /// </summary>
        /// <param name="commandArgs">完整的命令行参数</param>
        /// <returns>是否导出成功</returns>
        public bool ExportWithCommandArgs(string commandArgs)
        {
            if (!IsAvailable || string.IsNullOrEmpty(commandArgs))
                return false;

            try
            {
                // 给文件系统一点时间确保文件完全可访问
                System.Threading.Thread.Sleep(100);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = _nvttExportPath,
                    Arguments = commandArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit(30000); // 30秒超时

                    // 检查输出中是否包含 "Total processing time"，这表示转换完成
                    bool hasCompletionMessage = output.Contains("Total processing time");

                    if (!string.IsNullOrEmpty(error))
                    {
                        System.Diagnostics.Debug.WriteLine($"NVIDIA导出错误: {error}");
                    }

                    // 必须满足：退出码为0 AND 包含完成消息
                    if (process.ExitCode == 0 && hasCompletionMessage)
                    {
                        System.Diagnostics.Debug.WriteLine($"DDS导出成功");
                        System.Diagnostics.Debug.WriteLine($"NVIDIA输出: {output}");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"NVIDIA导出失败，退出码: {process.ExitCode}");
                        System.Diagnostics.Debug.WriteLine($"包含完成消息: {hasCompletionMessage}");
                        System.Diagnostics.Debug.WriteLine($"输出: {output}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"使用命令行参数导出DDS失败: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 创建临时PNG文件（用于不支持的格式转换）
        /// </summary>
        public string CreateTempPngForDds(System.Windows.Media.Imaging.BitmapSource source)
        {
            string tempPath = null;
            try
            {
                tempPath = Path.Combine(Path.GetTempPath(), $"picview_dds_temp_{Guid.NewGuid()}.png");

                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
                    encoder.Save(stream);
                    stream.Flush();
                }

                // 确保文件存在且可访问
                if (!File.Exists(tempPath))
                {
                    System.Diagnostics.Debug.WriteLine("临时PNG文件创建失败：文件不存在");
                    return null;
                }

                // 验证文件大小
                FileInfo fileInfo = new FileInfo(tempPath);
                if (fileInfo.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("临时PNG文件创建失败：文件为空");
                    File.Delete(tempPath);
                    return null;
                }

                return tempPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建临时PNG失败: {ex.Message}");

                // 清理失败的文件
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }

                return null;
            }
        }
    }

    /// <summary>
    /// DDS文件信息
    /// </summary>
    public class DdsFileInfo
    {
        public string Format { get; set; }
        public string CompressionFormat { get; set; }
        public bool HasMipmaps { get; set; }
        public int MipLevels { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
