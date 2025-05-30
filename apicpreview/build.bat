@echo off
echo 正在编译多格式图片预览器...
echo.

:: 检查是否有dotnet命令
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo 错误：找不到dotnet命令
    echo 请确保已安装.NET SDK
    echo 可以从 https://dotnet.microsoft.com/download 下载安装
    pause
    exit /b 1
)

echo 使用dotnet build编译项目...
echo.

:: 清理旧的编译文件
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"

:: 编译项目
dotnet build apicpreview.csproj --configuration Debug

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================
    echo          编译成功！
    echo ========================================
    echo.
    echo 可执行文件位置: bin\Debug\apicpreview.exe
    echo 文件大小: 
    dir "bin\Debug\apicpreview.exe" | findstr "apicpreview.exe"
    echo.
    echo 使用说明:
    echo 1. 双击运行 bin\Debug\apicpreview.exe
    echo 2. 拖拽图片文件到窗口或使用菜单打开
    echo 3. 查看"安装指南.md"了解如何支持更多格式
    echo 4. 使用快捷键进行各种操作
    echo.
    echo ========================================
    echo.
    set /p "run=是否现在运行程序？(y/n): "
    if /i "%run%"=="y" (
        echo 正在启动程序...
        start "" "bin\Debug\apicpreview.exe"
    )
) else (
    echo.
    echo ========================================
    echo          编译失败！
    echo ========================================
    echo.
    echo 请检查代码是否有错误或缺少依赖项
    echo 常见问题解决方案:
    echo 1. 确保安装了.NET Framework 4.8
    echo 2. 确保所有源文件都存在
    echo 3. 尝试清理后重新编译
)

echo.
pause