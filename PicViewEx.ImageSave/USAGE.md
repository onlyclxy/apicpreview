# 快速开始指南

## 1. 准备工作

### 安装依赖
确保你的项目中已安装以下NuGet包：
```
Install-Package Magick.NET-Q16-AnyCPU
Install-Package Newtonsoft.Json
```

### 设置NVIDIA Texture Tools（如果需要DDS支持）
1. 下载NVIDIA Texture Tools
2. 创建文件夹：`你的程序目录/NVIDIA Texture Tools/`
3. 将以下文件复制到该文件夹：
   - nvddsinfo.exe
   - nvtt_export.exe
   - 以及相关的DLL文件

## 2. 基础使用示例

### 简单保存
```csharp
using PicViewEx.ImageSave;
using System.Windows.Media.Imaging;

// 加载图片
BitmapImage bitmap = new BitmapImage(new Uri(@"C:\test.jpg"));

// 创建保存器
IImageSaver saver = new ImageSaver();

// 直接保存到原路径
SaveResult result = saver.Save(bitmap, @"C:\test.jpg");

if (result.Success)
{
    MessageBox.Show("保存成功！");
}
else
{
    MessageBox.Show($"保存失败：{result.Message}");
}
```

### 另存为（带UI）
```csharp
// 打开另存为对话框
SaveResult result = saver.SaveAs(bitmap, @"C:\test.jpg");

if (result.Success && !string.IsNullOrEmpty(result.SavedPath))
{
    MessageBox.Show($"已保存到：{result.SavedPath}");
}
```

### 指定格式和参数保存
```csharp
// JPG格式，指定质量
var jpgOptions = new JpegSaveOptions { Quality = 90 };
SaveResult result = saver.SaveTo(bitmap, @"C:\output.jpg", jpgOptions);

// PNG格式（无损）
var pngOptions = new PngSaveOptions();
SaveResult result2 = saver.SaveTo(bitmap, @"C:\output.png", pngOptions);

// DDS格式，使用预设
var ddsOptions = new DdsSaveOptions
{
    PresetPath = @"C:\presets\BC7_Normal.dpf"
};
SaveResult result3 = saver.SaveTo(bitmap, @"C:\output.dds", ddsOptions);
```

## 3. 高级用法

### JPEG质量分析
```csharp
// 在保存之前，先分析原图的质量
string jpgPath = @"C:\photo.jpg";
int originalQuality = JpegQualityAnalyzer.EstimateQuality(jpgPath);

Console.WriteLine($"原图质量估算：{originalQuality}");

// 使用相同或更高的质量保存
var options = new JpegSaveOptions { Quality = originalQuality };
saver.SaveTo(bitmap, @"C:\photo_edited.jpg", options);
```

### DDS信息查询
```csharp
// 查询DDS文件的详细信息
NvidiaTextureTools tools = new NvidiaTextureTools();

if (tools.IsAvailable)
{
    DdsFileInfo info = tools.GetDdsInfo(@"C:\texture.dds");

    if (info != null)
    {
        Console.WriteLine($"格式：{info.Format}");
        Console.WriteLine($"压缩：{info.CompressionFormat}");
        Console.WriteLine($"Mipmap：{info.HasMipmaps}");
        Console.WriteLine($"Mip级别：{info.MipLevels}");
        Console.WriteLine($"分辨率：{info.Width}x{info.Height}");
    }
}
```

### DDS预设管理
```csharp
DdsPresetManager manager = new DdsPresetManager();

// 列出所有可用预设
List<DdsPresetInfo> presets = manager.GetAllPresets();
foreach (var preset in presets)
{
    Console.WriteLine($"{preset.FileName} - 修改于 {preset.LastModified}");
}

// 获取最近使用的预设
List<DdsPresetInfo> recent = manager.GetHistoryPresets();
```

## 4. 在你的图片查看器中集成

### 示例：添加保存和另存为菜单
```csharp
public class ImageViewer
{
    private BitmapSource _currentImage;
    private string _currentFilePath;
    private IImageSaver _imageSaver = new ImageSaver();

    // 保存按钮
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null) return;

        // 显示保存中...
        StatusText.Text = "正在保存...";

        SaveResult result = _imageSaver.Save(_currentImage, _currentFilePath);

        if (result.Success)
        {
            StatusText.Text = "保存成功";
        }
        else
        {
            StatusText.Text = $"保存失败：{result.Message}";
            MessageBox.Show(result.Message, "保存失败",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 另存为按钮
    private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null) return;

        SaveResult result = _imageSaver.SaveAs(_currentImage, _currentFilePath);

        if (result.Success && !string.IsNullOrEmpty(result.SavedPath))
        {
            StatusText.Text = $"已保存到：{Path.GetFileName(result.SavedPath)}";
        }
    }

    // 旋转后保存
    private void RotateAndSave()
    {
        // 旋转图片
        TransformedBitmap rotated = new TransformedBitmap();
        rotated.BeginInit();
        rotated.Source = _currentImage;
        rotated.Transform = new RotateTransform(90);
        rotated.EndInit();
        rotated.Freeze();

        // 保存旋转后的图片
        SaveResult result = _imageSaver.Save(rotated, _currentFilePath);

        if (result.Success)
        {
            // 更新显示
            _currentImage = rotated;
            UpdateDisplay();
        }
    }
}
```

### 快捷键集成
```csharp
// 在窗口的KeyDown事件中
private void Window_KeyDown(object sender, KeyEventArgs e)
{
    // Ctrl+S 保存
    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.S)
    {
        SaveCurrent();
        e.Handled = true;
    }

    // Ctrl+Shift+S 另存为
    if (e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
        && e.Key == Key.S)
    {
        SaveAsDialog();
        e.Handled = true;
    }
}

private void SaveCurrent()
{
    var result = _imageSaver.Save(_currentImage, _currentFilePath);
    ShowSaveStatus(result);
}

private void SaveAsDialog()
{
    var result = _imageSaver.SaveAs(_currentImage, _currentFilePath);
    if (result.Success)
    {
        ShowSaveStatus(result);
    }
}
```

## 5. 错误处理

### 完整的错误处理示例
```csharp
try
{
    SaveResult result = saver.Save(image, filePath);

    if (result.Success)
    {
        // 成功
        Console.WriteLine($"✓ {result.Message}");
    }
    else
    {
        // 失败，但有错误信息
        Console.WriteLine($"✗ {result.Message}");

        if (!string.IsNullOrEmpty(result.ErrorDetails))
        {
            // 记录详细错误
            LogError($"保存失败详情：{result.ErrorDetails}");
        }

        // 根据错误类型采取不同措施
        if (result.Message.Contains("NVIDIA"))
        {
            MessageBox.Show(
                "DDS保存需要NVIDIA Texture Tools。\n" +
                "请安装并将工具放置在程序根目录的 'NVIDIA Texture Tools' 文件夹中。",
                "缺少依赖", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
catch (Exception ex)
{
    // 未预期的异常
    LogError($"严重错误：{ex}");
    MessageBox.Show($"发生未预期的错误：{ex.Message}",
        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
}
```

## 6. 性能优化建议

### BitmapSource的Freeze
```csharp
// 在创建BitmapSource后立即Freeze，这样可以跨线程使用
BitmapImage bitmap = new BitmapImage();
bitmap.BeginInit();
bitmap.UriSource = new Uri(filePath);
bitmap.CacheOption = BitmapCacheOption.OnLoad;
bitmap.EndInit();
bitmap.Freeze(); // 重要！

// 现在可以安全地在后台线程中保存
Task.Run(() =>
{
    SaveResult result = saver.Save(bitmap, filePath);
});
```

### 大文件处理
```csharp
// 对于大图片，考虑显示进度
public async Task<SaveResult> SaveLargeImageAsync(BitmapSource image, string path)
{
    // 显示进度提示
    ProgressDialog.Show("正在保存大文件...");

    try
    {
        SaveResult result = await Task.Run(() =>
        {
            return _imageSaver.Save(image, path);
        });

        return result;
    }
    finally
    {
        ProgressDialog.Hide();
    }
}
```

## 7. 常见问题

### Q: 如何确定JPEG的最佳质量？
A: 使用 `JpegQualityAnalyzer.EstimateQuality()` 分析原图，然后使用相同或稍高的质量值。

### Q: DDS保存失败怎么办？
A:
1. 检查NVIDIA Texture Tools是否正确安装
2. 确认预设文件是否存在且有效
3. 尝试使用自定义UI模式

### Q: 如何支持更多格式？
A: 通过ImageMagick（Magick.NET），本库支持200+种图片格式的读取，但保存时建议只使用常见格式。

### Q: 保存会修改原文件吗？
A: `Save()` 方法会先保存到临时文件，成功后才替换原文件，确保安全性。

## 8. 调试技巧

### 启用详细日志
```csharp
// 在调试时，可以输出更多信息
SaveResult result = saver.Save(image, path);

Debug.WriteLine($"保存结果：{result.Success}");
Debug.WriteLine($"消息：{result.Message}");
Debug.WriteLine($"路径：{result.SavedPath}");
Debug.WriteLine($"错误详情：{result.ErrorDetails}");
```

### 测试不同格式
使用提供的TestWindow.xaml进行快速测试：
1. 运行程序
2. 打开测试图片
3. 尝试不同的保存操作
4. 查看日志输出

## 需要帮助？

如果遇到问题：
1. 查看README.md了解完整功能
2. 运行测试程序检查配置
3. 检查依赖项是否正确安装
4. 查看错误日志获取详细信息
