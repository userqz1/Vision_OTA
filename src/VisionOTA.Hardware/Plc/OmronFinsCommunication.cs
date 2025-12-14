using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Hardware.Plc
{
    /// <summary>
    /// 欧姆龙FINS协议通讯实现
    /// </summary>
    public class OmronFinsCommunication : IPlcCommunication
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private bool _isConnected;
        private readonly string _ipAddress;
        private readonly int _port;
        private readonly int _timeout;
        private CancellationTokenSource _heartbeatCts;
        private readonly object _lockObject = new object();

        // FINS节点地址 (握手后获取)
        private byte _pcNode = 0;
        private byte _plcNode = 0;

        // FINS命令代码
        private const byte FINS_MEMORY_READ = 0x01;
        private const byte FINS_MEMORY_WRITE = 0x02;

        // 内存区域代码
        private const byte AREA_DM_WORD = 0x82;     // D区 字访问
        private const byte AREA_DM_BIT = 0x02;      // D区 位访问
        private const byte AREA_WR_BIT = 0x31;      // W区 位访问
        private const byte AREA_CIO_BIT = 0x30;     // CIO区 位访问

        public bool IsConnected => _isConnected;

        public event EventHandler<PlcConnectionChangedEventArgs> ConnectionChanged;
        public event EventHandler<PlcErrorEventArgs> CommunicationError;

        public OmronFinsCommunication(string ipAddress, int port, int timeout = 3000)
        {
            _ipAddress = ipAddress;
            _port = port;
            _timeout = timeout;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _tcpClient = new TcpClient
                {
                    ReceiveTimeout = _timeout,
                    SendTimeout = _timeout
                };

                var connectTask = _tcpClient.ConnectAsync(_ipAddress, _port);
                if (await Task.WhenAny(connectTask, Task.Delay(_timeout)) != connectTask)
                {
                    throw new TimeoutException("连接超时");
                }

                _stream = _tcpClient.GetStream();

                // FINS/TCP握手
                if (!await PerformHandshakeAsync())
                {
                    throw new Exception("FINS握手失败");
                }

                _isConnected = true;
                FileLogger.Instance.Info($"PLC已连接 {_ipAddress}:{_port}, PC节点:{_pcNode}, PLC节点:{_plcNode}", "FINS");

                // 启动心跳检测
                StartHeartbeat();

                ConnectionChanged?.Invoke(this, new PlcConnectionChangedEventArgs
                {
                    IsConnected = true,
                    Message = $"已连接到 {_ipAddress}:{_port}"
                });

                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"PLC连接失败: {ex.Message}", ex, "FINS");
                _isConnected = false;
                CommunicationError?.Invoke(this, new PlcErrorEventArgs
                {
                    ErrorMessage = $"连接失败: {ex.Message}",
                    Exception = ex
                });
                return false;
            }
        }

        /// <summary>
        /// FINS/TCP握手 - 获取节点地址
        /// </summary>
        private async Task<bool> PerformHandshakeAsync()
        {
            try
            {
                // FINS/TCP握手帧
                // 头: FINS (4字节) + 长度(4字节) + 命令(4字节) + 错误码(4字节) + 客户端节点(4字节)
                byte[] handshakeRequest = new byte[20];

                // FINS标识
                handshakeRequest[0] = 0x46; // F
                handshakeRequest[1] = 0x49; // I
                handshakeRequest[2] = 0x4E; // N
                handshakeRequest[3] = 0x53; // S

                // 长度 (后续数据长度: 12字节)
                handshakeRequest[4] = 0x00;
                handshakeRequest[5] = 0x00;
                handshakeRequest[6] = 0x00;
                handshakeRequest[7] = 0x0C;

                // 命令 (握手: 0x00000000)
                handshakeRequest[8] = 0x00;
                handshakeRequest[9] = 0x00;
                handshakeRequest[10] = 0x00;
                handshakeRequest[11] = 0x00;

                // 错误码
                handshakeRequest[12] = 0x00;
                handshakeRequest[13] = 0x00;
                handshakeRequest[14] = 0x00;
                handshakeRequest[15] = 0x00;

                // 客户端节点 (0表示自动分配)
                handshakeRequest[16] = 0x00;
                handshakeRequest[17] = 0x00;
                handshakeRequest[18] = 0x00;
                handshakeRequest[19] = 0x00;

                await _stream.WriteAsync(handshakeRequest, 0, handshakeRequest.Length);

                // 读取响应
                byte[] response = new byte[24];
                int bytesRead = await _stream.ReadAsync(response, 0, response.Length);

                if (bytesRead >= 24)
                {
                    // 检查FINS标识
                    if (response[0] == 0x46 && response[1] == 0x49 && response[2] == 0x4E && response[3] == 0x53)
                    {
                        // 获取节点地址
                        _pcNode = response[19];   // 客户端节点
                        _plcNode = response[23];  // 服务端节点
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"FINS握手异常: {ex.Message}", ex, "FINS");
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                _heartbeatCts?.Cancel();
                _stream?.Close();
                _tcpClient?.Close();
                _isConnected = false;

                FileLogger.Instance.Info("PLC已断开", "FINS");
                ConnectionChanged?.Invoke(this, new PlcConnectionChangedEventArgs
                {
                    IsConnected = false,
                    Message = "PLC已断开"
                });
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"PLC断开失败: {ex.Message}", ex, "FINS");
            }
        }

        private void StartHeartbeat()
        {
            _heartbeatCts = new CancellationTokenSource();
            var config = ConfigManager.Instance.Plc;
            var heartbeatInterval = config?.Heartbeat?.Interval ?? 1000;

            Task.Run(async () =>
            {
                while (!_heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(heartbeatInterval, _heartbeatCts.Token);

                        // 读取心跳地址验证连接
                        var heartbeatAddress = config?.Heartbeat?.Address ?? "D4410";
                        await ReadWordAsync(heartbeatAddress);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Instance.Warning($"PLC心跳检测失败: {ex.Message}", "FINS");
                        HandleDisconnection();
                        break;
                    }
                }
            }, _heartbeatCts.Token);
        }

        private void HandleDisconnection()
        {
            if (!_isConnected)
                return;

            _isConnected = false;
            ConnectionChanged?.Invoke(this, new PlcConnectionChangedEventArgs
            {
                IsConnected = false,
                Message = "PLC连接已断开"
            });

            // 尝试重连
            var config = ConfigManager.Instance.Plc;
            var reconnectInterval = config?.Connection?.ReconnectInterval ?? 5000;

            Task.Run(async () =>
            {
                while (!_isConnected && !_heartbeatCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(reconnectInterval);
                    FileLogger.Instance.Info("尝试重连PLC...", "FINS");
                    if (await ConnectAsync())
                        break;
                }
            });
        }

        /// <summary>
        /// 解析地址字符串 (如 D4400, W0.00)
        /// </summary>
        private (byte areaCode, ushort address, byte bit) ParseAddress(string addressStr)
        {
            addressStr = addressStr.ToUpper().Trim();
            byte areaCode;
            ushort address;
            byte bit = 0;

            if (addressStr.StartsWith("D"))
            {
                areaCode = AREA_DM_WORD;
                var addrPart = addressStr.Substring(1);
                if (addrPart.Contains("."))
                {
                    var parts = addrPart.Split('.');
                    address = ushort.Parse(parts[0]);
                    bit = byte.Parse(parts[1]);
                    areaCode = AREA_DM_BIT;
                }
                else
                {
                    address = ushort.Parse(addrPart);
                }
            }
            else if (addressStr.StartsWith("W"))
            {
                areaCode = AREA_WR_BIT;
                var addrPart = addressStr.Substring(1);
                if (addrPart.Contains("."))
                {
                    var parts = addrPart.Split('.');
                    address = ushort.Parse(parts[0]);
                    bit = byte.Parse(parts[1]);
                }
                else
                {
                    address = ushort.Parse(addrPart);
                }
            }
            else if (addressStr.StartsWith("CIO") || char.IsDigit(addressStr[0]))
            {
                areaCode = AREA_CIO_BIT;
                var addrPart = addressStr.StartsWith("CIO") ? addressStr.Substring(3) : addressStr;
                if (addrPart.Contains("."))
                {
                    var parts = addrPart.Split('.');
                    address = ushort.Parse(parts[0]);
                    bit = byte.Parse(parts[1]);
                }
                else
                {
                    address = ushort.Parse(addrPart);
                }
            }
            else
            {
                throw new ArgumentException($"不支持的地址格式: {addressStr}");
            }

            return (areaCode, address, bit);
        }

        /// <summary>
        /// 构建FINS/TCP读取命令
        /// </summary>
        private byte[] BuildReadCommand(byte areaCode, ushort address, byte bit, ushort count)
        {
            // FINS/TCP帧: TCP头(16字节) + FINS头(10字节) + 命令数据(8字节)
            byte[] command = new byte[34];

            // TCP头
            command[0] = 0x46; command[1] = 0x49; command[2] = 0x4E; command[3] = 0x53;
            command[4] = 0x00; command[5] = 0x00; command[6] = 0x00; command[7] = 0x1A; // 长度26
            command[8] = 0x00; command[9] = 0x00; command[10] = 0x00; command[11] = 0x02; // 命令
            command[12] = 0x00; command[13] = 0x00; command[14] = 0x00; command[15] = 0x00; // 错误码

            // FINS头
            command[16] = 0x80;        // ICF
            command[17] = 0x00;        // RSV
            command[18] = 0x02;        // GCT
            command[19] = 0x00;        // DNA
            command[20] = _plcNode;    // DA1 (PLC节点)
            command[21] = 0x00;        // DA2
            command[22] = 0x00;        // SNA
            command[23] = _pcNode;     // SA1 (PC节点)
            command[24] = 0x00;        // SA2
            command[25] = 0x00;        // SID

            // 命令代码: 0101 = 内存区域读取
            command[26] = 0x01;
            command[27] = 0x01;

            // 地址
            command[28] = areaCode;
            command[29] = (byte)(address >> 8);
            command[30] = (byte)(address & 0xFF);
            command[31] = bit;

            // 读取长度
            command[32] = (byte)(count >> 8);
            command[33] = (byte)(count & 0xFF);

            return command;
        }

        /// <summary>
        /// 构建FINS/TCP写入命令
        /// </summary>
        private byte[] BuildWriteCommand(byte areaCode, ushort address, byte bit, byte[] data)
        {
            int dataLen = data.Length;
            int count = dataLen / 2;
            byte[] command = new byte[34 + dataLen];

            // TCP头
            command[0] = 0x46; command[1] = 0x49; command[2] = 0x4E; command[3] = 0x53;
            int len = 26 + dataLen;
            command[4] = (byte)(len >> 24);
            command[5] = (byte)(len >> 16);
            command[6] = (byte)(len >> 8);
            command[7] = (byte)(len & 0xFF);
            command[8] = 0x00; command[9] = 0x00; command[10] = 0x00; command[11] = 0x02;
            command[12] = 0x00; command[13] = 0x00; command[14] = 0x00; command[15] = 0x00;

            // FINS头
            command[16] = 0x80;
            command[17] = 0x00;
            command[18] = 0x02;
            command[19] = 0x00;
            command[20] = _plcNode;
            command[21] = 0x00;
            command[22] = 0x00;
            command[23] = _pcNode;
            command[24] = 0x00;
            command[25] = 0x00;

            // 命令代码: 0102 = 内存区域写入
            command[26] = 0x01;
            command[27] = 0x02;

            // 地址
            command[28] = areaCode;
            command[29] = (byte)(address >> 8);
            command[30] = (byte)(address & 0xFF);
            command[31] = bit;

            // 写入长度
            command[32] = (byte)(count >> 8);
            command[33] = (byte)(count & 0xFF);

            // 数据
            Array.Copy(data, 0, command, 34, dataLen);

            return command;
        }

        public async Task<short> ReadWordAsync(string address)
        {
            if (!_isConnected)
                throw new InvalidOperationException("PLC未连接");

            try
            {
                lock (_lockObject)
                {
                    var (areaCode, addr, bit) = ParseAddress(address);
                    var command = BuildReadCommand(areaCode, addr, bit, 1);

                    _stream.Write(command, 0, command.Length);

                    byte[] response = new byte[256];
                    int bytesRead = _stream.Read(response, 0, response.Length);

                    if (bytesRead >= 32)
                    {
                        // 检查响应码
                        if (response[28] == 0x00 && response[29] == 0x00)
                        {
                            // 数据从第30字节开始
                            short value = (short)((response[30] << 8) | response[31]);
                            return value;
                        }
                    }

                    return 0;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"读取字失败 {address}: {ex.Message}", ex, "FINS");
                throw;
            }
        }

        public async Task<bool> WriteWordAsync(string address, short value)
        {
            if (!_isConnected)
                return false;

            try
            {
                lock (_lockObject)
                {
                    var (areaCode, addr, bit) = ParseAddress(address);
                    byte[] data = new byte[] { (byte)(value >> 8), (byte)(value & 0xFF) };
                    var command = BuildWriteCommand(areaCode, addr, bit, data);

                    _stream.Write(command, 0, command.Length);

                    byte[] response = new byte[256];
                    int bytesRead = _stream.Read(response, 0, response.Length);

                    if (bytesRead >= 30)
                    {
                        return response[28] == 0x00 && response[29] == 0x00;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"写入字失败 {address}: {ex.Message}", ex, "FINS");
                return false;
            }
        }

        public async Task<int> ReadDWordAsync(string address)
        {
            if (!_isConnected)
                throw new InvalidOperationException("PLC未连接");

            try
            {
                lock (_lockObject)
                {
                    var (areaCode, addr, bit) = ParseAddress(address);
                    var command = BuildReadCommand(areaCode, addr, bit, 2); // 读2个字

                    _stream.Write(command, 0, command.Length);

                    byte[] response = new byte[256];
                    int bytesRead = _stream.Read(response, 0, response.Length);

                    if (bytesRead >= 34)
                    {
                        if (response[28] == 0x00 && response[29] == 0x00)
                        {
                            // 大端序转换
                            int value = (response[30] << 24) | (response[31] << 16) |
                                       (response[32] << 8) | response[33];
                            return value;
                        }
                    }

                    return 0;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"读取双字失败 {address}: {ex.Message}", ex, "FINS");
                throw;
            }
        }

        public async Task<bool> WriteDWordAsync(string address, int value)
        {
            if (!_isConnected)
                return false;

            try
            {
                lock (_lockObject)
                {
                    var (areaCode, addr, bit) = ParseAddress(address);
                    byte[] data = new byte[]
                    {
                        (byte)(value >> 24),
                        (byte)(value >> 16),
                        (byte)(value >> 8),
                        (byte)(value & 0xFF)
                    };
                    var command = BuildWriteCommand(areaCode, addr, bit, data);

                    _stream.Write(command, 0, command.Length);

                    byte[] response = new byte[256];
                    int bytesRead = _stream.Read(response, 0, response.Length);

                    if (bytesRead >= 30)
                    {
                        return response[28] == 0x00 && response[29] == 0x00;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"写入双字失败 {address}: {ex.Message}", ex, "FINS");
                return false;
            }
        }

        public async Task<bool> ReadBitAsync(string address)
        {
            if (!_isConnected)
                return false;

            try
            {
                var word = await ReadWordAsync(address.Split('.')[0]);
                var bitIndex = int.Parse(address.Split('.')[1]);
                return ((word >> bitIndex) & 1) == 1;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"读取位失败 {address}: {ex.Message}", ex, "FINS");
                throw;
            }
        }

        public async Task<bool> WriteBitAsync(string address, bool value)
        {
            // 位写入需要先读取字，修改位，再写回
            // 这里简化处理，实际应使用FINS位写入命令
            try
            {
                var parts = address.Split('.');
                var wordAddr = parts[0];
                var bitIndex = int.Parse(parts[1]);

                var word = await ReadWordAsync(wordAddr);
                if (value)
                    word |= (short)(1 << bitIndex);
                else
                    word &= (short)~(1 << bitIndex);

                return await WriteWordAsync(wordAddr, word);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"写入位失败 {address}: {ex.Message}", ex, "FINS");
                return false;
            }
        }

        public async Task<float> ReadFloatAsync(string address)
        {
            var intValue = await ReadDWordAsync(address);
            // 欧姆龙使用大端序存储浮点数
            byte[] bytes = BitConverter.GetBytes(intValue);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToSingle(bytes, 0);
        }

        public async Task<bool> WriteFloatAsync(string address, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            int intValue = BitConverter.ToInt32(bytes, 0);
            return await WriteDWordAsync(address, intValue);
        }

        public void Dispose()
        {
            Disconnect();
            _heartbeatCts?.Dispose();
        }
    }
}
