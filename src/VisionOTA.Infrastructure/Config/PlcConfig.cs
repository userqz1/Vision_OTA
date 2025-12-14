namespace VisionOTA.Infrastructure.Config
{
    /// <summary>
    /// PLC配置
    /// </summary>
    public class PlcConfig
    {
        /// <summary>
        /// 连接配置
        /// </summary>
        public PlcConnectionConfig Connection { get; set; } = new PlcConnectionConfig();

        /// <summary>
        /// 输出地址配置
        /// </summary>
        public PlcOutputAddresses OutputAddresses { get; set; } = new PlcOutputAddresses();

        /// <summary>
        /// 输入地址配置
        /// </summary>
        public PlcInputAddresses InputAddresses { get; set; } = new PlcInputAddresses();

        /// <summary>
        /// 心跳配置
        /// </summary>
        public PlcHeartbeatConfig Heartbeat { get; set; } = new PlcHeartbeatConfig();
    }

    /// <summary>
    /// PLC连接配置
    /// </summary>
    public class PlcConnectionConfig
    {
        public string IP { get; set; } = "192.168.1.1";
        public int Port { get; set; } = 9600;
        public int Timeout { get; set; } = 3000;
        public int ReconnectInterval { get; set; } = 5000;
    }

    /// <summary>
    /// 地址项配置
    /// </summary>
    public class PlcAddressItem
    {
        public string Address { get; set; }
        public string DataType { get; set; } = "REAL";
        public string Description { get; set; }
    }

    /// <summary>
    /// 输出地址配置
    /// </summary>
    public class PlcOutputAddresses
    {
        /// <summary>
        /// 输出值地址
        /// </summary>
        public PlcAddressItem OutputValue { get; set; } = new PlcAddressItem
        {
            Address = "D4400",
            DataType = "REAL",
            Description = "输出地址"
        };

        /// <summary>
        /// 旋转角度地址
        /// </summary>
        public PlcAddressItem RotationAngle { get; set; } = new PlcAddressItem
        {
            Address = "D4402",
            DataType = "REAL",
            Description = "旋转地址"
        };

        /// <summary>
        /// 结果地址 (1=OK, 0=NG)
        /// </summary>
        public PlcAddressItem Result { get; set; } = new PlcAddressItem
        {
            Address = "D4404",
            DataType = "REAL",
            Description = "良品地址"
        };
    }

    /// <summary>
    /// 输入地址配置
    /// </summary>
    public class PlcInputAddresses
    {
        /// <summary>
        /// 工位1触发信号
        /// </summary>
        public PlcAddressItem Station1Trigger { get; set; } = new PlcAddressItem
        {
            Address = "W0.00",
            DataType = "BOOL",
            Description = "工位1触发信号"
        };

        /// <summary>
        /// 工位2触发信号
        /// </summary>
        public PlcAddressItem Station2Trigger { get; set; } = new PlcAddressItem
        {
            Address = "W0.01",
            DataType = "BOOL",
            Description = "工位2触发信号"
        };
    }

    /// <summary>
    /// 心跳配置
    /// </summary>
    public class PlcHeartbeatConfig
    {
        public string Address { get; set; } = "D4410";
        public int Interval { get; set; } = 1000;
    }
}
