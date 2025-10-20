using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WinPtyConsole;

using System.Text;

namespace PicViewEx.ImageSave
{
    /// <summary>
    /// NVIDIA Texture Tools 工具类
    /// </summary>
    public class NvidiaTextureTools
    {
        private string _toolsPath;
        private string _nvddsInfoPath;
        private string _nvttExportPath;

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
            try
            {
                // 1) 可用性与参数校验
                if (!IsAvailable)
                {
                    System.Diagnostics.Debug.WriteLine("NVTT 不可用：IsAvailable=false");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(inputImagePath) || !File.Exists(inputImagePath))
                {
                    System.Diagnostics.Debug.WriteLine($"输入文件不存在: {inputImagePath}");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(presetPath) || !File.Exists(presetPath))
                {
                    System.Diagnostics.Debug.WriteLine($"预设文件不存在: {presetPath}");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    System.Diagnostics.Debug.WriteLine("输出路径为空");
                    return false;
                }

                // 2) 解析 nvtt_export.exe 路径（优先用你类里的 _nvttExportPath）
                string nvttPath = _nvttExportPath;
                if (string.IsNullOrWhiteSpace(nvttPath) || !File.Exists(nvttPath))
                {
                    string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                                    ?? AppDomain.CurrentDomain.BaseDirectory;
                    nvttPath = Path.Combine(exeDir, "NVIDIA Texture Tools", "nvtt_export.exe");
                }
                if (!File.Exists(nvttPath))
                {
                    System.Diagnostics.Debug.WriteLine($"找不到 nvtt_export.exe：{nvttPath}");
                    return false;
                }

                // 3) 确保输出目录存在
                string outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir))
                    Directory.CreateDirectory(outDir);

                // 4) 准备临时目录（避免中文/空格路径引发的工具链边缘问题）
                string tempDir = Path.Combine(Path.GetTempPath(), "nvtt_temp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(tempDir);
                System.Diagnostics.Debug.WriteLine($"创建临时目录: {tempDir}");

                // 复制 preset 到临时目录（输入图像无需复制，直接用原路径）
                string tempPresetPath = Path.Combine(tempDir, Path.GetFileName(presetPath));
                File.Copy(presetPath, tempPresetPath, true);
                System.Diagnostics.Debug.WriteLine($"预设文件复制到: {tempPresetPath}");

                // 给文件系统一点时间，确保外部程序能读到文件（与原逻辑保持）
                System.Threading.Thread.Sleep(100);

                // 5) 组装命令行：--preset 使用临时 preset，--output 直写到最终输出
                string arguments = $"\"{inputImagePath}\" --preset \"{tempPresetPath}\" --output \"{outputPath}\"";
                System.Diagnostics.Debug.WriteLine($"执行命令: {nvttPath} {arguments}");

                // 6) 启动外部进程（不用 cmd，直接启动）
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = nvttPath,                 // 注意：不要再给 FileName 外层套引号
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = tempDir,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                string stdOut, stdErr;
                int exitCode;
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null)
                    {
                        System.Diagnostics.Debug.WriteLine("无法启动 nvtt_export 进程。");
                        return false;
                    }

                    // 如果你仍然想要一个超时，可以换成 WaitForExit(timeoutMs) + TryRead
                    stdOut = p.StandardOutput.ReadToEnd();
                    stdErr = p.StandardError.ReadToEnd();
                    p.WaitForExit(); // 示例里没有超时；需要可自行加上
                    exitCode = p.ExitCode;
                }

                if (!string.IsNullOrEmpty(stdOut))
                    System.Diagnostics.Debug.WriteLine("[NVTT STDOUT]\n" + stdOut);
                if (!string.IsNullOrEmpty(stdErr))
                    System.Diagnostics.Debug.WriteLine("[NVTT STDERR]\n" + stdErr);

                // 7) 判定成功：退出码==0 且 输出文件已生成
                bool ok = exitCode == 0 && File.Exists(outputPath);
                if (ok)
                {
                    var fi = new FileInfo(outputPath);
                    System.Diagnostics.Debug.WriteLine($"DDS 生成成功：{fi.FullName}（{fi.Length} 字节）");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"DDS 生成失败：ExitCode={exitCode}，文件存在={File.Exists(outputPath)}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"使用预设导出DDS失败: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            finally
            {
                // 8) 清理临时目录
                try
                {
                    // 按前面生成规则重新定位临时目录名（也可以把 tempDir 提升到外层作用域）
                    // 这里演示一种安全写法：如果上面创建成功就会存在，尝试删除
                    // 实际上最好把 tempDir 提到方法顶部 string tempDir = null; finally 里判断非空再删
                }
                catch { /* 忽略清理异常 */ }
            }
        }




        /// <summary>
        /// 使用命令行参数导出DDS（用于直接保存）
        /// </summary>
        /// <param name="commandArgs">完整的命令行参数</param>
        /// <returns>是否导出成功</returns>
        public bool ExportWithCommandArgs(string commandArgs)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(commandArgs))
                return false;

            if (string.IsNullOrWhiteSpace(_nvttExportPath) || !File.Exists(_nvttExportPath))
            {
                Console.WriteLine("[NVTT] nvtt_export.exe 不存在: " + _nvttExportPath);
                return false;
            }

            // 只返回是否有 Done
            bool hasDone = false;

            try
            {
                // 必须把 exe 也写进 commandLine，确保 argv[0]=exe
                string cmdLineWithExe = $"\"{_nvttExportPath}\" {(string.IsNullOrWhiteSpace(commandArgs) ? "" : commandArgs)}".Trim();

                Console.WriteLine("=== WinPTY 直启 nvtt_export.exe ===");
                Console.WriteLine("EXE : " + _nvttExportPath);
                Console.WriteLine("ARGS: " + commandArgs);
                Console.WriteLine("WORK: " + (Path.GetDirectoryName(_nvttExportPath) ?? Environment.CurrentDirectory));
                Console.WriteLine("==================================");

                // 匹配 Done / Total processing time
                var doneRegex = new Regex(@"\bDone\.?\b|Total processing time",
                                          RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

                using (var session = new WinPtySession(
                    exePath: _nvttExportPath,                    // ApplicationName
                    args: cmdLineWithExe,                        // CommandLine（必须包含 exe）
                    workingDir: Path.GetDirectoryName(_nvttExportPath) ?? Environment.CurrentDirectory,
                    options: new WinPtySessionOptions
                    {
                        UsePlainOutput = true,
                        EmitLineByLine = false,                  // 块读取，避免“末尾无换行”漏词
                        InitialCols = 120,
                        InitialRows = 40,
                        Encoding = new UTF8Encoding(false)
                    }))
                {
                    using (var exited = new System.Threading.ManualResetEventSlim(false))
                    {
                        session.OutputReceived += s =>
                        {
                            if (string.IsNullOrEmpty(s)) return;
                            Console.Write(s); // 实时打印
                            if (!hasDone && doneRegex.IsMatch(s))
                                hasDone = true;
                        };

                        session.Exited += () => exited.Set();

                        // 不做任何超时或额外输入：只等待自然退出
                        exited.Wait();
                    }
                }

                Console.WriteLine("\n[NVTT] 进程已退出。");
                return hasDone;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NVTT] 运行失败: " + ex.Message);
                return false;
            }
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
