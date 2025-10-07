using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PicViewEx
{
    /// <summary>
    /// GIF和WebP播放器类，封装了Rust DLL的调用
    /// </summary>
    public class GifWebpPlayer : IDisposable
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

        // 播放控制接口
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

        // 索引状态查询接口
        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int IsIndexReady(ulong handle, out uint ready);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int GetIndexProgress(ulong handle, out uint progress);

        [DllImport("gifplayer.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int IsBuildingIndex(ulong handle, out uint building);

        // 私有字段
        private ulong _handle = 0;
        private WriteableBitmap _bitmap;
        private DispatcherTimer _timer;
        private DispatcherTimer _indexCheckTimer;
        private uint _width = 0;
        private uint _height = 0;
        private bool _isPlaying = false;
        private uint _totalFrames = 0;
        private uint _currentFrameIndex = 0;
        private bool _indexReady = false;
        private bool _isBuildingIndex = false;
        private bool _disposed = false;
        private DateTime _lastFrameTime;
        private int _frameCount = 0;
        private DateTime _fpsStartTime;
        private bool _manualControl = false;
        private uint _indexProgress = 0;
        private bool _indexBuilding = false;

        // 事件
        public event EventHandler<FrameUpdatedEventArgs> FrameUpdated;
        public event EventHandler<StatusUpdatedEventArgs> StatusUpdated;
        public event EventHandler<IndexStatusEventArgs> IndexStatusChanged;

        public GifWebpPlayer()
        {
            // 初始化计时器
            _timer = new DispatcherTimer();
            _timer.Tick += Timer_Tick;
            
            // 初始化索引检查计时器
            _indexCheckTimer = new DispatcherTimer();
            _indexCheckTimer.Interval = TimeSpan.FromMilliseconds(500);
            _indexCheckTimer.Tick += IndexCheckTimer_Tick;
        }

        /// <summary>
        /// 加载GIF或WebP文件
        /// </summary>
        public bool LoadFile(string filePath)
        {
            try
            {
                // 关闭之前的播放器
                if (_handle != 0)
                {
                    ClosePlayer(_handle);
                    _handle = 0;
                }

                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    OnStatusUpdated("文件不存在！");
                    return false;
                }

                // 优先使用UTF-8版本的InitPlayer来支持中文路径
                _handle = LoadFileUtf8(filePath);
                
                if (_handle == 0)
                {
                    // 如果UTF-8版本失败，尝试传统版本
                    _handle = LoadFileAnsi(filePath);
                }
                
                if (_handle == 0)
                {
                    OnStatusUpdated("加载文件失败");
                    return false;
                }

                // 获取图像尺寸
                if (GetGifInfo(_handle, out _width, out _height) != 0)
                {
                    ClosePlayer(_handle);
                    _handle = 0;
                    OnStatusUpdated("获取图像信息失败");
                    return false;
                }

                // 获取总帧数
                if (GetTotalFrames(_handle, out _totalFrames) != 0)
                {
                    _totalFrames = 0; // 初始设为0，表示未知，等待索引构建完成后更新
                }

                // 重置帧索引
                _currentFrameIndex = 0;

                // 创建 WriteableBitmap
                _bitmap = new WriteableBitmap(
                    (int)_width, 
                    (int)_height, 
                    96, 96, 
                    PixelFormats.Bgra32, 
                    null);

                // 启动索引状态监控
                _indexReady = false;
                _indexProgress = 0;
                _indexBuilding = false;
                _indexCheckTimer.Start();

                OnStatusUpdated($"已加载: {Path.GetFileName(filePath)}");
                
                return true;
            }
            catch (Exception ex)
            {
                OnStatusUpdated($"加载失败: {ex.Message}");
                return false;
            }
        }

        private ulong LoadFileUtf8(string filePath)
        {
            try
            {
                byte[] pathBytes = System.Text.Encoding.UTF8.GetBytes(filePath);
                return InitPlayerUtf8(pathBytes, (uint)pathBytes.Length);
            }
            catch
            {
                return 0;
            }
        }

        private ulong LoadFileAnsi(string filePath)
        {
            try
            {
                return InitPlayer(filePath);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 开始播放
        /// </summary>
        public void Play()
        {
            if (_handle == 0) return;
            
            if (!_isPlaying)
            {
                _isPlaying = true;
                _lastFrameTime = DateTime.Now;
                _frameCount = 0;
                _fpsStartTime = DateTime.Now;
                _timer.Start();
                OnStatusUpdated("播放中...");
            }
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            if (_isPlaying)
            {
                _isPlaying = false;
                _timer.Stop();
                OnStatusUpdated("已停止");
            }
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            if (_isPlaying)
            {
                _isPlaying = false;
                _timer.Stop();
                OnStatusUpdated("已暂停");
            }
        }

        /// <summary>
        /// 重置到第一帧
        /// </summary>
        public void ResetToFirstFrame()
        {
            if (_handle == 0) return;
            
            _manualControl = true;
            _isPlaying = false;
            _timer.Stop();
            
            if (Reset(_handle) == 0)
            {
                _currentFrameIndex = 0;
                OnStatusUpdated("已重置到第一帧");
                
                // 获取第一帧并显示
                if (GetFrame(_handle, 0, out IntPtr data, out uint width, out uint height, out uint delayMs) == 0)
                {
                    UpdateBitmap(data, width, height);
                    OnFrameUpdated(_bitmap, delayMs);
                }
            }
        }

        /// <summary>
        /// 跳转到上一帧
        /// </summary>
        public void PreviousFrame()
        {
            if (_handle == 0 || !_indexReady) return;
            
            _manualControl = true;
            _isPlaying = false;
            _timer.Stop();
            
            // 如果已经是第一帧，循环到最后一帧
            if (_currentFrameIndex == 0)
            {
                SeekToFrame(_totalFrames - 1);
                return;
            }
            
            if (GetPreviousFrame(_handle, out IntPtr data, out uint width, out uint height, out uint delayMs) == 0)
            {
                UpdateBitmap(data, width, height);
                GetCurrentFrameIndex(_handle, out _currentFrameIndex);
                OnFrameUpdated(_bitmap, delayMs);
            }
        }

        /// <summary>
        /// 跳转到下一帧
        /// </summary>
        public void NextFrame()
        {
            if (_handle == 0) return;
            
            _manualControl = true;
            _isPlaying = false;
            _timer.Stop();
            
            // 如果已经是最后一帧，循环到第一帧
            if (_currentFrameIndex >= _totalFrames - 1)
            {
                SeekToFrame(0);
                return;
            }
            
            if (GetNextFrame(_handle, out IntPtr data, out uint width, out uint height, out uint delayMs) == 0)
            {
                UpdateBitmap(data, width, height);
                GetCurrentFrameIndex(_handle, out _currentFrameIndex);
                OnFrameUpdated(_bitmap, delayMs);
            }
        }

        /// <summary>
        /// 跳转到指定帧
        /// </summary>
        public void SeekToFrame(uint frameIndex)
        {
            if (_handle == 0 || !_indexReady) return;
            
            if (frameIndex >= _totalFrames) return;
            
            _manualControl = true;
            _isPlaying = false;
            _timer.Stop();
            
            if (SeekToFrame(_handle, frameIndex) == 0)
            {
                if (GetFrame(_handle, frameIndex, out IntPtr data, out uint width, out uint height, out uint delayMs) == 0)
                {
                    UpdateBitmap(data, width, height);
                    _currentFrameIndex = frameIndex;
                    OnFrameUpdated(_bitmap, delayMs);
                }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isPlaying || _handle == 0) return;

            UpdateCurrentFrame();
        }

        private void IndexCheckTimer_Tick(object sender, EventArgs e)
        {
            if (_handle == 0) return;

            // 检查索引状态
            int readyResult = IsIndexReady(_handle, out uint ready);
            int progressResult = GetIndexProgress(_handle, out uint progress);
            int buildingResult = IsBuildingIndex(_handle, out uint building);

            bool isReady = ready == 1;
            bool isBuilding = building == 1;

            if (isReady != _indexReady || isBuilding != _isBuildingIndex)
            {
                _indexReady = isReady;
                _isBuildingIndex = isBuilding;
                OnIndexStatusChanged(isReady, progress, isBuilding);
            }

            // 如果索引构建完成，重新获取总帧数并停止检查定时器
            if (isReady && !isBuilding)
            {
                // 重新获取准确的总帧数
                if (GetTotalFrames(_handle, out uint newTotalFrames) == 0)
                {
                    _totalFrames = newTotalFrames;
                    
                    // 确保显示第一帧（索引0）
                    if (GetFrame(_handle, 0, out IntPtr data, out uint width, out uint height, out uint delayMs) == 0)
                    {
                        UpdateBitmap(data, width, height);
                        _currentFrameIndex = 0;
                        OnFrameUpdated(_bitmap, delayMs);
                    }
                }
                
                if (_indexCheckTimer != null)
                {
                    _indexCheckTimer.Stop();
                }
            }
        }

        private void UpdateBitmap(IntPtr data, uint width, uint height)
        {
            if (_bitmap == null || data == IntPtr.Zero) return;

            try
            {
                _bitmap.Lock();
                
                // 计算数据大小
                int dataSize = (int)(width * height * 4); // RGBA

                // 复制数据到 bitmap 并进行颜色格式转换
                unsafe
                {
                    byte* srcPtr = (byte*)data.ToPointer();
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
                
                _bitmap.AddDirtyRect(new Int32Rect(0, 0, (int)width, (int)height));
            }
            finally
            {
                _bitmap.Unlock();
            }
        }

        private void UpdateCurrentFrame()
        {
            if (_handle == 0) return;
            
            if (GetNextFrame(_handle, out IntPtr data, out uint width, out uint height, out uint delayMs) == 0)
            {
                UpdateBitmap(data, width, height);
                GetCurrentFrameIndex(_handle, out _currentFrameIndex);
                OnFrameUpdated(_bitmap, delayMs);
                
                // 根据帧延迟设置下一帧的定时器间隔
                uint actualDelay = Math.Max(delayMs, 16); // 最少 16ms (60fps)
                _timer.Interval = TimeSpan.FromMilliseconds(actualDelay);
            }
        }

        private void OnFrameUpdated(WriteableBitmap bitmap, uint delayMs)
        {
            if (FrameUpdated != null)
                FrameUpdated.Invoke(this, new FrameUpdatedEventArgs(bitmap, delayMs, _currentFrameIndex, _totalFrames, _width, _height));
        }

        private void OnStatusUpdated(string status)
        {
            if (StatusUpdated != null)
                StatusUpdated.Invoke(this, new StatusUpdatedEventArgs(status));
        }

        private void OnIndexStatusChanged(bool ready, uint progress, bool building)
        {
            if (IndexStatusChanged != null)
                IndexStatusChanged.Invoke(this, new IndexStatusEventArgs(ready, progress, building));
        }

        private void StopTimers()
        {
            if (_timer != null)
                _timer.Stop();
            if (_indexCheckTimer != null)
                _indexCheckTimer.Stop();
        }

        public void Dispose()
        {
            StopTimers();
            
            if (_handle != 0)
            {
                ClosePlayer(_handle);
                _handle = 0;
            }
        }

        // 属性
        public bool IsLoaded => _handle != 0;
        public ulong Handle => _handle;
        public bool IsPlaying => _isPlaying;
        public uint TotalFrames => _totalFrames;
        public uint CurrentFrameIndex => _currentFrameIndex;
        public uint Width => _width;
        public uint Height => _height;
        public bool IndexReady => _indexReady;
        public WriteableBitmap Bitmap => _bitmap;
    }

    // 事件参数类
    public class FrameUpdatedEventArgs : EventArgs
    {
        public WriteableBitmap Bitmap { get; }
        public uint DelayMs { get; }
        public uint CurrentFrame { get; }
        public uint TotalFrames { get; }
        public uint Width { get; }
        public uint Height { get; }

        public FrameUpdatedEventArgs(WriteableBitmap bitmap, uint delayMs, uint currentFrame, uint totalFrames, uint width, uint height)
        {
            Bitmap = bitmap;
            DelayMs = delayMs;
            CurrentFrame = currentFrame;
            TotalFrames = totalFrames;
            Width = width;
            Height = height;
        }
    }

    public class StatusUpdatedEventArgs : EventArgs
    {
        public string Status { get; }

        public StatusUpdatedEventArgs(string status)
        {
            Status = status;
        }
    }

    public class IndexStatusEventArgs : EventArgs
    {
        public bool Ready { get; }
        public uint Progress { get; }
        public bool Building { get; }

        public IndexStatusEventArgs(bool ready, uint progress, bool building)
        {
            Ready = ready;
            Progress = progress;
            Building = building;
        }
    }
}