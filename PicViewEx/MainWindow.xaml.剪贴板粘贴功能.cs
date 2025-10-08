using ImageMagick;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Interop;


namespace PicViewEx
{
    public partial class MainWindow
    {

        #region 剪贴板图片粘贴功能


        // Win32 兜底：直接从剪贴板取 CF_DIB / CF_DIBV5 的 HGLOBAL 字节
        [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll")] private static extern bool CloseClipboard();
        [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr hMem);

        private const uint CF_DIB = 8;
        private const uint CF_DIBV5 = 17;

        private byte[] TryGetDibBytesWin32(uint format)
        {
            byte[] result = null;
            if (!OpenClipboard(IntPtr.Zero)) return null;
            try
            {
                IntPtr h = GetClipboardData(format);
                if (h == IntPtr.Zero) return null;

                UIntPtr upSize = GlobalSize(h);
                int size = (int)upSize;
                if (size <= 0) return null; // 极少数返回0，就放弃这条

                IntPtr p = GlobalLock(h);
                if (p == IntPtr.Zero) return null;
                try
                {
                    result = new byte[size];
                    Marshal.Copy(p, result, 0, size);
                }
                finally
                {
                    GlobalUnlock(h);
                }
            }
            finally
            {
                CloseClipboard();
            }
            return result;
        }


        /// <summary>
        /// 从剪贴板粘贴图片
        /// </summary>
        private void PasteImageFromClipboard()
        {
            try
            {
                BitmapSource clipboardImage = null;
                string sourceInfo = "";

                // 尝试多种方式获取剪贴板图像数据
                clipboardImage = GetClipboardImageAdvanced(out sourceInfo);

                if (clipboardImage == null)
                {
                    if (statusText != null)
                        UpdateStatusText("剪贴板中没有检测到图像数据");
                    return;
                }

                // 显示确认对话框
                var result = ShowPasteConfirmDialog(sourceInfo, clipboardImage);

                if (result == MessageBoxResult.Yes)
                {
                    LoadImageFromClipboard(clipboardImage, sourceInfo);
                    RecordToolUsage("PasteFromClipboard");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴图片失败: {ex.Message}", "粘贴错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (statusText != null)
                    UpdateStatusText($"粘贴失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 高级剪贴板图像获取方法，支持多种格式
        /// </summary>

        private BitmapSource GetClipboardImageAdvanced(out string sourceInfo)
        {
            sourceInfo = "";

            DumpClipboardFormatsSafe(); // 调试

            // A) 直接 BitmapSource 对象
            const string BmpSrcFmt = "System.Windows.Media.Imaging.BitmapSource";
            if (Clipboard.ContainsData(BmpSrcFmt))
            {
                var obj = Clipboard.GetData(BmpSrcFmt) as BitmapSource;
                if (obj != null) { sourceInfo = "剪贴板图像 (BitmapSource 对象)"; return obj; }
            }

            // B) 标准位图
            if (Clipboard.ContainsImage())
            {
                var image = Clipboard.GetImage();
                if (image != null) { sourceInfo = "剪贴板图像 (标准)"; return image; }
            }

            // C) CF_BITMAP/HBITMAP 或 System.Drawing.Bitmap
            if (Clipboard.ContainsData(DataFormats.Bitmap))
            {
                var obj = Clipboard.GetData(DataFormats.Bitmap);
                var asBs = obj as BitmapSource;
                if (asBs != null) { sourceInfo = "剪贴板图像 (BitmapSource in DataFormats.Bitmap)"; return asBs; }

                var viaEnc = TryDecodeObjectAsEncodedImage(obj);
                if (viaEnc != null) { sourceInfo = "剪贴板图像 (System.Drawing.Bitmap)"; return viaEnc; }

                var hbmp = TryGetHBitmap(obj);
                if (hbmp != IntPtr.Zero)
                {
                    var bs = CreateBitmapFromHBitmap(hbmp);
                    if (bs != null) { sourceInfo = "剪贴板图像 (CF_BITMAP/HBITMAP)"; return bs; }
                }
            }

            // *** D) CF_DIBV5 (Format17) —— 这是你日志里的关键 ***
            // 正确的 API：GetDataFormat(int)
            // *** D) CF_DIBV5 (Format17) ***
            string dibv5Name = System.Windows.DataFormats.GetDataFormat(17).Name; // 常为 "Format17"
            object dibv5Obj = null;
            if (Clipboard.ContainsData(dibv5Name))
                dibv5Obj = Clipboard.GetData(dibv5Name);
            else if (Clipboard.ContainsData("Format17"))
                dibv5Obj = Clipboard.GetData("Format17");

            if (dibv5Obj != null)
            {
                var bmp = CreateBitmapFromDibStream(dibv5Obj);
                if (bmp != null) { sourceInfo = "剪贴板图像 (DIBV5→BMP)"; return bmp; }

                var bs = CreateBitmapFromDibBytesDirect(dibv5Obj);
                if (bs != null) { sourceInfo = "剪贴板图像 (DIBV5 直解)"; return bs; }
            }

            // —— WPF 取不到/失败时，用 Win32 生拽字节兜底：
            var dibv5Bytes = TryGetDibBytesWin32(CF_DIBV5);
            if (dibv5Bytes != null && dibv5Bytes.Length >= 40)
            {
                var bs = CreateBitmapFromDibBytesDirect(dibv5Bytes);
                if (bs != null) { sourceInfo = "剪贴板图像 (DIBV5 Win32 直解)"; return bs; }
            }


            // E) CF_DIB（传统 DIB）
            if (Clipboard.ContainsData(DataFormats.Dib))
            {
                var dibObj = Clipboard.GetData(DataFormats.Dib);
                var bmp = CreateBitmapFromDibStream(dibObj);
                if (bmp != null) { sourceInfo = "剪贴板图像 (DIB→BMP)"; return bmp; }

                var bs = CreateBitmapFromDibBytesDirect(dibObj);
                if (bs != null) { sourceInfo = "剪贴板图像 (DIB 直解)"; return bs; }
            }

            // —— 同样加 Win32 兜底：
            var dibBytes = TryGetDibBytesWin32(CF_DIB);
            if (dibBytes != null && dibBytes.Length >= 40)
            {
                var bs2 = CreateBitmapFromDibBytesDirect(dibBytes);
                if (bs2 != null) { sourceInfo = "剪贴板图像 (DIB Win32 直解)"; return bs2; }
            }


            // F) PNG 原始流
            if (Clipboard.ContainsData("PNG"))
            {
                var pngObj = Clipboard.GetData("PNG");
                var img = CreateBitmapFromEncodedStreamOrBytes(pngObj);
                if (img != null) { sourceInfo = "剪贴板图像 (PNG)"; return img; }
            }

            // G) 其他 mime
            string[] fmts = { "image/png", "image/jpeg", "image/gif", "image/bmp", "Bitmap" };
            foreach (var fmt in fmts)
            {
                if (Clipboard.ContainsData(fmt))
                {
                    var obj = Clipboard.GetData(fmt);
                    var img = CreateBitmapFromEncodedStreamOrBytes(obj);
                    if (img != null) { sourceInfo = "剪贴板图像 (" + fmt + ")"; return img; }
                }
            }

            // H) 文件拖放
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                foreach (string file in files)
                {
                    string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    if (supportedFormats.Contains(ext))
                    {
                        var image = imageLoader.LoadImage(file);
                        if (image != null)
                        {
                            sourceInfo = "剪贴板文件: " + System.IO.Path.GetFileName(file);
                            return image;
                        }
                    }
                }
            }

            return null;
        }


        private BitmapSource CreateBitmapFromDibBytesDirect(object dibDataObj)
        {
            try
            {
                byte[] dib;
                if (dibDataObj is byte[]) dib = (byte[])dibDataObj;
                else if (dibDataObj is Stream s)
                {
                    using (var ms = new MemoryStream())
                    {
                        if (s.CanSeek) s.Position = 0;
                        s.CopyTo(ms);
                        dib = ms.ToArray();
                    }
                }
                else return null;

                if (dib.Length < 40) return null; // 至少有 BITMAPINFOHEADER

                // 解析 BITMAPINFOHEADER
                int biSize = BitConverter.ToInt32(dib, 0);   // 40 / 108 / 124
                int width = BitConverter.ToInt32(dib, 4);
                int height = BitConverter.ToInt32(dib, 8);   // 正值=bottom-up，负值=top-down
                short planes = BitConverter.ToInt16(dib, 12);
                short bpp = BitConverter.ToInt16(dib, 14);  // 1/4/8/16/24/32
                int compress = BitConverter.ToInt32(dib, 16);  // 0=BI_RGB, 3=BI_BITFIELDS
                int clrUsed = (dib.Length >= 36) ? BitConverter.ToInt32(dib, 32) : 0;

                Console.WriteLine($"DIB直解: biSize={biSize}, bpp={bpp}, compress={compress}, size={dib.Length}, width={width}, height={height}");

                if (biSize != 40 && biSize != 108 && biSize != 124) biSize = 40;

                // 计算调色板大小
                int paletteEntries = 0;
                if (bpp <= 8) paletteEntries = (clrUsed > 0) ? clrUsed : (1 << bpp);
                int paletteSize = paletteEntries * 4;

                // 计算掩码大小
                const int BI_BITFIELDS = 3;
                int masksSize = 0;
                if (biSize == 40 && compress == BI_BITFIELDS && (bpp == 16 || bpp == 32))
                    masksSize = 12; // R/G/B 三个 DWORD

                // 像素数据起始偏移
                int offBits = biSize + masksSize + paletteSize;
                if (dib.Length < offBits) return null;

                // 仅实现常见的 32bpp 路线（Snipaste 常见）
                // 如需 24bpp/8bpp，可再扩展
                if (bpp == 32)
                {
                    // 掩码（若 BI_BITFIELDS）
                    uint maskR = 0x00FF0000, maskG = 0x0000FF00, maskB = 0x000000FF, maskA = 0xFF000000;
                    if (compress == BI_BITFIELDS && masksSize == 12)
                    {
                        int mBase = biSize; // 紧跟在头后
                        maskR = BitConverter.ToUInt32(dib, mBase + 0);
                        maskG = BitConverter.ToUInt32(dib, mBase + 4);
                        maskB = BitConverter.ToUInt32(dib, mBase + 8);
                        // A 掩码有时不给（=0），若不给就假设 0xFF000000
                    }

                    int absH = Math.Abs(height);
                    int strideSrc = ((width * bpp + 31) / 32) * 4; // DIB行对齐
                    int bytesNeeded = strideSrc * absH;
                    if (offBits + bytesNeeded > dib.Length) return null;

                    // 读取像素并转为 BGRA32（WPF：PixelFormats.Bgra32，预乘不强求）
                    byte[] pixels = new byte[width * absH * 4];
                    int destStride = width * 4;

                    // 简化：若掩码就是标准 BGRA 顺序，就可直接拷贝行；否则做逐像素重排
                    bool masksAreBGRA =
                        maskR == 0x00FF0000 && maskG == 0x0000FF00 && maskB == 0x000000FF;

                    for (int row = 0; row < absH; row++)
                    {
                        int srcRowIndex = (height > 0) ? (absH - 1 - row) : row; // bottom-up -> top-down
                        int srcOffset = offBits + srcRowIndex * strideSrc;
                        int dstOffset = row * destStride;

                        if (masksAreBGRA)
                        {
                            // 直接复制每像素 4 字节（DIB 为 BGRA，WPF 也用 BGRA）
                            Buffer.BlockCopy(dib, srcOffset, pixels, dstOffset, destStride);
                        }
                        else
                        {
                            // 掩码非常规：逐像素拆分
                            for (int x = 0; x < width; x++)
                            {
                                uint val = BitConverter.ToUInt32(dib, srcOffset + x * 4);
                                byte r = (byte)((val & maskR) >> 16);
                                byte g = (byte)((val & maskG) >> 8);
                                byte b = (byte)((val & maskB) >> 0);
                                byte a = (byte)((maskA != 0) ? ((val & maskA) >> 24) : 0xFF);

                                int di = dstOffset + x * 4;
                                pixels[di + 0] = b;
                                pixels[di + 1] = g;
                                pixels[di + 2] = r;
                                pixels[di + 3] = a;
                            }
                        }
                    }

                    // 生成 BitmapSource
                    var bs = BitmapSource.Create(
                        width, absH, 96, 96,
                        PixelFormats.Bgra32, null,
                        pixels, destStride);

                    bs.Freeze();

                    return bs;
                }
                // … 32bpp 分支后面 …
                else if (bpp == 24)
                {
                    int absH = Math.Abs(height);
                    int strideSrc = ((width * bpp + 31) / 32) * 4; // DIB 行对齐
                    int bytesNeeded = strideSrc * absH;
                    if (offBits + bytesNeeded > dib.Length) return null;

                    byte[] pixels = new byte[width * absH * 4]; // 目标 BGRA32
                    int destStride = width * 4;

                    for (int row = 0; row < absH; row++)
                    {
                        int srcRowIndex = (height > 0) ? (absH - 1 - row) : row; // bottom-up → top-down
                        int srcOffset = offBits + srcRowIndex * strideSrc;
                        int dstOffset = row * destStride;

                        // 源每像素 3 字节：BGR
                        int xSrc = srcOffset;
                        int xDst = dstOffset;
                        for (int x = 0; x < width; x++)
                        {
                            byte b = dib[xSrc + 0];
                            byte g = dib[xSrc + 1];
                            byte r = dib[xSrc + 2];
                            pixels[xDst + 0] = b;
                            pixels[xDst + 1] = g;
                            pixels[xDst + 2] = r;
                            pixels[xDst + 3] = 0xFF; // 无 alpha，默认不透明
                            xSrc += 3;
                            xDst += 4;
                        }
                    }

                    var bs = BitmapSource.Create(
                        width, absH, 96, 96,
                        PixelFormats.Bgra32, null,
                        pixels, destStride);

                    bs.Freeze();
                    return bs;
                }

                // （可按需拓展 24bpp/8bpp 路线）


                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("DIB直解失败: " + ex.Message);
                return null;
            }
        }


        private BitmapSource CreateBitmapFromEncodedStreamOrBytes(object obj)
        {
            try
            {
                if (obj is byte[] bytes)
                {
                    using (var ms = new MemoryStream(bytes))
                    {
                        var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        return dec.Frames.FirstOrDefault();
                    }
                }
                if (obj is Stream s)
                {
                    using (var ms = new MemoryStream())
                    {
                        if (s.CanSeek) s.Position = 0;
                        s.CopyTo(ms);
                        ms.Position = 0;
                        var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        return dec.Frames.FirstOrDefault();
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private BitmapSource CreateBitmapFromEncodedStream(Stream stream)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    if (stream.CanSeek) stream.Position = 0;
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    var decoder = BitmapDecoder.Create(
                        ms,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);
                    return decoder.Frames.FirstOrDefault();
                }
            }
            catch
            {
                return null;
            }
        }



        // === 核心修复：把 DIB 包装成 BMP 再解码 ===
        // === 核心修复：把 DIB 包装成 BMP 再解码（C# 7.3 兼容版） ===
        private BitmapSource CreateBitmapFromDibStream(object dibDataObj)
        {
            try
            {
                // 统一拿到 byte[]
                byte[] dibBytes;
                if (dibDataObj is byte[] bytes)
                {
                    dibBytes = bytes;
                }
                else if (dibDataObj is Stream dibStream)
                {
                    using (var ms = new MemoryStream())
                    {
                        if (dibStream.CanSeek) dibStream.Position = 0;
                        dibStream.CopyTo(ms);
                        dibBytes = ms.ToArray();
                    }
                }
                else
                {
                    return null;
                }

                if (dibBytes.Length < 40) return null; // 至少有 BITMAPINFOHEADER(40)

                // --- 解析 DIB 头关键字段 ---
                int biSize = BitConverter.ToInt32(dibBytes, 0);   // 头大小：40 / 108 / 124
                int biWidth = BitConverter.ToInt32(dibBytes, 4);
                int biHeight = BitConverter.ToInt32(dibBytes, 8);   // 负数表示 top-down
                short biPlanes = BitConverter.ToInt16(dibBytes, 12);
                short biBitCnt = BitConverter.ToInt16(dibBytes, 14);  // 1/4/8/16/24/32
                int biCompress = BitConverter.ToInt32(dibBytes, 16);  // 0=BI_RGB, 3=BI_BITFIELDS
                int biClrUsed = (dibBytes.Length >= 36) ? BitConverter.ToInt32(dibBytes, 32) : 0;

                // 防御：未知头一律当 40 处理（仍可用）
                if (biSize != 40 && biSize != 108 && biSize != 124)
                    biSize = 40;

                // 计算调色板大小（仅<=8bpp 才有；若 ClrUsed=0 则默认 2^bpp）
                int paletteEntries = 0;
                if (biBitCnt <= 8)
                    paletteEntries = (biClrUsed > 0) ? biClrUsed : (1 << biBitCnt);
                int paletteSize = paletteEntries * 4; // 每项 RGBA 4 字节

                // **关键修复点**：
                // V3(40字节) + BI_BITFIELDS + (16/32bpp) 时，紧跟在头后还有 3 个 DWORD 掩码（12 字节）
                int masksSize = 0;
                const int BI_BITFIELDS = 3;
                if (biSize == 40 && biCompress == BI_BITFIELDS && (biBitCnt == 16 || biBitCnt == 32))
                    masksSize = 12;

                // BMP 文件头固定 14 字节；像素起始偏移 = 14 + 头 + 掩码 + 调色板
                int bfOffBits = 14 + biSize + masksSize + paletteSize;
                int bfSize = 14 + dibBytes.Length; // 整个 BMP 文件大小 = 14 + DIB 总长

                using (var msBmp = new MemoryStream(14 + dibBytes.Length))
                {
                    using (var bw = new BinaryWriter(msBmp, System.Text.Encoding.UTF8, true))
                    {
                        // 写 BITMAPFILEHEADER (14 bytes)
                        bw.Write((byte)'B');
                        bw.Write((byte)'M');
                        bw.Write(bfSize);        // bfSize
                        bw.Write((short)0);      // bfReserved1
                        bw.Write((short)0);      // bfReserved2
                        bw.Write(bfOffBits);     // bfOffBits

                        // 紧接着写原始的 DIB 数据
                        bw.Write(dibBytes);
                    }

                    msBmp.Position = 0;
                    var decoder = BitmapDecoder.Create(
                        msBmp,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                    return decoder.Frames[0];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DIB→BMP 失败: " + ex.Message);
                return null;
            }
        }






        /// <summary>
        /// 显示粘贴确认对话框
        /// </summary>
        private MessageBoxResult ShowPasteConfirmDialog(string sourceInfo, BitmapSource image)
        {
            string message = $"检测到图像数据！\n\n" +
                           $"来源: {sourceInfo}\n" +
                           $"尺寸: {image.PixelWidth} × {image.PixelHeight}\n" +
                           $"格式: {image.Format}\n\n" +
                           $"是否将当前图片更新为粘贴的图像？\n\n" +
                           $"注意: 这将替换当前显示的图片";

            return MessageBox.Show(message, "发现剪贴板图像",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        }

        /// <summary>
        /// 从剪贴板加载图像到查看器
        /// </summary>
        private void LoadImageFromClipboard(BitmapSource clipboardImage, string sourceInfo)
        {
            try
            {
                // 如果当前有序列帧在播放，停止并重置
                if (hasSequenceLoaded)
                {
                    if (isSequencePlaying)
                    {
                        PauseSequence();
                    }

                    hasSequenceLoaded = false;
                    sequenceFrames.Clear();
                    currentFrameIndex = 0;
                    originalImage = null;

                    EnableSequenceControls(false);
                    UpdateFrameDisplay();
                }

                // 清除之前的图片路径信息，因为这是从剪贴板来的
                currentImagePath = "";
                currentImageList.Clear();
                currentImageIndex = -1;

                // 清除可能的GIF动画


                // 设置图片源
                mainImage.Source = clipboardImage;

                // 重置变换和缩放
                currentTransform = Transform.Identity;
                currentZoom = 1.0;
                imagePosition = new Point(0, 0);
                rotationAngle = 0.0; // 重置旋转角度

                // 检查图片尺寸是否超过窗口尺寸，决定是否自动适应
                if (imageContainer != null)
                {
                    double containerWidth = imageContainer.ActualWidth;
                    double containerHeight = imageContainer.ActualHeight;

                    // 计算有效显示区域宽度
                    double effectiveWidth = containerWidth;

                    // 只有当通道面板真正显示时才减去其宽度
                    if (showChannels && channelPanel != null && channelPanel.Visibility == Visibility.Visible)
                    {
                        effectiveWidth = Math.Max(100, containerWidth - 305); // 确保至少有100像素显示区域
                    }

                    // 如果图片尺寸超过容器的80%，自动适应窗口
                    if (clipboardImage.PixelWidth > effectiveWidth * 0.8 ||
                        clipboardImage.PixelHeight > containerHeight * 0.8)
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            FitToWindow();
                            PrintImageInfo("剪贴板图片加载 - 自动适应窗口");
                            if (statusText != null)
                                UpdateStatusText($"已粘贴并自动适应窗口: {sourceInfo}");
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                    else
                    {
                        // 否则居中显示
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            CenterImage();
                            PrintImageInfo("剪贴板图片加载 - 居中显示");
                            if (statusText != null)
                                UpdateStatusText($"已粘贴: {sourceInfo}");
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }

                // 更新图片信息显示
                UpdateImageInfoForClipboard(clipboardImage, sourceInfo);

                // 如果显示通道面板，尝试生成通道（但可能会失败，因为没有文件路径）
                if (showChannels)
                {
                    LoadClipboardImageChannels(clipboardImage);
                }

                if (statusText != null && !showChannels)
                    UpdateStatusText($"已从剪贴板粘贴: {sourceInfo}");

            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载剪贴板图片失败: {ex.Message}", "加载错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (statusText != null)
                    UpdateStatusText("剪贴板图片加载失败");
            }
        }

        /// <summary>
        /// 更新剪贴板图片的信息显示
        /// </summary>
        private void UpdateImageInfoForClipboard(BitmapSource image, string sourceInfo)
        {
            if (imageInfoText != null)
            {
                // 由于是剪贴板图片，无法获取文件大小，只显示尺寸和来源
                imageInfoText.Text = $"{image.PixelWidth} × {image.PixelHeight} | {sourceInfo}";
            }
        }

        /// <summary>
        /// 为剪贴板图片加载通道信息
        /// </summary>
        private void LoadClipboardImageChannels(BitmapSource image)
        {
            try
            {
                if (channelStackPanel == null) return;
                channelStackPanel.Children.Clear();

                // 清除之前的缓存，因为这是新的剪贴板图片
                channelCache.Clear();
                currentChannelCachePath = null;

                if (statusText != null)
                    UpdateStatusText("正在为剪贴板图片生成通道...");
                var channels = imageLoader.LoadChannels(image);

                var loadedChannels = imageLoader.LoadChannels(image);

                foreach (var (name, channelImage) in loadedChannels)
                {
                    channelCache.Add(Tuple.Create(name, channelImage));
                    CreateChannelControl(name, channelImage);
                }

                if (statusText != null)
                    UpdateStatusText($"剪贴板图片通道加载完成 ({channelStackPanel.Children.Count}个)");
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    UpdateStatusText($"剪贴板图片通道生成失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为剪贴板图片创建临时文件（用于打开方式功能）
        /// </summary>
        private string CreateTemporaryImageFile()
        {
            try
            {
                if (mainImage?.Source == null)
                    throw new InvalidOperationException("没有可用的图片");

                var source = mainImage.Source as BitmapSource;
                if (source == null)
                    throw new InvalidOperationException("图片格式不支持");

                // 清理旧的临时文件
                CleanupTemporaryFile();

                // 创建临时文件路径
                string tempDir = Path.GetTempPath();
                string guidPart = Guid.NewGuid().ToString("N").Substring(0, 8);                // 取前8位
                string tempFileName = $"PicViewEx_Temp_{DateTime.Now:yyyyMMdd_HHmmss}_{guidPart}.png";
                temporaryImagePath = Path.Combine(tempDir, tempFileName);

                // 保存图片到临时文件
                SaveBitmapSource(source, temporaryImagePath);

                if (statusText != null)
                    UpdateStatusText($"已创建临时文件用于打开方式: {Path.GetFileName(temporaryImagePath)}");

                return temporaryImagePath;
            }
            catch (Exception ex)
            {
                if (statusText != null)
                    UpdateStatusText($"创建临时文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 清理临时文件
        /// </summary>
        private void CleanupTemporaryFile()
        {
            if (!string.IsNullOrEmpty(temporaryImagePath) && File.Exists(temporaryImagePath))
            {
                try
                {
                    File.Delete(temporaryImagePath);
                    if (statusText != null)
                        UpdateStatusText("已清理临时文件");
                }
                catch (Exception ex)
                {
                    // 临时文件清理失败不应该影响主要功能
                    Console.WriteLine($"清理临时文件失败: {ex.Message}");
                }
            }
            temporaryImagePath = null;
        }

        /// <summary>
        /// 获取当前图片的有效路径（包括临时文件）
        /// </summary>
        private string GetCurrentImagePath()
        {
            // 如果有原始文件路径，直接返回
            if (!string.IsNullOrEmpty(currentImagePath) && File.Exists(currentImagePath))
            {
                return currentImagePath;
            }

            // 如果是剪贴板图片，创建临时文件
            if (mainImage?.Source != null)
            {
                return CreateTemporaryImageFile();
            }

            // 没有可用的图片文件时，返回空字符串而不是抛出异常
            return string.Empty;
        }

        #endregion

    
    // 调用处尝试从对象提取 HBITMAP（有些剪贴板实现会给句柄）
private IntPtr TryGetHBitmap(object obj)
        {
            // 常见是 System.Drawing.Bitmap
            var bmp = obj as System.Drawing.Bitmap;
            if (bmp != null)
            {
                return bmp.GetHbitmap(); // 注意：CreateBitmapSourceFromHBitmap 后要 DeleteObject
            }
            // 其他类型（如 IntPtr）可在此扩展
            return IntPtr.Zero;
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private BitmapSource CreateBitmapFromHBitmap(IntPtr hBmp)
        {
            if (hBmp == IntPtr.Zero) return null;
            try
            {
                var src = Imaging.CreateBitmapSourceFromHBitmap(
                    hBmp, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally
            {
                // 释放 GDI 句柄，避免 GDI 对象泄露
                DeleteObject(hBmp);
            }
        }

        // 调试：打印剪贴板格式（确保在 STA 线程）
        private void DumpClipboardFormatsSafe()
        {
            try
            {
                var data = Clipboard.GetDataObject();
                if (data == null) { Console.WriteLine("Clipboard empty"); return; }
                var formats = data.GetFormats();
                Console.WriteLine("=== Clipboard Formats ===");
                foreach (var f in formats) Console.WriteLine(f);
                Console.WriteLine("=========================");
            }
            catch (Exception ex)
            {
                Console.WriteLine("DumpClipboardFormats error: " + ex.Message);
            }
        }

        // 若某些对象其实是可编码图像（Image / Bitmap），试着用编码器之路转 BitmapSource
        private BitmapSource TryDecodeObjectAsEncodedImage(object obj)
        {
            try
            {
                var bmp = obj as System.Drawing.Bitmap;
                if (bmp == null) return null;
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    return dec.Frames.FirstOrDefault();
                }
            }
            catch { return null; }
        }
    
    
    }
}
