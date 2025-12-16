using System;
using System.Drawing;

namespace VisionOTA.Hardware.Camera
{
    /// <summary>
    /// 触发源
    /// </summary>
    public enum TriggerSource
    {
        /// <summary>
        /// 连续采集（自由运行）
        /// </summary>
        Continuous = 0,

        /// <summary>
        /// 软件触发
        /// </summary>
        Software = 1,

        /// <summary>
        /// LINE0 硬件触发
        /// </summary>
        Line0 = 2,

        /// <summary>
        /// LINE1 硬件触发
        /// </summary>
        Line1 = 3,

        /// <summary>
        /// LINE2 硬件触发
        /// </summary>
        Line2 = 4,

        /// <summary>
        /// LINE3 硬件触发（编码器）
        /// </summary>
        Line3 = 5
    }

    /// <summary>
    /// 触发信号类型（硬件触发时使用）
    /// </summary>
    public enum TriggerEdge
    {
        /// <summary>
        /// 上升沿触发
        /// </summary>
        RisingEdge = 0,

        /// <summary>
        /// 下降沿触发
        /// </summary>
        FallingEdge = 1,

        /// <summary>
        /// 双边沿触发
        /// </summary>
        DoubleEdge = 2
    }

    /// <summary>
    /// 图像接收事件参数
    /// </summary>
    public class ImageReceivedEventArgs : EventArgs
    {
        public Bitmap Image { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 连接状态变更事件参数
    /// </summary>
    public class ConnectionChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// 相机信息（用于枚举搜索结果）
    /// </summary>
    public class CameraInfo
    {
        /// <summary>
        /// 相机友好名称
        /// </summary>
        public string FriendlyName { get; set; }

        /// <summary>
        /// 用户自定义名称
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 序列号
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// IP地址（GigE相机）
        /// </summary>
        public string IPAddress { get; set; }

        /// <summary>
        /// 枚举索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 显示名称（用于下拉列表）
        /// </summary>
        public string DisplayName
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(UserId))
                    parts.Add(UserId);
                if (!string.IsNullOrEmpty(FriendlyName))
                    parts.Add(FriendlyName);
                if (!string.IsNullOrEmpty(IPAddress))
                    parts.Add(IPAddress);
                return parts.Count > 0 ? string.Join(" | ", parts) : $"Camera_{Index}";
            }
        }
    }

    /// <summary>
    /// 相机接口
    /// </summary>
    public interface ICamera : IDisposable
    {
        /// <summary>
        /// 相机友好名称
        /// </summary>
        string FriendlyName { get; }

        /// <summary>
        /// 用户自定义名称
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// 相机序列号
        /// </summary>
        string SerialNumber { get; }

        /// <summary>
        /// 相机IP地址
        /// </summary>
        string IPAddress { get; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 是否正在采集
        /// </summary>
        bool IsGrabbing { get; }

        /// <summary>
        /// 当前触发源
        /// </summary>
        TriggerSource CurrentTriggerSource { get; }

        /// <summary>
        /// 图像接收事件
        /// </summary>
        event EventHandler<ImageReceivedEventArgs> ImageReceived;

        /// <summary>
        /// 连接状态变更事件
        /// </summary>
        event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;

        /// <summary>
        /// 连接相机（通过UserId）
        /// </summary>
        /// <param name="userId">用户自定义名称</param>
        /// <returns>是否成功</returns>
        bool Connect(string userId);

        /// <summary>
        /// 连接相机（通过枚举索引）
        /// </summary>
        /// <param name="index">枚举索引</param>
        /// <returns>是否成功</returns>
        bool ConnectByIndex(int index);

        /// <summary>
        /// 断开相机
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 开始采集（使用当前触发源设置）
        /// </summary>
        /// <returns>是否成功</returns>
        bool StartGrab();

        /// <summary>
        /// 停止采集
        /// </summary>
        void StopGrab();

        /// <summary>
        /// 软件触发（仅在Software触发源下有效）
        /// </summary>
        /// <returns>是否成功</returns>
        bool SoftTrigger();

        /// <summary>
        /// 设置曝光时间(微秒)
        /// </summary>
        bool SetExposure(int exposureUs);

        /// <summary>
        /// 获取曝光时间(微秒)
        /// </summary>
        int GetExposure();

        /// <summary>
        /// 设置增益
        /// </summary>
        bool SetGain(double gain);

        /// <summary>
        /// 获取增益
        /// </summary>
        double GetGain();

        /// <summary>
        /// 设置触发源
        /// </summary>
        /// <param name="source">触发源</param>
        bool SetTriggerSource(TriggerSource source);

        /// <summary>
        /// 获取触发源
        /// </summary>
        TriggerSource GetTriggerSource();

        /// <summary>
        /// 设置触发边沿（硬件触发时使用）
        /// </summary>
        /// <param name="edge">触发边沿类型</param>
        bool SetTriggerEdge(TriggerEdge edge);

        /// <summary>
        /// 获取触发边沿
        /// </summary>
        TriggerEdge GetTriggerEdge();

        /// <summary>
        /// 搜索相机
        /// </summary>
        /// <returns>相机信息列表</returns>
        CameraInfo[] SearchCameras();
    }

    /// <summary>
    /// 线扫相机接口
    /// </summary>
    public interface ILineCamera : ICamera
    {
        /// <summary>
        /// 设置行频
        /// </summary>
        bool SetLineRate(int lineRate);

        /// <summary>
        /// 获取行频
        /// </summary>
        int GetLineRate();

        /// <summary>
        /// 设置采集行数
        /// </summary>
        bool SetLineCount(int lineCount);

        /// <summary>
        /// 获取采集行数
        /// </summary>
        int GetLineCount();
    }
}
