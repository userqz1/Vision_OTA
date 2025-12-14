using System;
using System.Drawing;
using VisionOTA.Common.Constants;

namespace VisionOTA.Core.Models
{
    /// <summary>
    /// 检测结果模型
    /// </summary>
    public class InspectionResult
    {
        /// <summary>
        /// 工位ID
        /// </summary>
        public int StationId { get; set; }

        /// <summary>
        /// 检测时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 结果类型
        /// </summary>
        public InspectionResultType ResultType { get; set; }

        /// <summary>
        /// 是否OK
        /// </summary>
        public bool IsOk => ResultType == InspectionResultType.Ok;

        /// <summary>
        /// 匹配分数
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// 角度值（OK时有效）
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// X坐标
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y坐标
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// 处理耗时(毫秒)
        /// </summary>
        public double ProcessTimeMs { get; set; }

        /// <summary>
        /// 原始图像路径
        /// </summary>
        public string ImagePath { get; set; }

        /// <summary>
        /// 结果图像（带标注）
        /// </summary>
        public Bitmap ResultImage { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
