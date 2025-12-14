namespace VisionOTA.Infrastructure.Config
{
    /// <summary>
    /// 视觉算法配置
    /// </summary>
    public class VisionConfig
    {
        /// <summary>
        /// 工位1 VisionPro工具块路径
        /// </summary>
        public string Station1VppPath { get; set; } = "vision\\station1.vpp";

        /// <summary>
        /// 工位2 VisionPro工具块路径
        /// </summary>
        public string Station2VppPath { get; set; } = "vision\\station2.vpp";

        /// <summary>
        /// 匹配分数阈值 (0-1)
        /// </summary>
        public double ScoreThreshold { get; set; } = 0.7;

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
    }
}
