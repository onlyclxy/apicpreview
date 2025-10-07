using System;
using System.IO;
using Leadtools;
using Leadtools.Codecs;

namespace TestLeadtoolsConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("开始测试LEADTOOLS PDF加载功能...");
            
            try
            {
                // 初始化LEADTOOLS
                InitializeLeadtools();
                
                // 测试加载PDF
                string pdfPath = "test.pdf";
                if (File.Exists(pdfPath))
                {
                    TestPdfLoading(pdfPath);
                }
                else
                {
                    Console.WriteLine($"PDF文件不存在: {pdfPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试失败: {ex.Message}");
                Console.WriteLine($"详细信息: {ex.StackTrace}");
            }
            
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
        
        private static void InitializeLeadtools()
        {
            Console.WriteLine("正在初始化LEADTOOLS...");
            
            try
            {
                // 设置许可证
                string keyPath = "full_license.key";
                string licPath = "full_license.lic";
                
                if (File.Exists(keyPath) && File.Exists(licPath))
                {
                    string key = File.ReadAllText(keyPath).Trim();
                    RasterSupport.SetLicense(licPath, key);
                    Console.WriteLine("LEADTOOLS许可证设置成功");
                }
                else
                {
                    Console.WriteLine("许可证文件不存在，使用评估版");
                }
                
                // 启动LEADTOOLS
                RasterSupport.Startup();
                Console.WriteLine("LEADTOOLS启动成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LEADTOOLS初始化失败: {ex.Message}");
                throw;
            }
        }
        
        private static void TestPdfLoading(string pdfPath)
        {
            Console.WriteLine($"正在测试加载PDF文件: {pdfPath}");
            
            try
            {
                using (var codecs = new RasterCodecs())
                {
                    // 获取PDF页数
                    var info = codecs.GetInformation(pdfPath, true);
                    Console.WriteLine($"PDF页数: {info.TotalPages}");
                    
                    // 加载第一页
                    using (var rasterImage = codecs.Load(pdfPath, 1))
                    {
                        Console.WriteLine($"成功加载PDF第一页");
                        Console.WriteLine($"图像尺寸: {rasterImage.Width} x {rasterImage.Height}");
                        Console.WriteLine($"颜色深度: {rasterImage.BitsPerPixel} bits");
                        Console.WriteLine($"分辨率: {rasterImage.XResolution} x {rasterImage.YResolution} DPI");
                    }
                }
                
                Console.WriteLine("PDF加载测试成功！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PDF加载失败: {ex.Message}");
                Console.WriteLine($"详细信息: {ex.StackTrace}");
            }
        }
    }
}