using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace rustdll
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // DLL 导入声明 - UTF-8版本（推荐用于中文路径）
        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern ulong InitPlayerUtf8(byte[] pathBytes, uint pathLen);

        // DLL 导入声明 - 传统版本（兼容性保留）
        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        static extern ulong InitPlayer(string path);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int GetNextFrame(
            ulong handle,
            out IntPtr data,
            out uint width,
            out uint height,
            out uint delayMs);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void ClosePlayer(ulong handle);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int GetGifInfo(
            ulong handle,
            out uint width,
            out uint height);

        // 新增：播放控制接口
        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int GetTotalFrames(ulong handle, out uint total);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int GetCurrentFrameIndex(ulong handle, out uint index);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int SeekToFrame(ulong handle, uint frameIndex);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int GetFrame(
            ulong handle,
            uint frameIndex,
            out IntPtr data,
            out uint width,
            out uint height,
            out uint delayMs);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int GetPreviousFrame(
            ulong handle,
            out IntPtr data,
            out uint width,
            out uint height,
            out uint delayMs);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int Reset(ulong handle);

        // 新增：索引状态查询接口
        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int IsIndexReady(ulong handle, out uint ready);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int GetIndexProgress(ulong handle, out uint progress);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int IsBuildingIndex(ulong handle, out uint building);

        private ulong _handle = 0;
        private WriteableBitmap? _bitmap;
        private DispatcherTimer _timer;
        private uint _width, _height;
        private bool _isPlaying = false;
        private DateTime _lastFrameTime = DateTime.Now;
        private int _frameCount = 0;
        private DateTime _fpsStartTime = DateTime.Now;

        // 新增：帧控制相关字段
        private uint _totalFrames = 0;
        private uint _currentFrameIndex = 0;
        private bool _manualControl = false; // 是否处于手动控制模式

        // 新增：索引状态相关字段
        private bool _indexReady = false;
        private uint _indexProgress = 0;
        private bool _indexBuilding = false;
        private DispatcherTimer _indexCheckTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化计时器
            _timer = new DispatcherTimer();
            _timer.Tick += Timer_Tick;
            
            // 初始化索引检查计时器
            _indexCheckTimer = new DispatcherTimer();
            _indexCheckTimer.Interval = TimeSpan.FromMilliseconds(500); // 每500ms检查一次
            _indexCheckTimer.Tick += IndexCheckTimer_Tick;
            
            // 自动加载测试文件
            LoadTestGif();
        }

        private void LoadTestGif()
        {
            string testGifPath = Path.Combine(Directory.GetCurrentDirectory(), "test.gif");
            if (File.Exists(testGifPath))
            {
                LoadImageFile(testGifPath);
                InfoText.Text = "已自动加载 test.gif";
            }
        }

        private void LoadGif_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "动画图像文件 (*.gif;*.webp)|*.gif;*.webp|GIF files (*.gif)|*.gif|WebP files (*.webp)|*.webp|All files (*.*)|*.*",
                Title = "选择动画图像文件 (GIF 或 WebP)"
            };
            
            if (dialog.ShowDialog() == true)
            {
                LoadImageFile(dialog.FileName);
            }
        }

        private bool LoadImageFile(string filePath)
        {
            try
            {
                // 关闭之前的播放器
                if (_handle != 0)
                {
                    ClosePlayer(_handle);
                    _handle = 0;
                }

                // 详细的调试信息
                string fileName = Path.GetFileName(filePath);
                string extension = Path.GetExtension(filePath).ToLower();
                StatusText.Text = $"尝试加载文件: {fileName} ({extension})";
                
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    StatusText.Text = "文件不存在！";
                    return false;
                }
                
                // 显示文件信息
                var fileInfo = new FileInfo(filePath);
                InfoText.Text = $"文件大小: {fileInfo.Length / (1024 * 1024):F1}MB ({extension} 格式) | 总帧数: {_totalFrames}";
                
                // 优先使用UTF-8版本的InitPlayer来支持中文路径和多种格式
                StatusText.Text = "尝试UTF-8编码加载...";
                _handle = LoadImageFileUtf8(filePath);
                
                if (_handle != 0)
                {
                    StatusText.Text = "UTF-8编码加载成功";
                }
                else
                {
                    // 如果UTF-8版本失败，尝试传统版本
                    StatusText.Text = "UTF-8失败，尝试ANSI编码...";
                    _handle = LoadImageFileAnsi(filePath);
                    
                    if (_handle != 0)
                    {
                        StatusText.Text = "ANSI编码加载成功";
                    }
                }
                
                if (_handle == 0)
                {
                    StatusText.Text = $"加载 {extension.ToUpper()} 失败 - 所有编码方式都失败了";
                    InfoText.Text = $"路径: {filePath}";
                    return false;
                }

                // 获取图像尺寸
                StatusText.Text = "获取图像信息...";
                if (GetGifInfo(_handle, out _width, out _height) != 0)
                {
                    ClosePlayer(_handle);
                    _handle = 0;
                    StatusText.Text = "获取图像信息失败";
                    return false;
                }

                // 获取总帧数
                if (GetTotalFrames(_handle, out _totalFrames) != 0)
                {
                    _totalFrames = 1; // 如果获取失败，假设只有1帧
                }

                // 重置帧索引
                _currentFrameIndex = 0;

                // 创建 WriteableBitmap
                StatusText.Text = "创建位图...";
                _bitmap = new WriteableBitmap(
                    (int)_width, 
                    (int)_height, 
                    96, 96, 
                    PixelFormats.Bgra32, 
                    null);

                GifImage.Source = _bitmap;
                
                // 更新状态信息
                SizeText.Text = $"大小: {_width}x{_height} | 帧数: {_currentFrameIndex + 1}/{_totalFrames}";
                StatusText.Text = $"已加载: {fileName} ({extension.ToUpper()})";
                
                // 启用控制按钮
                PlayButton.IsEnabled = true;
                PauseButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                
                // 初始禁用帧控制按钮，等待索引构建完成
                EnableFrameControls(false);
                
                // 启动索引状态监控
                _indexReady = false;
                _indexProgress = 0;
                _indexBuilding = false;
                _indexCheckTimer.Start();
                
                // 立即开始播放，不等待索引构建！
                StatusText.Text = "🚀 立即播放中... | 🔨 后台构建索引";
                StartPlaying(); // 立即开始播放
                
                // 重置计数器
                _frameCount = 0;
                _fpsStartTime = DateTime.Now;
                
                return true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"加载错误: {ex.Message}";
                InfoText.Text = $"异常详情: {ex}";
                return false;
            }
        }

        // UTF-8版本加载方法（支持中文路径和多种格式）
        private ulong LoadImageFileUtf8(string filePath)
        {
            try
            {
                // 将字符串转换为UTF-8字节数组
                byte[] utf8Bytes = Encoding.UTF8.GetBytes(filePath);
                
                // 调试信息
                System.Diagnostics.Debug.WriteLine($"UTF-8 bytes for '{filePath}': {BitConverter.ToString(utf8Bytes)}");
                System.Diagnostics.Debug.WriteLine($"UTF-8 byte count: {utf8Bytes.Length}");
                
                return InitPlayerUtf8(utf8Bytes, (uint)utf8Bytes.Length);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UTF-8 loading error: {ex}");
                return 0;
            }
        }

        // ANSI版本加载方法（兼容性保留）
        private ulong LoadImageFileAnsi(string filePath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ANSI loading: '{filePath}'");
                return InitPlayer(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ANSI loading error: {ex}");
                return 0;
            }
        }

        private void Play_Click(object? sender, RoutedEventArgs e)
        {
            StartPlaying();
        }

        private void Pause_Click(object? sender, RoutedEventArgs e)
        {
            PausePlaying();
        }

        private void Stop_Click(object? sender, RoutedEventArgs e)
        {
            StopPlaying();
        }

        private void StartPlaying()
        {
            if (_handle == 0 || _bitmap == null)
                return;

            _isPlaying = true;
            PlayButton.IsEnabled = false;
            PauseButton.IsEnabled = true;
            StatusText.Text = "播放中...";
            
            // 播放第一帧
            PlayNextFrame();
        }

        private void PausePlaying()
        {
            _isPlaying = false;
            _timer.Stop();
            PlayButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            StatusText.Text = "已暂停";
        }

        private void StopPlaying()
        {
            _isPlaying = false;
            _timer.Stop();
            PlayButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            StatusText.Text = "已停止";
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_isPlaying)
            {
                PlayNextFrame();
            }
        }

        private void PlayNextFrame()
        {
            if (_handle == 0 || !_isPlaying || _bitmap == null)
                return;

            try
            {
                int result = GetNextFrame(_handle, out IntPtr dataPtr, out uint w, out uint h, out uint delay);
                if (result == 0 && dataPtr != IntPtr.Zero)
                {
                    // 更新当前帧索引
                    UpdateCurrentFrameIndex();
                    
                    // 锁定 bitmap 进行写入
                    _bitmap.Lock();

                    try
                    {
                        // 计算数据大小
                        int dataSize = (int)(w * h * 4); // RGBA

                        // 复制数据到 bitmap
                        unsafe
                        {
                            byte* srcPtr = (byte*)dataPtr.ToPointer();
                            byte* dstPtr = (byte*)_bitmap.BackBuffer.ToPointer();
                            
                            // RGBA 转 BGRA
                            for (int i = 0; i < dataSize; i += 4)
                            {
                                dstPtr[i] = srcPtr[i + 2];     // B
                                dstPtr[i + 1] = srcPtr[i + 1]; // G
                                dstPtr[i + 2] = srcPtr[i];     // R  
                                dstPtr[i + 3] = srcPtr[i + 3]; // A
                            }
                        }

                        // 标记整个区域为脏区域
                        _bitmap.AddDirtyRect(new Int32Rect(0, 0, (int)w, (int)h));
                    }
                    finally
                    {
                        _bitmap.Unlock();
                    }

                    // 更新FPS计算
                    _frameCount++;
                    var now = DateTime.Now;
                    var elapsed = (now - _fpsStartTime).TotalSeconds;
                    if (elapsed >= 1.0)
                    {
                        double fps = _frameCount / elapsed;
                        FpsText.Text = $"FPS: {fps:F1}";
                        _frameCount = 0;
                        _fpsStartTime = now;
                    }

                    // 更新延迟信息
                    DelayText.Text = $"延迟: {delay}ms";

                    // 设置下一帧的延迟
                    uint actualDelay = Math.Max(delay, 16); // 最少 16ms (60fps)
                    _timer.Interval = TimeSpan.FromMilliseconds(actualDelay);
                    _timer.Start();
                }
                else
                {
                    StatusText.Text = "读取帧失败";
                    PausePlaying();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"播放错误: {ex.Message}";
                PausePlaying();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // 停止计时器
            _timer?.Stop();
            _indexCheckTimer?.Stop();
            
            // 关闭播放器
            if (_handle != 0)
            {
                ClosePlayer(_handle);
                _handle = 0;
            }
        }

        // 新增：帧控制事件处理函数
        private void Reset_Click(object? sender, RoutedEventArgs e)
        {
            if (_handle == 0) return;
            
            Reset(_handle);
            _currentFrameIndex = 0;
            UpdateFrameDisplay();
            UpdateFrameInfo();
        }

        private void PrevFrame_Click(object? sender, RoutedEventArgs e)
        {
            if (_handle == 0) return;
            
            _manualControl = true;
            PausePlaying();
            
            int result = GetPreviousFrame(_handle, out IntPtr dataPtr, out uint w, out uint h, out uint delay);
            if (result == 0 && dataPtr != IntPtr.Zero)
            {
                UpdateCurrentFrameIndex();
                DisplayFrame(dataPtr, w, h, delay);
            }
        }

        private void NextFrame_Click(object? sender, RoutedEventArgs e)
        {
            if (_handle == 0) return;
            
            _manualControl = true;
            PausePlaying();
            
            int result = GetNextFrame(_handle, out IntPtr dataPtr, out uint w, out uint h, out uint delay);
            if (result == 0 && dataPtr != IntPtr.Zero)
            {
                UpdateCurrentFrameIndex();
                DisplayFrame(dataPtr, w, h, delay);
            }
        }

        private void Seek_Click(object? sender, RoutedEventArgs e)
        {
            if (_handle == 0) return;
            
            if (uint.TryParse(FrameIndexTextBox.Text, out uint frameIndex))
            {
                if (frameIndex >= _totalFrames)
                {
                    MessageBox.Show($"帧索引超出范围！有效范围: 0-{_totalFrames - 1}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                _manualControl = true;
                PausePlaying();
                
                if (SeekToFrame(_handle, frameIndex) == 0)
                {
                    _currentFrameIndex = frameIndex;
                    // 获取当前帧显示
                    int result = GetFrame(_handle, frameIndex, out IntPtr dataPtr, out uint w, out uint h, out uint delay);
                    if (result == 0 && dataPtr != IntPtr.Zero)
                    {
                        DisplayFrame(dataPtr, w, h, delay);
                    }
                    UpdateFrameInfo();
                }
            }
            else
            {
                MessageBox.Show("请输入有效的帧索引数字！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 新增：更新当前帧索引
        private void UpdateCurrentFrameIndex()
        {
            if (_handle != 0)
            {
                if (GetCurrentFrameIndex(_handle, out uint index) == 0)
                {
                    _currentFrameIndex = index;
                }
                UpdateFrameInfo();
            }
        }

        // 新增：更新帧信息显示
        private void UpdateFrameInfo()
        {
            SizeText.Text = $"大小: {_width}x{_height} | 帧数: {_currentFrameIndex + 1}/{_totalFrames}";
        }

        // 新增：显示帧的通用方法
        private void DisplayFrame(IntPtr dataPtr, uint w, uint h, uint delay)
        {
            if (_bitmap == null) return;
            
            try
            {
                // 锁定 bitmap 进行写入
                _bitmap.Lock();

                try
                {
                    // 计算数据大小
                    int dataSize = (int)(w * h * 4); // RGBA

                    // 复制数据到 bitmap
                    unsafe
                    {
                        byte* srcPtr = (byte*)dataPtr.ToPointer();
                        byte* dstPtr = (byte*)_bitmap.BackBuffer.ToPointer();
                        
                        // RGBA 转 BGRA
                        for (int i = 0; i < dataSize; i += 4)
                        {
                            dstPtr[i] = srcPtr[i + 2];     // B
                            dstPtr[i + 1] = srcPtr[i + 1]; // G
                            dstPtr[i + 2] = srcPtr[i];     // R  
                            dstPtr[i + 3] = srcPtr[i + 3]; // A
                        }
                    }

                    // 标记整个区域为脏区域
                    _bitmap.AddDirtyRect(new Int32Rect(0, 0, (int)w, (int)h));
                }
                finally
                {
                    _bitmap.Unlock();
                }

                // 更新延迟信息
                DelayText.Text = $"延迟: {delay}ms";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"显示帧错误: {ex.Message}";
            }
        }

        // 新增：更新帧显示（用于重置后）
        private void UpdateFrameDisplay()
        {
            if (_handle == 0) return;

            int result = GetFrame(_handle, _currentFrameIndex, out IntPtr dataPtr, out uint w, out uint h, out uint delay);
            if (result == 0 && dataPtr != IntPtr.Zero)
            {
                DisplayFrame(dataPtr, w, h, delay);
            }
        }

        // 新增：索引状态检查定时器回调
        private void IndexCheckTimer_Tick(object? sender, EventArgs e)
        {
            if (_handle == 0) return;

            try
            {
                // 检查索引是否准备就绪
                if (IsIndexReady(_handle, out uint ready) == 0)
                {
                    bool newIndexReady = ready != 0;
                    
                    // 如果索引状态发生变化
                    if (newIndexReady != _indexReady)
                    {
                        _indexReady = newIndexReady;
                        
                        if (_indexReady)
                        {
                            // 索引构建完成，启用所有控制按钮
                            EnableFrameControls(true);
                            
                            // 更新状态，但不影响播放状态
                            if (_isPlaying)
                            {
                                StatusText.Text = "🚀 播放中 | ✅ 索引完成 - 帧控制已启用";
                            }
                            else
                            {
                                StatusText.Text = "✅ 索引构建完成 - 所有帧控制功能已启用";
                            }
                            _indexCheckTimer.Stop(); // 停止检查
                        }
                    }
                }

                // 检查构建进度（只在索引未完成时）
                if (!_indexReady && GetIndexProgress(_handle, out uint progress) == 0)
                {
                    _indexProgress = progress;
                    
                    // 检查是否正在构建
                    if (IsBuildingIndex(_handle, out uint building) == 0)
                    {
                        _indexBuilding = building != 0;
                        
                        if (_indexBuilding)
                        {
                            // 显示进度，但保持播放状态
                            if (_isPlaying)
                            {
                                StatusText.Text = $"🚀 播放中 | 🔨 构建索引... {_indexProgress}%";
                            }
                            else
                            {
                                StatusText.Text = $"🔨 正在构建帧索引... 进度: {_indexProgress}%";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IndexCheckTimer_Tick error: {ex.Message}");
            }
        }

        // 新增：启用/禁用帧控制按钮
        private void EnableFrameControls(bool enabled)
        {
            Dispatcher.Invoke(() =>
            {
                ResetButton.IsEnabled = enabled;
                PrevFrameButton.IsEnabled = enabled;
                NextFrameButton.IsEnabled = enabled;
                SeekButton.IsEnabled = enabled;
                FrameIndexTextBox.IsEnabled = enabled;
                
                if (enabled)
                {
                    // 更新总帧数信息
                    if (_handle != 0 && GetTotalFrames(_handle, out uint totalFrames) == 0)
                    {
                        _totalFrames = totalFrames;
                        UpdateFrameInfo();
                    }
                }
            });
        }
    }
}