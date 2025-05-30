using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace apicpreview
{
    public static class ImageFormatHelper
    {
        // 支持的文件扩展名
        public static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // .NET 原生支持的格式
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".ico",
            // 需要特殊处理的格式
            ".tga", ".dds", ".psd", ".webp",
            // 现有的.a文件
            ".a"
        };

        /// <summary>
        /// 检查文件是否是支持的图片格式
        /// </summary>
        public static bool IsSupportedImageFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string extension = Path.GetExtension(filePath);
            return SupportedExtensions.Contains(extension);
        }

        /// <summary>
        /// 获取支持的文件格式过滤器字符串（用于OpenFileDialog）
        /// </summary>
        public static string GetFileFilter()
        {
            var filters = new List<string>
            {
                "所有支持的图片格式|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.tiff;*.tif;*.ico;*.tga;*.dds;*.psd;*.webp;*.a",
                "JPEG 文件|*.jpg;*.jpeg",
                "PNG 文件|*.png",
                "GIF 文件|*.gif",
                "BMP 文件|*.bmp", 
                "TIFF 文件|*.tiff;*.tif",
                "图标文件|*.ico",
                "TGA 文件|*.tga",
                "DDS 文件|*.dds", 
                "PSD 文件|*.psd",
                "WebP 文件|*.webp",
                "A 文件|*.a",
                "所有文件|*.*"
            };

            return string.Join("|", filters);
        }

        /// <summary>
        /// 尝试加载图片，支持多种格式
        /// </summary>
        public static Image LoadImage(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在: {filePath}");

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                switch (extension)
                {
                    case ".tga":
                        return LoadTgaImage(filePath);
                    case ".dds":
                        return LoadDdsImage(filePath);
                    case ".psd":
                        return LoadPsdImage(filePath);
                    case ".webp":
                        return LoadWebPImage(filePath);
                    case ".a":
                        // 处理原有的.a文件
                        return LoadAFileAsImage(filePath);
                    default:
                        // 使用.NET原生支持
                        return Image.FromFile(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法加载图片 {filePath}: {ex.Message}", ex);
            }
        }

        private static Image LoadTgaImage(string filePath)
        {
            // TODO: 在安装Pfim包后实现
            // 目前返回占位符
            return CreatePlaceholderImage("TGA", "需要安装 Pfim NuGet 包");
        }

        private static Image LoadDdsImage(string filePath)
        {
            // TODO: 在安装Pfim包后实现
            return CreatePlaceholderImage("DDS", "需要安装 Pfim NuGet 包");
        }

        private static Image LoadPsdImage(string filePath)
        {
            // TODO: 实现PSD支持
            return CreatePlaceholderImage("PSD", "需要安装 ImageSharp 或专门的PSD库");
        }

        private static Image LoadWebPImage(string filePath)
        {
            // TODO: 在安装相关包后实现
            return CreatePlaceholderImage("WebP", "需要安装 KGySoft.Drawing 或 ImageSharp");
        }

        private static Image LoadAFileAsImage(string filePath)
        {
            // 保持原有的.a文件处理逻辑
            byte[] fileBytes = File.ReadAllBytes(filePath);
            using (MemoryStream ms = new MemoryStream(fileBytes))
            {
                return Image.FromStream(ms);
            }
        }

        private static Image CreatePlaceholderImage(string format, string message)
        {
            // 创建一个占位符图片，显示格式信息
            Bitmap placeholder = new Bitmap(400, 300);
            using (Graphics g = Graphics.FromImage(placeholder))
            {
                g.Clear(Color.LightGray);
                using (Font font = new Font("微软雅黑", 14, FontStyle.Bold))
                using (Brush brush = new SolidBrush(Color.Black))
                {
                    string text = $"{format} 格式\n\n{message}";
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(text, font, brush, new RectangleF(0, 0, 400, 300), sf);
                }
            }
            return placeholder;
        }

        /// <summary>
        /// 获取格式友好名称
        /// </summary>
        public static string GetFormatFriendlyName(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "JPEG图像";
                case ".png":
                    return "PNG图像";
                case ".gif":
                    return "GIF图像";
                case ".bmp":
                    return "位图";
                case ".tiff":
                case ".tif":
                    return "TIFF图像";
                case ".ico":
                    return "图标文件";
                case ".tga":
                    return "TGA纹理";
                case ".dds":
                    return "DDS纹理";
                case ".psd":
                    return "Photoshop文档";
                case ".webp":
                    return "WebP图像";
                case ".a":
                    return "A格式文件";
                default:
                    return "未知格式";
            }
        }
    }
} 