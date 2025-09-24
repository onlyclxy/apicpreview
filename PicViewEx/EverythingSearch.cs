using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PicViewEx
{
    public class EverythingSearch
    {
        #region Everything API declarations
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern int Everything_SetSearchW(string lpSearchString);

        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMatchPath(bool bEnable);

        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMatchCase(bool bEnable);

        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMatchWholeWord(bool bEnable);

        [DllImport("Everything64.dll")]
        public static extern void Everything_SetRegex(bool bEnable);

        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMax(int dwMax);

        [DllImport("Everything64.dll")]
        public static extern void Everything_SetOffset(int dwOffset);

        [DllImport("Everything64.dll")]
        public static extern bool Everything_QueryW(bool bWait);

        [DllImport("Everything64.dll")]
        public static extern int Everything_GetNumResults();

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultFullPathNameW(int nIndex, StringBuilder lpString, int nMaxCount);

        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsFileResult(int nIndex);

        [DllImport("Everything64.dll")]
        public static extern long Everything_GetResultSize(int nIndex);

        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetResultDateModified(int nIndex, out long lpFileTime);

        [DllImport("Everything64.dll")]
        public static extern int Everything_GetLastError();

        [DllImport("Everything64.dll")]
        public static extern int Everything_GetResultListSort();

        [DllImport("Everything64.dll")]
        public static extern void Everything_SetSort(int dwSortType);

        // Error codes
        public const int EVERYTHING_OK = 0;
        public const int EVERYTHING_ERROR_MEMORY = 1;
        public const int EVERYTHING_ERROR_IPC = 2;
        public const int EVERYTHING_ERROR_REGISTERCLASSEX = 3;
        public const int EVERYTHING_ERROR_CREATEWINDOW = 4;
        public const int EVERYTHING_ERROR_CREATETHREAD = 5;
        public const int EVERYTHING_ERROR_INVALIDINDEX = 6;
        public const int EVERYTHING_ERROR_INVALIDCALL = 7;

        // Sort types
        public const int EVERYTHING_SORT_NAME_ASCENDING = 1;
        public const int EVERYTHING_SORT_NAME_DESCENDING = 2;
        public const int EVERYTHING_SORT_PATH_ASCENDING = 3;
        public const int EVERYTHING_SORT_PATH_DESCENDING = 4;
        public const int EVERYTHING_SORT_SIZE_ASCENDING = 5;
        public const int EVERYTHING_SORT_SIZE_DESCENDING = 6;
        public const int EVERYTHING_SORT_EXTENSION_ASCENDING = 7;
        public const int EVERYTHING_SORT_EXTENSION_DESCENDING = 8;
        public const int EVERYTHING_SORT_TYPE_NAME_ASCENDING = 9;
        public const int EVERYTHING_SORT_TYPE_NAME_DESCENDING = 10;
        public const int EVERYTHING_SORT_DATE_CREATED_ASCENDING = 11;
        public const int EVERYTHING_SORT_DATE_CREATED_DESCENDING = 12;
        public const int EVERYTHING_SORT_DATE_MODIFIED_ASCENDING = 13;
        public const int EVERYTHING_SORT_DATE_MODIFIED_DESCENDING = 14;
        public const int EVERYTHING_SORT_ATTRIBUTES_ASCENDING = 15;
        public const int EVERYTHING_SORT_ATTRIBUTES_DESCENDING = 16;
        public const int EVERYTHING_SORT_FILE_LIST_FILENAME_ASCENDING = 17;
        public const int EVERYTHING_SORT_FILE_LIST_FILENAME_DESCENDING = 18;
        public const int EVERYTHING_SORT_RUN_COUNT_ASCENDING = 19;
        public const int EVERYTHING_SORT_RUN_COUNT_DESCENDING = 20;
        public const int EVERYTHING_SORT_DATE_RECENTLY_CHANGED_ASCENDING = 21;
        public const int EVERYTHING_SORT_DATE_RECENTLY_CHANGED_DESCENDING = 22;
        public const int EVERYTHING_SORT_DATE_ACCESSED_ASCENDING = 23;
        public const int EVERYTHING_SORT_DATE_ACCESSED_DESCENDING = 24;
        public const int EVERYTHING_SORT_DATE_RUN_ASCENDING = 25;
        public const int EVERYTHING_SORT_DATE_RUN_DESCENDING = 26;
        #endregion

        private readonly List<string> _supportedImageFormats;
        private bool _isEverythingAvailable;

        public EverythingSearch(List<string> supportedFormats)
        {
            _supportedImageFormats = supportedFormats;
            _isEverythingAvailable = CheckEverythingAvailability();
        }

        private bool CheckEverythingAvailability()
        {
            try
            {
                // 尝试调用Everything API来检查是否可用
                Everything_SetMax(1);
                Everything_SetSearchW("test");
                Everything_QueryW(true);
                return Everything_GetLastError() == EVERYTHING_OK || Everything_GetLastError() == EVERYTHING_ERROR_INVALIDINDEX;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public List<string> Search(string query, int maxResults = 1000)
        {
            var results = new List<string>();

            if (!_isEverythingAvailable)
            {
                // 如果Everything不可用，使用文件系统搜索作为备选
                return FallbackFileSearch(query, maxResults);
            }

            try
            {
                // 构建搜索查询，只搜索支持的图片格式
                string searchQuery = BuildImageSearchQuery(query);

                // 设置搜索参数
                Everything_SetSearchW(searchQuery);
                Everything_SetMatchCase(false);
                Everything_SetMatchPath(false);
                Everything_SetMatchWholeWord(false);
                Everything_SetRegex(false);
                Everything_SetMax(maxResults);
                Everything_SetOffset(0);
                Everything_SetSort(EVERYTHING_SORT_NAME_ASCENDING);

                // 执行搜索
                if (!Everything_QueryW(true))
                {
                    int error = Everything_GetLastError();
                    throw new Exception($"Everything搜索失败，错误代码: {error}");
                }

                // 获取结果
                int numResults = Everything_GetNumResults();
                StringBuilder pathBuffer = new StringBuilder(260);

                for (int i = 0; i < numResults; i++)
                {
                    if (Everything_IsFileResult(i))
                    {
                        pathBuffer.Clear();
                        Everything_GetResultFullPathNameW(i, pathBuffer, pathBuffer.Capacity);
                        string fullPath = pathBuffer.ToString();

                        if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                        {
                            string extension = Path.GetExtension(fullPath).ToLower();
                            if (_supportedImageFormats.Contains(extension))
                            {
                                results.Add(fullPath);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Everything搜索出错: {ex.Message}");
            }

            return results;
        }

        private string BuildImageSearchQuery(string userQuery)
        {
            // 为用户查询添加图片格式过滤
            var formatFilters = _supportedImageFormats.Select(ext => $"*{ext}").ToArray();
            string extensionFilter = string.Join(" | ", formatFilters);

            if (string.IsNullOrWhiteSpace(userQuery))
            {
                return $"({extensionFilter})";
            }
            else
            {
                // 组合用户查询和格式过滤
                return $"({userQuery}) ({extensionFilter})";
            }
        }

        private List<string> FallbackFileSearch(string query, int maxResults)
        {
            var results = new List<string>();

            try
            {
                // 使用简单的文件系统搜索作为备选方案
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

                foreach (var drive in drives)
                {
                    try
                    {
                        SearchDirectory(drive.RootDirectory.FullName, query, results, maxResults);
                        if (results.Count >= maxResults) break;
                    }
                    catch
                    {
                        // 忽略访问被拒绝的目录
                        continue;
                    }
                }
            }
            catch (Exception)
            {
                // 备选搜索失败，返回空结果
            }

            return results;
        }

        private void SearchDirectory(string directoryPath, string query, List<string> results, int maxResults)
        {
            if (results.Count >= maxResults) return;

            try
            {
                // 搜索当前目录中的图片文件
                foreach (string extension in _supportedImageFormats)
                {
                    try
                    {
                        var files = Directory.GetFiles(directoryPath, $"*{query}*{extension}", SearchOption.TopDirectoryOnly);
                        foreach (var file in files)
                        {
                            if (results.Count >= maxResults) return;
                            results.Add(file);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // 搜索子目录（限制深度以避免性能问题）
                var subDirectories = Directory.GetDirectories(directoryPath);
                foreach (var subDir in subDirectories.Take(10)) // 限制子目录数量
                {
                    if (results.Count >= maxResults) return;
                    SearchDirectory(subDir, query, results, maxResults);
                }
            }
            catch
            {
                // 忽略访问被拒绝的目录
            }
        }

        public bool IsEverythingAvailable => _isEverythingAvailable;

        public static bool StartEverything()
        {
            try
            {
                // 尝试启动Everything应用程序
                Process.Start(new ProcessStartInfo
                {
                    FileName = "Everything.exe",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
} 