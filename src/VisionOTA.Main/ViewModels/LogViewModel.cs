using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using VisionOTA.Common.Events;
using VisionOTA.Common.Mvvm;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// 日志视图模型 - 支持分模块查看
    /// </summary>
    public class LogViewModel : ViewModelBase
    {
        private readonly string _logBaseDirectory;
        private DateTime _selectedDate;
        private string _selectedLevel;
        private string _selectedModule;
        private bool _isRealTimeEnabled;
        private string _statusMessage;
        private int _logCount;
        private string _selectedEntry;
        private string[] _allLogLines;

        public ObservableCollection<string> LogEntries { get; } = new ObservableCollection<string>();

        /// <summary>
        /// 可用模块列表
        /// </summary>
        public List<string> AvailableModules { get; } = new List<string>
        {
            "All",        // 汇总日志
            "Camera",     // 相机模块
            "PLC",        // PLC通讯
            "Vision",     // 视觉算法
            "Inspection", // 检测流程
            "Auth",       // 权限/认证
            "Data",       // 数据/存储
            "System"      // 系统日志
        };

        /// <summary>
        /// 日志级别列表
        /// </summary>
        public List<string> LogLevels { get; } = new List<string>
        {
            "全部", "Debug", "Info", "Warning", "Error", "Fatal"
        };

        #region Properties

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set => SetProperty(ref _selectedDate, value);
        }

        public string SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                if (SetProperty(ref _selectedLevel, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string SelectedModule
        {
            get => _selectedModule;
            set
            {
                if (SetProperty(ref _selectedModule, value))
                {
                    LoadLogs();
                }
            }
        }

        public bool IsRealTimeEnabled
        {
            get => _isRealTimeEnabled;
            set
            {
                if (SetProperty(ref _isRealTimeEnabled, value))
                {
                    if (value)
                    {
                        SubscribeToLogs();
                    }
                    else
                    {
                        UnsubscribeFromLogs();
                    }
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int LogCount
        {
            get => _logCount;
            set => SetProperty(ref _logCount, value);
        }

        public string SelectedEntry
        {
            get => _selectedEntry;
            set => SetProperty(ref _selectedEntry, value);
        }

        #endregion

        #region Commands

        public ICommand LoadCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenFolderCommand { get; }

        #endregion

        public LogViewModel()
        {
            Title = "系统日志";
            _logBaseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _selectedDate = DateTime.Today;
            _selectedLevel = "全部";
            _selectedModule = "All";

            LoadCommand = new RelayCommand(_ => LoadLogs());
            RefreshCommand = new RelayCommand(_ => LoadLogs());
            OpenFolderCommand = new RelayCommand(_ => OpenLogFolder());

            LoadLogs();
        }

        private void LoadLogs()
        {
            try
            {
                LogEntries.Clear();
                _allLogLines = null;

                // 构建日志文件路径：logs/2024-12-19/Module.log
                var dateDir = Path.Combine(_logBaseDirectory, SelectedDate.ToString("yyyy-MM-dd"));
                var fileName = $"{SelectedModule}.log";
                var filePath = Path.Combine(dateDir, fileName);

                // 兼容旧格式：logs/2024-12-19.log
                if (!File.Exists(filePath))
                {
                    var oldFilePath = Path.Combine(_logBaseDirectory, $"{SelectedDate:yyyy-MM-dd}.log");
                    if (File.Exists(oldFilePath) && SelectedModule == "All")
                    {
                        filePath = oldFilePath;
                    }
                }

                if (File.Exists(filePath))
                {
                    _allLogLines = File.ReadAllLines(filePath);
                    ApplyFilter();
                    StatusMessage = $"已加载 {Path.GetFileName(filePath)}";
                }
                else
                {
                    StatusMessage = $"日志文件不存在: {fileName}";
                    LogCount = 0;
                }

                // 更新可用模块列表（检查实际存在的文件）
                UpdateAvailableModulesForDate(dateDir);
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载失败: {ex.Message}";
            }
        }

        private void UpdateAvailableModulesForDate(string dateDir)
        {
            // 可以在此检查该日期目录下实际存在哪些日志文件
            // 目前保持静态列表，后续可扩展
        }

        private void ApplyFilter()
        {
            if (_allLogLines == null)
                return;

            LogEntries.Clear();
            var filtered = _allLogLines.AsEnumerable();

            if (!string.IsNullOrEmpty(SelectedLevel) && SelectedLevel != "全部")
            {
                filtered = filtered.Where(line => line.Contains($"[{SelectedLevel}"));
            }

            foreach (var line in filtered)
            {
                LogEntries.Add(line);
            }

            LogCount = LogEntries.Count;
        }

        private void SubscribeToLogs()
        {
            EventAggregator.Instance.Subscribe<LogMessageEvent>(OnLogMessage);
            StatusMessage = "实时更新已启用";
        }

        private void UnsubscribeFromLogs()
        {
            EventAggregator.Instance.Unsubscribe<LogMessageEvent>(OnLogMessage);
            StatusMessage = "实时更新已停止";
        }

        private void OnLogMessage(LogMessageEvent e)
        {
            // 检查是否是当天的日志
            if (e.Timestamp.Date != SelectedDate.Date)
                return;

            // 检查模块筛选
            if (SelectedModule != "All")
            {
                var category = GetCategoryFromSource(e.Source);
                if (category != SelectedModule)
                    return;
            }

            var logLine = $"[{e.Timestamp:HH:mm:ss.fff}] [{e.Level,-7}] [{e.Source,-20}] {e.Message}";

            // 检查级别筛选
            if (!string.IsNullOrEmpty(SelectedLevel) && SelectedLevel != "全部")
            {
                if (!logLine.Contains($"[{SelectedLevel}"))
                    return;
            }

            BeginRunOnUIThread(() =>
            {
                LogEntries.Add(logLine);
                LogCount = LogEntries.Count;
            });
        }

        private string GetCategoryFromSource(string source)
        {
            if (string.IsNullOrEmpty(source))
                return "System";

            // 简化的分类匹配
            var lowerSource = source.ToLower();
            if (lowerSource.Contains("camera")) return "Camera";
            if (lowerSource.Contains("plc") || lowerSource.Contains("fins")) return "PLC";
            if (lowerSource.Contains("vision") || lowerSource.Contains("pattern")) return "Vision";
            if (lowerSource.Contains("inspection") || lowerSource.Contains("station")) return "Inspection";
            if (lowerSource.Contains("auth") || lowerSource.Contains("login") || lowerSource.Contains("user") || lowerSource.Contains("permission")) return "Auth";
            if (lowerSource.Contains("data") || lowerSource.Contains("storage") || lowerSource.Contains("statistics")) return "Data";

            return "System";
        }

        private void OpenLogFolder()
        {
            try
            {
                var dateDir = Path.Combine(_logBaseDirectory, SelectedDate.ToString("yyyy-MM-dd"));
                if (Directory.Exists(dateDir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", dateDir);
                }
                else if (Directory.Exists(_logBaseDirectory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", _logBaseDirectory);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"打开文件夹失败: {ex.Message}";
            }
        }

        public override void Cleanup()
        {
            UnsubscribeFromLogs();
            base.Cleanup();
        }
    }
}
