using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;


using Leadtools;
using Leadtools.Codecs;


namespace PicViewEx
{
    /// <summary>
    /// LEADTOOLS 图像加载器
    /// 提供高性能的图像加载功能，支持多种格式
    /// </summary>
    public static class LeadtoolsImageLoaderNew
    {

        private static RasterCodecs _codecs;
        private static bool _isInitialized = false;
        private static readonly object _initLock = new object();


        /// <summary>
        /// LEADTOOLS 支持的图片格式扩展名集合
        /// 基于 LEADTOOLS 19 的完整格式支持程序集
        /// </summary>
        public static readonly HashSet<string> SupportedFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 常见格式
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp",
            
            // RAW 相机格式
            ".cr2", ".cr3", ".nef", ".arw", ".dng", ".orf", ".rw2", ".pef", ".srw", ".x3f",
            ".raf", ".3fr", ".fff", ".dcr", ".kdc", ".srf", ".ari", ".bay", ".cap", ".dcs",
            ".dcx", ".erf", ".iiq", ".k25", ".mdc", ".mos", ".mrw", ".nrw", ".ptx", ".r3d",
            ".raw", ".rwl", ".rwz", ".sr2", ".srf", ".srw",
            
            // Adobe 格式
            ".psd", ".psb", ".ai", ".eps", ".pdf",
            
            // 专业图形格式
            ".tga", ".pcx", ".iff", ".sgi", ".hdr", ".pic", ".pct", ".mac",
            
            // 压缩格式
            ".j2k", ".jp2", ".jpx", ".jpm", ".mj2", ".jxr", ".hdp", ".wdp",
            
            // 矢量格式
            ".wmf", ".emf", ".cgm", ".drw", ".dwg", ".dxf", ".dgn", ".plt", ".hpgl",
            
            // 文档格式
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".rtf", ".txt", ".htm", ".html",
            ".pdf", ".xps",
            
            // 动画格式
            ".flc", ".fli", ".ani", ".mng",
            
            // 科学/医学格式
            ".dcm", ".dic", ".fit", ".fits", ".fts",
            
            // CAD 格式
            ".dwf", ".dwfx", ".dgn", ".plt", ".hpgl", ".cgm", ".dxf", ".dwg",
            
            // 地理信息格式
            ".shp", ".e00", ".mif", ".tab",
            
            // 传真格式
            ".cal", ".cals", ".dcf", ".sff", ".sfx",
            
            // 其他格式
            ".abc", ".abi", ".afp", ".anz", ".cin", ".clp", ".cmp", ".cmw", ".cmx",
            ".cut", ".dox", ".fpx", ".gbr", ".ica", ".img", ".ing", ".itg", ".jb2",
            ".jbg", ".jls", ".lma", ".lmb", ".msp", ".nap", ".pcl", ".pnm", ".ppx",
            ".pst", ".ptk", ".pub", ".ras", ".sct", ".smp", ".snp", ".tdb", ".tfx",
            ".vff", ".wfx", ".wmp", ".wmz", ".wpg", ".x9f", ".xbm", ".xlx", ".xmp",
            ".xpm", ".xwd", ".mod", ".pic", ".pix", ".sun", ".ras", ".im1", ".im8",
            ".im24", ".im32", ".rs", ".a11", ".att", ".bw", ".bytes", ".cr", ".g3",
            ".g4", ".gray", ".grey", ".mono", ".pal", ".palm", ".pam", ".pcc", ".pgm",
            ".pix", ".ppm", ".rgba", ".sfw", ".sgi", ".sun", ".uyvy", ".viff", ".xv",
            ".yuv", ".bpx", ".cin", ".dpx", ".exr", ".pam", ".pfs", ".yuv", ".fit"
        };

        /// <summary>
        /// 检查是否支持指定格式
        /// </summary>
        public static bool IsFormatSupported(string extension)
        {
            return SupportedFormats.Contains(extension);
        }


        /// <summary>
        /// 初始化 LEADTOOLS
        /// </summary>
        public static bool Initialize()
        {

            if (_isInitialized) return true;
            
            lock (_initLock)
            {
                if (_isInitialized) return true;
                
                try
                {
                    Console.WriteLine("--- InitializeLeadtools: Starting LEADTOOLS initialization ---");
                    
                    // 检查许可证文件是否存在
                    string keyPath = "full_license.key";
                    string licPath = "full_license.lic";
                    
                    if (File.Exists(keyPath) && File.Exists(licPath))
                    {
                        // 加载完整许可证
                        var key = File.ReadAllText(keyPath);
                        var lic = File.ReadAllBytes(licPath);
                        RasterSupport.SetLicense(lic, key);
                        Console.WriteLine("InitializeLeadtools: License loaded.");
                    }
                    else
                    {
                        Console.WriteLine("InitializeLeadtools: License files not found, running in evaluation mode...");
                    }

                    _codecs = new RasterCodecs();

                    // 启用所有可用的编解码器
                    _codecs.Options.Load.AllPages = true;
                    
                    // 为了更好地处理PDF等矢量格式，设置光栅化选项
                    _codecs.Options.RasterizeDocument.Load.XResolution = 300;
                    _codecs.Options.RasterizeDocument.Load.YResolution = 300;

                    _isInitialized = true;
                    Console.WriteLine("InitializeLeadtools: LEADTOOLS initialization successful.");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"InitializeLeadtools: LEADTOOLS initialization failed! Error: {ex.Message}");
                    _codecs?.Dispose();
                    _codecs = null;
                    return false;
                }
            }

   

        }

        /// <summary>
        /// 异步加载图像
        /// </summary>
        public static async Task<BitmapSource> LoadImageAsync(string imagePath)
        {

            Console.WriteLine($"LeadtoolsImageLoader.LoadImageAsync: Starting load for {imagePath}");
            
            if (!_isInitialized && !Initialize())
            {
                Console.WriteLine("LeadtoolsImageLoader: Initialization failed");
                throw new InvalidOperationException("LEADTOOLS not initialized");
            }
            
            if (_codecs == null)
            {
                Console.WriteLine("LeadtoolsImageLoader: Codecs not available");
                throw new InvalidOperationException("LEADTOOLS codecs not available");
            }
            
            try
            {
                // 检查文件是否存在
                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"LeadtoolsImageLoader: File not found: {imagePath}");
                    throw new FileNotFoundException($"File not found: {imagePath}");
                }
                
                Console.WriteLine($"LeadtoolsImageLoader: File exists, starting Task.Run for {imagePath}");
                
                return await Task.Run(() =>
                {
                    Console.WriteLine($"LeadtoolsImageLoader: Inside Task.Run for {imagePath}");
                    try
                    {
                        Console.WriteLine($"LeadtoolsImageLoader: About to call _codecs.Load for {imagePath}");
                        // 使用 LEADTOOLS 加载图像
                        using (var rasterImage = _codecs.Load(imagePath))
                        {
                            Console.WriteLine($"LeadtoolsImageLoader: Successfully loaded RasterImage for {imagePath}, size: {rasterImage.Width}x{rasterImage.Height}");
                            var result = ConvertToWpfBitmap(rasterImage);
                            Console.WriteLine($"LeadtoolsImageLoader: Converted to WPF bitmap for {imagePath}");
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"LEADTOOLS load failed for {imagePath}: {ex.Message}");
                        Console.WriteLine($"Exception type: {ex.GetType().Name}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        throw;
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LeadtoolsImageLoader.LoadImageAsync failed: {ex.Message}");
                throw;
            }

            await Task.CompletedTask;
            throw new NotSupportedException("LEADTOOLS support not compiled in");

        }


        /// <summary>
        /// 将 LEADTOOLS RasterImage 转换为 WPF BitmapSource
        /// </summary>
        private static BitmapSource ConvertToWpfBitmap(RasterImage rasterImage)
        {
            try
            {
                if (_codecs == null)
                    throw new InvalidOperationException("LEADTOOLS codecs not available");
                
                Console.WriteLine($"ConvertToWpfBitmap: Original image - Width: {rasterImage.Width}, Height: {rasterImage.Height}, BitsPerPixel: {rasterImage.BitsPerPixel}, Format: {rasterImage.OriginalFormat}");
                
                // 对于8位PNG，直接回退到ImageMagick以保持最佳质量和羽化效果
                if (rasterImage.OriginalFormat == RasterImageFormat.Png && rasterImage.BitsPerPixel == 8)
                {
                    Console.WriteLine($"ConvertToWpfBitmap: 8-bit PNG detected - falling back to ImageMagick for optimal quality");
                    throw new InvalidOperationException("8-bit PNG fallback to ImageMagick for quality preservation");
                }
                
                // 根据原始图像特性选择合适的保存格式
                RasterImageFormat saveFormat;
                int bitsPerPixel;
                
                // 更精确的透明通道和格式检测
                bool needsAlphaSupport = rasterImage.BitsPerPixel == 32 || 
                                        rasterImage.OriginalFormat == RasterImageFormat.Png ||
                                        rasterImage.OriginalFormat == RasterImageFormat.Tga ||
                                        rasterImage.OriginalFormat == RasterImageFormat.Gif;
                
                // 对于PNG格式，优先保持透明通道支持，不管位深度
                if (rasterImage.OriginalFormat == RasterImageFormat.Png)
                {
                    // PNG格式总是使用PNG保存，保持最大兼容性
                    saveFormat = RasterImageFormat.Png;
                    
                    if (rasterImage.BitsPerPixel == 32)
                    {
                        bitsPerPixel = 32; // 明确32位以保留ARGB
                        Console.WriteLine($"ConvertToWpfBitmap: 32-bit PNG - preserving full ARGB channels");
                    }
                    else if (rasterImage.BitsPerPixel == 24)
                    {
                        bitsPerPixel = 24; // 保持24位RGB
                        Console.WriteLine($"ConvertToWpfBitmap: 24-bit PNG - preserving RGB channels");
                    }
                    else if (rasterImage.BitsPerPixel == 8)
                    {
                        // 8位PNG使用自动处理，保持原始调色板+Alpha特性
                        bitsPerPixel = 0; // 让PNG编码器保持原始格式，保留羽化效果
                        Console.WriteLine($"ConvertToWpfBitmap: 8-bit PNG - preserving original palette/alpha format");
                    }
                    else
                    {
                        bitsPerPixel = 32; // 其他位深度也强制32位以防万一
                        Console.WriteLine($"ConvertToWpfBitmap: {rasterImage.BitsPerPixel}-bit PNG - forcing 32-bit conversion");
                    }
                }
                else if (rasterImage.OriginalFormat == RasterImageFormat.Tga && rasterImage.BitsPerPixel == 32)
                {
                    // 32位TGA特殊处理
                    saveFormat = RasterImageFormat.Png;
                    bitsPerPixel = 32;
                    Console.WriteLine($"ConvertToWpfBitmap: 32-bit TGA - converting to 32-bit PNG for transparency");
                }
                else if (needsAlphaSupport)
                {
                    // 其他可能有透明通道的格式
                    saveFormat = RasterImageFormat.Png;
                    if (rasterImage.BitsPerPixel == 32)
                    {
                        bitsPerPixel = 32;
                        Console.WriteLine($"ConvertToWpfBitmap: 32-bit {rasterImage.OriginalFormat} - preserving transparency");
                    }
                    else
                    {
                        bitsPerPixel = 0; // 让PNG自动处理
                        Console.WriteLine($"ConvertToWpfBitmap: {rasterImage.BitsPerPixel}-bit {rasterImage.OriginalFormat} - auto PNG conversion");
                    }
                }
                else
                {
                    // 不需要透明支持的格式，使用BMP提高性能
                    saveFormat = RasterImageFormat.Bmp;
                    bitsPerPixel = Math.Max(24, rasterImage.BitsPerPixel); // 至少24位
                    Console.WriteLine($"ConvertToWpfBitmap: {rasterImage.OriginalFormat} - using BMP format with {bitsPerPixel} bits");
                }
                
                // 首选方法：使用确定的格式
                using (var ms = new MemoryStream())
                {
                    _codecs.Save(rasterImage, ms, saveFormat, bitsPerPixel);
                    ms.Position = 0;

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze(); // 允许在非UI线程上访问
                    
                    Console.WriteLine($"ConvertToWpfBitmap: Successfully converted using {saveFormat} format with {bitsPerPixel} bits");
                    return bmp;
                }
            }
            catch (Exception ex1)
            {
                Console.WriteLine($"Primary conversion failed: {ex1.Message}");
                
                // 备选方法1：强制使用32位PNG以最大化兼容性
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        // 尝试32位PNG以确保透明通道不丢失
                        _codecs.Save(rasterImage, ms, RasterImageFormat.Png, 32);
                        ms.Position = 0;

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        
                        Console.WriteLine("ConvertToWpfBitmap: Successfully converted using 32-bit PNG fallback");
                        return bmp;
                    }
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"32-bit PNG fallback failed: {ex2.Message}");
                    
                    // 备选方法2：使用自动位深度的PNG
                    try
                    {
                        using (var ms = new MemoryStream())
                        {
                            _codecs.Save(rasterImage, ms, RasterImageFormat.Png, 0);
                            ms.Position = 0;

                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = ms;
                            bmp.EndInit();
                            bmp.Freeze();
                            
                            Console.WriteLine("ConvertToWpfBitmap: Successfully converted using auto PNG fallback");
                            return bmp;
                        }
                    }
                    catch (Exception ex3)
                    {
                        Console.WriteLine($"Auto PNG fallback failed: {ex3.Message}");
                        
                        // 最后的备选方法：TIFF格式
                        try
                        {
                            using (var ms = new MemoryStream())
                            {
                                _codecs.Save(rasterImage, ms, RasterImageFormat.Tif, 0);
                                ms.Position = 0;

                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.StreamSource = ms;
                                bmp.EndInit();
                                bmp.Freeze();
                                
                                Console.WriteLine("ConvertToWpfBitmap: Successfully converted using TIFF format");
                                return bmp;
                            }
                        }
                        catch (Exception ex4)
                        {
                            Console.WriteLine($"TIFF conversion failed: {ex4.Message}");
                            throw new InvalidOperationException($"Failed to convert RasterImage using all formats. Primary: {ex1.Message}, 32PNG: {ex2.Message}, AutoPNG: {ex3.Message}, TIFF: {ex4.Message}");
                        }
                    }
                }
            }
        }


        /// <summary>
        /// 检查文件信息
        /// </summary>
        public static async Task<(int width, int height, int pageCount)?> GetImageInfoAsync(string imagePath)
        {

            if (!_isInitialized && !Initialize())
            {
                return null;
            }
            
            if (_codecs == null)
            {
                return null;
            }
            
            try
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        var info = _codecs.GetInformation(imagePath, true);
                        return ((int, int, int)?)(info.Width, info.Height, info.TotalPages);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"LEADTOOLS GetInformation failed for {imagePath}: {ex.Message}");
                        return ((int, int, int)?)null;
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LeadtoolsImageLoader.GetImageInfoAsync failed: {ex.Message}");
                return null;
            }

            await Task.CompletedTask;
            return null;

        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup()
        {

            try
            {
                lock (_initLock)
                {
                    if (_codecs != null)
                    {
                        _codecs.Dispose();
                        _codecs = null;
                    }
                    _isInitialized = false;
                }
                Console.WriteLine("LeadtoolsImageLoader: Cleanup completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LeadtoolsImageLoader.Cleanup error: {ex.Message}");
            }

        }

        /// <summary>
        /// 获取支持格式的描述信息
        /// </summary>
        public static string GetSupportedFormatsDescription()
        {
            return $"LEADTOOLS 支持 {SupportedFormats.Count} 种图片格式，包括：\n" +
                   "• 常见格式：JPG, PNG, GIF, BMP, TIFF, WebP\n" +
                   "• RAW相机格式：CR2, CR3, NEF, ARW, DNG, ORF, RW2, PEF, SRW, X3F 等40+种\n" +
                   "• Adobe格式：PSD, PSB, AI, EPS, PDF\n" +
                   "• 专业图形：TGA, PCX, IFF, SGI, HDR, PIC\n" +
                   "• 压缩格式：J2K, JP2, JXR, HDP, WDP\n" +
                   "• 矢量格式：WMF, EMF, CGM, DWG, DXF\n" +
                   "• 文档格式：DOC, XLS, PPT, RTF, HTML, PDF, XPS\n" +
                   "• 动画格式：FLC, FLI, ANI, MNG\n" +
                   "• 科学/医学：DCM, FIT, FITS\n" +
                   "• CAD格式：DWF, DGN, PLT, HPGL\n" +
                   "• 地理信息：SHP, E00, MIF\n" +
                   "• 传真格式：CAL, DCF, SFF\n" +
                   "• 以及更多专业和罕见格式";
        }
    }
}