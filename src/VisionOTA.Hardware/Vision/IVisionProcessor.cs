using System;
using System.Drawing;

namespace VisionOTA.Hardware.Vision
{
    /// <summary>
    /// 视觉处理结果
    /// </summary>
    public class VisionResult
    {
        /// <summary>
        /// 是否匹配成功（找到图案）
        /// </summary>
        public bool Found { get; set; }

        /// <summary>
        /// 匹配分数 (0-1)
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// 角度值（匹配成功时有效）
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
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 结果图像（带标注）
        /// </summary>
        public Bitmap ResultImage { get; set; }
    }

    /// <summary>
    /// 视觉处理完成事件参数
    /// </summary>
    public class VisionProcessCompletedEventArgs : EventArgs
    {
        public VisionResult Result { get; set; }
        public int StationId { get; set; }
    }

    /// <summary>
    /// 视觉处理器接口
    /// </summary>
    public interface IVisionProcessor : IDisposable
    {
        /// <summary>
        /// 工具块是否已加载
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// 处理完成事件
        /// </summary>
        event EventHandler<VisionProcessCompletedEventArgs> ProcessCompleted;

        /// <summary>
        /// 错误事件
        /// </summary>
        event EventHandler<Exception> ProcessError;

        /// <summary>
        /// 加载VisionPro工具块
        /// </summary>
        /// <param name="vppPath">.vpp文件路径</param>
        /// <returns>是否成功</returns>
        bool LoadToolBlock(string vppPath);

        /// <summary>
        /// 卸载工具块
        /// </summary>
        void UnloadToolBlock();

        /// <summary>
        /// 执行视觉处理
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <returns>处理结果</returns>
        VisionResult Execute(Bitmap image);

        /// <summary>
        /// 获取最近的处理结果
        /// </summary>
        VisionResult GetLastResult();

        /// <summary>
        /// 设置匹配分数阈值
        /// </summary>
        void SetScoreThreshold(double threshold);
    }
}
