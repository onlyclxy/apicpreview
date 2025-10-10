using PicViewEx.ImageChannels;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace PicViewEx.ImageChannels
{
    /// <summary>
    /// 表示一个通道位图，包含通道名称、位图数据和分辨率信息
    /// </summary>
    public sealed class ChannelBitmap
    {
        /// <summary>
        /// 通道名称（Red, Green, Blue, Alpha）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 通道位图数据，已经Freeze()处理，可在任意线程访问
        /// </summary>
        public BitmapSource Bitmap { get; set; }

        /// <summary>
        /// 是否为全分辨率版本（true=原图尺寸，false=预览尺寸）
        /// </summary>
        public bool IsFullRes { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">通道名称</param>
        /// <param name="bitmap">位图数据</param>
        /// <param name="isFullRes">是否为全分辨率</param>
        public ChannelBitmap(string name, BitmapSource bitmap, bool isFullRes)
        {
            Name = name;
            Bitmap = bitmap;
            IsFullRes = isFullRes;
        }
    }

    /// <summary>
    /// 通道服务接口，提供图像通道提取和缓存功能
    /// </summary>
    public interface IChannelService
    {
        /// <summary>
        /// 获取预览尺寸的所有通道（R/G/B/A）
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <param name="maxEdge">预览图最大边长，默认300像素</param>
        /// <returns>包含四个通道的预览位图列表</returns>
        IReadOnlyList<ChannelBitmap> GetPreviewChannels(string path, int maxEdge = 300);

        /// <summary>
        /// 获取指定通道的全分辨率版本
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <param name="channelName">通道名称（Red, Green, Blue, Alpha）</param>
        /// <returns>指定通道的全分辨率位图</returns>
        ChannelBitmap GetFullResChannel(string path, string channelName);
    }

    /// <summary>
    /// 红色通道访问器
    /// </summary>
    public class Red
    {
        private readonly string _imagePath;

        /// <summary>
        /// 通道名称常量
        /// </summary>
        public const string ChannelName = "Red";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="imagePath">图像文件路径</param>
        public Red(string imagePath)
        {
            _imagePath = imagePath;
        }

        /// <summary>
        /// 获取红色通道预览位图
        /// </summary>
        public ChannelBitmap Preview => GetPreview();

        /// <summary>
        /// 获取红色通道全尺寸位图
        /// </summary>
        public ChannelBitmap FullRes => GetFullRes();

        /// <summary>
        /// 获取指定图像的红色通道预览
        /// </summary>
        /// <param name="maxEdge">预览最大边长</param>
        /// <returns>红色通道位图</returns>
        public ChannelBitmap GetPreview(int maxEdge = 300)
        {
            var channels = ChannelService.Instance.GetPreviewChannels(_imagePath, maxEdge);
            return channels?.FirstOrDefault(c => c.Name == ChannelName);
        }

        /// <summary>
        /// 获取指定图像的红色通道全尺寸版本
        /// </summary>
        /// <returns>红色通道位图</returns>
        public ChannelBitmap GetFullRes()
        {
            return ChannelService.Instance.GetFullResChannel(_imagePath, ChannelName);
        }

        /// <summary>
        /// 静态方法：获取指定路径图像的红色通道预览
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <param name="maxEdge">预览最大边长</param>
        /// <returns>红色通道位图</returns>
        public static ChannelBitmap GetPreview(string path, int maxEdge = 300)
        {
            var channels = ChannelService.Instance.GetPreviewChannels(path, maxEdge);
            return channels?.FirstOrDefault(c => c.Name == ChannelName);
        }

        /// <summary>
        /// 静态方法：获取指定路径图像的红色通道全尺寸版本
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <returns>红色通道位图</returns>
        public static ChannelBitmap GetFullRes(string path)
        {
            return ChannelService.Instance.GetFullResChannel(path, ChannelName);
        }
    }

    /// <summary>
    /// 绿色通道访问器
    /// </summary>
    public class Green
    {
        private readonly string _imagePath;

        /// <summary>
        /// 通道名称常量
        /// </summary>
        public const string ChannelName = "Green";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="imagePath">图像文件路径</param>
        public Green(string imagePath)
        {
            _imagePath = imagePath;
        }

        /// <summary>
        /// 获取绿色通道预览位图
        /// </summary>
        public ChannelBitmap Preview => GetPreview();

        /// <summary>
        /// 获取绿色通道全尺寸位图
        /// </summary>
        public ChannelBitmap FullRes => GetFullRes();

        /// <summary>
        /// 获取指定图像的绿色通道预览
        /// </summary>
        /// <param name="maxEdge">预览最大边长</param>
        /// <returns>绿色通道位图</returns>
        public ChannelBitmap GetPreview(int maxEdge = 300)
        {
            var channels = ChannelService.Instance.GetPreviewChannels(_imagePath, maxEdge);
            return channels?.FirstOrDefault(c => c.Name == ChannelName);
        }

        /// <summary>
        /// 获取指定图像的绿色通道全尺寸版本
        /// </summary>
        /// <returns>绿色通道位图</returns>
        public ChannelBitmap GetFullRes()
        {
            return ChannelService.Instance.GetFullResChannel(_imagePath, ChannelName);
        }

        /// <summary>
        /// 静态方法：获取指定路径图像的绿色通道预览
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <param name="maxEdge">预览最大边长</param>
        /// <returns>绿色通道位图</returns>
        public static ChannelBitmap GetPreview(string path, int maxEdge = 300)
        {
            var channels = ChannelService.Instance.GetPreviewChannels(path, maxEdge);
            return channels?.FirstOrDefault(c => c.Name == ChannelName);
        }

        /// <summary>
        /// 静态方法：获取指定路径图像的绿色通道全尺寸版本
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <returns>绿色通道位图</returns>
        public static ChannelBitmap GetFullRes(string path)
        {
            return ChannelService.Instance.GetFullResChannel(path, ChannelName);
        }
    }

    /// <summary>
    /// 蓝色通道访问器
    /// </summary>
    public class Blue
    {
        private readonly string _imagePath;

        /// <summary>
        /// 通道名称常量
        /// </summary>
        public const string ChannelName = "Blue";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="imagePath">图像文件路径</param>
        public Blue(string imagePath)
        {
            _imagePath = imagePath;
        }

        /// <summary>
        /// 获取蓝色通道预览位图
        /// </summary>
        public ChannelBitmap Preview => GetPreview();

        /// <summary>
        /// 获取蓝色通道全尺寸位图
        /// </summary>
        public ChannelBitmap FullRes => GetFullRes();

        /// <summary>
        /// 获取指定图像的蓝色通道预览
        /// </summary>
        /// <param name="maxEdge">预览最大边长</param>
        /// <returns>蓝色通道位图</returns>
        public ChannelBitmap GetPreview(int maxEdge = 300)
        {
            var channels = ChannelService.Instance.GetPreviewChannels(_imagePath, maxEdge);
            return channels?.FirstOrDefault(c => c.Name == ChannelName);
        }

        /// <summary>
        /// 获取指定图像的蓝色通道全尺寸版本
        /// </summary>
        /// <returns>蓝色通道位图</returns>
        public ChannelBitmap GetFullRes()
        {
            return ChannelService.Instance.GetFullResChannel(_imagePath, ChannelName);
        }

        /// <summary>
        /// 静态方法：获取指定路径图像的蓝色通道预览
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <param name="maxEdge">预览最大边长</param>
        /// <returns>蓝色通道位图</returns>
        public static ChannelBitmap GetPreview(string path, int maxEdge = 300)
        {
            var channels = ChannelService.Instance.GetPreviewChannels(path, maxEdge);
            return channels?.FirstOrDefault(c => c.Name == ChannelName);
        }

        /// <summary>
        /// 静态方法：获取指定路径图像的蓝色通道全尺寸版本
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <returns>蓝色通道位图</returns>
        public static ChannelBitmap GetFullRes(string path)
        {
            return ChannelService.Instance.GetFullResChannel(path, ChannelName);
        }
    }

    /// <summary>
    /// Alpha通道访问器
    /// </summary>
    public class Alpha
    {
        private readonly string _imagePath;

        /// <summary>
        /// 通道名称常量
        /// </summary>
        public const string ChannelName = "Alpha";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="imagePath">图像文件路径</param>
        public Alpha(string imagePath)
        {
            _imagePath = imagePath;
        }

        /// <summary>
        /// 获取Alpha通道预览位图
        /// </summary>
        public ChannelBitmap Preview => GetPreview();

        /// <summary>
        /// 获取Alpha通道全尺寸位图
        /// </summary>
        public ChannelBitmap FullRes => GetFullRes();

        /// <summary>
        /// 获取指定图像的Alpha通道预览
        /// </summary>
        /// <param name="maxEdge">预览最大边长</param>
        /// <returns>Alpha通道位图</returns>
        public ChannelBitmap GetPreview(int maxEdge = 300)
        {
            var channels = ChannelService.Instance.GetPreviewChannels(_imagePath, maxEdge);
            return channels?.FirstOrDefault(c => c.Name == ChannelName);
        }

        /// <summary>
        /// 获取指定图像的Alpha通道全尺寸版本
        /// </summary>
        /// <returns>Alpha通道位图</returns>
        public ChannelBitmap GetFullRes()
        {
            return ChannelService.Instance.GetFullResChannel(_imagePath, ChannelName);
        }

        /// <summary>
        /// 静态方法：获取指定路径图像的Alpha通道预览
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <param name="maxEdge">预览最大边长</param>
        /// <returns>Alpha通道位图</returns>
        public static ChannelBitmap GetPreview(string path, int maxEdge = 300)
        {
            var channels = ChannelService.Instance.GetPreviewChannels(path, maxEdge);
            return channels?.FirstOrDefault(c => c.Name == ChannelName);
        }

        /// <summary>
        /// 静态方法：获取指定路径图像的Alpha通道全尺寸版本
        /// </summary>
        /// <param name="path">图像文件路径</param>
        /// <returns>Alpha通道位图</returns>
        public static ChannelBitmap GetFullRes(string path)
        {
            return ChannelService.Instance.GetFullResChannel(path, ChannelName);
        }
    }
}