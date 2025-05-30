using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace apicpreview
{
    public partial class Form1 : Form
    {
        private PictureBox pictureBox;
        private float zoomFactor = 1.0f;
        private Point lastMousePosition;
        private bool isDragging = false;
        private Point imagePosition;
        private MenuStrip menuStrip;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel formatLabel;
        private ToolStripStatusLabel sizeLabel;
        private ToolStripStatusLabel zoomLabel;
        private string currentFilePath;

        public Form1()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = "多格式图片预览器";
            this.Size = new Size(1000, 700);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(600, 400);
            this.WindowState = FormWindowState.Maximized;

            // 创建菜单栏
            CreateMenuStrip();

            // 创建状态栏
            CreateStatusStrip();

            // 创建图片显示控件
            pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.DarkGray,
                AllowDrop = true
            };

            pictureBox.Paint += PictureBox_Paint;
            pictureBox.MouseWheel += PictureBox_MouseWheel;
            pictureBox.MouseDown += PictureBox_MouseDown;
            pictureBox.MouseMove += PictureBox_MouseMove;
            pictureBox.MouseUp += PictureBox_MouseUp;
            pictureBox.DragEnter += PictureBox_DragEnter;
            pictureBox.DragDrop += PictureBox_DragDrop;

            this.Controls.Add(pictureBox);
            
            // 支持拖放
            this.AllowDrop = true;
            this.DragEnter += PictureBox_DragEnter;
            this.DragDrop += PictureBox_DragDrop;

            // 窗口大小改变时重新计算图片位置
            this.Resize += Form1_Resize;

            // 键盘快捷键
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            UpdateStatusBar();
        }

        private void CreateMenuStrip()
        {
            menuStrip = new MenuStrip();

            // 文件菜单
            var fileMenu = new ToolStripMenuItem("文件(&F)");
            
            var openMenuItem = new ToolStripMenuItem("打开(&O)...", null, OpenFile_Click);
            openMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            fileMenu.DropDownItems.Add(openMenuItem);

            fileMenu.DropDownItems.Add(new ToolStripSeparator());

            var exitMenuItem = new ToolStripMenuItem("退出(&X)", null, (s, e) => this.Close());
            exitMenuItem.ShortcutKeys = Keys.Alt | Keys.F4;
            fileMenu.DropDownItems.Add(exitMenuItem);

            // 视图菜单
            var viewMenu = new ToolStripMenuItem("视图(&V)");
            
            var zoomInMenuItem = new ToolStripMenuItem("放大(&I)", null, ZoomIn_Click);
            zoomInMenuItem.ShortcutKeys = Keys.Control | Keys.Add;
            viewMenu.DropDownItems.Add(zoomInMenuItem);

            var zoomOutMenuItem = new ToolStripMenuItem("缩小(&O)", null, ZoomOut_Click);
            zoomOutMenuItem.ShortcutKeys = Keys.Control | Keys.Subtract;
            viewMenu.DropDownItems.Add(zoomOutMenuItem);

            var fitToWindowMenuItem = new ToolStripMenuItem("适应窗口(&F)", null, FitToWindow_Click);
            fitToWindowMenuItem.ShortcutKeys = Keys.Control | Keys.D0;
            viewMenu.DropDownItems.Add(fitToWindowMenuItem);

            var actualSizeMenuItem = new ToolStripMenuItem("实际大小(&A)", null, ActualSize_Click);
            actualSizeMenuItem.ShortcutKeys = Keys.Control | Keys.D1;
            viewMenu.DropDownItems.Add(actualSizeMenuItem);

            viewMenu.DropDownItems.Add(new ToolStripSeparator());

            var centerImageMenuItem = new ToolStripMenuItem("居中显示(&C)", null, CenterImage_Click);
            viewMenu.DropDownItems.Add(centerImageMenuItem);

            // 帮助菜单
            var helpMenu = new ToolStripMenuItem("帮助(&H)");
            
            var aboutMenuItem = new ToolStripMenuItem("关于(&A)...", null, About_Click);
            helpMenu.DropDownItems.Add(aboutMenuItem);

            var supportedFormatsMenuItem = new ToolStripMenuItem("支持的格式(&S)...", null, SupportedFormats_Click);
            helpMenu.DropDownItems.Add(supportedFormatsMenuItem);

            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, helpMenu });
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }

        private void CreateStatusStrip()
        {
            statusStrip = new StatusStrip();
            
            statusLabel = new ToolStripStatusLabel("就绪");
            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;

            formatLabel = new ToolStripStatusLabel("格式: 无");
            sizeLabel = new ToolStripStatusLabel("尺寸: 无");
            zoomLabel = new ToolStripStatusLabel("缩放: 100%");

            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, formatLabel, sizeLabel, zoomLabel });
            this.Controls.Add(statusStrip);
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    this.Close();
                    break;
                case Keys.F11:
                    ToggleFullScreen();
                    break;
            }
        }

        private void ToggleFullScreen()
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.WindowState = FormWindowState.Normal;
            }
        }

        private void OpenFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openDialog = new OpenFileDialog())
            {
                openDialog.Filter = ImageFormatHelper.GetFileFilter();
                openDialog.FilterIndex = 1;
                openDialog.Title = "选择图片文件";

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    LoadImage(openDialog.FileName);
                }
            }
        }

        private void ZoomIn_Click(object sender, EventArgs e)
        {
            if (pictureBox.Image != null)
            {
                zoomFactor *= 1.25f;
                zoomFactor = Math.Min(5f, zoomFactor);
                UpdateImageDisplay();
            }
        }

        private void ZoomOut_Click(object sender, EventArgs e)
        {
            if (pictureBox.Image != null)
            {
                zoomFactor *= 0.8f;
                zoomFactor = Math.Max(0.1f, zoomFactor);
                UpdateImageDisplay();
            }
        }

        private void FitToWindow_Click(object sender, EventArgs e)
        {
            if (pictureBox.Image != null)
            {
                float scaleX = (float)pictureBox.Width / pictureBox.Image.Width;
                float scaleY = (float)pictureBox.Height / pictureBox.Image.Height;
                zoomFactor = Math.Min(scaleX, scaleY);
                UpdateImageDisplay();
            }
        }

        private void ActualSize_Click(object sender, EventArgs e)
        {
            if (pictureBox.Image != null)
            {
                zoomFactor = 1.0f;
                UpdateImageDisplay();
            }
        }

        private void CenterImage_Click(object sender, EventArgs e)
        {
            CenterImage();
            pictureBox.Invalidate();
        }

        private void About_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "多格式图片预览器 v1.0\n\n" +
                "支持格式：\n" +
                "• 标准格式：JPG, PNG, GIF, BMP, TIFF, ICO\n" +
                "• 扩展格式：TGA, DDS, PSD, WebP\n" +
                "• 自定义格式：A文件\n\n" +
                "快捷键：\n" +
                "• Ctrl+O: 打开文件\n" +
                "• Ctrl++: 放大\n" +
                "• Ctrl+-: 缩小\n" +
                "• Ctrl+0: 适应窗口\n" +
                "• Ctrl+1: 实际大小\n" +
                "• F11: 全屏切换\n" +
                "• 鼠标滚轮: 缩放\n" +
                "• 拖拽: 移动图片\n" +
                "• 拖放文件: 打开图片",
                "关于",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void SupportedFormats_Click(object sender, EventArgs e)
        {
            string message = "支持的图片格式：\n\n";
            message += "✓ JPEG (.jpg, .jpeg) - 原生支持\n";
            message += "✓ PNG (.png) - 原生支持\n";
            message += "✓ GIF (.gif) - 原生支持\n";
            message += "✓ BMP (.bmp) - 原生支持\n";
            message += "✓ TIFF (.tiff, .tif) - 原生支持\n";
            message += "✓ ICO (.ico) - 原生支持\n";
            message += "○ TGA (.tga) - 需要 Pfim 包\n";
            message += "○ DDS (.dds) - 需要 Pfim 包\n";
            message += "○ PSD (.psd) - 需要 ImageSharp 包\n";
            message += "○ WebP (.webp) - 需要 KGySoft.Drawing 包\n";
            message += "✓ A文件 (.a) - 自定义支持\n\n";
            message += "注：标记为 ○ 的格式需要安装相应的 NuGet 包";

            MessageBox.Show(message, "支持的格式", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateImageDisplay()
        {
            CenterImage();
            pictureBox.Invalidate();
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            if (pictureBox.Image != null)
            {
                statusLabel.Text = string.IsNullOrEmpty(currentFilePath) ? "图片已加载" : $"已加载: {Path.GetFileName(currentFilePath)}";
                formatLabel.Text = $"格式: {ImageFormatHelper.GetFormatFriendlyName(currentFilePath ?? "")}";
                sizeLabel.Text = $"尺寸: {pictureBox.Image.Width} × {pictureBox.Image.Height}";
                zoomLabel.Text = $"缩放: {zoomFactor * 100:F0}%";
            }
            else
            {
                statusLabel.Text = "就绪";
                formatLabel.Text = "格式: 无";
                sizeLabel.Text = "尺寸: 无";
                zoomLabel.Text = "缩放: 100%";
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (pictureBox.Image != null)
            {
                CenterImage();
                pictureBox.Invalidate();
            }
        }

        private void CenterImage()
        {
            if (pictureBox.Image != null)
            {
                int scaledWidth = (int)(pictureBox.Image.Width * zoomFactor);
                int scaledHeight = (int)(pictureBox.Image.Height * zoomFactor);

                imagePosition = new Point(
                    (pictureBox.Width - scaledWidth) / 2,
                    (pictureBox.Height - scaledHeight) / 2
                );
            }
        }

        private void PictureBox_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(pictureBox.BackColor);

            if (pictureBox.Image != null)
            {
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                int scaledWidth = (int)(pictureBox.Image.Width * zoomFactor);
                int scaledHeight = (int)(pictureBox.Image.Height * zoomFactor);

                e.Graphics.DrawImage(pictureBox.Image, imagePosition.X, imagePosition.Y, scaledWidth, scaledHeight);
            }
            else
            {
                // 显示提示信息
                string message = "拖拽图片文件到此处或使用菜单打开图片\n\n支持格式：JPG, PNG, GIF, BMP, TIFF, ICO, TGA, DDS, PSD, WebP, A";
                using (Font font = new Font("微软雅黑", 12))
                using (Brush brush = new SolidBrush(Color.LightGray))
                {
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    e.Graphics.DrawString(message, font, brush, pictureBox.ClientRectangle, sf);
                }
            }
        }

        private void PictureBox_MouseWheel(object sender, MouseEventArgs e)
        {
            if (pictureBox.Image != null)
            {
                float oldZoom = zoomFactor;
                
                // 调整缩放系数
                if (e.Delta > 0)
                    zoomFactor *= 1.1f; // 放大10%
                else
                    zoomFactor *= 0.9f; // 缩小10%

                // 限制最小和最大缩放
                zoomFactor = Math.Max(0.05f, Math.Min(10f, zoomFactor));

                // 调整图片位置以保持鼠标指向的点不变
                float zoomRatio = zoomFactor / oldZoom;
                Point mousePos = e.Location;
                int newX = (int)(mousePos.X - (mousePos.X - imagePosition.X) * zoomRatio);
                int newY = (int)(mousePos.Y - (mousePos.Y - imagePosition.Y) * zoomRatio);
                
                imagePosition = new Point(newX, newY);
                
                pictureBox.Invalidate();
                UpdateStatusBar();
            }
        }

        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && pictureBox.Image != null)
            {
                isDragging = true;
                lastMousePosition = e.Location;
                pictureBox.Cursor = Cursors.Hand;
            }
        }

        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                int deltaX = e.X - lastMousePosition.X;
                int deltaY = e.Y - lastMousePosition.Y;

                imagePosition = new Point(imagePosition.X + deltaX, imagePosition.Y + deltaY);
                lastMousePosition = e.Location;

                pictureBox.Invalidate();
            }
        }

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
                pictureBox.Cursor = Cursors.Default;
            }
        }

        private void PictureBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && ImageFormatHelper.IsSupportedImageFile(files[0]))
                {
                    e.Effect = DragDropEffects.Copy;
                }
                else
                {
                    e.Effect = DragDropEffects.None;
                }
            }
        }

        private void PictureBox_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                LoadImage(files[0]);
            }
        }

        public void LoadImage(string filePath)
        {
            try
            {
                if (!ImageFormatHelper.IsSupportedImageFile(filePath))
                {
                    MessageBox.Show($"不支持的文件格式: {Path.GetExtension(filePath)}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 释放之前的图片
                if (pictureBox.Image != null)
                {
                    var temp = pictureBox.Image;
                    pictureBox.Image = null;
                    temp.Dispose();
                }

                // 加载新图片
                pictureBox.Image = ImageFormatHelper.LoadImage(filePath);
                currentFilePath = filePath;

                // 重置视图
                zoomFactor = 1.0f;
                CenterImage();

                this.Text = $"多格式图片预览器 - {Path.GetFileName(filePath)}";
                pictureBox.Invalidate();
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法加载图片: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (pictureBox.Image != null)
            {
                pictureBox.Image.Dispose();
                pictureBox.Image = null;
            }
            base.OnFormClosing(e);
        }
    }
} 