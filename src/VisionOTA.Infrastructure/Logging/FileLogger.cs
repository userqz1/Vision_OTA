using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VisionOTA.Common.Events;

namespace VisionOTA.Infrastructure.Logging
{
    /// <summary>
    /// 文件日志记录器
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        private static readonly Lazy<FileLogger> _instance =
            new Lazy<FileLogger>(() => new FileLogger());

        private readonly string _logDirectory;
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _writeTask;
        private readonly object _fileLock = new object();
        private bool _isDisposed;

        public static FileLogger Instance => _instance.Value;

        /// <summary>
        /// 最小日志级别
        /// </summary>
        public LogLevel MinLevel { get; set; } = LogLevel.Debug;

        private FileLogger()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            EnsureDirectoryExists();

            _logQueue = new ConcurrentQueue<LogEntry>();
            _cancellationTokenSource = new CancellationTokenSource();
            _writeTask = Task.Run(() => ProcessLogQueue(_cancellationTokenSource.Token));
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
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
            Log(LogLevel.Error, $"{message}\n{exception}", source);
        }

        public void Fatal(string message, string source = null)
        {
            Log(LogLevel.Fatal, message, source);
        }

        public void Fatal(string message, Exception exception, string source = null)
        {
            Log(LogLevel.Fatal, $"{message}\n{exception}", source);
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
                Source = source ?? "System"
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
            var fileName = $"{entry.Timestamp:yyyy-MM-dd}.log";
            var filePath = Path.Combine(_logDirectory, fileName);

            var logLine = $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level,-7}] [{entry.Source}] {entry.Message}";

            lock (_fileLock)
            {
                File.AppendAllText(filePath, logLine + Environment.NewLine);
            }
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
        }
    }
}
