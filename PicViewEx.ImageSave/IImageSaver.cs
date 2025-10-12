using System.Windows.Media.Imaging;

namespace PicViewEx.ImageSave
{
    /// <summary>
    /// 图片保存接口
    /// </summary>
    public interface IImageSaver
    {
        /// <summary>
        /// 直接保存图片到原路径，保持原始图片的所有参数（质量、压缩率等）
        /// </summary>
        /// <param name="source">要保存的图片数据</param>
        /// <param name="originalFilePath">原始文件路径</param>
        /// <returns>保存结果</returns>
        SaveResult Save(BitmapSource source, string originalFilePath);

        /// <summary>
        /// 另存为图片，允许用户选择格式和参数
        /// </summary>
        /// <param name="source">要保存的图片数据</param>
        /// <param name="originalFilePath">原始文件路径（用于确定默认格式）</param>
        /// <returns>保存结果</returns>
        SaveResult SaveAs(BitmapSource source, string originalFilePath);

        /// <summary>
        /// 保存到指定路径，使用特定参数
        /// </summary>
        /// <param name="source">要保存的图片数据</param>
        /// <param name="targetPath">目标文件路径</param>
        /// <param name="options">保存选项</param>
        /// <returns>保存结果</returns>
        SaveResult SaveTo(BitmapSource source, string targetPath, SaveOptions options);
    }

    /// <summary>
    /// 保存结果
    /// </summary>
    public class SaveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string SavedPath { get; set; }
        public string ErrorDetails { get; set; }
    }

    /// <summary>
    /// 保存选项基类
    /// </summary>
    public abstract class SaveOptions
    {
        public string Format { get; set; }
    }

    /// <summary>
    /// JPEG保存选项
    /// </summary>
    public class JpegSaveOptions : SaveOptions
    {
        public int Quality { get; set; } = 85;

        public JpegSaveOptions()
        {
            Format = "JPG";
        }
    }

    /// <summary>
    /// PNG保存选项
    /// </summary>
    public class PngSaveOptions : SaveOptions
    {
        public PngSaveOptions()
        {
            Format = "PNG";
        }
    }

    /// <summary>
    /// BMP保存选项
    /// </summary>
    public class BmpSaveOptions : SaveOptions
    {
        public BmpSaveOptions()
        {
            Format = "BMP";
        }
    }

    /// <summary>
    /// TGA保存选项
    /// </summary>
    public class TgaSaveOptions : SaveOptions
    {
        public TgaSaveOptions()
        {
            Format = "TGA";
        }
    }

    /// <summary>
    /// DDS保存选项
    /// </summary>
    public class DdsSaveOptions : SaveOptions
    {
        /// <summary>
        /// BC压缩格式（例如：BC1, BC3, BC7等）
        /// </summary>
        public string CompressionFormat { get; set; }

        /// <summary>
        /// 是否生成Mipmap
        /// </summary>
        public bool GenerateMipmaps { get; set; }

        /// <summary>
        /// 压缩质量
        /// </summary>
        public string Quality { get; set; } = "Normal";

        /// <summary>
        /// 预设文件路径
        /// </summary>
        public string PresetPath { get; set; }

        /// <summary>
        /// 是否使用NVIDIA UI（不使用预设时）
        /// </summary>
        public bool UseNvidiaUI { get; set; }

        public DdsSaveOptions()
        {
            Format = "DDS";
        }
    }
}
