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
                // 解析格式信息
                Match formatMatch = Regex.Match(output, @"Format:\s*(\S+)", RegexOptions.IgnoreCase);
                if (formatMatch.Success)
                {
                    info.Format = formatMatch.Groups[1].Value;
                }

                // 解析BC压缩格式
                Match bcMatch = Regex.Match(output, @"(BC\d\w*)", RegexOptions.IgnoreCase);
                if (bcMatch.Success)
                {
                    info.CompressionFormat = bcMatch.Groups[1].Value.ToUpper();
                }

                // 解析Mipmap信息
                Match mipMatch = Regex.Match(output, @"Mip\s*levels?:\s*(\d+)", RegexOptions.IgnoreCase);
                if (mipMatch.Success)
                {
                    info.MipLevels = int.Parse(mipMatch.Groups[1].Value);
                    info.HasMipmaps = info.MipLevels > 1;
                }

                // 解析分辨率
                Match resMatch = Regex.Match(output, @"(\d+)\s*x\s*(\d+)", RegexOptions.IgnoreCase);
                if (resMatch.Success)
                {
                    info.Width = int.Parse(resMatch.Groups[1].Value);
                    info.Height = int.Parse(resMatch.Groups[2].Value);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析DDS信息失败: {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// 使用NVIDIA UI导出DDS（让用户手动选择参数）
        /// </summary>
        public bool ExportWithUI(string inputImagePath)
        {
            if (!IsAvailable)
                return false;

            try
            {
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

            try
            {
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
                    process.WaitForExit(30000); // 30秒超时

                    if (process.ExitCode == 0 && File.Exists(outputPath))
                    {
                        return true;
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
        /// 创建临时PNG文件（用于不支持的格式转换）
        /// </summary>
        public string CreateTempPngForDds(System.Windows.Media.Imaging.BitmapSource source)
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"picview_dds_temp_{Guid.NewGuid()}.png");

                using (FileStream stream = new FileStream(tempPath, FileMode.Create))
                {
                    System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
                    encoder.Save(stream);
                }

                return tempPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建临时PNG失败: {ex.Message}");
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
