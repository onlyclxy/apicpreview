#!/usr/bin/env python3
"""
创建示例bg.png背景图片
运行此脚本生成一个渐变背景图片用于演示
"""

try:
    from PIL import Image, ImageDraw
    import os
    
    # 创建400x300的渐变背景
    width, height = 400, 300
    image = Image.new('RGB', (width, height))
    draw = ImageDraw.Draw(image)
    
    # 创建从深蓝到浅蓝的渐变
    for y in range(height):
        # 计算渐变颜色
        ratio = y / height
        r = int(30 + (100 - 30) * ratio)    # 30-100
        g = int(60 + (150 - 60) * ratio)    # 60-150  
        b = int(120 + (200 - 120) * ratio)  # 120-200
        
        # 绘制水平线
        draw.line([(0, y), (width, y)], fill=(r, g, b))
    
    # 添加一些装饰性的圆点
    for i in range(0, width, 40):
        for j in range(0, height, 40):
            # 半透明的白色圆点
            overlay = Image.new('RGBA', (width, height), (255, 255, 255, 0))
            overlay_draw = ImageDraw.Draw(overlay)
            overlay_draw.ellipse([i-5, j-5, i+5, j+5], fill=(255, 255, 255, 50))
            image = Image.alpha_composite(image.convert('RGBA'), overlay).convert('RGB')
    
    # 保存图片
    image.save('bg.png', 'PNG')
    print("✅ bg.png 背景图片已创建")
    print("📁 位置: bg.png")
    print("📐 尺寸: 400x300")
    print("🎨 效果: 蓝色渐变 + 装饰圆点")
    
except ImportError:
    print("❌ 需要安装 Pillow 库")
    print("运行: pip install Pillow")
    
    # 创建一个简单的文本文件说明
    with open('bg.png说明.txt', 'w', encoding='utf-8') as f:
        f.write("""bg.png 背景图片说明

这个文件应该是一个PNG图片，用作图片预览器的背景。

建议规格：
- 格式: PNG
- 尺寸: 任意（会自动平铺）
- 内容: 纹理、渐变或图案

您可以：
1. 安装Python Pillow库运行 create-bg.py 生成示例
2. 使用任意图片编辑软件创建自己的bg.png
3. 从网上下载合适的纹理图片重命名为bg.png

将bg.png放在exe文件同目录下即可使用默认背景功能。
""")
    print("📄 已创建说明文件: bg.png说明.txt")

except Exception as e:
    print(f"❌ 创建背景图片失败: {e}") 