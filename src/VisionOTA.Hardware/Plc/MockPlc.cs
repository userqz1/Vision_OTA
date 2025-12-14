using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Hardware.Plc
{
    /// <summary>
    /// 模拟PLC实现
    /// </summary>
    public class MockPlc : IPlcCommunication
    {
        private bool _isConnected;
        private readonly Dictionary<string, int> _wordMemory;
        private readonly Dictionary<string, bool> _bitMemory;

        public bool IsConnected => _isConnected;

        public event EventHandler<PlcConnectionChangedEventArgs> ConnectionChanged;
        public event EventHandler<PlcErrorEventArgs> CommunicationError;

        public MockPlc()
        {
            _wordMemory = new Dictionary<string, int>();
            _bitMemory = new Dictionary<string, bool>();
        }

        public Task<bool> ConnectAsync()
        {
            _isConnected = true;
            FileLogger.Instance.Info("模拟PLC已连接", "MockPlc");

            ConnectionChanged?.Invoke(this, new PlcConnectionChangedEventArgs
            {
                IsConnected = true,
                Message = "模拟PLC已连接"
            });

            return Task.FromResult(true);
        }

        public void Disconnect()
        {
            if (!_isConnected)
                return;

            _isConnected = false;
            FileLogger.Instance.Info("模拟PLC已断开", "MockPlc");

            ConnectionChanged?.Invoke(this, new PlcConnectionChangedEventArgs
            {
                IsConnected = false,
                Message = "模拟PLC已断开"
            });
        }

        public Task<short> ReadWordAsync(string address)
        {
            if (!_isConnected)
                return Task.FromResult<short>(0);

            var value = _wordMemory.ContainsKey(address) ? (short)_wordMemory[address] : (short)0;
            FileLogger.Instance.Debug($"模拟PLC读取字 {address} = {value}", "MockPlc");
            return Task.FromResult(value);
        }

        public Task<bool> WriteWordAsync(string address, short value)
        {
            if (!_isConnected)
                return Task.FromResult(false);

            _wordMemory[address] = value;
            FileLogger.Instance.Debug($"模拟PLC写入字 {address} = {value}", "MockPlc");
            return Task.FromResult(true);
        }

        public Task<int> ReadDWordAsync(string address)
        {
            if (!_isConnected)
                return Task.FromResult(0);

            var value = _wordMemory.ContainsKey(address) ? _wordMemory[address] : 0;
            FileLogger.Instance.Debug($"模拟PLC读取双字 {address} = {value}", "MockPlc");
            return Task.FromResult(value);
        }

        public Task<bool> WriteDWordAsync(string address, int value)
        {
            if (!_isConnected)
                return Task.FromResult(false);

            _wordMemory[address] = value;
            FileLogger.Instance.Debug($"模拟PLC写入双字 {address} = {value}", "MockPlc");
            return Task.FromResult(true);
        }

        public Task<bool> ReadBitAsync(string address)
        {
            if (!_isConnected)
                return Task.FromResult(false);

            var value = _bitMemory.ContainsKey(address) && _bitMemory[address];
            FileLogger.Instance.Debug($"模拟PLC读取位 {address} = {value}", "MockPlc");
            return Task.FromResult(value);
        }

        public Task<bool> WriteBitAsync(string address, bool value)
        {
            if (!_isConnected)
                return Task.FromResult(false);

            _bitMemory[address] = value;
            FileLogger.Instance.Debug($"模拟PLC写入位 {address} = {value}", "MockPlc");
            return Task.FromResult(true);
        }

        public Task<float> ReadFloatAsync(string address)
        {
            if (!_isConnected)
                return Task.FromResult(0f);

            var intValue = _wordMemory.ContainsKey(address) ? _wordMemory[address] : 0;
            var floatValue = BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);
            FileLogger.Instance.Debug($"模拟PLC读取浮点 {address} = {floatValue}", "MockPlc");
            return Task.FromResult(floatValue);
        }

        public Task<bool> WriteFloatAsync(string address, float value)
        {
            if (!_isConnected)
                return Task.FromResult(false);

            var intValue = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            _wordMemory[address] = intValue;
            FileLogger.Instance.Debug($"模拟PLC写入浮点 {address} = {value}", "MockPlc");
            return Task.FromResult(true);
        }

        public Task<ushort> ReadUIntAsync(string address)
        {
            if (!_isConnected)
                return Task.FromResult<ushort>(0);

            var value = _wordMemory.ContainsKey(address) ? (ushort)_wordMemory[address] : (ushort)0;
            return Task.FromResult(value);
        }

        public Task<bool> WriteUIntAsync(string address, ushort value)
        {
            if (!_isConnected)
                return Task.FromResult(false);

            _wordMemory[address] = value;
            return Task.FromResult(true);
        }

        public Task<uint> ReadUDIntAsync(string address)
        {
            if (!_isConnected)
                return Task.FromResult<uint>(0);

            var value = _wordMemory.ContainsKey(address) ? (uint)_wordMemory[address] : 0u;
            return Task.FromResult(value);
        }

        public Task<bool> WriteUDIntAsync(string address, uint value)
        {
            if (!_isConnected)
                return Task.FromResult(false);

            _wordMemory[address] = (int)value;
            return Task.FromResult(true);
        }

        public Task<long> ReadLIntAsync(string address)
        {
            if (!_isConnected)
                return Task.FromResult<long>(0);

            return Task.FromResult<long>(0);
        }

        public Task<bool> WriteLIntAsync(string address, long value)
        {
            if (!_isConnected)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        public Task<ulong> ReadULIntAsync(string address)
        {
            if (!_isConnected)
                return Task.FromResult<ulong>(0);

            return Task.FromResult<ulong>(0);
        }

        public Task<bool> WriteULIntAsync(string address, ulong value)
        {
            if (!_isConnected)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        public Task<double> ReadLRealAsync(string address)
        {
            if (!_isConnected)
                return Task.FromResult<double>(0);

            return Task.FromResult<double>(0);
        }

        public Task<bool> WriteLRealAsync(string address, double value)
        {
            if (!_isConnected)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        /// <summary>
        /// 模拟触发信号（供测试使用）
        /// </summary>
        public void SimulateTrigger(string address)
        {
            _bitMemory[address] = true;
            FileLogger.Instance.Info($"模拟PLC触发信号 {address}", "MockPlc");
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
