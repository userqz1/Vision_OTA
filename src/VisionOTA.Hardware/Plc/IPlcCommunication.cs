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
        /// 读取浮点数 (REAL - 32位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>浮点数值</returns>
        Task<float> ReadFloatAsync(string address);

        /// <summary>
        /// 写入浮点数 (REAL - 32位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        Task<bool> WriteFloatAsync(string address, float value);

        /// <summary>
        /// 读取无符号16位整数 (UINT)
        /// </summary>
        Task<ushort> ReadUIntAsync(string address);

        /// <summary>
        /// 写入无符号16位整数 (UINT)
        /// </summary>
        Task<bool> WriteUIntAsync(string address, ushort value);

        /// <summary>
        /// 读取无符号32位整数 (UDINT)
        /// </summary>
        Task<uint> ReadUDIntAsync(string address);

        /// <summary>
        /// 写入无符号32位整数 (UDINT)
        /// </summary>
        Task<bool> WriteUDIntAsync(string address, uint value);

        /// <summary>
        /// 读取64位有符号整数 (LINT) - NJ/NX系列
        /// </summary>
        Task<long> ReadLIntAsync(string address);

        /// <summary>
        /// 写入64位有符号整数 (LINT) - NJ/NX系列
        /// </summary>
        Task<bool> WriteLIntAsync(string address, long value);

        /// <summary>
        /// 读取无符号64位整数 (ULINT) - NJ/NX系列
        /// </summary>
        Task<ulong> ReadULIntAsync(string address);

        /// <summary>
        /// 写入无符号64位整数 (ULINT) - NJ/NX系列
        /// </summary>
        Task<bool> WriteULIntAsync(string address, ulong value);

        /// <summary>
        /// 读取64位双精度浮点数 (LREAL) - NJ/NX系列
        /// </summary>
        Task<double> ReadLRealAsync(string address);

        /// <summary>
        /// 写入64位双精度浮点数 (LREAL) - NJ/NX系列
        /// </summary>
        Task<bool> WriteLRealAsync(string address, double value);
    }
}
