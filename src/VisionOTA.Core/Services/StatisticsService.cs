using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VisionOTA.Common.Constants;
using VisionOTA.Core.Interfaces;
using VisionOTA.Core.Models;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Core.Services
{
    /// <summary>
    /// 统计服务实现
    /// </summary>
    public class StatisticsService : IStatisticsService, IDisposable
    {
        private readonly Dictionary<int, StationStatistics> _statistics;
        private readonly string _dataFilePath;
        private readonly Timer _autoSaveTimer;
        private readonly object _lockObject = new object();
        private bool _isDisposed;

        public StatisticsService()
        {
            _statistics = new Dictionary<int, StationStatistics>();

            // 初始化各工位统计
            for (int i = 1; i <= SystemConstants.StationCount; i++)
            {
                _statistics[i] = new StationStatistics
                {
                    StationId = i,
                    StartTime = DateTime.Now,
                    LastUpdateTime = DateTime.Now
                };
            }

            // 数据文件路径
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);
            _dataFilePath = Path.Combine(dataDir, "statistics.json");

            // 加载已有数据
            LoadStatistics();

            // 启动自动保存定时器
            var interval = ConfigManager.Instance.SystemCfg.StatisticsSaveInterval * 1000;
            _autoSaveTimer = new Timer(_ => SaveStatistics(), null, interval, interval);
        }

        public StationStatistics GetStationStatistics(int stationId)
        {
            lock (_lockObject)
            {
                return _statistics.ContainsKey(stationId) ? _statistics[stationId] : null;
            }
        }

        public void AddResult(int stationId, bool isOk)
        {
            lock (_lockObject)
            {
                if (_statistics.ContainsKey(stationId))
                {
                    _statistics[stationId].AddResult(isOk);
                    FileLogger.Instance.Debug(
                        $"工位{stationId}统计更新: Total={_statistics[stationId].TotalCount}, OK={_statistics[stationId].OkCount}, NG={_statistics[stationId].NgCount}",
                        "Statistics");
                }
            }
        }

        public void ResetStation(int stationId)
        {
            lock (_lockObject)
            {
                if (_statistics.ContainsKey(stationId))
                {
                    _statistics[stationId].Reset();
                    FileLogger.Instance.Info($"工位{stationId}统计已清零", "Statistics");
                }
            }
        }

        public void ResetAll()
        {
            lock (_lockObject)
            {
                foreach (var stat in _statistics.Values)
                {
                    stat.Reset();
                }
                FileLogger.Instance.Info("所有工位统计已清零", "Statistics");
            }
        }

        public void SaveStatistics()
        {
            try
            {
                lock (_lockObject)
                {
                    var data = new Dictionary<int, StatisticsData>();
                    foreach (var kvp in _statistics)
                    {
                        data[kvp.Key] = new StatisticsData
                        {
                            TotalCount = kvp.Value.TotalCount,
                            OkCount = kvp.Value.OkCount,
                            NgCount = kvp.Value.NgCount,
                            StartTime = kvp.Value.StartTime,
                            LastUpdateTime = kvp.Value.LastUpdateTime
                        };
                    }

                    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(_dataFilePath, json);
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"保存统计数据失败: {ex.Message}", ex, "Statistics");
            }
        }

        public void LoadStatistics()
        {
            try
            {
                if (!File.Exists(_dataFilePath))
                    return;

                var json = File.ReadAllText(_dataFilePath);
                var data = JsonConvert.DeserializeObject<Dictionary<int, StatisticsData>>(json);

                if (data == null)
                    return;

                lock (_lockObject)
                {
                    foreach (var kvp in data)
                    {
                        if (_statistics.ContainsKey(kvp.Key))
                        {
                            _statistics[kvp.Key].TotalCount = kvp.Value.TotalCount;
                            _statistics[kvp.Key].OkCount = kvp.Value.OkCount;
                            _statistics[kvp.Key].NgCount = kvp.Value.NgCount;
                            _statistics[kvp.Key].StartTime = kvp.Value.StartTime;
                            _statistics[kvp.Key].LastUpdateTime = kvp.Value.LastUpdateTime;
                        }
                    }
                }

                FileLogger.Instance.Info("统计数据已加载", "Statistics");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"加载统计数据失败: {ex.Message}", ex, "Statistics");
            }
        }

        public void ExportReport(string filePath, DateTime startTime, DateTime endTime)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("VisionOTA 统计报表");
                sb.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"统计区间: {startTime:yyyy-MM-dd HH:mm:ss} - {endTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine("工位,总数,OK数,NG数,OK率,产能(件/时)");

                lock (_lockObject)
                {
                    foreach (var stat in _statistics.Values)
                    {
                        sb.AppendLine($"工位{stat.StationId},{stat.TotalCount},{stat.OkCount},{stat.NgCount},{stat.OkRate:F2}%,{stat.ProductionRate:F1}");
                    }
                }

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                FileLogger.Instance.Info($"统计报表已导出: {filePath}", "Statistics");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"导出统计报表失败: {ex.Message}", ex, "Statistics");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _autoSaveTimer?.Dispose();
            SaveStatistics();
            _isDisposed = true;
        }

        private class StatisticsData
        {
            public int TotalCount { get; set; }
            public int OkCount { get; set; }
            public int NgCount { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime LastUpdateTime { get; set; }
        }
    }
}
