using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PicViewEx.ImageSave
{
    /// <summary>
    /// ImageSaver 扩展方法，提供带UI反馈的保存功能
    /// </summary>
    public static class ImageSaverExtensions
    {
        /// <summary>
        /// 直接保存并显示成功对话框
        /// </summary>
        /// <param name="saver">ImageSaver 实例</param>
        /// <param name="source">要保存的图片数据</param>
        /// <param name="originalFilePath">原始文件路径</param>
        /// <param name="owner">父窗口（可选）</param>
        /// <returns>保存结果</returns>
        public static async Task<SaveResult> SaveWithDialog(
            this IImageSaver saver, 
            BitmapSource source, 
            string originalFilePath, 
            Window owner = null)
        {
            var result = await saver.Save(source, originalFilePath);

            if (result.Success)
            {
                // 显示成功对话框
                SuccessDialog.Show(result.SavedPath, owner);
            }
            else
            {
                // 显示错误对话框
                CustomMessageBox.Show(
                    $"保存失败！\n\n{result.Message}\n{result.ErrorDetails}",
                    "错误",
                    CustomMessageBox.MessageBoxButtons.OK,
                    CustomMessageBox.MessageBoxType.Error,
                    owner);
            }

            return result;
        }

        /// <summary>
        /// 保存到指定路径并显示成功对话框
        /// </summary>
        /// <param name="saver">ImageSaver 实例</param>
        /// <param name="source">要保存的图片数据</param>
        /// <param name="targetPath">目标文件路径</param>
        /// <param name="options">保存选项</param>
        /// <param name="owner">父窗口（可选）</param>
        /// <returns>保存结果</returns>
        public static async Task<SaveResult> SaveToWithDialog(
            this IImageSaver saver,
            BitmapSource source,
            string targetPath,
            SaveOptions options,
            Window owner = null)
        {
            var result = await saver.SaveTo(source, targetPath, options);

            if (result.Success)
            {
                // 显示成功对话框
                SuccessDialog.Show(result.SavedPath, owner);
            }
            else
            {
                // 显示错误对话框
                CustomMessageBox.Show(
                    $"保存失败！\n\n{result.Message}\n{result.ErrorDetails}",
                    "错误",
                    CustomMessageBox.MessageBoxButtons.OK,
                    CustomMessageBox.MessageBoxType.Error,
                    owner);
            }

            return result;
        }
    }
}

