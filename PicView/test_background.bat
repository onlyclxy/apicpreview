@echo off
echo 测试默认背景图片功能
echo.
echo 测试说明:
echo 1. 程序启动后，点击"背景设置"下的"图片"单选按钮
echo 2. 应该自动加载默认背景图片 (res\01.jpg)
echo 3. 点击"选择背景图"按钮，然后取消选择
echo 4. 应该继续显示默认背景图片
echo 5. 选择一个不存在的图片路径，应该回退到默认图片
echo.

echo 正在启动程序...
dotnet run

pause 