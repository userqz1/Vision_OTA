using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using VisionOTA.Common.Events;
using VisionOTA.Common.Mvvm;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// 日志视图模型
    /// </summary>
    public class LogViewModel : ViewModelBase
    {
        private readonly string _logDirectory;
        private DateTime _selectedDate;
        private string _selectedLevel;
        private bool _isRealTimeEnabled;
        private string _statusMessage;
        private int _logCount;
        private string _selectedEntry;

        public ObservableCollection<string> LogEntries { get; } = new ObservableCollection<string>();

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

        #endregion

        private string[] _allLogLines;

        public LogViewModel()
        {
            Title = "系统日志";
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _selectedDate = DateTime.Today;
            _selectedLevel = "全部";

            LoadCommand = new RelayCommand(_ => LoadLogs());
            RefreshCommand = new RelayCommand(_ => LoadLogs());

            LoadLogs();
        }

        private void LoadLogs()
        {
            try
            {
                LogEntries.Clear();
                _allLogLines = null;

                var fileName = $"{SelectedDate:yyyy-MM-dd}.log";
                var filePath = Path.Combine(_logDirectory, fileName);

                if (File.Exists(filePath))
                {
                    _allLogLines = File.ReadAllLines(filePath);
                    ApplyFilter();
                    StatusMessage = $"已加载 {fileName}";
                }
                else
                {
                    StatusMessage = $"日志文件不存在: {fileName}";
                    LogCount = 0;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载失败: {ex.Message}";
            }
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
            var logLine = $"[{e.Timestamp:HH:mm:ss.fff}] [{e.Level,-7}] [{e.Source}] {e.Message}";

            // 检查是否符合当前筛选条件
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

        public override void Cleanup()
        {
            UnsubscribeFromLogs();
            base.Cleanup();
        }
    }
}
