namespace VisionOTA.Infrastructure.Config
{
    /// <summary>
    /// 系统配置
    /// </summary>
    public class SystemConfig
    {
        /// <summary>
        /// 系统名称
        /// </summary>
        public string SystemName { get; set; } = "VisionOTA 视觉检测系统";

        /// <summary>
        /// 图片存储根目录
        /// </summary>
        public string ImageRootPath { get; set; } = "images";

        /// <summary>
        /// NG图片存储目录
        /// </summary>
        public string NgImagePath { get; set; } = "ng_images";

        /// <summary>
        /// 日志存储目录
        /// </summary>
        public string LogPath { get; set; } = "logs";

        /// <summary>
        /// 最近图片保留数量
        /// </summary>
        public int RecentImageCount { get; set; } = 100;

        /// <summary>
        /// NG图片保留天数
        /// </summary>
        public int NgImageRetentionDays { get; set; } = 30;

        /// <summary>
        /// 是否保存OK图片
        /// </summary>
        public bool SaveOkImages { get; set; } = false;

        /// <summary>
        /// 自动登出时间(分钟，0表示不自动登出)
        /// </summary>
        public int AutoLogoutMinutes { get; set; } = 30;

        /// <summary>
        /// 登录失败最大次数
        /// </summary>
        public int MaxLoginAttempts { get; set; } = 5;

        /// <summary>
        /// 连续失败报警阈值
        /// </summary>
        public int ConsecutiveFailureAlarmThreshold { get; set; } = 10;

        /// <summary>
        /// 磁盘空间报警阈值(GB)
        /// </summary>
        public int DiskSpaceAlarmThresholdGB { get; set; } = 10;

        /// <summary>
        /// 统计数据自动保存间隔(秒)
        /// </summary>
        public int StatisticsSaveInterval { get; set; } = 60;
    }
}
