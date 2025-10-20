# DDS 复用原始参数功能实现说明

## 功能概述
在另存为窗口的 DDS 格式选项中，新增了"复用原始图片参数"按钮，允许用户在另存为 DDS 文件时，自动使用原始 DDS 文件的压缩参数。

## 修改内容

### 1. XAML 界面修改 (`SaveAsWindow.xaml`)
- **位置**: 第149-194行
- **修改内容**:
  - 将原来单个"自定义参数保存"按钮改为双按钮横向布局
  - 新增"复用原始图片参数"按钮（`BtnDdsReuseParams`）
  - 使用 Grid 布局，两个按钮各占一半宽度，中间间隔10像素
  - 新按钮鼠标悬停时显示绿色（`#FF00C853`），以区别于其他按钮的蓝色

### 2. 后端代码修改 (`SaveAsWindow.xaml.cs`)

#### 2.1 新增字段
- **位置**: 第21行
- **内容**: 添加 `_useDdsReuseParams` 布尔字段，用于标记是否使用复用参数模式

#### 2.2 修改 BtnDds_Click 方法
- **位置**: 第116-151行
- **新增功能**:
  - 检查原始文件是否为 DDS 格式
  - 根据检查结果启用或禁用"复用参数"按钮（`BtnDdsReuseParams.IsEnabled`）
  - 禁用时设置透明度为 0.5（显示灰色效果）
  - 添加工具提示说明按钮的使用条件

#### 2.3 新增按钮点击事件处理方法
- **方法名**: `BtnDdsReuseParams_Click`
- **位置**: 约第200-246行
- **功能流程**:
  1. 检查原始文件路径是否有效
  2. 验证原始文件是否存在
  3. 确认原始文件是否为 DDS 格式
  4. 调用 `_nvidiaTools.GetDdsInfo()` 获取原始 DDS 文件的参数信息
  5. 如果成功获取信息，设置 `_useDdsReuseParams = true`
  6. 打开保存对话框

#### 2.4 修改预设按钮点击事件
- **位置**: 约第332行
- **修改**: 添加 `_useDdsReuseParams = false`，确保使用预设时清除复用参数标记

#### 2.5 修改 ShowSaveDialog 方法
- **位置**: 约第410行
- **修改**: 在创建 DdsSaveOptions 时，添加 `UseOriginalParams = _useDdsReuseParams`

#### 2.6 重构 SaveDdsImage 方法
- **位置**: 约第453-562行
- **修改内容**:
  - 增加复用参数的处理逻辑（情况1）
  - 当 `options.UseOriginalParams` 为 true 时：
    - 从原始文件获取 DDS 信息
    - 使用 `DdsCommandBuilder.BuildArgumentsFromInfo()` 构建命令行参数
    - 调用 `_nvidiaTools.ExportWithCommandArgs()` 执行保存
    - 成功时显示"保存成功（使用原始DDS参数）"
  - 保持原有的预设文件处理逻辑（情况2）

### 3. 数据模型修改 (`IImageSaver.cs`)
- **位置**: 第133-136行
- **修改内容**: 在 `DdsSaveOptions` 类中新增 `UseOriginalParams` 属性
- **用途**: 标识是否使用原始 DDS 文件的参数进行保存

## 功能特点

1. **智能验证**: 
   - 只有当原始文件是 DDS 格式时才允许复用参数
   - 自动检查文件是否存在和格式是否正确
   - 提供详细的错误提示信息

2. **复用现有代码**:
   - 调用 `ImageSaver.cs` 中已有的 DDS 参数获取和转换逻辑
   - 使用 `NvidiaTextureTools.GetDdsInfo()` 获取原始文件信息
   - 使用 `DdsCommandBuilder.BuildArgumentsFromInfo()` 构建保存参数

3. **用户体验优化**:
   - 按钮布局清晰，两个选项并列显示
   - 不同功能使用不同的悬停颜色（蓝色/绿色）
   - 保存成功时明确提示使用了原始参数
   - **自动禁用不可用功能**：当原始文件不是 DDS 格式时，"复用参数"按钮显示为灰色（禁用状态）
   - 悬停在禁用按钮上时显示工具提示"仅当原始文件为DDS格式时可用"

4. **与现有功能兼容**:
   - 不影响原有的预设选择功能
   - 不影响自定义 NVIDIA UI 功能
   - 各种保存模式互不干扰

5. **界面布局优化**:
   - 预设列表布局：左侧显示"全部预设"（带滚动条），右侧显示"常用预设"（最多3个）
   - 更符合用户使用习惯，常用功能放在右侧更容易点击

## 使用场景

- **编辑 DDS 文件**: 用户打开一个 DDS 文件，进行旋转等操作后，需要用相同的压缩参数另存为新文件
- **批量处理**: 保持同一项目中所有 DDS 文件的压缩参数一致
- **格式转换**: 从其他格式转换为 DDS 时，可以参考现有 DDS 文件的参数设置

## 技术依赖

- `NvidiaTextureTools.GetDdsInfo()`: 解析 DDS 文件信息
- `DdsCommandBuilder.BuildArgumentsFromInfo()`: 构建命令行参数
- `NvidiaTextureTools.ExportWithCommandArgs()`: 执行 DDS 导出

