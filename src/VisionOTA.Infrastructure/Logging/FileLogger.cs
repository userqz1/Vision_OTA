using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VisionOTA.Common.Events;

namespace VisionOTA.Infrastructure.Logging
{
    /// <summary>
    /// 文件日志记录器 - 支持分模块日志
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        private static readonly Lazy<FileLogger> _instance =
            new Lazy<FileLogger>(() => new FileLogger());

        private readonly string _logBaseDirectory;
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _writeTask;
        private readonly object _fileLock = new object();
        private readonly Dictionary<string, string> _categoryMapping;
        private bool _isDisposed;
        private string _currentDateDirectory;
        private DateTime _currentDate;

        public static FileLogger Instance => _instance.Value;

        /// <summary>
        /// 最小日志级别
        /// </summary>
        public LogLevel MinLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// 是否启用分模块日志（默认启用）
        /// </summary>
        public bool EnableModuleLogs { get; set; } = true;

        private FileLogger()
        {
            _logBaseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _currentDate = DateTime.Today;
            _currentDateDirectory = GetDateDirectory(_currentDate);

            // 模块分类映射（使用大小写不敏感比较，避免重复键）
            _categoryMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // 相机模块
                { "Camera", "Camera" },
                { "Camera1", "Camera" },
                { "Camera2", "Camera" },
                { "AreaCamera", "Camera" },
                { "LineCamera", "Camera" },
                { "CameraSettingsViewModel", "Camera" },

                // PLC模块
                { "PLC", "PLC" },
                { "OmronFins", "PLC" },
                { "PlcSettingsViewModel", "PLC" },

                // 视觉算法模块
                { "Vision", "Vision" },
                { "VisionMaster", "Vision" },
                { "VisionProcessor", "Vision" },
                { "PatternMatch", "Vision" },
                { "VisionMasterSettingsWindow", "Vision" },

                // 检测流程模块
                { "Inspection", "Inspection" },
                { "InspectionService", "Inspection" },
                { "Station1", "Inspection" },
                { "Station2", "Inspection" },
                { "StationViewModel", "Inspection" },

                // 权限/认证模块
                { "Auth", "Auth" },
                { "Permission", "Auth" },
                { "Login", "Auth" },
                { "User", "Auth" },
                { "LoginViewModel", "Auth" },
                { "UserManagementViewModel", "Auth" },

                // 数据/存储模块
                { "Data", "Data" },
                { "Storage", "Data" },
                { "Statistics", "Data" },
                { "ImageStorage", "Data" },

                // 系统模块（默认）
                { "System", "System" },
                { "App", "System" },
                { "MainViewModel", "System" },
                { "MainWindow", "System" },
                { "Config", "System" },
                { "ConfigManager", "System" }
            };

            EnsureDirectoryExists(_logBaseDirectory);
            EnsureDirectoryExists(_currentDateDirectory);

            _logQueue = new ConcurrentQueue<LogEntry>();
            _cancellationTokenSource = new CancellationTokenSource();
            _writeTask = Task.Run(() => ProcessLogQueue(_cancellationTokenSource.Token));
        }

        private string GetDateDirectory(DateTime date)
        {
            return Path.Combine(_logBaseDirectory, date.ToString("yyyy-MM-dd"));
        }

        private void EnsureDirectoryExists(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private string GetCategory(string source)
        {
            if (string.IsNullOrEmpty(source))
                return "System";

            // 先尝试直接匹配
            if (_categoryMapping.TryGetValue(source, out var category))
                return category;

            // 尝试部分匹配
            foreach (var kvp in _categoryMapping)
            {
                if (source.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kvp.Value;
            }

            return "System";
        }

        public void Debug(string message, string source = null)
        {
            Log(LogLevel.Debug, message, source);
        }

        public void Info(string message, string source = null)
        {
            Log(LogLevel.Info, message, source);
        }

        public void Warning(string message, string source = null)
        {
            Log(LogLevel.Warning, message, source);
        }

        public void Error(string message, string source = null)
        {
            Log(LogLevel.Error, message, source);
        }

        public void Error(string message, Exception exception, string source = null)
        {
            var fullMessage = exception != null
                ? $"{message}\n  异常类型: {exception.GetType().Name}\n  异常信息: {exception.Message}\n  堆栈跟踪: {exception.StackTrace}"
                : message;
            Log(LogLevel.Error, fullMessage, source);
        }

        public void Fatal(string message, string source = null)
        {
            Log(LogLevel.Fatal, message, source);
        }

        public void Fatal(string message, Exception exception, string source = null)
        {
            var fullMessage = exception != null
                ? $"{message}\n  异常类型: {exception.GetType().Name}\n  异常信息: {exception.Message}\n  堆栈跟踪: {exception.StackTrace}"
                : message;
            Log(LogLevel.Fatal, fullMessage, source);
        }

        private void Log(LogLevel level, string message, string source)
        {
            if (level < MinLevel)
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Source = source ?? "System",
                Category = GetCategory(source)
            };

            _logQueue.Enqueue(entry);

            // 发布日志事件
            EventAggregator.Instance.Publish(new LogMessageEvent
            {
                Timestamp = entry.Timestamp,
                Level = level.ToString(),
                Message = message,
                Source = entry.Source
            });
        }

        private async Task ProcessLogQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_logQueue.TryDequeue(out var entry))
                    {
                        WriteToFile(entry);
                    }
                    else
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"日志写入错误: {ex.Message}");
                }
            }

            // 写入剩余日志
            while (_logQueue.TryDequeue(out var entry))
            {
                WriteToFile(entry);
            }
        }

        private void WriteToFile(LogEntry entry)
        {
            // 检查日期是否变更，需要创建新目录
            if (entry.Timestamp.Date != _currentDate)
            {
                _currentDate = entry.Timestamp.Date;
                _currentDateDirectory = GetDateDirectory(_currentDate);
                EnsureDirectoryExists(_currentDateDirectory);
            }

            var logLine = $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level,-7}] [{entry.Source,-20}] {entry.Message}";

            lock (_fileLock)
            {
                // 写入汇总日志 All.log
                var allLogPath = Path.Combine(_currentDateDirectory, "All.log");
                File.AppendAllText(allLogPath, logLine + Environment.NewLine);

                // 写入模块日志
                if (EnableModuleLogs)
                {
                    var moduleLogPath = Path.Combine(_currentDateDirectory, $"{entry.Category}.log");
                    File.AppendAllText(moduleLogPath, logLine + Environment.NewLine);
                }
            }
        }

        /// <summary>
        /// 添加自定义模块映射
        /// </summary>
        /// <param name="source">源名称</param>
        /// <param name="category">分类名称</param>
        public void AddCategoryMapping(string source, string category)
        {
            _categoryMapping[source] = category;
        }

        /// <summary>
        /// 获取日志目录路径
        /// </summary>
        public string GetLogDirectory()
        {
            return _currentDateDirectory;
        }

        /// <summary>
        /// 获取指定日期的日志目录
        /// </summary>
        public string GetLogDirectory(DateTime date)
        {
            return GetDateDirectory(date);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _cancellationTokenSource.Cancel();
            _writeTask.Wait(TimeSpan.FromSeconds(5));
            _cancellationTokenSource.Dispose();
            _isDisposed = true;
        }

        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; }
            public string Source { get; set; }
            public string Category { get; set; }
        }
    }
}
