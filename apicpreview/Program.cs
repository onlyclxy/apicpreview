using System;
using System.Windows.Forms;

namespace apicpreview
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            Form1 form = new Form1();
            
            // 如果有命令行参数，尝试加载第一个参数作为图片文件
            if (args.Length > 0)
            {
                form.LoadImage(args[0]);
            }
            
            Application.Run(form);
        }
    }
}
