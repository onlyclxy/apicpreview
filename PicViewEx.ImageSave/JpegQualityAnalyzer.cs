using System;
using System.IO;
using System.Linq;

namespace PicViewEx.ImageSave
{
    /// <summary>
    /// JPEG质量检测工具类
    /// 通过分析量化表来估算JPEG图片的质量参数
    /// </summary>
    public class JpegQualityAnalyzer
    {
        /// <summary>
        /// 标准JPEG量化表（质量100时的基准）
        /// </summary>
        private static readonly byte[] StandardLuminanceQuantTable = new byte[]
        {
            16, 11, 10, 16, 24, 40, 51, 61,
            12, 12, 14, 19, 26, 58, 60, 55,
            14, 13, 16, 24, 40, 57, 69, 56,
            14, 17, 22, 29, 51, 87, 80, 62,
            18, 22, 37, 56, 68, 109, 103, 77,
            24, 35, 55, 64, 81, 104, 113, 92,
            49, 64, 78, 87, 103, 121, 120, 101,
            72, 92, 95, 98, 112, 100, 103, 99
        };

        /// <summary>
        /// 从JPEG文件中估算质量参数
        /// </summary>
        /// <param name="filePath">JPEG文件路径</param>
        /// <returns>估算的质量值（0-100），如果无法确定则返回85</returns>
        public static int EstimateQuality(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return 85; // 默认值

                byte[] quantTable = ExtractQuantizationTable(filePath);
                if (quantTable == null || quantTable.Length < 64)
                    return 85; // 无法提取量化表，返回默认值

                return CalculateQualityFromQuantTable(quantTable);
            }
            catch
            {
                return 85; // 发生错误，返回默认值
            }
        }

        /// <summary>
        /// 从JPEG文件中提取量化表
        /// </summary>
        private static byte[] ExtractQuantizationTable(string filePath)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    // 检查JPEG文件头
                    if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8)
                        return null;

                    while (fs.Position < fs.Length - 1)
                    {
                        if (br.ReadByte() != 0xFF)
                            continue;

                        byte marker = br.ReadByte();

                        // DQT (Define Quantization Table) marker
                        if (marker == 0xDB)
                        {
                            int length = (br.ReadByte() << 8) | br.ReadByte();
                            byte precision = br.ReadByte();
                            int tableId = precision & 0x0F;

                            // 读取第一个量化表（通常是亮度表）
                            if (tableId == 0)
                            {
                                byte[] table = br.ReadBytes(64);
                                return table;
                            }
                            else
                            {
                                br.ReadBytes(length - 3);
                            }
                        }
                        else if (marker == 0xDA) // SOS (Start of Scan) marker
                        {
                            break; // 已经到图像数据，停止搜索
                        }
                        else if (marker >= 0xD0 && marker <= 0xD9)
                        {
                            // 独立标记，无长度字段
                            continue;
                        }
                        else
                        {
                            // 跳过其他段
                            int segLength = (br.ReadByte() << 8) | br.ReadByte();
                            br.ReadBytes(segLength - 2);
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// 根据量化表计算质量值
        /// </summary>
        private static int CalculateQualityFromQuantTable(byte[] quantTable)
        {
            if (quantTable.Length < 64)
                return 85;

            // 计算量化表的平均值
            double avgQuantValue = 0;
            double avgStdValue = 0;

            for (int i = 0; i < 64; i++)
            {
                avgQuantValue += quantTable[i];
                avgStdValue += StandardLuminanceQuantTable[i];
            }

            avgQuantValue /= 64.0;
            avgStdValue /= 64.0;

            // 根据IJG（Independent JPEG Group）算法估算质量
            double quality;

            if (avgQuantValue < avgStdValue)
            {
                // 高质量范围 (50-100)
                quality = (200.0 - avgQuantValue * 100.0 / avgStdValue) / 2.0;
            }
            else
            {
                // 低质量范围 (1-50)
                quality = 5000.0 / avgQuantValue - 50.0;
            }

            // 限制范围并向上取整以避免质量损失
            int estimatedQuality = (int)Math.Ceiling(Math.Max(1, Math.Min(100, quality)));

            // 为了保险起见，如果估算质量高于90，保持在90-95之间
            if (estimatedQuality > 95)
                estimatedQuality = 95;

            // 如果估算质量低于60，至少设为70以避免明显质量损失
            if (estimatedQuality < 60)
                estimatedQuality = 70;

            return estimatedQuality;
        }

        /// <summary>
        /// 验证估算的质量值是否合理
        /// </summary>
        public static int ValidateQuality(int quality)
        {
            if (quality < 1)
                return 70;
            if (quality > 100)
                return 95;

            // 推荐质量范围 70-95
            if (quality < 70)
                return 70;
            if (quality > 95)
                return 95;

            return quality;
        }
    }
}
