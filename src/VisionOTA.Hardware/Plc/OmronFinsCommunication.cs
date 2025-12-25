using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Hardware.Plc
{
    /// <summary>
    /// 欧姆龙FINS/TCP协议通讯实现 (调试版本)
    /// 
    /// 内存区域代码参考 (CS/CJ系列):
    /// ┌──────────┬───────────┬───────────┐
    /// │ 区域     │ Word代码  │ Bit代码   │
    /// ├──────────┼───────────┼───────────┤
    /// │ CIO      │ 0xB0      │ 0x30      │
    /// │ WR       │ 0xB1      │ 0x31      │
    /// │ HR       │ 0xB2      │ 0x32      │
    /// │ AR       │ 0xB3      │ 0x33      │
    /// │ DM       │ 0x82      │ 0x02      │
    /// │ EM(bank0)│ 0xA0      │ 0x20      │
    /// └──────────┴───────────┴───────────┘
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
        private int _heartbeatCount = 0;
        private const int HeartbeatLogInterval = 12; // 每12次心跳记录一次（约60秒）

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _disposeCts;
        private bool _isDisposed;

        // FINS节点地址
        private byte _pcNode = 0;
        private byte _plcNode = 0;

        // 启用详细调试
        private bool _enableDebug = false;

        // 内存区域代码 - Word访问
        private const byte AREA_CIO_WORD = 0xB0;
        private const byte AREA_WR_WORD = 0xB1;
        private const byte AREA_HR_WORD = 0xB2;
        private const byte AREA_AR_WORD = 0xB3;
        private const byte AREA_DM_WORD = 0x82;
        private const byte AREA_EM0_WORD = 0xA0;

        // 内存区域代码 - Bit访问
        private const byte AREA_CIO_BIT = 0x30;
        private const byte AREA_WR_BIT = 0x31;
        private const byte AREA_HR_BIT = 0x32;
        private const byte AREA_AR_BIT = 0x33;
        private const byte AREA_DM_BIT = 0x02;
        private const byte AREA_EM0_BIT = 0x20;

        private byte _sid = 0;

        public bool IsConnected => _isConnected;

        public event EventHandler<PlcConnectionChangedEventArgs> ConnectionChanged;
        public event EventHandler<PlcErrorEventArgs> CommunicationError;

        public OmronFinsCommunication(string ipAddress, int port, int timeout = 3000)
        {
            _ipAddress = ipAddress;
            _port = port;
            _timeout = timeout;
        }

        /// <summary>
        /// 启用或禁用调试输出
        /// </summary>
        public void SetDebugMode(bool enable)
        {
            _enableDebug = enable;
        }

        private void DebugLog(string message)
        {
            if (_enableDebug)
            {
                FileLogger.Instance.Debug($"[FINS-DEBUG] {message}", "FINS");
                System.Diagnostics.Debug.WriteLine($"[FINS-DEBUG] {message}");
            }
        }

        private string BytesToHex(byte[] data, int offset = 0, int length = -1)
        {
            if (data == null) return "null";
            if (length < 0) length = data.Length - offset;
            var sb = new StringBuilder();
            for (int i = 0; i < length && (offset + i) < data.Length; i++)
            {
                sb.Append(data[offset + i].ToString("X2"));
                sb.Append(" ");
            }
            return sb.ToString().TrimEnd();
        }

        #region 连接管理

        public async Task<bool> ConnectAsync()
        {
            if (_isDisposed)
                return false;

            try
            {
                DebugLog($"正在连接 {_ipAddress}:{_port}...");

                _disposeCts = new CancellationTokenSource();
                _tcpClient = new TcpClient
                {
                    ReceiveTimeout = _timeout,
                    SendTimeout = _timeout
                };

                var connectTask = _tcpClient.ConnectAsync(_ipAddress, _port);
                var timeoutTask = Task.Delay(_timeout, _disposeCts.Token);

                try
                {
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        // 超时或取消，关闭TcpClient以取消连接
                        _tcpClient?.Close();
                        _tcpClient = null;

                        if (_disposeCts.Token.IsCancellationRequested)
                            return false;

                        throw new TimeoutException("TCP连接超时");
                    }

                    await connectTask;
                }
                catch (ObjectDisposedException)
                {
                    // TcpClient已被释放（程序退出时）
                    return false;
                }
                catch (NullReferenceException)
                {
                    // TcpClient在连接过程中被释放
                    return false;
                }

                if (_isDisposed || _tcpClient == null)
                    return false;

                _stream = _tcpClient.GetStream();

                DebugLog("TCP连接成功，开始FINS握手...");

                if (!await PerformHandshakeAsync())
                {
                    throw new Exception("FINS握手失败");
                }

                _isConnected = true;
                DebugLog($"FINS连接成功! PC节点={_pcNode}, PLC节点={_plcNode}");

                FileLogger.Instance.Info($"PLC已连接 {_ipAddress}:{_port}", "FINS");

                StartHeartbeat();

                ConnectionChanged?.Invoke(this, new PlcConnectionChangedEventArgs
                {
                    IsConnected = true,
                    Message = $"已连接到 {_ipAddress}:{_port}"
                });

                return true;
            }
            catch (ObjectDisposedException)
            {
                // 程序退出时正常情况，不需要记录错误
                return false;
            }
            catch (OperationCanceledException)
            {
                // 操作被取消，正常情况
                return false;
            }
            catch (Exception ex)
            {
                if (_isDisposed) return false;

                DebugLog($"连接失败: {ex.Message}");
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

        private async Task<bool> PerformHandshakeAsync()
        {
            try
            {
                // FINS/TCP握手请求 (20字节)
                byte[] handshakeRequest = new byte[]
                {
                    0x46, 0x49, 0x4E, 0x53,  // "FINS"
                    0x00, 0x00, 0x00, 0x0C,  // 长度=12
                    0x00, 0x00, 0x00, 0x00,  // 命令=握手
                    0x00, 0x00, 0x00, 0x00,  // 错误码=0
                    0x00, 0x00, 0x00, 0x00   // 客户端节点=自动分配
                };

                DebugLog($"发送握手请求: {BytesToHex(handshakeRequest)}");
                await _stream.WriteAsync(handshakeRequest, 0, handshakeRequest.Length);

                // 读取响应 (24字节)
                byte[] response = new byte[24];
                int totalRead = 0;
                while (totalRead < 24)
                {
                    int bytesRead = await _stream.ReadAsync(response, totalRead, 24 - totalRead);
                    if (bytesRead == 0)
                        throw new Exception("连接已关闭");
                    totalRead += bytesRead;
                }

                DebugLog($"收到握手响应: {BytesToHex(response)}");

                // 验证FINS标识
                if (response[0] != 0x46 || response[1] != 0x49 ||
                    response[2] != 0x4E || response[3] != 0x53)
                {
                    DebugLog("错误: 无效的FINS标识");
                    return false;
                }

                // 检查错误码
                int errorCode = (response[12] << 24) | (response[13] << 16) |
                               (response[14] << 8) | response[15];
                if (errorCode != 0)
                {
                    DebugLog($"错误: 握手错误码=0x{errorCode:X8}");
                    return false;
                }

                // 获取节点地址
                _pcNode = response[19];
                _plcNode = response[23];

                DebugLog($"握手成功: PC节点={_pcNode}, PLC节点={_plcNode}");
                return true;
            }
            catch (Exception ex)
            {
                DebugLog($"握手异常: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                // 取消所有待处理的操作
                _disposeCts?.Cancel();
                _heartbeatCts?.Cancel();

                try { _stream?.Close(); } catch { }
                try { _tcpClient?.Close(); } catch { }

                _stream = null;
                _tcpClient = null;
                _isConnected = false;

                DebugLog("已断开连接");
                ConnectionChanged?.Invoke(this, new PlcConnectionChangedEventArgs
                {
                    IsConnected = false,
                    Message = "PLC已断开"
                });
            }
            catch (Exception ex)
            {
                DebugLog($"断开连接异常: {ex.Message}");
            }
        }

        private void StartHeartbeat()
        {
            _heartbeatCts = new CancellationTokenSource();
            _heartbeatCount = 0;
            var config = ConfigManager.Instance.Plc;
            var heartbeatInterval = config?.Heartbeat?.Interval ?? 5000;

            Task.Run(async () =>
            {
                while (!_heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(heartbeatInterval, _heartbeatCts.Token);
                        var heartbeatAddress = config?.Heartbeat?.Address ?? "D0";
                        if (heartbeatAddress.Contains("."))
                            heartbeatAddress = heartbeatAddress.Split('.')[0];

                        await ReadWordAsync(heartbeatAddress);
                        _heartbeatCount++;

                        // 每隔一段时间记录一次心跳正常
                        if (_heartbeatCount % HeartbeatLogInterval == 0)
                        {
                            FileLogger.Instance.Debug($"PLC心跳正常 (已运行 {_heartbeatCount * heartbeatInterval / 1000}秒)", "FINS");
                        }
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
            if (!_isConnected || _isDisposed) return;

            _isConnected = false;
            ConnectionChanged?.Invoke(this, new PlcConnectionChangedEventArgs
            {
                IsConnected = false,
                Message = "PLC连接已断开"
            });

            if (_isDisposed) return;

            var config = ConfigManager.Instance.Plc;
            var reconnectInterval = config?.Connection?.ReconnectInterval ?? 5000;

            Task.Run(async () =>
            {
                while (!_isConnected && !_isDisposed &&
                       _heartbeatCts != null && !_heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(reconnectInterval, _heartbeatCts.Token);
                        if (_isDisposed) break;
                        DebugLog("尝试重连...");
                        if (await ConnectAsync())
                            break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }

        #endregion

        #region 地址解析

        private (byte wordCode, byte bitCode, ushort address, byte bit, bool hasBit) ParseAddressEx(string addressStr)
        {
            addressStr = addressStr.ToUpper().Trim();
            byte wordCode, bitCode;
            ushort address;
            byte bit = 0;
            bool hasBit = false;
            string addrPart;

            if (addressStr.StartsWith("DM"))
            {
                wordCode = AREA_DM_WORD;
                bitCode = AREA_DM_BIT;
                addrPart = addressStr.Substring(2);
            }
            else if (addressStr.StartsWith("D"))
            {
                wordCode = AREA_DM_WORD;
                bitCode = AREA_DM_BIT;
                addrPart = addressStr.Substring(1);
            }
            else if (addressStr.StartsWith("WR"))
            {
                wordCode = AREA_WR_WORD;
                bitCode = AREA_WR_BIT;
                addrPart = addressStr.Substring(2);
            }
            else if (addressStr.StartsWith("W"))
            {
                wordCode = AREA_WR_WORD;
                bitCode = AREA_WR_BIT;
                addrPart = addressStr.Substring(1);
            }
            else if (addressStr.StartsWith("HR"))
            {
                wordCode = AREA_HR_WORD;
                bitCode = AREA_HR_BIT;
                addrPart = addressStr.Substring(2);
            }
            else if (addressStr.StartsWith("H"))
            {
                wordCode = AREA_HR_WORD;
                bitCode = AREA_HR_BIT;
                addrPart = addressStr.Substring(1);
            }
            else if (addressStr.StartsWith("AR"))
            {
                wordCode = AREA_AR_WORD;
                bitCode = AREA_AR_BIT;
                addrPart = addressStr.Substring(2);
            }
            else if (addressStr.StartsWith("A"))
            {
                wordCode = AREA_AR_WORD;
                bitCode = AREA_AR_BIT;
                addrPart = addressStr.Substring(1);
            }
            else if (addressStr.StartsWith("CIO"))
            {
                wordCode = AREA_CIO_WORD;
                bitCode = AREA_CIO_BIT;
                addrPart = addressStr.Substring(3);
            }
            else if (addressStr.StartsWith("EM") || addressStr.StartsWith("E"))
            {
                wordCode = AREA_EM0_WORD;
                bitCode = AREA_EM0_BIT;
                addrPart = addressStr.StartsWith("EM") ? addressStr.Substring(2) : addressStr.Substring(1);
            }
            else if (char.IsDigit(addressStr[0]))
            {
                // 纯数字默认为CIO
                wordCode = AREA_CIO_WORD;
                bitCode = AREA_CIO_BIT;
                addrPart = addressStr;
            }
            else
            {
                throw new ArgumentException($"不支持的地址格式: {addressStr}");
            }

            if (addrPart.Contains("."))
            {
                var parts = addrPart.Split('.');
                address = ushort.Parse(parts[0]);
                bit = byte.Parse(parts[1]);
                hasBit = true;
                if (bit > 15)
                    throw new ArgumentException($"位号必须在0-15之间: {addressStr}");
            }
            else
            {
                address = ushort.Parse(addrPart);
            }

            DebugLog($"解析地址 '{addressStr}' => WordCode=0x{wordCode:X2}, Address={address}, Bit={bit}, HasBit={hasBit}");
            return (wordCode, bitCode, address, bit, hasBit);
        }

        #endregion

        #region 命令构建

        private byte GetNextSid()
        {
            return ++_sid;
        }

        /// <summary>
        /// 构建读取命令
        /// </summary>
        private byte[] BuildReadCommand(byte areaCode, ushort address, byte bit, ushort count)
        {
            // 计算长度
            // FINS头(10) + 命令码(2) + 参数(6) = 18
            int finsLen = 18;
            int tcpPayloadLen = 8 + finsLen;  // TCP命令头(8) + FINS数据

            byte[] cmd = new byte[16 + finsLen];  // TCP头(16) + FINS数据
            int i = 0;

            // TCP头
            cmd[i++] = 0x46; cmd[i++] = 0x49; cmd[i++] = 0x4E; cmd[i++] = 0x53;  // "FINS"
            cmd[i++] = (byte)(tcpPayloadLen >> 24);
            cmd[i++] = (byte)(tcpPayloadLen >> 16);
            cmd[i++] = (byte)(tcpPayloadLen >> 8);
            cmd[i++] = (byte)(tcpPayloadLen & 0xFF);
            cmd[i++] = 0x00; cmd[i++] = 0x00; cmd[i++] = 0x00; cmd[i++] = 0x02;  // FINS帧命令
            cmd[i++] = 0x00; cmd[i++] = 0x00; cmd[i++] = 0x00; cmd[i++] = 0x00;  // 错误码

            // FINS头
            cmd[i++] = 0x80;         // ICF: 需要响应
            cmd[i++] = 0x00;         // RSV
            cmd[i++] = 0x02;         // GCT
            cmd[i++] = 0x00;         // DNA: 目标网络
            cmd[i++] = _plcNode;     // DA1: 目标节点
            cmd[i++] = 0x00;         // DA2: 目标单元
            cmd[i++] = 0x00;         // SNA: 源网络
            cmd[i++] = _pcNode;      // SA1: 源节点
            cmd[i++] = 0x00;         // SA2: 源单元
            cmd[i++] = GetNextSid(); // SID

            // 命令码: 0101 = Memory Area Read
            cmd[i++] = 0x01;
            cmd[i++] = 0x01;

            // 参数
            cmd[i++] = areaCode;                      // 内存区域
            cmd[i++] = (byte)(address >> 8);          // 地址高字节
            cmd[i++] = (byte)(address & 0xFF);        // 地址低字节
            cmd[i++] = bit;                           // 位地址
            cmd[i++] = (byte)(count >> 8);            // 数量高字节
            cmd[i++] = (byte)(count & 0xFF);          // 数量低字节

            DebugLog($"构建读取命令: Area=0x{areaCode:X2}, Addr={address}, Bit={bit}, Count={count}");
            DebugLog($"完整命令: {BytesToHex(cmd)}");

            return cmd;
        }

        /// <summary>
        /// 构建写入命令
        /// </summary>
        private byte[] BuildWriteCommand(byte areaCode, ushort address, byte bit, byte[] data)
        {
            int count = data.Length / 2;
            int finsLen = 18 + data.Length;  // FINS头(10) + 命令码(2) + 参数(6) + 数据
            int tcpPayloadLen = 8 + finsLen;

            byte[] cmd = new byte[16 + finsLen];
            int i = 0;

            // TCP头
            cmd[i++] = 0x46; cmd[i++] = 0x49; cmd[i++] = 0x4E; cmd[i++] = 0x53;
            cmd[i++] = (byte)(tcpPayloadLen >> 24);
            cmd[i++] = (byte)(tcpPayloadLen >> 16);
            cmd[i++] = (byte)(tcpPayloadLen >> 8);
            cmd[i++] = (byte)(tcpPayloadLen & 0xFF);
            cmd[i++] = 0x00; cmd[i++] = 0x00; cmd[i++] = 0x00; cmd[i++] = 0x02;
            cmd[i++] = 0x00; cmd[i++] = 0x00; cmd[i++] = 0x00; cmd[i++] = 0x00;

            // FINS头
            cmd[i++] = 0x80;
            cmd[i++] = 0x00;
            cmd[i++] = 0x02;
            cmd[i++] = 0x00;
            cmd[i++] = _plcNode;
            cmd[i++] = 0x00;
            cmd[i++] = 0x00;
            cmd[i++] = _pcNode;
            cmd[i++] = 0x00;
            cmd[i++] = GetNextSid();

            // 命令码: 0102 = Memory Area Write
            cmd[i++] = 0x01;
            cmd[i++] = 0x02;

            // 参数
            cmd[i++] = areaCode;
            cmd[i++] = (byte)(address >> 8);
            cmd[i++] = (byte)(address & 0xFF);
            cmd[i++] = bit;
            cmd[i++] = (byte)(count >> 8);
            cmd[i++] = (byte)(count & 0xFF);

            // 数据
            Array.Copy(data, 0, cmd, i, data.Length);

            DebugLog($"构建写入命令: Area=0x{areaCode:X2}, Addr={address}, Bit={bit}, Count={count}");
            DebugLog($"写入数据: {BytesToHex(data)}");
            DebugLog($"完整命令: {BytesToHex(cmd)}");

            return cmd;
        }

        #endregion

        #region 通讯

        private async Task<byte[]> SendAndReceiveAsync(byte[] command, int expectedDataLen)
        {
            await _semaphore.WaitAsync();
            try
            {
                DebugLog($"发送: {BytesToHex(command)}");

                // 发送
                await _stream.WriteAsync(command, 0, command.Length);

                // 读取TCP头
                byte[] tcpHeader = new byte[16];
                await ReadExactAsync(tcpHeader, 0, 16);

                DebugLog($"收到TCP头: {BytesToHex(tcpHeader)}");

                // 验证FINS标识
                if (tcpHeader[0] != 0x46 || tcpHeader[1] != 0x49 ||
                    tcpHeader[2] != 0x4E || tcpHeader[3] != 0x53)
                {
                    throw new Exception("无效的FINS响应标识");
                }

                // 获取载荷长度
                // Length字段的值包含Command(4)+Error(4)+FINS帧
                // 我们已经在tcpHeader中读取了Command和Error，所以实际需要读取的是 Length-8
                int totalPayloadLen = (tcpHeader[4] << 24) | (tcpHeader[5] << 16) |
                                      (tcpHeader[6] << 8) | tcpHeader[7];
                int finsFrameLen = totalPayloadLen - 8;  // 减去已读取的Command+Error

                DebugLog($"载荷长度: {totalPayloadLen}, FINS帧长度: {finsFrameLen}");

                if (finsFrameLen < 0 || finsFrameLen > 65536)
                {
                    throw new Exception($"无效的FINS帧长度: {finsFrameLen}");
                }

                // 读取FINS帧
                byte[] finsFrame = new byte[finsFrameLen];
                if (finsFrameLen > 0)
                {
                    await ReadExactAsync(finsFrame, 0, finsFrameLen);
                }

                DebugLog($"收到FINS帧: {BytesToHex(finsFrame)}");

                // 组合响应: TCP头(16) + FINS帧
                byte[] response = new byte[16 + finsFrameLen];
                Array.Copy(tcpHeader, 0, response, 0, 16);
                if (finsFrameLen > 0)
                {
                    Array.Copy(finsFrame, 0, response, 16, finsFrameLen);
                }

                // 检查响应码 (在FINS帧中的位置: FINS头(10) + 命令码(2) = 12)
                if (finsFrameLen >= 14)
                {
                    byte mainCode = finsFrame[12];
                    byte subCode = finsFrame[13];
                    DebugLog($"响应码: Main=0x{mainCode:X2}, Sub=0x{subCode:X2}");

                    if (mainCode != 0x00 || subCode != 0x00)
                    {
                        string errorMsg = GetFinsErrorMessage(mainCode, subCode);
                        throw new Exception($"FINS错误: {errorMsg} (0x{mainCode:X2}{subCode:X2})");
                    }
                }

                return response;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task ReadExactAsync(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = await _stream.ReadAsync(buffer, offset + totalRead, count - totalRead);
                if (bytesRead == 0)
                    throw new Exception("连接已关闭");
                totalRead += bytesRead;
            }
        }

        private string GetFinsErrorMessage(byte mainCode, byte subCode)
        {
            switch (mainCode)
            {
                case 0x00: return subCode == 0x00 ? "成功" : $"警告(0x{subCode:X2})";
                case 0x01: return "服务取消";
                case 0x02: return "本地节点错误";
                case 0x03: return "目标节点错误";
                case 0x04: return "控制器错误";
                case 0x05: return "服务不支持";
                case 0x10: return "路由错误";
                case 0x11: return "命令格式错误";
                case 0x20: return "参数错误";
                case 0x21: return "读取长度超限";
                case 0x22: return "参数过长";
                case 0x23: return "参数不足";
                case 0x40: return "地址错误";
                default: return "未知错误";
            }
        }

        #endregion

        #region 读写实现

        public async Task<short> ReadWordAsync(string address)
        {
            if (!_isConnected)
                throw new InvalidOperationException("PLC未连接");

            try
            {
                var (wordCode, bitCode, addr, bit, hasBit) = ParseAddressEx(address);
                var command = BuildReadCommand(wordCode, addr, 0, 1);
                var response = await SendAndReceiveAsync(command, 2);

                // 数据位置: TCP头(16) + FINS头(10) + 命令码(2) + 响应码(2) = 30
                // 即 payload[14] 开始
                int dataOffset = 30;

                if (response.Length < dataOffset + 2)
                {
                    DebugLog($"响应太短: {response.Length} 字节");
                    throw new Exception("响应数据不完整");
                }

                // 大端序
                short value = (short)((response[dataOffset] << 8) | response[dataOffset + 1]);
                DebugLog($"读取 {address} = {value} (0x{(ushort)value:X4})");
                return value;
            }
            catch (Exception ex)
            {
                DebugLog($"读取字失败 {address}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> WriteWordAsync(string address, short value)
        {
            if (!_isConnected)
                return false;

            try
            {
                var (wordCode, bitCode, addr, bit, hasBit) = ParseAddressEx(address);

                // 大端序
                byte[] data = new byte[] { (byte)(value >> 8), (byte)(value & 0xFF) };

                DebugLog($"写入 {address} = {value} (0x{(ushort)value:X4})");

                var command = BuildWriteCommand(wordCode, addr, 0, data);
                await SendAndReceiveAsync(command, 0);
                return true;
            }
            catch (Exception ex)
            {
                DebugLog($"写入字失败 {address}: {ex.Message}");
                return false;
            }
        }

        public async Task<int> ReadDWordAsync(string address)
        {
            if (!_isConnected)
                throw new InvalidOperationException("PLC未连接");

            try
            {
                var (wordCode, bitCode, addr, bit, hasBit) = ParseAddressEx(address);
                var command = BuildReadCommand(wordCode, addr, 0, 2);
                var response = await SendAndReceiveAsync(command, 4);

                int dataOffset = 30;  // TCP头(16) + FINS头(10) + 命令码(2) + 响应码(2)

                // 欧姆龙PLC 32位数据: 低字在低地址，每字大端
                // 读取: [低字高字节, 低字低字节, 高字高字节, 高字低字节]
                // 需要转换为: [低字低字节, 低字高字节, 高字低字节, 高字高字节]
                byte[] bytes = new byte[4];
                bytes[0] = response[dataOffset + 1];  // 低字低字节
                bytes[1] = response[dataOffset];      // 低字高字节
                bytes[2] = response[dataOffset + 3];  // 高字低字节
                bytes[3] = response[dataOffset + 2];  // 高字高字节

                int value = BitConverter.ToInt32(bytes, 0);
                DebugLog($"读取双字 {address} = {value} (0x{value:X8})");
                return value;
            }
            catch (Exception ex)
            {
                DebugLog($"读取双字失败 {address}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> WriteDWordAsync(string address, int value)
        {
            if (!_isConnected)
                return false;

            try
            {
                var (wordCode, bitCode, addr, bit, hasBit) = ParseAddressEx(address);

                byte[] intBytes = BitConverter.GetBytes(value);

                // 转换为PLC格式: 低字(大端) + 高字(大端)
                byte[] data = new byte[4];
                data[0] = intBytes[1];  // 低字高字节
                data[1] = intBytes[0];  // 低字低字节
                data[2] = intBytes[3];  // 高字高字节
                data[3] = intBytes[2];  // 高字低字节

                DebugLog($"写入双字 {address} = {value}");

                var command = BuildWriteCommand(wordCode, addr, 0, data);
                await SendAndReceiveAsync(command, 0);
                return true;
            }
            catch (Exception ex)
            {
                DebugLog($"写入双字失败 {address}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReadBitAsync(string address)
        {
            if (!_isConnected)
                throw new InvalidOperationException("PLC未连接");

            try
            {
                var (wordCode, bitCode, addr, bit, hasBit) = ParseAddressEx(address);
                if (!hasBit) bit = 0;

                // 读取整字然后提取位
                var command = BuildReadCommand(wordCode, addr, 0, 1);
                var response = await SendAndReceiveAsync(command, 2);

                int dataOffset = 30;  // TCP头(16) + FINS头(10) + 命令码(2) + 响应码(2)
                ushort word = (ushort)((response[dataOffset] << 8) | response[dataOffset + 1]);

                bool value = ((word >> bit) & 1) == 1;
                DebugLog($"读取位 {address} = {value}");
                return value;
            }
            catch (Exception ex)
            {
                DebugLog($"读取位失败 {address}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> WriteBitAsync(string address, bool value)
        {
            if (!_isConnected)
                return false;

            try
            {
                var (wordCode, bitCode, addr, bit, hasBit) = ParseAddressEx(address);
                if (!hasBit) bit = 0;

                // 读取-修改-写入
                var readCmd = BuildReadCommand(wordCode, addr, 0, 1);
                var response = await SendAndReceiveAsync(readCmd, 2);

                int dataOffset = 30;  // TCP头(16) + FINS头(10) + 命令码(2) + 响应码(2)
                ushort word = (ushort)((response[dataOffset] << 8) | response[dataOffset + 1]);

                if (value)
                    word |= (ushort)(1 << bit);
                else
                    word &= (ushort)~(1 << bit);

                byte[] data = new byte[] { (byte)(word >> 8), (byte)(word & 0xFF) };

                DebugLog($"写入位 {address} = {value}");

                var writeCmd = BuildWriteCommand(wordCode, addr, 0, data);
                await SendAndReceiveAsync(writeCmd, 0);
                return true;
            }
            catch (Exception ex)
            {
                DebugLog($"写入位失败 {address}: {ex.Message}");
                return false;
            }
        }

        public async Task<float> ReadFloatAsync(string address)
        {
            int intBits = await ReadDWordAsync(address);
            return BitConverter.ToSingle(BitConverter.GetBytes(intBits), 0);
        }

        public async Task<bool> WriteFloatAsync(string address, float value)
        {
            int intBits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            return await WriteDWordAsync(address, intBits);
        }

        public async Task<ushort> ReadUIntAsync(string address)
        {
            return (ushort)await ReadWordAsync(address);
        }

        public async Task<bool> WriteUIntAsync(string address, ushort value)
        {
            return await WriteWordAsync(address, (short)value);
        }

        public async Task<uint> ReadUDIntAsync(string address)
        {
            return (uint)await ReadDWordAsync(address);
        }

        public async Task<bool> WriteUDIntAsync(string address, uint value)
        {
            return await WriteDWordAsync(address, (int)value);
        }

        public async Task<long> ReadLIntAsync(string address)
        {
            if (!_isConnected)
                throw new InvalidOperationException("PLC未连接");

            try
            {
                var (wordCode, bitCode, addr, bit, hasBit) = ParseAddressEx(address);
                var command = BuildReadCommand(wordCode, addr, 0, 4);
                var response = await SendAndReceiveAsync(command, 8);

                int dataOffset = 30;  // TCP头(16) + FINS头(10) + 命令码(2) + 响应码(2)

                byte[] bytes = new byte[8];
                // 转换: 4个字，每字大端，低字在前
                bytes[0] = response[dataOffset + 1];
                bytes[1] = response[dataOffset];
                bytes[2] = response[dataOffset + 3];
                bytes[3] = response[dataOffset + 2];
                bytes[4] = response[dataOffset + 5];
                bytes[5] = response[dataOffset + 4];
                bytes[6] = response[dataOffset + 7];
                bytes[7] = response[dataOffset + 6];

                return BitConverter.ToInt64(bytes, 0);
            }
            catch (Exception ex)
            {
                DebugLog($"读取64位整数失败 {address}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> WriteLIntAsync(string address, long value)
        {
            if (!_isConnected)
                return false;

            try
            {
                var (wordCode, bitCode, addr, bit, hasBit) = ParseAddressEx(address);

                byte[] longBytes = BitConverter.GetBytes(value);
                byte[] data = new byte[8];
                data[0] = longBytes[1]; data[1] = longBytes[0];
                data[2] = longBytes[3]; data[3] = longBytes[2];
                data[4] = longBytes[5]; data[5] = longBytes[4];
                data[6] = longBytes[7]; data[7] = longBytes[6];

                var command = BuildWriteCommand(wordCode, addr, 0, data);
                await SendAndReceiveAsync(command, 0);
                return true;
            }
            catch (Exception ex)
            {
                DebugLog($"写入64位整数失败 {address}: {ex.Message}");
                return false;
            }
        }

        public async Task<ulong> ReadULIntAsync(string address)
        {
            return (ulong)await ReadLIntAsync(address);
        }

        public async Task<bool> WriteULIntAsync(string address, ulong value)
        {
            return await WriteLIntAsync(address, (long)value);
        }

        public async Task<double> ReadLRealAsync(string address)
        {
            long longBits = await ReadLIntAsync(address);
            return BitConverter.ToDouble(BitConverter.GetBytes(longBits), 0);
        }

        public async Task<bool> WriteLRealAsync(string address, double value)
        {
            long longBits = BitConverter.ToInt64(BitConverter.GetBytes(value), 0);
            return await WriteLIntAsync(address, longBits);
        }

        #endregion

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Disconnect();

            try { _semaphore?.Dispose(); } catch { }
            try { _heartbeatCts?.Dispose(); } catch { }
            try { _disposeCts?.Dispose(); } catch { }
        }
    }
}