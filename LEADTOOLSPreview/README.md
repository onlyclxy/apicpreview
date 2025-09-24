# LEADTOOLS Preview - .NET 8 兼容版本

## 概述
这个项目演示了如何在 .NET 8 项目中使用 LEADTOOLS v19 的 .NET Framework 版本 DLL。通过特定的配置，我们成功实现了跨框架兼容性。

## 兼容性解决方案

### 1. 项目配置 (LEADTOOLSPreview.csproj)
- 启用了 `UseWindowsDesktopSdk` 支持
- 添加了 `FrameworkReference` 到 `Microsoft.WindowsDesktop.App`
- 配置了自动复制 LEADTOOLS 依赖文件
- 使用 `Private=true` 确保 DLL 复制到输出目录

### 2. 运行时配置 (runtimeconfig.template.json)
- 启用了对 .NET Framework 程序集的支持
- 配置了额外的探测路径
- 启用了二进制序列化支持（兼容性需要）

### 3. 关键配置项
```xml
<UseWindowsDesktopSdk>true</UseWindowsDesktopSdk>
<GenerateAssemblyInfo>false</GenerateAssemblyInfo>
<DisableWinExeOutputInference>true</DisableWinExeOutputInference>
```

## 构建和部署

### 开发调试
```powershell
dotnet build
dotnet run
```

### 发布部署
运行 `publish.ps1` 脚本自动创建两个版本：
1. **单文件版本** - 完全自包含，无需安装 .NET 运行时
2. **框架依赖版本** - 需要目标机器安装 .NET 8 桌面运行时

```powershell
.\publish.ps1
```

## 依赖要求

### 开发环境
- .NET 8 SDK
- LEADTOOLS v19（.NET Framework 版本）
- Visual Studio 2022 或 VS Code

### 运行环境
- Windows x64
- .NET 8 桌面运行时（框架依赖版本）
- LEADTOOLS 许可证文件

## 已解决的问题

### 原始问题
```
Leadtools.RasterException
在 Leadtools.RasterException.CheckErrorCode(Int32 code)
在 Leadtools.Codecs.RasterCodecs.DoLoad(LoadParams loadParams)
```

### 解决方案
1. 使用正确的 .NET Framework 版本 DLL（`C:\LEADTOOLS 19\Bin\Dotnet4\x64\`）
2. 配置 .NET 8 项目支持加载 .NET Framework 程序集
3. 确保所有 LEADTOOLS 依赖文件正确复制到输出目录

## 支持的文件格式
- BMP, JPG, JPEG, PNG, TIF, TIFF, GIF
- PDF 文件（多页支持）
- 其他 LEADTOOLS 支持的格式

## 功能特性
- 多页文档浏览
- 页面导航（前一页/后一页）
- 异步图像加载
- 内存优化的图像显示
- 完整的错误处理

## 注意事项
1. 确保 LEADTOOLS 许可证文件在应用程序目录
2. 第一次运行可能需要较长时间加载 LEADTOOLS 依赖
3. 部署时需要包含所有 LEADTOOLS DLL 文件
4. 建议在目标环境测试兼容性

## 技术细节
这个解决方案利用了 .NET 8 的向后兼容能力，通过以下机制实现：
- **程序集加载器重定向** - 允许加载 .NET Framework 程序集
- **运行时兼容垫片** - 处理框架差异
- **自动依赖解析** - 确保所有依赖在运行时可用

## 性能优化
- 使用异步加载避免 UI 冻结
- 实现了图像内存管理和释放
- 优化了多页文档的页面切换性能 