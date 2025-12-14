using System;
using System.Runtime.InteropServices;

namespace VisionOTA.Hardware.Camera.Dushen
{
    /// <summary>
    /// 度申相机SDK状态码
    /// </summary>
    public enum dvpStatus : int
    {
        DVP_STATUS_OK = 0,
        DVP_STATUS_FAILED = -1,
        DVP_STATUS_INVALID_HANDLE = -2,
        DVP_STATUS_INVALID_PARAMETER = -3,
        DVP_STATUS_NOT_SUPPORTED = -4,
        DVP_STATUS_NOT_READY = -5,
        DVP_STATUS_BUSY = -6,
        DVP_STATUS_TIMEOUT = -7,
        DVP_STATUS_NOT_FOUND = -8,
        DVP_STATUS_IN_PROCESS = -9,
        DVP_STATUS_NOT_INITIALIZED = -10,
        DVP_STATUS_UNSUPPORTED_OPERATION = -11,
    }

    /// <summary>
    /// 相机打开模式
    /// </summary>
    public enum dvpOpenMode : int
    {
        OPEN_NORMAL = 0,
        OPEN_DEBUG = 1,
        OPEN_OFFLINE = 2
    }

    /// <summary>
    /// 触发源
    /// </summary>
    public enum dvpTriggerSource : int
    {
        TRIGGER_SOURCE_SOFTWARE = 0,    // 软件触发
        TRIGGER_SOURCE_LINE0 = 1,       // LINE0 硬件触发
        TRIGGER_SOURCE_LINE1 = 2,       // LINE1 硬件触发
        TRIGGER_SOURCE_LINE2 = 3,       // LINE2 硬件触发
        TRIGGER_SOURCE_LINE3 = 4,       // LINE3 硬件触发
    }

    /// <summary>
    /// 触发输入信号类型
    /// </summary>
    public enum dvpTriggerInputType : int
    {
        TRIGGER_INPUT_RISING_EDGE = 0,   // 上升沿触发
        TRIGGER_INPUT_FALLING_EDGE = 1,  // 下降沿触发
        TRIGGER_INPUT_HIGH_LEVEL = 2,    // 高电平触发
        TRIGGER_INPUT_LOW_LEVEL = 3,     // 低电平触发
        TRIGGER_INPUT_DOUBLE_EDGE = 4,   // 双边沿触发
    }

    /// <summary>
    /// 自动曝光操作模式
    /// </summary>
    public enum dvpAeOperation : int
    {
        AE_OP_OFF = 0,          // 关闭自动曝光
        AE_OP_ONCE = 1,         // 单次自动曝光
        AE_OP_CONTINUOUS = 2,   // 连续自动曝光
    }

    /// <summary>
    /// 抗闪烁模式
    /// </summary>
    public enum dvpAntiFlick : int
    {
        ANTIFLICK_DISABLE = 0,  // 禁用抗闪烁
        ANTIFLICK_50HZ = 1,     // 50Hz抗闪烁
        ANTIFLICK_60HZ = 2,     // 60Hz抗闪烁
    }

    /// <summary>
    /// 流回调事件类型
    /// </summary>
    public enum dvpStreamEvent : int
    {
        CYCLOBUFFER_RAW = 0,    // 图像到达后
        CYCLOBUFFER_ISP = 1,    // 图像处理后(ISP)
    }

    /// <summary>
    /// 事件回调类型
    /// </summary>
    public enum dvpEvent : int
    {
        EVENT_DISCONNECTED = 0,      // 断开连接
        EVENT_CONNECTED = 1,         // 连接成功
        EVENT_FRAME_START = 2,       // 帧开始
        EVENT_FRAME_END = 3,         // 帧结束
        EVENT_STREAM_STARTED = 4,    // 开始传输
        EVENT_STREAM_STOPPED = 5,    // 停止传输
    }

    /// <summary>
    /// 相机信息结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct dvpCameraInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string FriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string Model;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string SerialNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string Manufacturer;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string UserId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string CameraId;
    }

    /// <summary>
    /// 帧信息结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct dvpFrame
    {
        public int format;           // 像素格式
        public int bits;             // 位深度
        public int width;            // 图像宽度
        public int height;           // 图像高度
        public long timestamp;       // 时间戳
        public long frameCount;      // 帧计数
    }

    /// <summary>
    /// 浮点范围描述
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct dvpDoubleDescr
    {
        public double fMin;
        public double fMax;
        public double fStep;
        public double fDefault;
    }

    /// <summary>
    /// 整型范围描述
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct dvpIntDescr
    {
        public int iMin;
        public int iMax;
        public int iStep;
        public int iDefault;
    }

    /// <summary>
    /// 流回调委托
    /// </summary>
    /// <param name="handle">相机句柄</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="pContext">用户上下文</param>
    /// <param name="pFrame">帧信息</param>
    /// <param name="pBuffer">图像数据</param>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void dvpStreamCallback(uint handle, dvpStreamEvent eventType, IntPtr pContext, ref dvpFrame pFrame, IntPtr pBuffer);

    /// <summary>
    /// 事件回调委托
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void dvpEventCallback(uint handle, dvpEvent eventType, IntPtr pContext, int param);

    /// <summary>
    /// 度申相机SDK P/Invoke封装
    /// </summary>
    public static class DvpCamera
    {
        private const string DllName = "DVPCamera64.dll";

        #region 初始化和枚举

        /// <summary>
        /// 刷新相机列表并获取数量
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpRefresh(ref uint count);

        /// <summary>
        /// 枚举相机信息
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpEnum(uint index, ref dvpCameraInfo info);

        #endregion

        #region 打开和关闭

        /// <summary>
        /// 根据索引打开相机
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpOpen(uint index, dvpOpenMode mode, ref uint handle);

        /// <summary>
        /// 根据友好名称打开相机
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern dvpStatus dvpOpenByName(string friendlyName, dvpOpenMode mode, ref uint handle);

        /// <summary>
        /// 根据用户ID打开相机
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern dvpStatus dvpOpenByUserId(string userId, dvpOpenMode mode, ref uint handle);

        /// <summary>
        /// 关闭相机
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpClose(uint handle);

        /// <summary>
        /// 检查相机是否有效
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpIsValid(uint handle, ref bool isValid);

        #endregion

        #region 采集控制

        /// <summary>
        /// 启动视频流
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpStart(uint handle);

        /// <summary>
        /// 停止视频流
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpStop(uint handle);

        /// <summary>
        /// 获取帧(同步采集)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetFrame(uint handle, ref dvpFrame frame, ref IntPtr pBuffer, uint timeout);

        /// <summary>
        /// 注册流回调
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpRegisterStreamCallback(uint handle, dvpStreamCallback callback, dvpStreamEvent eventType, IntPtr pContext);

        /// <summary>
        /// 注册事件回调
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpRegisterEventCallback(uint handle, dvpEventCallback callback, dvpEvent eventType, IntPtr pContext);

        #endregion

        #region 触发控制

        /// <summary>
        /// 设置触发状态(开启/关闭触发模式)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetTriggerState(uint handle, bool state);

        /// <summary>
        /// 获取触发状态
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetTriggerState(uint handle, ref bool state);

        /// <summary>
        /// 设置触发源
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetTriggerSource(uint handle, dvpTriggerSource source);

        /// <summary>
        /// 获取触发源
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetTriggerSource(uint handle, ref dvpTriggerSource source);

        /// <summary>
        /// 设置触发输入信号类型
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetTriggerInputType(uint handle, dvpTriggerInputType type);

        /// <summary>
        /// 获取触发输入信号类型
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetTriggerInputType(uint handle, ref dvpTriggerInputType type);

        /// <summary>
        /// 软件触发(单次)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpTriggerFire(uint handle);

        /// <summary>
        /// 设置软触发循环状态
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetSoftTriggerLoopState(uint handle, bool state);

        /// <summary>
        /// 获取软触发循环状态
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetSoftTriggerLoopState(uint handle, ref bool state);

        /// <summary>
        /// 设置软触发循环时间间隔(us)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetSoftTriggerLoop(uint handle, double interval);

        /// <summary>
        /// 获取软触发循环时间间隔
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetSoftTriggerLoop(uint handle, ref double interval);

        /// <summary>
        /// 获取软触发循环范围描述
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetSoftTriggerLoopDescr(uint handle, ref dvpDoubleDescr descr);

        /// <summary>
        /// 设置触发延迟(us)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetTriggerDelay(uint handle, double delay);

        /// <summary>
        /// 获取触发延迟
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetTriggerDelay(uint handle, ref double delay);

        #endregion

        #region 曝光控制

        /// <summary>
        /// 设置自动曝光操作模式
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetAeOperation(uint handle, dvpAeOperation operation);

        /// <summary>
        /// 获取自动曝光操作模式
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetAeOperation(uint handle, ref dvpAeOperation operation);

        /// <summary>
        /// 设置抗闪烁模式
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetAntiFlick(uint handle, dvpAntiFlick antiFlick);

        /// <summary>
        /// 获取曝光时间范围描述
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetExposureDescr(uint handle, ref dvpDoubleDescr descr);

        /// <summary>
        /// 设置曝光时间(us)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetExposure(uint handle, double exposure);

        /// <summary>
        /// 获取曝光时间(us)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetExposure(uint handle, ref double exposure);

        #endregion

        #region 增益控制

        /// <summary>
        /// 获取模拟增益范围描述
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetAnalogGainDescr(uint handle, ref dvpDoubleDescr descr);

        /// <summary>
        /// 设置模拟增益
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetAnalogGain(uint handle, double gain);

        /// <summary>
        /// 获取模拟增益
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetAnalogGain(uint handle, ref double gain);

        #endregion

        #region 线扫相机专用

        /// <summary>
        /// 获取行频范围描述
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetLineRateDescr(uint handle, ref dvpDoubleDescr descr);

        /// <summary>
        /// 设置行频(Hz)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetLineRate(uint handle, double lineRate);

        /// <summary>
        /// 获取行频(Hz)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetLineRate(uint handle, ref double lineRate);

        /// <summary>
        /// 获取ROI高度范围(用于设置采集行数)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetHeightDescr(uint handle, ref dvpIntDescr descr);

        /// <summary>
        /// 设置ROI高度(采集行数)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpSetHeight(uint handle, int height);

        /// <summary>
        /// 获取ROI高度
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetHeight(uint handle, ref int height);

        #endregion

        #region 其他

        /// <summary>
        /// 获取相机序列号
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetSerialNumber(uint handle, [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder serialNumber, uint size);

        /// <summary>
        /// 获取SDK版本
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern dvpStatus dvpGetLibraryVersion([MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder version, uint size);

        #endregion
    }
}
