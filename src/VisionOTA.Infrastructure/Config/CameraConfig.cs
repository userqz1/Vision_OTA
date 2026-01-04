namespace VisionOTA.Infrastructure.Config
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class CameraConfig
    {
        /// <summary>
        /// 工位1配置（面阵相机）
        /// </summary>
        public StationCameraConfig Station1 { get; set; } = new StationCameraConfig
        {
            Type = "AreaScan",
            UserId = "",
            IP = "192.168.1.10",
            Exposure = 5000,
            Gain = 1.0,
            TriggerSource = "Continuous",
            TriggerEdge = "RisingEdge",
            Timeout = 5000
        };

        /// <summary>
        /// 工位2配置（线扫相机）
        /// </summary>
        public LineCameraConfig Station2 { get; set; } = new LineCameraConfig
        {
            Type = "LineScan",
            UserId = "",
            IP = "192.168.1.11",
            Exposure = 2000,
            Gain = 1.0,
            TriggerSource = "Continuous",
            TriggerEdge = "RisingEdge",
            Timeout = 5000,
            LineRate = 10000
        };
    }

    /// <summary>
    /// 工位相机配置
    /// </summary>
    public class StationCameraConfig
    {
        /// <summary>
        /// 相机类型 (AreaScan/LineScan)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 相机用户自定义名称（用于连接识别）
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 相机IP地址
        /// </summary>
        public string IP { get; set; }

        /// <summary>
        /// 曝光时间(微秒)
        /// </summary>
        public int Exposure { get; set; }

        /// <summary>
        /// 增益
        /// </summary>
        public double Gain { get; set; }

        /// <summary>
        /// 触发源 (Continuous/Software/Line1-Line8)
        /// </summary>
        public string TriggerSource { get; set; } = "Continuous";

        /// <summary>
        /// 触发边沿 (RisingEdge/FallingEdge/DoubleEdge)
        /// 仅硬件触发(Line1-8)时有效
        /// </summary>
        public string TriggerEdge { get; set; } = "RisingEdge";

        /// <summary>
        /// 采集超时时间(毫秒)
        /// </summary>
        public int Timeout { get; set; }
    }

    /// <summary>
    /// 线扫相机配置
    /// </summary>
    public class LineCameraConfig : StationCameraConfig
    {
        /// <summary>
        /// 行频
        /// </summary>
        public int LineRate { get; set; }

        /// <summary>
        /// 行触发使能（使用外部编码器/触发信号控制行频）
        /// true: 跟随外部信号采集
        /// false: 使用内部行频采集
        /// </summary>
        public bool LineTrigEnable { get; set; } = false;
    }
}
