# 多格式图片预览器

一个功能丰富的Windows图片预览器，支持多种图片格式，包括常见格式和特殊格式。

## 🎯 功能特性

### 基础功能
- 🖼️ **多格式支持**: JPG, PNG, GIF, BMP, TIFF, ICO等原生格式
- 🔍 **缩放功能**: 鼠标滚轮缩放，支持0.05x到10x缩放范围
- 🖱️ **拖拽移动**: 点击拖拽移动图片位置
- 📂 **文件操作**: 拖放文件打开，文件对话框选择

### 高级功能
- 🎯 **智能视图**: 适应窗口、实际大小、居中显示
- ⌨️ **快捷键**: 丰富的键盘快捷键支持
- 📊 **状态信息**: 实时显示格式、尺寸、缩放比例
- 🖥️ **全屏模式**: F11切换全屏预览
- 🎨 **专业界面**: 菜单栏、状态栏、现代化UI

### 扩展格式支持（需要NuGet包）
- 🎮 **TGA格式**: 游戏纹理格式 (需要Pfim包)
- 🎮 **DDS格式**: DirectX纹理格式 (需要Pfim包)  
- 🎨 **PSD格式**: Photoshop文档 (需要ImageSharp包)
- 🌐 **WebP格式**: 现代Web图片格式 (需要KGySoft.Drawing包)

## 🚀 快速开始

### 直接使用
1. 运行 `build.bat` 编译项目
2. 运行生成的 `bin\Debug\apicpreview.exe`
3. 拖拽图片文件到窗口或使用Ctrl+O打开文件

### 扩展格式支持
查看 `安装指南.md` 了解如何安装NuGet包以支持更多格式。

## ⌨️ 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+O` | 打开文件 |
| `Ctrl++` | 放大 |
| `Ctrl+-` | 缩小 |
| `Ctrl+0` | 适应窗口 |
| `Ctrl+1` | 实际大小 |
| `F11` | 全屏切换 |
| `Esc` | 退出 |
| 鼠标滚轮 | 缩放 |
| 拖拽 | 移动图片 |

## 📁 支持的格式

### ✅ 原生支持（无需额外包）
- **JPEG** (.jpg, .jpeg) - 照片压缩格式
- **PNG** (.png) - 透明图片格式  
- **GIF** (.gif) - 动图格式
- **BMP** (.bmp) - Windows位图
- **TIFF** (.tiff, .tif) - 高质量图片
- **ICO** (.ico) - 图标文件
- **A文件** (.a) - 自定义格式

### 🔧 扩展支持（需要NuGet包）
- **TGA** (.tga) - Targa格式，游戏纹理
- **DDS** (.dds) - DirectX Surface，压缩纹理
- **PSD** (.psd) - Photoshop文档
- **WebP** (.webp) - Google Web图片格式

## 🛠️ 技术栈

- **.NET Framework 4.8**: 主要框架
- **Windows Forms**: UI框架
- **System.Drawing**: 图片处理
- **GDI+**: 图形渲染

### 推荐的扩展包
- **Pfim**: TGA/DDS格式支持
- **KGySoft.Drawing**: WebP格式和增强功能
- **SixLabors.ImageSharp**: PSD格式和现代图片处理

## 📂 项目结构

```
apicpreview/
├── Form1.cs                 // 主窗体逻辑
├── Form1.Designer.cs        // 窗体设计器文件
├── ImageFormatHelper.cs     // 图片格式处理助手
├── Program.cs              // 程序入口点
├── apicpreview.csproj      // 项目文件
├── packages.config         // NuGet包配置
├── build.bat              // 编译脚本
├── 安装指南.md            // 详细安装指南
└── README.md              // 项目说明
```

## 🔄 开发计划

- [ ] 添加图片旋转功能
- [ ] 支持图片格式转换
- [ ] 添加EXIF信息显示
- [ ] 支持多页TIFF/PDF预览
- [ ] 添加图片缩略图导航
- [ ] 支持批量文件预览

## 📝 许可证

本项目基于MIT许可证开源。

## 🤝 贡献

欢迎提交Issue和Pull Request来帮助改进这个项目！

---

**注意**: 某些格式需要安装额外的NuGet包，详见`安装指南.md`。 