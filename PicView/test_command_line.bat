@echo off
echo PicView 命令行测试
echo.
echo 使用方法:
echo 1. 将图片文件拖拽到此批处理文件上
echo 2. 或者运行: dotnet run -- "图片路径"
echo.

if "%~1"=="" (
    echo 没有提供图片路径参数
    echo 请将图片文件拖拽到此批处理文件上，或者手动指定路径
    echo.
    set /p imagePath=请输入图片路径: 
) else (
    set imagePath=%~1
)

if not "%imagePath%"=="" (
    echo 正在打开: %imagePath%
    dotnet run -- "%imagePath%"
) else (
    echo 未提供有效的图片路径
)

pause 