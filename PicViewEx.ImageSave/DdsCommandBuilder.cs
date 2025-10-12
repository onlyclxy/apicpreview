using System.Collections.Generic;
using System.Text;

namespace PicViewEx.ImageSave
{
    /// <summary>
    /// DDS命令行参数构建器
    /// 用于将DDS选项转换为nvtt_export.exe支持的命令行参数
    /// </summary>
    public class DdsCommandBuilder
    {
        /// <summary>
        /// 根据DDS信息构建命令行参数
        /// </summary>
        /// <param name="ddsInfo">DDS文件信息</param>
        /// <param name="inputPath">输入文件路径</param>
        /// <param name="outputPath">输出文件路径</param>
        /// <returns>完整的命令行参数字符串</returns>
        public static string BuildArgumentsFromInfo(DdsFileInfo ddsInfo, string inputPath, string outputPath)
        {
            if (ddsInfo == null)
                return null;

            var args = new List<string>();

            // 输入文件
            args.Add($"\"{inputPath}\"");

            // 输出文件
            args.Add("--output");
            args.Add($"\"{outputPath}\"");

            // 压缩格式
            if (!string.IsNullOrEmpty(ddsInfo.CompressionFormat))
            {
                string format = ConvertToNvttFormat(ddsInfo.CompressionFormat);
                if (!string.IsNullOrEmpty(format))
                {
                    args.Add("--format");
                    args.Add(format);
                }
            }

            // Mipmap设置
            if (ddsInfo.HasMipmaps && ddsInfo.MipLevels > 1)
            {
                args.Add("--mips");
                args.Add(ddsInfo.MipLevels.ToString());
            }
            else
            {
                // 不生成mipmap
                args.Add("--no-mips");
            }

            return string.Join(" ", args);
        }

        /// <summary>
        /// 根据DDS选项构建命令行参数
        /// </summary>
        /// <param name="options">DDS保存选项</param>
        /// <param name="inputPath">输入文件路径</param>
        /// <param name="outputPath">输出文件路径</param>
        /// <returns>完整的命令行参数字符串</returns>
        public static string BuildArgumentsFromOptions(DdsSaveOptions options, string inputPath, string outputPath)
        {
            if (options == null)
                return null;

            var args = new List<string>();

            // 输入文件
            args.Add($"\"{inputPath}\"");

            // 输出文件
            args.Add("--output");
            args.Add($"\"{outputPath}\"");

            // 如果有预设文件，直接使用预设
            if (!string.IsNullOrEmpty(options.PresetPath))
            {
                args.Add("--preset");
                args.Add($"\"{options.PresetPath}\"");
                return string.Join(" ", args);
            }

            // 压缩格式
            if (!string.IsNullOrEmpty(options.CompressionFormat))
            {
                string format = ConvertToNvttFormat(options.CompressionFormat);
                if (!string.IsNullOrEmpty(format))
                {
                    args.Add("--format");
                    args.Add(format);
                }
            }

            // Mipmap设置
            if (options.GenerateMipmaps)
            {
                args.Add("--mips");
            }
            else
            {
                args.Add("--no-mips");
            }

            // 质量设置
            if (!string.IsNullOrEmpty(options.Quality))
            {
                string quality = ConvertToNvttQuality(options.Quality);
                if (!string.IsNullOrEmpty(quality))
                {
                    args.Add("--quality");
                    args.Add(quality);
                }
            }

            return string.Join(" ", args);
        }

        /// <summary>
        /// 将BC格式转换为nvtt_export支持的格式参数
        /// </summary>
        private static string ConvertToNvttFormat(string bcFormat)
        {
            if (string.IsNullOrEmpty(bcFormat))
                return null;

            string format = bcFormat.ToUpper().Trim();

            // nvtt_export支持的格式
            switch (format)
            {
                case "BC1":
                    return "bc1";
                case "BC2":
                    return "bc2";
                case "BC3":
                    return "bc3";
                case "BC4":
                    return "bc4";
                case "BC5":
                    return "bc5";
                case "BC6H":
                    return "bc6h";
                case "BC7":
                    return "bc7";

                // DXT别名
                case "DXT1":
                    return "bc1";
                case "DXT2":
                case "DXT3":
                    return "bc2";
                case "DXT4":
                case "DXT5":
                    return "bc3";

                // 未压缩格式
                case "RGBA":
                case "RGB":
                    return "rgba";

                default:
                    // 如果无法识别，返回小写形式
                    return format.ToLower();
            }
        }

        /// <summary>
        /// 将质量设置转换为nvtt_export支持的参数
        /// </summary>
        private static string ConvertToNvttQuality(string quality)
        {
            if (string.IsNullOrEmpty(quality))
                return null;

            string q = quality.ToLower().Trim();

            switch (q)
            {
                case "fastest":
                    return "fastest";
                case "normal":
                    return "normal";
                case "production":
                    return "production";
                case "highest":
                    return "highest";
                default:
                    return "normal";
            }
        }
    }
}
