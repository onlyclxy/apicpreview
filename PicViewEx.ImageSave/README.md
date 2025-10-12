# PicViewEx.ImageSave 图片保存库

这是一个强大的C#图片保存类库，专为图片查看器设计，能够保存图片并保持原始图片的所有参数（质量、压缩率等）。

## 功能特性

### 1. 直接保存 (Save)
- 保存到原路径，自动保持原始图片参数
- **PNG/BMP/TGA**: 按最高规格保存（无损）
- **JPG**: 智能分析原图的量化表，自动计算合适的压缩质量
- **DDS**: 读取原始DDS参数（BC格式、Mipmap等），使用相同参数保存

### 2. 另存为 (SaveAs)
- 提供友好的自定义UI界面
- 支持多种格式：PNG, JPG, BMP, TGA, DDS
- 每种格式都有对应的参数设置

#### PNG/BMP/TGA
- 点击格式按钮后直接弹出保存对话框
- 默认为无损最高质量

#### JPG
- 点击后下方显示质量调节滑块
- 可以实时调整压缩质量（1-100）
- 如果原图是JPG，会自动检测并建议合适的质量值

#### DDS
- **预设模式**:
  - 扫描 `exe根目录/DDS presets` 文件夹中的 .dpf 预设文件
  - 按修改时间排序显示
  - 记录最近使用的3个预设，显示在顶部（类似QQ表情的常用功能）
  - 历史记录保存在 `PicViewEx.ImageSave.DdsPresetHistory.json`

- **自定义模式**:
  - 启动NVIDIA Texture Tools UI界面
  - 用户可以手动选择所有参数和保存路径
  - 适合需要精细控制DDS参数的场景

### 3. JPEG质量智能检测
- 通过读取JPEG文件的量化表来估算原始压缩质量
- 基于IJG（Independent JPEG Group）算法
- 确保不会越存越损失画质
- 推荐质量范围：70-95

### 4. DDS完整支持
- 需要NVIDIA Texture Tools（放置在exe根目录的 `NVIDIA Texture Tools` 文件夹）
- 支持的工具：
  - `nvddsinfo.exe`: 获取DDS文件信息
  - `nvtt_export.exe`: 导出DDS文件
- 自动检测工具可用性
- 对于nvtt_export不支持的格式，自动转换为PNG临时文件再处理

## 项目结构

```
PicViewEx.ImageSave/
├── IImageSaver.cs              # 主接口定义
├── ImageSaver.cs               # 核心实现类
├── JpegQualityAnalyzer.cs      # JPEG质量分析器
├── NvidiaTextureTools.cs       # NVIDIA工具集成
├── DdsPresetManager.cs         # DDS预设管理器
├── SaveAsWindow.xaml           # 另存为窗口UI
├── SaveAsWindow.xaml.cs        # 另存为窗口逻辑
├── TestWindow.xaml             # 测试界面UI
├── TestWindow.xaml.cs          # 测试界面逻辑
├── App.xaml                    # WPF应用程序定义
└── App.xaml.cs                 # WPF应用程序逻辑
```

## 使用方法

### 基础用法

```csharp
using PicViewEx.ImageSave;
using System.Windows.Media.Imaging;

// 创建保存器实例
IImageSaver saver = new ImageSaver();

// 1. 直接保存（保持原始参数）
BitmapSource image = ...; // 你的图片
string originalPath = @"C:\test.jpg";
SaveResult result = saver.Save(image, originalPath);

if (result.Success)
{
    Console.WriteLine($"保存成功: {result.Message}");
}

// 2. 另存为（弹出UI界面）
SaveResult result2 = saver.SaveAs(image, originalPath);

// 3. 保存到指定路径（使用特定参数）
var jpgOptions = new JpegSaveOptions { Quality = 90 };
SaveResult result3 = saver.SaveTo(image, @"C:\output.jpg", jpgOptions);
```

### DDS预设管理

```csharp
// DDS预设文件管理
DdsPresetManager presetManager = new DdsPresetManager();

// 获取所有预设
List<DdsPresetInfo> allPresets = presetManager.GetAllPresets();

// 获取历史记录
List<DdsPresetInfo> history = presetManager.GetHistoryPresets();

// 添加到历史
presetManager.AddToHistory(@"C:\presets\BC7_HighQuality.dpf");
```

### JPEG质量检测

```csharp
// 检测JPEG文件的质量
int quality = JpegQualityAnalyzer.EstimateQuality(@"C:\test.jpg");
Console.WriteLine($"估算质量: {quality}");
```

## 依赖项

- .NET Framework 4.8
- Magick.NET-Q16-AnyCPU (14.8.2)
- Newtonsoft.Json (13.0.3)
- WindowsBase
- PresentationCore
- PresentationFramework

## 构建说明

1. 在Visual Studio中打开 `PicViewEx.ImageSave.csproj`
2. 右键点击项目 -> 还原NuGet包
3. 按 F6 构建项目
4. 如果需要测试，直接运行（当前配置为控制台应用程序）

## 测试

项目包含完整的测试界面（TestWindow），可以：
- 打开任意格式的图片
- 测试直接保存功能
- 测试另存为功能
- 查看详细的操作日志
- 显示图片信息和格式参数

## 外部工具要求

### NVIDIA Texture Tools (可选，仅DDS需要)
- 下载地址：https://developer.nvidia.com/nvidia-texture-tools-exporter
- 将工具放置在：`exe根目录/NVIDIA Texture Tools/`
- 必需文件：
  - `nvddsinfo.exe`
  - `nvtt_export.exe`

### DDS预设文件夹
- 位置：`exe根目录/DDS presets/`
- 支持格式：`.dpf` 文件
- 可以通过NVIDIA Texture Tools创建预设文件

## 配置文件

- `PicViewEx.ImageSave.DdsPresetHistory.json`: DDS预设历史记录（自动生成）

## 注意事项

1. **转换为类库**：当前项目配置为控制台应用程序以便测试。要转换为类库：
   - 打开项目属性
   - 将输出类型从 "Exe" 改为 "Library"
   - 删除 App.xaml 和 TestWindow（如果不需要）

2. **JPEG质量**：
   - 自动检测的质量是估算值，可能与原始值有偏差
   - 为避免质量损失，建议值范围是70-95
   - 如果无法检测，默认使用85

3. **DDS限制**：
   - DDS保存完全依赖NVIDIA Texture Tools
   - 如果工具不可用，DDS保存功能将无法使用
   - 某些特殊的DDS格式可能不支持

4. **线程安全**：
   - BitmapSource需要Freeze后才能跨线程使用
   - UI操作需要在UI线程执行

## 许可证

根据你的项目需求设置许可证。

## 作者

PicViewEx Team

## 更新日志

### v1.0.0 (2025-10-12)
- 初始版本发布
- 支持PNG、JPG、BMP、TGA、DDS格式
- JPEG智能质量检测
- DDS预设管理
- 完整的测试界面
