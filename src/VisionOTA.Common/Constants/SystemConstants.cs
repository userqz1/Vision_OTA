namespace VisionOTA.Common.Constants
{
    /// <summary>
    /// 系统常量定义
    /// </summary>
    public static class SystemConstants
    {
        /// <summary>
        /// 工位数量
        /// </summary>
        public const int StationCount = 2;

        /// <summary>
        /// 工位1 - 面阵相机
        /// </summary>
        public const int Station1 = 1;

        /// <summary>
        /// 工位2 - 线扫相机
        /// </summary>
        public const int Station2 = 2;

        /// <summary>
        /// 配置文件目录
        /// </summary>
        public const string ConfigDirectory = "configs";

        /// <summary>
        /// 日志文件目录
        /// </summary>
        public const string LogDirectory = "logs";

        /// <summary>
        /// 图片存储根目录
        /// </summary>
        public const string ImageDirectory = "images";

        /// <summary>
        /// NG图片目录
        /// </summary>
        public const string NgImageDirectory = "ng_images";

        /// <summary>
        /// 默认最近图片保留数量
        /// </summary>
        public const int DefaultRecentImageCount = 100;

        /// <summary>
        /// 默认NG图片保留天数
        /// </summary>
        public const int DefaultNgImageRetentionDays = 30;
    }

    /// <summary>
    /// 系统状态枚举
    /// </summary>
    public enum SystemState
    {
        /// <summary>
        /// 空闲待机
        /// </summary>
        Idle,

        /// <summary>
        /// 运行检测中
        /// </summary>
        Running,

        /// <summary>
        /// 暂停
        /// </summary>
        Paused,

        /// <summary>
        /// 错误停止
        /// </summary>
        Error,

        /// <summary>
        /// 离线调试模式
        /// </summary>
        Offline
    }

    /// <summary>
    /// 检测结果枚举
    /// </summary>
    public enum InspectionResultType
    {
        /// <summary>
        /// 检测通过
        /// </summary>
        Ok,

        /// <summary>
        /// 检测不通过
        /// </summary>
        Ng,

        /// <summary>
        /// 检测超时
        /// </summary>
        Timeout,

        /// <summary>
        /// 检测错误
        /// </summary>
        Error
    }

    /// <summary>
    /// 触发模式枚举
    /// </summary>
    public enum TriggerMode
    {
        /// <summary>
        /// 硬件触发
        /// </summary>
        Hardware,

        /// <summary>
        /// 软件触发
        /// </summary>
        Software
    }
}
