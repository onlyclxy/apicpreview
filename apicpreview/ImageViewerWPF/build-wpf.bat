@echo off
echo 正在编译高级图片预览器 (WPF版本)...

:: 检查.NET SDK
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误：未找到 .NET SDK
    echo 请下载并安装 .NET 8.0 SDK: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo 找到 .NET SDK，开始编译...

:: 恢复 NuGet 包
echo 正在恢复 NuGet 包...
dotnet restore ImageViewerWPF.csproj

if %errorlevel% neq 0 (
    echo NuGet 包恢复失败！
    pause
    exit /b 1
)

:: 编译项目
echo 正在编译项目...
dotnet build ImageViewerWPF.csproj --configuration Release --no-restore

if %errorlevel% equ 0 (
    echo.
    echo ✅ 编译成功！
    echo.
    echo 📁 可执行文件位置:
    echo    bin\Release\net8.0-windows\ImageViewerWPF.exe
    echo.
    echo 🚀 使用说明:
    echo 1. 运行 ImageViewerWPF.exe
    echo 2. 拖拽图片文件到窗口或使用菜单打开
    echo 3. 使用背景工具栏调整背景模式
    echo 4. 勾选"显示通道"查看RGB/Alpha通道
    echo 5. 使用旋转按钮并另存为转换格式
    echo.
    echo 🎨 特色功能:
    echo • 透明方格背景
    echo • 自定义纯色背景（色相/明度滑块）
    echo • 图片背景叠加
    echo • 通道分析显示
    echo • 图片格式转换
    echo • 窗口透明模式
    echo.
    
    :: 检查是否有bg.png
    if not exist "bg.png" (
        echo ⚠️  提示: bg.png 背景图片文件不存在
        echo    您可以放置一个 bg.png 文件以使用默认图片背景功能
        echo.
    )
    
    echo 按任意键运行程序...
    pause >nul
    start "" "bin\Release\net8.0-windows\ImageViewerWPF.exe"
) else (
    echo.
    echo ❌ 编译失败！
    echo 请检查代码是否有错误，或查看上方的错误信息
    echo.
    echo 常见问题解决:
    echo 1. 确保安装了 .NET 8.0 SDK
    echo 2. 检查 NuGet 包是否正确恢复
    echo 3. 确保没有语法错误
)

pause 