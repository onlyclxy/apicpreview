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

namespace PicViewEx.ImageChannels
{
    /// <summary>
    /// 通道服务实现类，提供图像解码、通道提取和缓存功能
    /// 解码路由：WIC优先 → STBImageSharp兜底(TGA/DDS) → Magick.NET兜底(PSD/CMYK/16bit)
    /// </summary>
    public sealed class ChannelService : IChannelService
    {
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

                // 提取通道
                var channels = ExtractChannels(previewBitmap, false);

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

                // 提取所有通道（为了缓存效率）
                var channels = ExtractChannels(fullResBitmap, true);

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
        private List<ChannelBitmap> ExtractChannels(BitmapSource source, bool isFullRes)
        {
            if (source == null) return new List<ChannelBitmap>();

            var width = source.PixelWidth;
            var height = source.PixelHeight;
            var stride = width * 4; // BGRA32 = 4字节/像素
            var pixelData = new byte[height * stride];

            // 复制像素数据
            source.CopyPixels(pixelData, stride, 0);

            var channels = new List<ChannelBitmap>(4);
            var channelNames = new[] { "Blue", "Green", "Red", "Alpha" }; // BGRA顺序

            // 并行生成四个通道
            Parallel.For(0, 4, channelIndex =>
            {
                var channelData = new byte[pixelData.Length];

                // 提取指定通道并生成灰度图
                Parallel.For(0, width * height, pixelIndex =>
                {
                    var baseOffset = pixelIndex * 4;
                    var channelValue = pixelData[baseOffset + channelIndex];

                    // 生成灰度图：B=G=R=通道值，A=255
                    channelData[baseOffset] = channelValue;     // B
                    channelData[baseOffset + 1] = channelValue; // G  
                    channelData[baseOffset + 2] = channelValue; // R
                    channelData[baseOffset + 3] = 255;          // A
                });

                // 创建通道位图
                var channelBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                var rect = new Int32Rect(0, 0, width, height);
                channelBitmap.WritePixels(rect, channelData, stride, 0);
                channelBitmap.Freeze();

                var channelName = channelNames[channelIndex];
                lock (channels)
                {
                    channels.Add(new ChannelBitmap(channelName, channelBitmap, isFullRes));
                }
            });

            // 按照R/G/B/A顺序排序返回
            var orderedChannels = new List<ChannelBitmap>(4);
            var nameOrder = new[] { "Red", "Green", "Blue", "Alpha" };

            foreach (var name in nameOrder)
            {
                var channel = channels.FirstOrDefault(c => c.Name == name);
                if (channel != null)
                    orderedChannels.Add(channel);
            }

            return orderedChannels;
        }
    }
}