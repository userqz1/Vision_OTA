namespace VisionOTA.Infrastructure.Config
{
    /// <summary>
    /// 视觉算法配置
    /// </summary>
    public class VisionConfig
    {
        /// <summary>
        /// VisionMaster配置
        /// </summary>
        public VisionMasterConfig VisionMaster { get; set; } = new VisionMasterConfig();

        /// <summary>
        /// 工位1配置（瓶底工位）
        /// </summary>
        public StationVisionConfig Station1 { get; set; } = new StationVisionConfig
        {
            ProcedureName = "流程10000",
            AngleOutputName = "瓶底角度",
            ResultImageOutputName = "瓶底结果图"
        };

        /// <summary>
        /// 工位2配置（瓶身工位）
        /// </summary>
        public StationVisionConfig Station2 { get; set; } = new StationVisionConfig
        {
            ProcedureName = "流程10001",
            AngleOutputName = "瓶身角度",
            ResultImageOutputName = "瓶身结果图"
        };

        /// <summary>
        /// 算法执行超时时间(毫秒)
        /// </summary>
        public int Timeout { get; set; } = 5000;

        /// <summary>
        /// 是否显示算法中间结果
        /// </summary>
        public bool ShowIntermediateResults { get; set; } = false;

        /// <summary>
        /// 是否启用图形标注
        /// </summary>
        public bool EnableGraphicOverlay { get; set; } = true;

        // 兼容旧配置
        public string Station1VppPath { get; set; }
        public string Station2VppPath { get; set; }
        public double ScoreThreshold { get; set; } = 0.7;
    }

    /// <summary>
    /// VisionMaster方案配置
    /// </summary>
    public class VisionMasterConfig
    {
        /// <summary>
        /// 方案文件路径 (.sol)
        /// </summary>
        public string SolutionPath { get; set; } = "";

        /// <summary>
        /// 方案密码（可选）
        /// </summary>
        public string Password { get; set; } = "";
    }

    /// <summary>
    /// 工位视觉配置
    /// </summary>
    public class StationVisionConfig
    {
        /// <summary>
        /// 流程名称（VisionMaster中的流程名）
        /// </summary>
        public string ProcedureName { get; set; }

        /// <summary>
        /// 角度输出变量名（流程输出设置中配置的变量名）
        /// </summary>
        public string AngleOutputName { get; set; }

        /// <summary>
        /// 结果图输出变量名（流程输出设置中配置的变量名）
        /// </summary>
        public string ResultImageOutputName { get; set; }

        /// <summary>
        /// 匹配分数阈值 (0-1)
        /// </summary>
        public double ScoreThreshold { get; set; } = 0.7;
    }
}
