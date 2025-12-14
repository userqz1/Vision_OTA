using System;
using VisionOTA.Common.Mvvm;

namespace VisionOTA.Core.Models
{
    /// <summary>
    /// 工位统计数据模型
    /// </summary>
    public class StationStatistics : ObservableObject
    {
        private int _stationId;
        private int _totalCount;
        private int _okCount;
        private int _ngCount;
        private DateTime _startTime;
        private DateTime _lastUpdateTime;

        /// <summary>
        /// 工位ID
        /// </summary>
        public int StationId
        {
            get => _stationId;
            set => SetProperty(ref _stationId, value);
        }

        /// <summary>
        /// 总检测数
        /// </summary>
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                SetProperty(ref _totalCount, value);
                OnPropertyChanged(nameof(OkRate));
            }
        }

        /// <summary>
        /// OK数量
        /// </summary>
        public int OkCount
        {
            get => _okCount;
            set
            {
                SetProperty(ref _okCount, value);
                OnPropertyChanged(nameof(OkRate));
            }
        }

        /// <summary>
        /// NG数量
        /// </summary>
        public int NgCount
        {
            get => _ngCount;
            set => SetProperty(ref _ngCount, value);
        }

        /// <summary>
        /// OK率 (百分比)
        /// </summary>
        public double OkRate => TotalCount > 0 ? (double)OkCount / TotalCount * 100 : 0;

        /// <summary>
        /// 统计开始时间
        /// </summary>
        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                SetProperty(ref _startTime, value);
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(ProductionRate));
            }
        }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set
            {
                SetProperty(ref _lastUpdateTime, value);
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(ProductionRate));
            }
        }

        /// <summary>
        /// 运行时长
        /// </summary>
        public TimeSpan Duration => LastUpdateTime > StartTime ? LastUpdateTime - StartTime : TimeSpan.Zero;

        /// <summary>
        /// 产能 (件/小时)
        /// </summary>
        public double ProductionRate
        {
            get
            {
                var hours = Duration.TotalHours;
                return hours > 0 ? TotalCount / hours : 0;
            }
        }

        /// <summary>
        /// 清零统计数据
        /// </summary>
        public void Reset()
        {
            TotalCount = 0;
            OkCount = 0;
            NgCount = 0;
            StartTime = DateTime.Now;
            LastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// 添加一次检测结果
        /// </summary>
        /// <param name="isOk">是否OK</param>
        public void AddResult(bool isOk)
        {
            TotalCount++;
            if (isOk)
                OkCount++;
            else
                NgCount++;
            LastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// 创建副本
        /// </summary>
        public StationStatistics Clone()
        {
            return new StationStatistics
            {
                StationId = StationId,
                TotalCount = TotalCount,
                OkCount = OkCount,
                NgCount = NgCount,
                StartTime = StartTime,
                LastUpdateTime = LastUpdateTime
            };
        }
    }
}
