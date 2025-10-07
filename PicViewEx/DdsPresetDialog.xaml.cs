using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Diagnostics;

namespace PicViewEx
{
    public partial class DdsPresetDialog : Window
    {
        public class PresetItem
        {
            public string Name { get; set; }
            public string FilePath { get; set; }
            public string ModifiedDate { get; set; }
        }

        public string SelectedPresetPath { get; private set; }
        public bool UseCustomPanel { get; private set; }
        public PresetItem SelectedPreset { get; private set; }
        public bool IsCustomPanelSelected => UseCustomPanel;

        public DdsPresetDialog()
        {
            InitializeComponent();
            LoadPresets();
        }

        private void LoadPresets()
        {
            try
            {
                // 获取预设文件夹路径
                string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) 
                    ?? AppDomain.CurrentDomain.BaseDirectory;
                string presetsFolder = Path.Combine(exeDirectory, "DDS Presets");

                Console.WriteLine($"加载DDS预设文件夹: {presetsFolder}");

                if (!Directory.Exists(presetsFolder))
                {
                    Console.WriteLine("预设文件夹不存在，创建默认文件夹");
                    Directory.CreateDirectory(presetsFolder);
                    return;
                }

                // 获取所有.dpf文件
                var presetFiles = Directory.GetFiles(presetsFolder, "*.dpf");
                Console.WriteLine($"找到 {presetFiles.Length} 个预设文件");

                var presetItems = new List<PresetItem>();

                foreach (var file in presetFiles)
                {
                    var fileInfo = new FileInfo(file);
                    var presetItem = new PresetItem
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                    };
                    presetItems.Add(presetItem);
                    Console.WriteLine($"加载预设: {presetItem.Name} ({presetItem.ModifiedDate})");
                }

                // 按修改日期排序，最新的在上面
                presetItems = presetItems.OrderByDescending(p => File.GetLastWriteTime(p.FilePath)).ToList();

                PresetListBox.ItemsSource = presetItems;

                // 默认选择第一个预设
                if (presetItems.Count > 0)
                {
                    PresetListBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载预设文件时出错: {ex.Message}");
                MessageBox.Show($"加载预设文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CustomPanelButton_Click(object sender, RoutedEventArgs e)
        {
            UseCustomPanel = true;
            DialogResult = true;
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetListBox.SelectedItem is PresetItem selectedPreset)
            {
                SelectedPresetPath = selectedPreset.FilePath;
                SelectedPreset = selectedPreset;
                UseCustomPanel = false;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("请选择一个预设", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}