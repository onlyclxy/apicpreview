using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using MColorType = ImageMagick.ColorType; // ← 新增

namespace PicViewEx.ImageChannels
{
    /// <summary>
    /// 通道服务实现类，提供图像解码、通道提取和缓存功能
    /// 解码路由：WIC优先 → STBImageSharp兜底(TGA/DDS) → Magick.NET兜底(PSD/CMYK/16bit)
    /// </summary>
    public sealed class ChannelService : IChannelService
    {

        // 放在 ChannelService 类内（字段/构造函数附近都可以）
        private enum ChannelLayout { Rgb, Gray /* 以后要扩展 CMYK / Spot 可再加 */ }

        // 用 Magick 读取文件头信息：颜色类型 + 是否带 Alpha（Alpha 即使全 255 也算“有”）
        private static (ChannelLayout layout, bool hasAlpha) ProbeLayout(string path)
        {
            try
            {
                using (var m = new ImageMagick.MagickImage(path))
                {
                    var ct = m.ColorType;
                    bool hasAlpha = m.HasAlpha; // Matte/Alpha 都会返回 true

                    switch (m.ColorType)
                    {
                        case MColorType.Grayscale:
                        case MColorType.GrayscaleAlpha:            // ← 用 GrayscaleAlpha 代替 GrayscaleMatte
                            return (ChannelLayout.Gray, hasAlpha);

                        // （可选）如果以后要区分 CMYK：
                        // case MColorType.ColorSeparation:
                        // case MColorType.ColorSeparationAlpha:
                        //     return (ChannelLayout.Cmyk, hasAlpha);

                        default:
                            return (ChannelLayout.Rgb, hasAlpha);
                    }
                }
            }
            catch
            {
                // 保险：探测失败按 RGB、无 Alpha 处理（不影响稳定性）
                return (ChannelLayout.Rgb, false);
            }
        }




        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly ChannelService Instance = new ChannelService();

        /// <summary>
        /// 私有构造函数，确保单例模式
        /// </summary>
        private ChannelService() { }

        // 缓存Key格式："{FullPath}|{Length}|{LastWriteTimeUtc.Ticks}"
        // 预览缓存：路径 -> 四个通道的预览位图列表
        private readonly ConcurrentDictionary<string, List<ChannelBitmap>> _previewCache =
            new ConcurrentDictionary<string, List<ChannelBitmap>>();

        // 全尺寸缓存：路径 -> (通道名 -> 全尺寸位图)
        private readonly ConcurrentDictionary<string, Dictionary<string, ChannelBitmap>> _fullResCache =
            new ConcurrentDictionary<string, Dictionary<string, ChannelBitmap>>();

        // WIC支持的格式
        private readonly HashSet<string> _wicFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".webp"
        };

        // STB优先处理的格式
        private readonly HashSet<string> _stbFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".tga", ".dds"
        };

        /// <summary>
        /// 获取预览尺寸的所有通道（R/G/B/A）
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <param name="maxEdge">预览图最大边长，默认300像素</param>
        /// <returns>包含四个通道的预览位图列表</returns>
        public IReadOnlyList<ChannelBitmap> GetPreviewChannels(string path, int maxEdge = 300)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new List<ChannelBitmap>();

            var cacheKey = GenerateCacheKey(path);

            // 检查预览缓存
            if (_previewCache.TryGetValue(cacheKey, out var cachedChannels))
            {
                return cachedChannels;
            }

            try
            {
                // 解码预览图像
                var previewBitmap = DecodePreviewImage(path, maxEdge);
                if (previewBitmap == null)
                    return new List<ChannelBitmap>();

                // 新增：探测真实布局 + 是否带 Alpha
                var (layout, hasAlpha) = ProbeLayout(path);

                // 提取通道（按真实布局决定生成什么）
                var channels = ExtractChannels(previewBitmap, isFullRes: false, layout: layout, includeAlpha: hasAlpha);
                

                // 缓存结果
                _previewCache[cacheKey] = channels;

                return channels;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取预览通道失败: {path}, 错误: {ex.Message}");
                return new List<ChannelBitmap>();
            }
        }

        /// <summary>
        /// 获取指定通道的全分辨率版本
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <param name="channelName">通道名称（Red, Green, Blue, Alpha）</param>
        /// <returns>指定通道的全分辨率位图</returns>
        public ChannelBitmap GetFullResChannel(string path, string channelName)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(channelName))
                return null;

            var cacheKey = GenerateCacheKey(path);

            // 检查全尺寸缓存
            if (_fullResCache.TryGetValue(cacheKey, out var cachedChannels) &&
                cachedChannels.TryGetValue(channelName, out var cachedChannel))
            {
                return cachedChannel;
            }

            try
            {
                // 解码全尺寸图像
                var fullResBitmap = DecodeFullResImage(path);
                if (fullResBitmap == null)
                    return null;

                // 新增：探测真实布局 + 是否带 Alpha
                var (layout, hasAlpha) = ProbeLayout(path);

                // 提取所有通道（按真实布局）
                var channels = ExtractChannels(fullResBitmap, isFullRes: true, layout: layout, includeAlpha: hasAlpha);

                // 缓存所有通道
                var channelDict = channels.ToDictionary(c => c.Name, c => c);
                _fullResCache[cacheKey] = channelDict;

                // 返回请求的通道
                return channelDict.TryGetValue(channelName, out var requestedChannel) ? requestedChannel : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取全尺寸通道失败: {path}, 通道: {channelName}, 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 生成缓存键：路径|文件大小|最后修改时间
        /// </summary>
        private string GenerateCacheKey(string path)
        {
            var fileInfo = new FileInfo(path);
            return $"{fileInfo.FullName}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
        }

        /// <summary>
        /// 解码预览图像（最大边300像素）
        /// 解码路由：WIC优先 → STB兜底 → Magick兜底
        /// </summary>
        private BitmapSource DecodePreviewImage(string path, int maxEdge)
        {
            var extension = Path.GetExtension(path);

            // 1. WIC优先：支持常见格式，解码阶段就降采样
            if (_wicFormats.Contains(extension))
            {
                var wicResult = TryDecodeWithWic(path, maxEdge);
                if (wicResult != null) return wicResult;
            }

            // 2. STB兜底：TGA/DDS等格式
            if (_stbFormats.Contains(extension))
            {
                var stbResult = TryDecodeWithStb(path, maxEdge);
                if (stbResult != null) return stbResult;
            }

            // 3. Magick兜底：PSD/CMYK/16bit等复杂格式
            return TryDecodeWithMagick(path, maxEdge);
        }

        /// <summary>
        /// 解码全尺寸图像
        /// </summary>
        private BitmapSource DecodeFullResImage(string path)
        {
            var extension = Path.GetExtension(path);

            // 1. WIC优先：不降采样
            if (_wicFormats.Contains(extension))
            {
                var wicResult = TryDecodeWithWic(path, 0); // 0表示不降采样
                if (wicResult != null) return wicResult;
            }

            // 2. STB兜底
            if (_stbFormats.Contains(extension))
            {
                var stbResult = TryDecodeWithStb(path, 0);
                if (stbResult != null) return stbResult;
            }

            // 3. Magick兜底
            return TryDecodeWithMagick(path, 0);
        }

        /// <summary>
        /// 使用WIC解码图像
        /// </summary>
        private BitmapSource TryDecodeWithWic(string path, int maxEdge)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;

                // 预览模式：解码阶段降采样
                if (maxEdge > 0)
                {
                    bitmap.DecodePixelWidth = maxEdge;
                }

                bitmap.EndInit();

                // 转换为BGRA32格式
                var convertedBitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                convertedBitmap.Freeze();

                return convertedBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WIC解码失败: {path}, 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 使用STBImageSharp解码图像（TGA/DDS等）
        /// 注意：此方法为空实现，需要安装StbImageSharp包后启用
        /// </summary>
        private BitmapSource TryDecodeWithStb(string path, int maxEdge)
        {
            // TODO: 实现STB解码
            // 1. 安装NuGet包：StbImageSharp
            // 2. 使用StbImage.LoadFromFile()解码到RGBA8
            // 3. 转换为BGRA32的WriteableBitmap
            // 4. 如果maxEdge > 0，进行降采样
            // 5. Freeze()后返回

            System.Diagnostics.Debug.WriteLine($"STB解码暂未实现: {path}");
            return null;
        }

        /// <summary>
        /// 使用Magick.NET解码图像（PSD/CMYK/16bit等复杂格式）
        /// </summary>
        private BitmapSource TryDecodeWithMagick(string path, int maxEdge)
        {
            try
            {
                using (var magickImage = new MagickImage(path))
                {
                    // 设置颜色空间为sRGB，启用Alpha通道
                    magickImage.ColorSpace = ColorSpace.sRGB;
                    magickImage.Alpha(AlphaOption.On);

                    // 预览模式：降采样
                    if (maxEdge > 0)
                    {
                        var geometry = new MagickGeometry((uint)maxEdge, (uint)maxEdge)
                        {
                            IgnoreAspectRatio = false,
                            Greater = false,
                            Less = true
                        };
                        magickImage.Resize(geometry);
                    }

                    // 转换为BGRA8格式
                    magickImage.Format = MagickFormat.Bgra;
                    var pixelData = magickImage.ToByteArray();

                    // 创建WriteableBitmap
                    var writeableBitmap = new WriteableBitmap(
                        (int)magickImage.Width,
                        (int)magickImage.Height,
                        96, 96,
                        PixelFormats.Bgra32,
                        null);

                    // 写入像素数据
                    var rect = new Int32Rect(0, 0, (int)magickImage.Width, (int)magickImage.Height);
                    var stride = (int)magickImage.Width * 4; // BGRA32 = 4字节/像素
                    writeableBitmap.WritePixels(rect, pixelData, stride, 0);

                    writeableBitmap.Freeze();
                    return writeableBitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Magick解码失败: {path}, 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从BGRA32位图中并行提取R/G/B/A四个通道
        /// </summary>
        private List<ChannelBitmap> ExtractChannels(BitmapSource source, bool isFullRes, ChannelLayout layout, bool includeAlpha)
        {
            var result = new List<ChannelBitmap>();
            if (source == null) return result;

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * 4; // 输入是 BGRA32
            var pixelData = new byte[height * stride];
            source.CopyPixels(pixelData, stride, 0);

            // 一个小工具：把某个“通道值”复制到灰度图（R=G=B=val，A=255）
            ChannelBitmap MakeGrayFromSelector(string name, Func<int, byte> select)
            {
                var gray = new byte[pixelData.Length];
                int pixels = width * height;

                for (int i = 0; i < pixels; i++)
                {
                    int o = i * 4;
                    byte v = select(o);
                    gray[o + 0] = v; // B
                    gray[o + 1] = v; // G
                    gray[o + 2] = v; // R
                    gray[o + 3] = 255;
                }

                var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                wb.WritePixels(new Int32Rect(0, 0, width, height), gray, stride, 0);
                wb.Freeze();
                return new ChannelBitmap(name, wb, isFullRes);
            }

            if (layout == ChannelLayout.Gray)
            {
                // 源是“灰度系”（没有独立的 R/G/B）
                // WIC/STB 解码到 BGRA 时三通道会相等，这里取 R（索引 2）即可
                result.Add(MakeGrayFromSelector("Gray", o => pixelData[o + 2]));

                if (includeAlpha)
                {
                    result.Add(MakeGrayFromSelector("Alpha", o => pixelData[o + 3]));
                }

                return result;
            }

            // layout == RGB：按 B/G/R（BGRA 顺序索引 0/1/2）提取
            // 名字按 R/G/B 排序输出（符合常见 UI 习惯）
            var red = MakeGrayFromSelector("红通道", o => pixelData[o + 2]);
            var green = MakeGrayFromSelector("绿通道", o => pixelData[o + 1]);
            var blue = MakeGrayFromSelector("蓝通道", o => pixelData[o + 0]);

            result.Add(red);
            result.Add(green);
            result.Add(blue);

            if (includeAlpha)
            {
                var alpha = MakeGrayFromSelector("透明通道", o => pixelData[o + 3]);
                result.Add(alpha);
            }

            return result;
        }

    }
}