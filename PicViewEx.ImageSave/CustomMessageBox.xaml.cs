using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PicViewEx.ImageSave
{
    public partial class CustomMessageBox : Window
    {
        public enum MessageBoxType
        {
            Information,
            Warning,
            Error,
            Success
        }

        public enum MessageBoxButtons
        {
            OK,
            OKCancel,
            YesNo,
            YesNoCancel
        }

        public MessageBoxResult Result { get; private set; }

        private CustomMessageBox()
        {
            InitializeComponent();
            Result = MessageBoxResult.None;
        }

        /// <summary>
        /// 显示自定义消息框
        /// </summary>
        public static MessageBoxResult Show(string message, string title = "提示", 
            MessageBoxButtons buttons = MessageBoxButtons.OK, 
            MessageBoxType type = MessageBoxType.Information,
            Window owner = null)
        {
            var dialog = new CustomMessageBox();
            
            if (owner != null)
            {
                dialog.Owner = owner;
            }

            // 设置标题
            dialog.Title = title;
            dialog.TitleText.Text = title;

            // 设置消息
            dialog.MessageText.Text = message;

            // 设置图标和颜色
            switch (type)
            {
                case MessageBoxType.Information:
                    dialog.IconText.Text = "ℹ️";
                    dialog.IconText.Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                    break;
                case MessageBoxType.Warning:
                    dialog.IconText.Text = "⚠️";
                    dialog.IconText.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                    break;
                case MessageBoxType.Error:
                    dialog.IconText.Text = "❌";
                    dialog.IconText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    break;
                case MessageBoxType.Success:
                    dialog.IconText.Text = "✅";
                    dialog.IconText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    break;
            }

            // 添加按钮
            dialog.CreateButtons(buttons);

            // 显示对话框
            dialog.ShowDialog();

            return dialog.Result;
        }

        private void CreateButtons(MessageBoxButtons buttons)
        {
            ButtonPanel.Children.Clear();

            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    AddButton("确定", MessageBoxResult.OK, true);
                    break;

                case MessageBoxButtons.OKCancel:
                    AddButton("确定", MessageBoxResult.OK, true);
                    AddButton("取消", MessageBoxResult.Cancel, false);
                    break;

                case MessageBoxButtons.YesNo:
                    AddButton("是", MessageBoxResult.Yes, true);
                    AddButton("否", MessageBoxResult.No, false);
                    break;

                case MessageBoxButtons.YesNoCancel:
                    AddButton("是", MessageBoxResult.Yes, true);
                    AddButton("否", MessageBoxResult.No, false);
                    AddButton("取消", MessageBoxResult.Cancel, false);
                    break;
            }
        }

        private void AddButton(string text, MessageBoxResult result, bool isDefault)
        {
            var button = new Button
            {
                Content = text,
                Style = (Style)FindResource("DialogButtonStyle"),
                IsDefault = isDefault
            };

            button.Click += (s, e) =>
            {
                Result = result;
                DialogResult = true;
                Close();
            };

            ButtonPanel.Children.Add(button);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}

