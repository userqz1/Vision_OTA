using System;
using System.Threading.Tasks;

namespace VisionOTA.Hardware.Plc
{
    /// <summary>
    /// PLC通讯事件参数
    /// </summary>
    public class PlcConnectionChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// PLC通讯错误事件参数
    /// </summary>
    public class PlcErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// PLC通讯接口
    /// </summary>
    public interface IPlcCommunication : IDisposable
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接状态变更事件
        /// </summary>
        event EventHandler<PlcConnectionChangedEventArgs> ConnectionChanged;

        /// <summary>
        /// 通讯错误事件
        /// </summary>
        event EventHandler<PlcErrorEventArgs> CommunicationError;

        /// <summary>
        /// 连接PLC
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> ConnectAsync();

        /// <summary>
        /// 断开PLC
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 读取字(16位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>读取的值</returns>
        Task<short> ReadWordAsync(string address);

        /// <summary>
        /// 写入字(16位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        Task<bool> WriteWordAsync(string address, short value);

        /// <summary>
        /// 读取双字(32位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>读取的值</returns>
        Task<int> ReadDWordAsync(string address);

        /// <summary>
        /// 写入双字(32位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        Task<bool> WriteDWordAsync(string address, int value);

        /// <summary>
        /// 读取位
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>位状态</returns>
        Task<bool> ReadBitAsync(string address);

        /// <summary>
        /// 写入位
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        Task<bool> WriteBitAsync(string address, bool value);

        /// <summary>
        /// 读取浮点数
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>浮点数值</returns>
        Task<float> ReadFloatAsync(string address);

        /// <summary>
        /// 写入浮点数
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        Task<bool> WriteFloatAsync(string address, float value);
    }
}
