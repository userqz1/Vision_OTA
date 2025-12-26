using System;
using System.Drawing;
using System.Threading.Tasks;
using VisionOTA.Common.Constants;
using VisionOTA.Core.Models;

namespace VisionOTA.Core.Interfaces
{
    /// <summary>
    /// 检测完成事件参数
    /// </summary>
    public class InspectionCompletedEventArgs : EventArgs
    {
        public InspectionResult Result { get; set; }
    }

    /// <summary>
    /// 检测服务接口
    /// </summary>
    public interface IInspectionService : IDisposable
    {
        /// <summary>
        /// 当前系统状态
        /// </summary>
        SystemState CurrentState { get; }

        /// <summary>
        /// 检测完成事件
        /// </summary>
        event EventHandler<InspectionCompletedEventArgs> InspectionCompleted;

        /// <summary>
        /// 状态变更事件
        /// </summary>
        event EventHandler<SystemState> StateChanged;

        /// <summary>
        /// 错误事件
        /// </summary>
        event EventHandler<string> ErrorOccurred;

        /// <summary>
        /// 初始化服务
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// 启动检测
        /// </summary>
        Task<bool> StartAsync();

        /// <summary>
        /// 停止检测
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 暂停检测
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复检测
        /// </summary>
        void Resume();

        /// <summary>
        /// 执行单次检测（离线模式）
        /// </summary>
        /// <param name="stationId">工位ID</param>
        /// <param name="image">测试图像（为空则从相机采集）</param>
        Task<InspectionResult> ExecuteSingleAsync(int stationId, Bitmap image = null);

        /// <summary>
        /// 软件触发采集（对焦模式）
        /// </summary>
        /// <param name="stationId">工位ID</param>
        Task<Bitmap> TriggerCaptureAsync(int stationId);

        /// <summary>
        /// 发送测试触发信号到PLC
        /// </summary>
        /// <param name="stationId">工位ID (1=面阵, 2=线扫)</param>
        /// <returns>是否成功</returns>
        Task<bool> SendTestTriggerAsync(int stationId);
    }
}
