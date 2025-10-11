using ImageMagick;
using Leadtools;
using Leadtools.Codecs;
using Leadtools.Pdf;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace PicViewEx
{
    /// <summary>
    /// 集中管理图片及相关资源的加载逻辑，便于在不同场景复用。
    /// </summary>
    public class ImageLoader
    {
        // ImageLoader 内新增（静态共享）
        private static readonly object s_leadLock = new object();
        private static RasterCodecs s_leadCodecs;

        private bool HasEffectiveWhitelist(ImageEngine engine, out List<string> wl)
        {
            if (engineWhitelistExtensions.TryGetValue(engine, out wl) && wl != null && wl.Count > 0)
                return true;
            wl = null;
            return false;
        }


        /// <summary>
        /// 自动引擎模式：按优先级尝试不同引擎加载图片
        /// </summary>
        private BitmapSource LoadImageWithAutoEngine(string imagePath)
        {
            string extension = Path.GetExtension(imagePath).ToLowerInvariant();
            
            // 定义引擎尝试顺序
            var engineOrder = new List<ImageEngine> { ImageEngine.STBImageSharp, ImageEngine.Leadtools, ImageEngine.Magick };

            foreach (var engine in engineOrder)
            {
                // ① 白名单优先：只有当“当前引擎存在且白名单非空”时才启用白名单判断
                if (HasEffectiveWhitelist(engine, out var wl))
                {
                    if (!wl.Contains(extension))
                    {
                        Console.WriteLine($"跳过引擎 {engine}，扩展名 {extension} 不在白名单中");
                        continue;
                    }
                }
                else
                {
                    // ② 没有白名单（或空白名单）→ 使用黑名单
                    if (engineSkipExtensions.TryGetValue(engine, out var bl) && bl != null && bl.Contains(extension))
                    {
                        Console.WriteLine($"跳过引擎 {engine}，扩展名 {extension} 在黑名单中");
                        continue;
                    }
                }

                // ③ 可用性检查（保持你原来的判断）
                if (engine == ImageEngine.Leadtools && !IsLeadtoolsAvailable())
                {
                    Console.WriteLine($"跳过引擎 {engine}，引擎不可用");
                    continue;
                }

                try
                {
                    Console.WriteLine($"尝试使用引擎 {engine} 加载图片: {Path.GetFileName(imagePath)}");

                    BitmapSource result = null;
                    switch (engine)
                    {
                        case ImageEngine.STBImageSharp:
                            result = LoadImageWithSTBImageSharp(imagePath);
                            break;
                        case ImageEngine.Leadtools:
                            result = LoadImageWithLeadtools(imagePath);
                            break;
                        case ImageEngine.Magick:
                            result = LoadImageWithMagick(imagePath);
                            break;
                    }

                    if (result != null)
                    {
                        Console.WriteLine($"成功使用引擎 {engine} 加载图片");
                        lastUsedAutoEngine = engine;
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"引擎 {engine} 加载失败: {ex.Message}");
                    // 继续尝试下一个引擎
                }
            }



            // 所有引擎都失败了
            Console.WriteLine("所有引擎都无法加载图片");
            lastUsedAutoEngine = ImageEngine.Auto; // 重置为Auto
            return CreateErrorImage("图片加载错误");
        }



        private static bool PsdHasAlphaQuick(string path)
        {
            // 只读 PSD 头 26 字节
            const int HeaderLen = 26;
            byte[] hdr = new byte[HeaderLen];
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < HeaderLen) return false;
                fs.Read(hdr, 0, HeaderLen);
            }

            // "8BPS"
            if (!(hdr[0] == 0x38 && hdr[1] == 0x42 && hdr[2] == 0x50 && hdr[3] == 0x53))
                return false;

            int channels = (hdr[12] << 8) | hdr[13];
            int colorMode = (hdr[24] << 8) | hdr[25];

            // 按色彩空间估算基础通道数（不含 Alpha/专色）
            int baseCh;
            switch (colorMode)
            {
                case 0: // Bitmap
                case 1: // Gray
                case 2: // Indexed
                case 8: // Duotone
                    baseCh = 1;
                    break;

                case 3: // RGB
                case 9: // Lab
                    baseCh = 3;
                    break;

                case 4: // CMYK
                    baseCh = 4;
                    break;

                case 7: // Multichannel（保守按 3 计算）
                    baseCh = 3;
                    break;

                default:
                    baseCh = 3;
                    break;
            }

            // PSD 的 channels 包括 Alpha/专色。常见透明：RGB(3)+A(1)=4
            return channels > baseCh;
        }


        /// <summary>
        /// 创建错误提示图片
        /// </summary>
        private BitmapSource CreateErrorImage(string errorMessage)
        {
            try
            {
                // 创建一个简单的错误图片，使用透明背景
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    var rect = new Rect(0, 0, 400, 300);
                    // 使用透明背景，不绘制背景矩形
                    
                    var formattedText = new FormattedText(
                        errorMessage,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        16,
                        Brushes.Red,
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                    
                    var textPoint = new Point(
                        (rect.Width - formattedText.Width) / 2,
                        (rect.Height - formattedText.Height) / 2);
                    
                    drawingContext.DrawText(formattedText, textPoint);
                }
                
                var renderTargetBitmap = new RenderTargetBitmap(400, 300, 96, 96, PixelFormats.Pbgra32);
                renderTargetBitmap.Render(drawingVisual);
                
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
                
                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    stream.Position = 0;
                    
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    
                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建错误图片失败: {ex.Message}");
                // 返回一个最基本的BitmapImage
                return new BitmapImage();
            }
        }
        /// 图像引擎类型枚举
        /// </summary>
        public enum ImageEngine
    {
        Auto,
        STBImageSharp,
        Leadtools,
        Magick
    }

        private readonly double backgroundOpacity;
        private ImageEngine currentEngine;
        private ImageEngine lastUsedAutoEngine; // 记录自动模式下最后使用的引擎
        private RasterCodecs leadtoolsCodecs;
        private readonly Dictionary<ImageEngine, List<string>> engineSkipExtensions;
        private readonly Dictionary<ImageEngine, List<string>> engineWhitelistExtensions;


        public ImageLoader(double backgroundOpacity = 0.3, ImageEngine engine = ImageEngine.Auto)
        {
            this.backgroundOpacity = backgroundOpacity;
            
            // 初始化引擎跳过扩展名列表
            engineSkipExtensions = new Dictionary<ImageEngine, List<string>>
            {
                //[ImageEngine.STBImageSharp] = new List<string> {  ".tiff", ".tif", ".pdf",".dds" },
                [ImageEngine.Leadtools] = new List<string> { ".webp" ,".dds"},
                [ImageEngine.Magick] = new List<string> { ".pdf" }
            };
            
            // 初始化引擎白名单扩展名列表
            engineWhitelistExtensions = new Dictionary<ImageEngine, List<string>>
            {
                [ImageEngine.STBImageSharp] = new List<string> { ".jpg", ".jpeg", ".png", ".bmp", ".tga" },
                //[ImageEngine.Leadtools] = new List<string> { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".psd" },
                //[ImageEngine.Magick] = new List<string> { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif", ".psd" }
            };
            
            // 智能选择引擎：如果指定的引擎不可用，自动回退到可用的引擎
            if (engine == ImageEngine.Leadtools && !IsLeadtoolsAvailable())
            {
                Console.WriteLine("LEADTOOLS not available, falling back to ImageMagick");
                this.currentEngine = ImageEngine.Magick;
            }
            else
            {
                this.currentEngine = engine;
            }
            
            if (this.currentEngine == ImageEngine.Leadtools)
            {
                if (!InitializeLeadtools())
                {
                    // 如果LEADTOOLS初始化失败，回退到Magick
                    this.currentEngine = ImageEngine.Magick;
                }
            }
        }

        /// <summary>
        /// 切换图像引擎
        /// </summary>
        public void SwitchEngine(ImageEngine engine)
        {
            if (currentEngine == engine) return;

            // 检查目标引擎是否可用
            if (engine == ImageEngine.Leadtools && !IsLeadtoolsAvailable())
            {
                Console.WriteLine("Cannot switch to LEADTOOLS: not available");
                Application.Current?.Dispatcher.Invoke(() =>
            MessageBox.Show("无法切换到 LEADTOOLS：缺少必要的依赖或许可文件。", "切换失败",
                MessageBoxButton.OK, MessageBoxImage.Warning));
                return;
            }

            // 清理旧引擎资源
            if (currentEngine == ImageEngine.Leadtools && leadtoolsCodecs != null)
            {
                leadtoolsCodecs.Dispose();
                leadtoolsCodecs = null;
            }

            currentEngine = engine;

            // 初始化新引擎
            if (engine == ImageEngine.Leadtools)
            {
                InitializeLeadtools();
            }
        }



        /// <summary>
        /// 检查LEADTOOLS DLL是否存在
        /// </summary>
        private bool IsLeadtoolsAvailable()
        {
            try
            {
                // 获取当前应用程序的目录
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] dlls = { "Leadtools.dll", "Leadtools.Codecs.dll", "Ltkrnx.dll" };
                foreach (var dll in dlls)
                {
                    LogDllInfo(Path.Combine(appDir, dll));
                }


                // 检查核心LEADTOOLS DLL是否存在
                string[] requiredDlls = {
                    "Leadtools.dll",
                    "Leadtools.Codecs.dll",
                    "Ltkrnx.dll"
                };

                foreach (string dll in requiredDlls)
                {
                    string dllPath = Path.Combine(appDir, dll);
                    if (!File.Exists(dllPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"LEADTOOLS DLL not found: {dllPath}");
                        return false;
                    }
                }

                // 尝试加载Leadtools程序集
                Assembly.LoadFrom(Path.Combine(appDir, "Leadtools.dll"));
                Assembly.LoadFrom(Path.Combine(appDir, "Leadtools.Codecs.dll"));
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LEADTOOLS availability check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取可用的引擎列表
        /// </summary>
        public List<ImageEngine> GetAvailableEngines()
        {
            var engines = new List<ImageEngine> { ImageEngine.Auto, ImageEngine.STBImageSharp };
            
            if (IsLeadtoolsAvailable())
            {
                engines.Add(ImageEngine.Leadtools);
            }
            
            engines.Add(ImageEngine.Magick);
            return engines;
        }

        /// <summary>
        /// 获取当前使用的引擎
        /// </summary>
        public ImageEngine GetCurrentEngine()
        {
            return currentEngine;
        }
        
        /// <summary>
        /// 获取自动模式下实际使用的引擎
        /// </summary>
        public ImageEngine GetLastUsedAutoEngine()
        {
            return lastUsedAutoEngine;
        }



        /// <summary>
        /// 初始化LEADTOOLS
        /// </summary>
        public static bool InitializeLeadtools()
        {
            if (s_leadCodecs != null)
            {
                Log("[INIT] LEADTOOLS 已初始化（复用现有实例）。");
                return true;
            }

            lock (s_leadLock)
            {
                if (s_leadCodecs != null)
                {
                    Log("[INIT] LEADTOOLS 已初始化（复用现有实例，锁内）。");
                    return true;
                }

                // ===== 环境信息 =====
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                Log($"[INIT] 开始初始化 LEADTOOLS");
                Log($"[ENV ] AppDir = {appDir}");
                Log($"[ENV ] OS     = {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
                Log($"[ENV ] Arch   = {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
                Log($"[ENV ] .NET   = {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");

                // ===== 许可证检测与加载 =====
                string keyPath = System.IO.Path.Combine(appDir, "full_license.key");
                string licPath = System.IO.Path.Combine(appDir, "full_license.lic");
                bool keyExists = System.IO.File.Exists(keyPath);
                bool licExists = System.IO.File.Exists(licPath);
                Log($"[LIC ] key 存在: {keyExists}  ({keyPath})");
                Log($"[LIC ] lic 存在: {licExists}  ({licPath})");

                bool licenseLoaded = false;
                if (keyExists && licExists)
                {
                    try
                    {
                        var key = System.IO.File.ReadAllText(keyPath);
                        var lic = System.IO.File.ReadAllBytes(licPath);
                        Leadtools.RasterSupport.SetLicense(lic, key);
                        licenseLoaded = true;
                        Log("[LIC ] 许可证加载成功（RasterSupport.SetLicense OK）。");
                    }
                    catch (Exception ex)
                    {
                        Log($"[LIC ] 许可证加载失败：{ex.Message}");
                        Log($"[LIC ] 将尝试在评估模式（evaluation）下继续。");
                    }
                }
                else
                {
                    Log("[LIC ] 未找到完整许可证文件，将在评估模式（evaluation）下运行（若许可允许）。");
                }

                // ===== 必需 DLL 检查（存在 + 版本）=====
                string[] dlls = { "Leadtools.dll", "Leadtools.Codecs.dll", "Ltkrnx.dll" };
                foreach (var dll in dlls)
                {
                    string p = System.IO.Path.Combine(appDir, dll);
                    if (System.IO.File.Exists(p))
                    {
                        string ver;
                        try
                        {
                            var an = System.Reflection.AssemblyName.GetAssemblyName(p);
                            ver = an?.Version?.ToString() ?? "unknown";
                        }
                        catch (Exception vex)
                        {
                            ver = $"unknown（GetAssemblyName失败: {vex.Message}）";
                        }
                        Log($"[DLL ] 存在: {dll}, 版本: {ver}");
                    }
                    else
                    {
                        Log($"[DLL ] 缺失: {dll} （路径: {p}）");
                    }
                }

                // ===== 创建 RasterCodecs 并设置关键选项 =====
                try
                {
                    var codecs = new Leadtools.Codecs.RasterCodecs();
                    Log("[CODE] RasterCodecs 实例创建成功。");

                    // 关键选项（按你原有配置打印出来）
                    codecs.Options.Load.AllPages = true;
                    codecs.Options.RasterizeDocument.Load.XResolution = 300;
                    codecs.Options.RasterizeDocument.Load.YResolution = 300;

                    // 🟢 关键三行：               
                    codecs.Options.Load.AutoDetectAlpha = true;
                    codecs.Options.Load.PremultiplyAlpha = true;

                    Log($"[CODE] 选项：Load.AllPages = {codecs.Options.Load.AllPages}");
                    Log($"[CODE] 选项：RasterizeDocument.Load = " +
                        $"{codecs.Options.RasterizeDocument.Load.XResolution} x " +
                        $"{codecs.Options.RasterizeDocument.Load.YResolution} dpi");

                    s_leadCodecs = codecs;

                    Log($"[DONE] LEADTOOLS 初始化成功。" +
                        $" 许可证状态: {(licenseLoaded ? "已加载" : "评估/未加载")}");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"[FAIL] 创建 RasterCodecs 失败：{ex.Message}");
                    Log(ex.ToString());
                    s_leadCodecs = null;
                    return false;
                }
            }

        }


        private static void LogDllInfo(string fullPath)
        {
            string file = Path.GetFileName(fullPath);
            if (!File.Exists(fullPath))
            {
                Log($"[DLL ] 缺失: {file} （{fullPath}）");
                return;
            }

            // 先尝试按“托管程序集”方式获取
            try
            {
                var an = AssemblyName.GetAssemblyName(fullPath);
                Log($"[DLL ] 托管: {file}  AssemblyVer={an.Version}");
                return;
            }
            catch
            {
                // 不是托管程序集 → 当成本机 DLL 处理
            }

            // 本机 DLL：用 FileVersionInfo + 读取 PE 头看架构
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(fullPath);
                string arch = ReadPeMachine(fullPath); // x86/x64/ARM64/…
                string fv = string.IsNullOrWhiteSpace(vi.FileVersion) ? "unknown" : vi.FileVersion;
                string pv = string.IsNullOrWhiteSpace(vi.ProductVersion) ? "unknown" : vi.ProductVersion;
                Log($"[DLL ] 本机: {file}  FileVer={fv}  ProductVer={pv}  Arch={arch}");
            }
            catch (Exception ex)
            {
                Log($"[DLL ] 本机: {file}  读取版本失败：{ex.Message}");
            }
        }

        private static string ReadPeMachine(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var br = new BinaryReader(fs))
            {
                // DOS header: e_lfanew at 0x3C → PE header偏移
                fs.Seek(0x3C, SeekOrigin.Begin);
                int peOffset = br.ReadInt32();
                fs.Seek(peOffset, SeekOrigin.Begin);

                uint sig = br.ReadUInt32(); // "PE\0\0" = 0x00004550
                if (sig != 0x00004550)
                    return "unknown-PE";

                ushort machine = br.ReadUInt16();
                // 常见 Machine 值
                switch (machine)
                {
                    case 0x014c: return "x86";
                    case 0x8664: return "x64";
                    case 0xAA64: return "ARM64";
                    case 0x0200: return "IA64";
                    default: return $"0x{machine:X4}";
                }
            }
        }

        private static void Log(string msg)
        {
            //var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            var line = $"{msg}";
            Console.WriteLine(line);
            Debug.WriteLine(line);
        }


        public static void ShutdownLeadtools()
        {
            lock (s_leadLock)
            {
                s_leadCodecs?.Dispose();
                s_leadCodecs = null;
            }
        }

        // 给实例用的访问器（把原来的实例字段 leadtoolsCodecs 替换成这个）
        private static RasterCodecs LeadCodecsOrNull => s_leadCodecs;


        private static BitmapSource EnsurePbgra32(BitmapSource src)
        {
            if (src == null) return null;

            // 先把奇怪的格式转成 BGRA，再转成 PBGRA（这一步会真正做"乘以alpha"）
            if (src.Format != PixelFormats.Bgra32 && src.Format != PixelFormats.Pbgra32)
            {
                src = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            }
            if (src.Format != PixelFormats.Pbgra32)
            {
                src = new FormatConvertedBitmap(src, PixelFormats.Pbgra32, null, 0);
            }

            if (src.CanFreeze) src.Freeze();
            return src;
        }

        private static BitmapSource ForceMaterializePbgra32(BitmapSource src)
        {
            if (src == null) return null;

            // 先通过 WIC 转成 Pbgra32（仍可能是惰性的）
            var conv = src.Format == PixelFormats.Pbgra32
                ? src
                : new FormatConvertedBitmap(src, PixelFormats.Pbgra32, null, 0);

            // 真正分配一块 Pbgra32 的像素缓冲区并拷贝进来
            int w = conv.PixelWidth, h = conv.PixelHeight;
            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Pbgra32, null);
            int stride = w * 4;
            var buf = new byte[stride * h];
            conv.CopyPixels(buf, stride, 0);
            wb.WritePixels(new Int32Rect(0, 0, w, h), buf, stride, 0);
            wb.Freeze();
            return wb;
        }




        /// <summary>
        /// 加载常规图片资源，根据当前引擎选择加载方式。
        /// </summary>
        public BitmapSource LoadImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return CreateErrorImage("文件不存在");

            try
            {
                string extension = Path.GetExtension(imagePath).ToLowerInvariant();
                if (extension == ".gif" || extension == ".webp")
                {
                    // 这两类你原来就是走 Magick 的第一帧
                    return SafeLoad(
                        () => LoadImageWithMagick(imagePath),
                        "ImageMagick",
                        imagePath,
                        showDialogOnError: currentEngine != ImageEngine.Auto // 手动模式才弹
                    );
                }

                switch (currentEngine)
                {
                    case ImageEngine.Auto:
                        // 自动模式：你已有的“逐个尝试+吞异常”的逻辑
                        return LoadImageWithAutoEngine(imagePath);

                    case ImageEngine.STBImageSharp:
                        return SafeLoad(
                            () => LoadImageWithSTBImageSharp(imagePath),
                            "STBImageSharp",
                            imagePath,
                            showDialogOnError: true
                        );

                    case ImageEngine.Leadtools:
                        return SafeLoad(
                            () => LoadImageWithLeadtools(imagePath),
                            "LEADTOOLS",
                            imagePath,
                            showDialogOnError: true
                        );

                    case ImageEngine.Magick:
                        return SafeLoad(
                            () => LoadImageWithMagick(imagePath),
                            "ImageMagick",
                            imagePath,
                            showDialogOnError: true
                        );

                    default:
                        return CreateErrorImage("未知的图像引擎");
                }
            }
            catch (Exception ex)
            {
                // 额外兜底（理论上不会到这里）
                Console.WriteLine($"加载图片失败（兜底）: {ex}");
                return CreateErrorImage($"加载失败: {ex.Message}");
            }
        }



        //public BitmapSource LoadImageWithLeadtools(string imagePath)
        //{
        //    try
        //    {
        //        if (!InitializeLeadtools())
        //            throw new Exception("LEADTOOLS initialization failed");

        //        // 你自己的异步加载
        //        var task = LeadtoolsImageLoaderNew.LoadImageAsync(imagePath);
        //        return task.GetAwaiter().GetResult();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"LEADTOOLS failed to load {imagePath}: {ex.Message}");
        //        throw;
        //    }
        //}

        public BitmapSource LoadImageWithLeadtools(string imagePath)
        {
            try
            {
                if (!InitializeLeadtools())
                    throw new Exception("LEADTOOLS initialization failed");


                //这一块用于psd乘以alpha的处理. 但是大文件比如1.56G的psd可能会慢个400ms左右
                var ext = Path.GetExtension(imagePath).ToLowerInvariant();
                BitmapSource bmp;

                if (ext == ".psd")
                {
                    // 1) 只读头，极速判断
                    bool likelyHasAlpha = PsdHasAlphaQuick(imagePath);

                    // 2) 用你控制的 RasterCodecs 读取（确保 Options.Load.PremultiplyAlpha = true）
                    using (var img = s_leadCodecs.Load(imagePath))
                    {
                        bmp = ConvertRasterImageToBitmapImage(img);
                    }

                    // 3) 只有“可能有 Alpha”才做预乘收口；否则直接返回
                    if (likelyHasAlpha)
                    {
                        bmp = EnsurePbgra32(bmp);          // 轻量：FormatConvertedBitmap 到 Pbgra32
                        if (LooksLikeStraightAlpha(bmp))   // 仍像直通道 → 兜底手工预乘（仅少数 PSD 会触发）
                            bmp = ForcePremultiply(bmp);
                    }
                    // 没有 Alpha 的 PSD（很少见）就不做任何转换，保持极速
                    return bmp;
                }




                // 你已有的异步加载，返回 BitmapSource 或者先得到 RasterImage 再转 BitmapSource
                var task = LeadtoolsImageLoaderNew.LoadImageAsync(imagePath).GetAwaiter().GetResult();


                // 关键：统一转为 Pbgra32（预乘）
                return ForceMaterializePbgra32(task);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"LEADTOOLS failed to load {imagePath}: {ex.Message}");
                throw;
            }
        }


        private static bool LooksLikeStraightAlpha(BitmapSource src, int step = 16)
        {
            var tmp = src.Format == PixelFormats.Bgra32 ? src
                     : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);

            int stride = (tmp.PixelWidth * 32 + 7) / 8;
            var buf = new byte[stride * tmp.PixelHeight];
            tmp.CopyPixels(buf, stride, 0);

            for (int y = 0; y < tmp.PixelHeight; y += step)
            {
                int row = y * stride;
                for (int x = 0; x < tmp.PixelWidth; x += step)
                {
                    int i = row + x * 4;
                    byte b = buf[i + 0], g = buf[i + 1], r = buf[i + 2], a = buf[i + 3];
                    if (a < 255)
                    {
                        // 预乘数据应满足  r,g,b <= a（线性空间近似，允许一点容差）
                        if (r > a + 1 || g > a + 1 || b > a + 1)
                            return true; // 很可能是“未预乘”的直通道
                    }
                }
            }
            return false;
        }

        private static BitmapSource ForcePremultiply(BitmapSource src)
        {
            // 先转 BGRA32，逐像素做 r=g=b=channel * a / 255
            var baseBgra = src.Format == PixelFormats.Bgra32
                ? src
                : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);

            int w = baseBgra.PixelWidth, h = baseBgra.PixelHeight;
            int stride = (w * 32 + 7) / 8;
            var buf = new byte[stride * h];
            baseBgra.CopyPixels(buf, stride, 0);

            for (int i = 0; i < buf.Length; i += 4)
            {
                byte a = buf[i + 3];
                if (a != 255)
                {
                    buf[i + 0] = (byte)((buf[i + 0] * a + 127) / 255); // B
                    buf[i + 1] = (byte)((buf[i + 1] * a + 127) / 255); // G
                    buf[i + 2] = (byte)((buf[i + 2] * a + 127) / 255); // R
                }
            }

            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Pbgra32, null);
            wb.Lock();
            wb.WritePixels(new Int32Rect(0, 0, w, h), buf, stride, 0);
            wb.Unlock();
            wb.Freeze();
            return wb;
        }


        /// <summary>
        /// 使用STBImageSharp加载图片
        /// </summary>
        private BitmapSource LoadImageWithSTBImageSharp(string imagePath)
        {
            byte[] bytes = File.ReadAllBytes(imagePath);
            var result = StbImageSharp.ImageResult.FromMemory(bytes, StbImageSharp.ColorComponents.RedGreenBlueAlpha); // RGBA

            var wb = new WriteableBitmap(result.Width, result.Height, 96, 96, PixelFormats.Bgra32, null);
            wb.Lock();
            unsafe
            {
                byte* dst = (byte*)wb.BackBuffer.ToPointer();
                int stride = wb.BackBufferStride;
                fixed (byte* src = result.Data)
                {
                    for (int y = 0; y < result.Height; y++)
                    {
                        byte* row = dst + y * stride;
                        int si = y * result.Width * 4;
                        for (int x = 0; x < result.Width; x++)
                        {
                            int s = si + x * 4;   // RGBA
                            int d = x * 4;        // BGRA
                            row[d + 0] = src[s + 2]; // B
                            row[d + 1] = src[s + 1]; // G
                            row[d + 2] = src[s + 0]; // R
                            row[d + 3] = src[s + 3]; // A
                        }
                    }
                }
            }
            wb.AddDirtyRect(new Int32Rect(0, 0, result.Width, result.Height));
            wb.Unlock();
            wb.Freeze();
            return wb;
        }


        /// <summary>
        /// 将STBImageSharp的ImageResult转换为BitmapImage
        /// </summary>
        private BitmapSource ConvertSTBImageToBitmapImage(ImageResult result)
        {
            try
            {
                // 创建WriteableBitmap
                var writeableBitmap = new WriteableBitmap(result.Width, result.Height, 96, 96, PixelFormats.Bgra32, null);
                
                // 锁定位图进行写入
                writeableBitmap.Lock();
                try
                {
                    unsafe
                    {
                        byte* pixels = (byte*)writeableBitmap.BackBuffer.ToPointer();
                        int stride = writeableBitmap.BackBufferStride;
                        
                        // STBImageSharp返回RGBA格式，需要转换为BGRA格式
                        for (int y = 0; y < result.Height; y++)
                        {
                            for (int x = 0; x < result.Width; x++)
                            {
                                int srcIndex = (y * result.Width + x) * 4;
                                int dstIndex = y * stride + x * 4;
                                
                                // RGBA -> BGRA
                                pixels[dstIndex] = result.Data[srcIndex + 2];     // B
                                pixels[dstIndex + 1] = result.Data[srcIndex + 1]; // G
                                pixels[dstIndex + 2] = result.Data[srcIndex];     // R
                                pixels[dstIndex + 3] = result.Data[srcIndex + 3]; // A
                            }
                        }
                    }
                    
                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, result.Width, result.Height));
                }
                finally
                {
                    writeableBitmap.Unlock();
                }
                
                // 转换为BitmapImage
                using (var ms = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                    encoder.Save(ms);
                    ms.Position = 0;
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"STBImageSharp conversion failed: {ex.Message}");
                throw;
            }
        }

        ///// <summary>
        ///// 使用ImageMagick加载图片
        ///// </summary>
        //private BitmapSource LoadImageWithMagick(string imagePath)
        //{
        //    try
        //    {
        //        using (var magickImage = new MagickImage(imagePath))
        //        {
        //            return CreateBitmapFromMagickImage(magickImage);
        //        }
        //    }
        //    catch
        //    {
        //        try
        //        {
        //            return LoadBitmapImageFromFile(imagePath);
        //        }
        //        catch (Exception ex)
        //        {
        //            throw new InvalidOperationException($"无法加载图片: {imagePath}", ex);
        //        }
        //    }
        //}

        private BitmapSource LoadImageWithMagick(string imagePath)
        {
            using (var m = new MagickImage(imagePath))
            {
                // 可选：m.ColorSpace = ColorSpace.sRGB; m.Depth = 8;
                var src = m.ToBitmapSource();  // 直接得到 BitmapSource
                src.Freeze();
                return src;
            }
        }



        /// <summary>
        /// 将LEADTOOLS RasterImage转换为BitmapImage
        /// </summary>
        private BitmapSource ConvertRasterImageToBitmapImage(RasterImage rasterImage)
        {

            var codecs = LeadCodecsOrNull ?? throw new InvalidOperationException("LEADTOOLS 未初始化");
            try
            {
                using (var ms = new MemoryStream())
                {
                    leadtoolsCodecs.Save(rasterImage, ms, RasterImageFormat.Png, 0);
                    ms.Position = 0;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                // 如果PNG格式失败，尝试BMP格式
                using (var ms = new MemoryStream())
                {
                    leadtoolsCodecs.Save(rasterImage, ms, RasterImageFormat.Bmp, 24);
                    ms.Position = 0;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
        }

        /// <summary>
        /// 为 GIF 动画加载静态源图像（用于 WpfAnimatedGif 控件）。
        /// </summary>
        public BitmapSource LoadGifAnimationSource(string gifPath)
        {
            if (string.IsNullOrWhiteSpace(gifPath))
                throw new ArgumentException("gifPath 不能为空", nameof(gifPath));

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(gifPath);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }

        /// <summary>
        /// 从文件加载背景图片，并返回包含图像刷的结果。
        /// </summary>
        public BackgroundImageResult LoadBackgroundImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("imagePath 不能为空", nameof(imagePath));
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("背景图片不存在", imagePath);

            var bitmap = LoadBitmapImageFromFile(imagePath);
            var brush = CreateBackgroundBrush(bitmap);
            return new BackgroundImageResult(brush, imagePath, usedFallback: false);
        }

        /// <summary>
        /// 加载默认背景图片，如果默认资源不存在则回退到渐变背景。
        /// </summary>
        public BackgroundImageResult LoadDefaultBackgroundImage(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("baseDirectory 不能为空", nameof(baseDirectory));

            string defaultImagePath = Path.Combine(baseDirectory, "res", "01.jpg");
            if (File.Exists(defaultImagePath))
            {
                var bitmap = LoadBitmapImageFromFile(defaultImagePath);
                var brush = CreateBackgroundBrush(bitmap);
                return new BackgroundImageResult(brush, defaultImagePath, usedFallback: false);
            }

            var fallbackBrush = CreateBackgroundBrush(CreateGradientImage());
            return new BackgroundImageResult(fallbackBrush, null, usedFallback: true);
        }

        /// <summary>
        /// 为指定图片生成 RGB/Alpha 通道。
        /// </summary>
        public List<Tuple<string, BitmapImage>> LoadChannels(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("imagePath 不能为空", nameof(imagePath));

            using (var magickImage = new MagickImage(imagePath))
            {
                return LoadChannelsFromMagickImage(magickImage);
            }
        }

        /// <summary>
        /// 为剪贴板图片生成通道信息。
        /// </summary>
        public List<Tuple<string, BitmapImage>> LoadChannels(BitmapSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            byte[] imageBytes = ConvertBitmapSourceToBytes(source);
            using (var magickImage = new MagickImage(imageBytes))
            {
                return LoadChannelsFromMagickImage(magickImage);
            }
        }

        private List<Tuple<string, BitmapImage>> LoadChannelsFromMagickImage(MagickImage magickImage)
        {
            var channels = new List<Tuple<string, BitmapImage>>();

            var redImage = new MagickImage(magickImage);
            try
            {
                redImage.Evaluate(Channels.Green, EvaluateOperator.Set, 0);
                redImage.Evaluate(Channels.Blue, EvaluateOperator.Set, 0);
                var redBitmap = CreateBitmapFromMagickImage(redImage);
                channels.Add(Tuple.Create("红色 (R)", redBitmap));
            }
            finally
            {
                redImage.Dispose();
            }

            var greenImage = new MagickImage(magickImage);
            try
            {
                greenImage.Evaluate(Channels.Red, EvaluateOperator.Set, 0);
                greenImage.Evaluate(Channels.Blue, EvaluateOperator.Set, 0);
                var greenBitmap = CreateBitmapFromMagickImage(greenImage);
                channels.Add(Tuple.Create("绿色 (G)", greenBitmap));
            }
            finally
            {
                greenImage.Dispose();
            }

            var blueImage = new MagickImage(magickImage);
            try
            {
                blueImage.Evaluate(Channels.Red, EvaluateOperator.Set, 0);
                blueImage.Evaluate(Channels.Green, EvaluateOperator.Set, 0);
                var blueBitmap = CreateBitmapFromMagickImage(blueImage);
                channels.Add(Tuple.Create("蓝色 (B)", blueBitmap));
            }
            finally
            {
                blueImage.Dispose();
            }

            if (magickImage.HasAlpha)
            {
                var alphaImage = new MagickImage(magickImage);
                try
                {
                    alphaImage.Alpha(AlphaOption.Extract);
                    alphaImage.Format = MagickFormat.Png;
                    var alphaBitmap = CreateBitmapFromMagickImage(alphaImage);
                    channels.Add(Tuple.Create("透明 (Alpha)", alphaBitmap));
                }
                finally
                {
                    alphaImage.Dispose();
                }
            }

            int expectedChannels = magickImage.HasAlpha ? 4 : 3;
            if (channels.Count != expectedChannels)
            {
                throw new InvalidOperationException($"通道生成不完整，预期 {expectedChannels} 个通道，实际生成 {channels.Count} 个");
            }

            return channels;
        }

        private BitmapSource LoadBitmapImageFromFile(string imagePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private ImageBrush CreateBackgroundBrush(BitmapSource source)
        {
            return new ImageBrush(source)
            {
                Stretch = Stretch.UniformToFill,
                TileMode = TileMode.Tile,
                Opacity = backgroundOpacity
            };
        }

        private BitmapSource CreateGradientImage()
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var gradientBrush = new LinearGradientBrush();
                gradientBrush.StartPoint = new Point(0, 0);
                gradientBrush.EndPoint = new Point(1, 1);
                gradientBrush.GradientStops.Add(new GradientStop(Colors.LightBlue, 0.0));
                gradientBrush.GradientStops.Add(new GradientStop(Colors.LightGray, 1.0));

                context.DrawRectangle(gradientBrush, null, new Rect(0, 0, 256, 256));
            }

            var renderBitmap = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(visual);
            renderBitmap.Freeze();
            return renderBitmap;
        }

        private BitmapImage CreateBitmapFromMagickImage(MagickImage magickImage)
        {
            magickImage.Format = MagickFormat.Png;
            byte[] imageBytes = magickImage.ToByteArray();

            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(imageBytes);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        private byte[] ConvertBitmapSourceToBytes(BitmapSource bitmapSource)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            /*
            if (leadtoolsCodecs != null)
            {
                leadtoolsCodecs.Dispose();
                leadtoolsCodecs = null;
            }
            */
        }

        private BitmapSource SafeLoad(Func<BitmapSource> loader, string engineName, string imagePath, bool showDialogOnError)
        {
            try
            {
                return loader();
            }
            catch (Exception ex)
            {
                // 记录日志
                Console.WriteLine($"[{engineName}] 加载失败: {imagePath}\n{ex}");

                // 手动模式：给用户一个可理解的弹窗
                if (showDialogOnError)
                {
                    // 确保在UI线程弹窗
                    if (Application.Current?.Dispatcher != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                $"使用引擎 {engineName} 加载失败：\n{ex.Message}",
                                "加载失败",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                        });
                    }
                    else
                    {
                        // 兜底
                        MessageBox.Show(
                            $"使用引擎 {engineName} 加载失败：\n{ex.Message}",
                            "加载失败",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                }

                // 返回一张“错误图片”，避免崩溃
                return CreateErrorImage($"加载失败（{engineName}）");
            }
        }

    }

    public class BackgroundImageResult
    {
        public BackgroundImageResult(ImageBrush brush, string sourcePath, bool usedFallback)
        {
            Brush = brush ?? throw new ArgumentNullException(nameof(brush));
            SourcePath = sourcePath;
            UsedFallback = usedFallback;
        }

        public ImageBrush Brush { get; }
        public string SourcePath { get; }
        public bool UsedFallback { get; }
    }


}
