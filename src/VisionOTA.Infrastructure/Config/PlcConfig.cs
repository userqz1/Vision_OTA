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

        /// <summary>
        /// 测试触发配置
        /// </summary>
        public PlcTestTriggerConfig TestTrigger { get; set; } = new PlcTestTriggerConfig();
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
        /// 瓶身旋转地址 (1=旋转, 0=不旋转)
        /// </summary>
        public PlcAddressItem OutputValue { get; set; } = new PlcAddressItem
        {
            Address = "D4400",
            DataType = "REAL",
            Description = "瓶身旋转地址 (1=旋转, 0=不旋转)"
        };

        /// <summary>
        /// 定位角度地址
        /// </summary>
        public PlcAddressItem RotationAngle { get; set; } = new PlcAddressItem
        {
            Address = "D4402",
            DataType = "REAL",
            Description = "定位角度地址"
        };

        /// <summary>
        /// 产品合格地址 (2=合格, 3=不合格)
        /// </summary>
        public PlcAddressItem Result { get; set; } = new PlcAddressItem
        {
            Address = "D4404",
            DataType = "REAL",
            Description = "产品合格地址 (2=合格, 3=不合格)"
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

        /// <summary>
        /// 瓶身旋转角度（启动时读取写入VisionMaster）
        /// </summary>
        public PlcAddressItem RotationAngle { get; set; } = new PlcAddressItem
        {
            Address = "D4204",
            DataType = "REAL",
            Description = "瓶身旋转角度（启动时读取写入VisionMaster）"
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

    /// <summary>
    /// 测试触发配置
    /// </summary>
    public class PlcTestTriggerConfig
    {
        /// <summary>
        /// 工位1(面阵)测试触发地址
        /// </summary>
        public PlcAddressItem Station1Trigger { get; set; } = new PlcAddressItem
        {
            Address = "CIO211.12",
            DataType = "BOOL",
            Description = "工位1(面阵)测试触发"
        };

        /// <summary>
        /// 工位2(线扫)测试触发地址
        /// </summary>
        public PlcAddressItem Station2Trigger { get; set; } = new PlcAddressItem
        {
            Address = "CIO211.11",
            DataType = "BOOL",
            Description = "工位2(线扫)测试触发"
        };
    }
}
