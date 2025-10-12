using System;
using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace PicViewEx.ImageSave
{
    /// <summary>
    /// 图片保存核心实现类
    /// </summary>
    public class ImageSaver : IImageSaver
    {
        private readonly NvidiaTextureTools _nvidiaTools;

        public ImageSaver()
        {
            _nvidiaTools = new NvidiaTextureTools();
        }

        /// <summary>
        /// 直接保存图片到原路径，保持原始参数
        /// </summary>
        public SaveResult Save(BitmapSource source, string originalFilePath)
        {
            try
            {
                if (source == null)
                {
                    return new SaveResult
                    {
                        Success = false,
                        Message = "图片数据为空"
                    };
                }

                if (!File.Exists(originalFilePath))
                {
                    return new SaveResult
                    {
                        Success = false,
                        Message = "原始文件不存在"
                    };
                }

                string extension = Path.GetExtension(originalFilePath).ToLower();

                // 根据原始格式选择保存策略
                SaveOptions options = null;

                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        int quality = JpegQualityAnalyzer.EstimateQuality(originalFilePath);
                        options = new JpegSaveOptions { Quality = quality };
                        break;

                    case ".png":
                        options = new PngSaveOptions();
                        break;

                    case ".bmp":
                        options = new BmpSaveOptions();
                        break;

                    case ".tga":
                        options = new TgaSaveOptions();
                        break;

                    case ".dds":
                        return SaveDdsFile(source, originalFilePath, originalFilePath);

                    default:
                        return new SaveResult
                        {
                            Success = false,
                            Message = $"不支持的文件格式: {extension}"
                        };
                }

                // 先保存到临时文件，成功后再替换原文件
                string tempFile = Path.Combine(Path.GetTempPath(), $"picview_save_{Guid.NewGuid()}{extension}");

                var result = SaveToInternal(source, tempFile, options);

                if (result.Success)
                {
                    try
                    {
                        // 备份原文件（可选）
                        File.Copy(tempFile, originalFilePath, true);
                        File.Delete(tempFile);

                        result.SavedPath = originalFilePath;
                        result.Message = "保存成功";
                    }
                    catch (Exception ex)
                    {
                        result.Success = false;
                        result.Message = "替换原文件失败";
                        result.ErrorDetails = ex.Message;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return new SaveResult
                {
                    Success = false,
                    Message = "保存失败",
                    ErrorDetails = ex.Message
                };
            }
        }

        /// <summary>
        /// 另存为（打开对话框）
        /// </summary>
        public SaveResult SaveAs(BitmapSource source, string originalFilePath)
        {
            try
            {
                // 打开自定义另存为窗口
                var saveAsWindow = new SaveAsWindow(source, originalFilePath, _nvidiaTools);
                bool? result = saveAsWindow.ShowDialog();

                if (result == true)
                {
                    return new SaveResult
                    {
                        Success = true,
                        Message = "保存成功",
                        SavedPath = saveAsWindow.SavedFilePath
                    };
                }
                else
                {
                    return new SaveResult
                    {
                        Success = false,
                        Message = "用户取消保存"
                    };
                }
            }
            catch (Exception ex)
            {
                return new SaveResult
                {
                    Success = false,
                    Message = "另存为失败",
                    ErrorDetails = ex.Message
                };
            }
        }

        /// <summary>
        /// 保存到指定路径
        /// </summary>
        public SaveResult SaveTo(BitmapSource source, string targetPath, SaveOptions options)
        {
            try
            {
                if (options is DdsSaveOptions ddsOptions)
                {
                    return SaveDdsFile(source, null, targetPath, ddsOptions);
                }

                return SaveToInternal(source, targetPath, options);
            }
            catch (Exception ex)
            {
                return new SaveResult
                {
                    Success = false,
                    Message = "保存失败",
                    ErrorDetails = ex.Message
                };
            }
        }

        /// <summary>
        /// 内部保存方法（PNG/JPG/BMP/TGA）
        /// </summary>
        private SaveResult SaveToInternal(BitmapSource source, string targetPath, SaveOptions options)
        {
            try
            {
                // 转换为Magick.NET格式
                byte[] imageData = ConvertBitmapSourceToBytes(source);

                using (MagickImage image = new MagickImage(imageData))
                {
                    // 根据不同格式设置参数
                    if (options is JpegSaveOptions jpegOptions)
                    {
                        image.Format = MagickFormat.Jpeg;
                        image.Quality = (uint)jpegOptions.Quality;
                    }
                    else if (options is PngSaveOptions)
                    {
                        image.Format = MagickFormat.Png;
                        image.Quality = 100; // PNG无损
                    }
                    else if (options is BmpSaveOptions)
                    {
                        image.Format = MagickFormat.Bmp;
                        image.Quality = 100;
                    }
                    else if (options is TgaSaveOptions)
                    {
                        image.Format = MagickFormat.Tga;
                        image.Quality = 100;
                    }

                    // 确保目标目录存在
                    string directory = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    image.Write(targetPath);

                    return new SaveResult
                    {
                        Success = true,
                        Message = "保存成功",
                        SavedPath = targetPath
                    };
                }
            }
            catch (Exception ex)
            {
                return new SaveResult
                {
                    Success = false,
                    Message = "保存失败",
                    ErrorDetails = ex.Message
                };
            }
        }

        /// <summary>
        /// 保存DDS文件
        /// </summary>
        private SaveResult SaveDdsFile(BitmapSource source, string originalFilePath, string targetPath, DdsSaveOptions options = null)
        {
            try
            {
                if (!_nvidiaTools.IsAvailable)
                {
                    return new SaveResult
                    {
                        Success = false,
                        Message = "NVIDIA Texture Tools 不可用，无法保存DDS文件"
                    };
                }

                // 如果是直接保存且有原始DDS文件，获取原始参数
                if (originalFilePath != null && File.Exists(originalFilePath) &&
                    Path.GetExtension(originalFilePath).ToLower() == ".dds")
                {
                    var ddsInfo = _nvidiaTools.GetDdsInfo(originalFilePath);
                    if (ddsInfo != null && options == null)
                    {
                        options = new DdsSaveOptions
                        {
                            CompressionFormat = ddsInfo.CompressionFormat,
                            GenerateMipmaps = ddsInfo.HasMipmaps
                        };
                    }
                }

                // 创建临时PNG文件
                string tempPngPath = _nvidiaTools.CreateTempPngForDds(source);
                if (string.IsNullOrEmpty(tempPngPath))
                {
                    return new SaveResult
                    {
                        Success = false,
                        Message = "创建临时PNG文件失败"
                    };
                }

                try
                {
                    bool success = false;

                    // 如果指定了预设文件
                    if (options?.PresetPath != null && File.Exists(options.PresetPath))
                    {
                        success = _nvidiaTools.ExportWithPreset(tempPngPath, options.PresetPath, targetPath);
                    }
                    // 如果要使用NVIDIA UI
                    else if (options?.UseNvidiaUI == true)
                    {
                        success = _nvidiaTools.ExportWithUI(tempPngPath);
                        // UI模式下用户自己选择保存路径，无法确定是否成功
                        return new SaveResult
                        {
                            Success = true,
                            Message = "已启动NVIDIA Texture Tools UI"
                        };
                    }
                    else
                    {
                        // 尝试使用默认参数或启动UI
                        success = _nvidiaTools.ExportWithUI(tempPngPath);
                        return new SaveResult
                        {
                            Success = true,
                            Message = "已启动NVIDIA Texture Tools UI"
                        };
                    }

                    return new SaveResult
                    {
                        Success = success,
                        Message = success ? "DDS保存成功" : "DDS保存失败",
                        SavedPath = success ? targetPath : null
                    };
                }
                finally
                {
                    // 清理临时文件
                    if (File.Exists(tempPngPath))
                    {
                        try { File.Delete(tempPngPath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                return new SaveResult
                {
                    Success = false,
                    Message = "DDS保存失败",
                    ErrorDetails = ex.Message
                };
            }
        }

        /// <summary>
        /// 将BitmapSource转换为字节数组
        /// </summary>
        private byte[] ConvertBitmapSourceToBytes(BitmapSource source)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(stream);
                return stream.ToArray();
            }
        }
    }
}
