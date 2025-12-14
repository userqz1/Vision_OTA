using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VisionOTA.Hardware.Camera.Dushen;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Hardware.Camera
{
    /// <summary>
    /// 度申面阵相机实现
    /// </summary>
    public class DushenAreaCamera : ICamera
    {
        private uint _handle;
        private bool _isConnected;
        private bool _isGrabbing;
        private int _exposure = 5000;
        private double _gain = 1.0;
        private TriggerSource _currentTriggerSource = TriggerSource.Continuous;
        private TriggerEdge _triggerEdge = TriggerEdge.RisingEdge;
        private CancellationTokenSource _grabCts;
        private Task _continuousGrabTask;
        private dvpStreamCallback _streamCallback;
        private dvpEventCallback _eventCallback;
        private GCHandle _callbackHandle;

        public string FriendlyName { get; private set; }
        public string UserId { get; private set; }
        public string SerialNumber { get; private set; }
        public string IPAddress { get; private set; }
        public bool IsConnected => _isConnected;
        public bool IsGrabbing => _isGrabbing;
        public TriggerSource CurrentTriggerSource => _currentTriggerSource;

        public event EventHandler<ImageReceivedEventArgs> ImageReceived;
        public event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;

        public DushenAreaCamera()
        {
            FriendlyName = "";
            UserId = "";
            SerialNumber = "";
            IPAddress = "";
        }

        /// <summary>
        /// 搜索相机
        /// </summary>
        public CameraInfo[] SearchCameras()
        {
            var cameras = new List<CameraInfo>();

            try
            {
                // 刷新相机列表
                uint count = 0;
                var status = DvpCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK || count == 0)
                {
                    FileLogger.Instance.Debug($"度申相机搜索: 未找到设备 (status={status}, count={count})", "DushenCamera");
                    return cameras.ToArray();
                }

                // 枚举所有相机
                for (uint i = 0; i < count; i++)
                {
                    dvpCameraInfo info = new dvpCameraInfo();
                    status = DvpCamera.dvpEnum(i, ref info);
                    if (status == dvpStatus.DVP_STATUS_OK)
                    {
                        cameras.Add(new CameraInfo
                        {
                            FriendlyName = info.FriendlyName,
                            UserId = info.UserId,
                            SerialNumber = info.SerialNumber,
                            Index = (int)i
                        });
                        FileLogger.Instance.Debug($"发现相机[{i}]: {info.FriendlyName}, UserId: {info.UserId}, SN: {info.SerialNumber}", "DushenCamera");
                    }
                }
            }
            catch (DllNotFoundException ex)
            {
                FileLogger.Instance.Error($"度申相机SDK未安装或DLL未找到: {ex.Message}", ex, "DushenCamera");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机搜索失败: {ex.Message}", ex, "DushenCamera");
            }

            return cameras.ToArray();
        }

        /// <summary>
        /// 通过UserId连接相机
        /// </summary>
        public bool Connect(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    FileLogger.Instance.Warning("度申相机连接失败: UserId为空", "DushenCamera");
                    return false;
                }

                // 刷新相机列表
                uint count = 0;
                var status = DvpCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK || count == 0)
                {
                    FileLogger.Instance.Warning($"度申相机连接失败: 未找到任何设备", "DushenCamera");
                    return false;
                }

                // 使用dvpOpenByUserId打开相机
                uint handle = 0;
                status = DvpCamera.dvpOpenByUserId(userId, dvpOpenMode.OPEN_NORMAL, ref handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申相机通过UserId '{userId}' 打开失败: {status}", null, "DushenCamera");
                    return false;
                }

                _handle = handle;
                UserId = userId;

                // 获取相机信息
                GetCameraInfo();

                // 初始化相机
                InitializeCamera();

                _isConnected = true;
                FileLogger.Instance.Info($"度申相机已连接 (UserId: {UserId}, FriendlyName: {FriendlyName})", "DushenCamera");

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = true,
                    Message = "度申相机已连接"
                });

                return true;
            }
            catch (DllNotFoundException ex)
            {
                FileLogger.Instance.Error($"度申相机SDK未安装或DLL未找到: {ex.Message}", ex, "DushenCamera");
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机连接失败: {ex.Message}", ex, "DushenCamera");
                return false;
            }
        }

        /// <summary>
        /// 通过枚举索引连接相机
        /// </summary>
        public bool ConnectByIndex(int index)
        {
            try
            {
                // 刷新相机列表
                uint count = 0;
                var status = DvpCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK || count == 0)
                {
                    FileLogger.Instance.Warning($"度申相机连接失败: 未找到任何设备", "DushenCamera");
                    return false;
                }

                if (index < 0 || index >= count)
                {
                    FileLogger.Instance.Error($"度申相机连接失败: 索引 {index} 超出范围 (0-{count - 1})", null, "DushenCamera");
                    return false;
                }

                // 获取相机信息
                dvpCameraInfo info = new dvpCameraInfo();
                status = DvpCamera.dvpEnum((uint)index, ref info);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申相机枚举失败: {status}", null, "DushenCamera");
                    return false;
                }

                // 打开相机
                uint handle = 0;
                status = DvpCamera.dvpOpen((uint)index, dvpOpenMode.OPEN_NORMAL, ref handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申相机打开失败: {status}", null, "DushenCamera");
                    return false;
                }

                _handle = handle;
                FriendlyName = info.FriendlyName;
                UserId = info.UserId;
                SerialNumber = info.SerialNumber;

                // 初始化相机
                InitializeCamera();

                _isConnected = true;
                FileLogger.Instance.Info($"度申相机已连接 (Index: {index}, FriendlyName: {FriendlyName})", "DushenCamera");

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = true,
                    Message = "度申相机已连接"
                });

                return true;
            }
            catch (DllNotFoundException ex)
            {
                FileLogger.Instance.Error($"度申相机SDK未安装或DLL未找到: {ex.Message}", ex, "DushenCamera");
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机连接失败: {ex.Message}", ex, "DushenCamera");
                return false;
            }
        }

        private void GetCameraInfo()
        {
            // 尝试获取相机信息（通过重新枚举）
            try
            {
                uint count = 0;
                DvpCamera.dvpRefresh(ref count);
                for (uint i = 0; i < count; i++)
                {
                    dvpCameraInfo info = new dvpCameraInfo();
                    if (DvpCamera.dvpEnum(i, ref info) == dvpStatus.DVP_STATUS_OK)
                    {
                        if (info.UserId == UserId)
                        {
                            FriendlyName = info.FriendlyName;
                            SerialNumber = info.SerialNumber;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private void InitializeCamera()
        {
            // 注册流回调
            _streamCallback = OnStreamCallback;
            _callbackHandle = GCHandle.Alloc(this);
            var status = DvpCamera.dvpRegisterStreamCallback(_handle, _streamCallback, dvpStreamEvent.CYCLOBUFFER_ISP, GCHandle.ToIntPtr(_callbackHandle));
            if (status != dvpStatus.DVP_STATUS_OK)
            {
                FileLogger.Instance.Warning($"度申相机注册流回调失败: {status}", "DushenCamera");
            }

            // 注册事件回调
            _eventCallback = OnEventCallback;
            DvpCamera.dvpRegisterEventCallback(_handle, _eventCallback, dvpEvent.EVENT_DISCONNECTED, GCHandle.ToIntPtr(_callbackHandle));

            // 关闭自动曝光
            DvpCamera.dvpSetAeOperation(_handle, dvpAeOperation.AE_OP_OFF);
            DvpCamera.dvpSetAntiFlick(_handle, dvpAntiFlick.ANTIFLICK_DISABLE);

            // 设置初始参数
            DvpCamera.dvpSetExposure(_handle, _exposure);
            DvpCamera.dvpSetAnalogGain(_handle, _gain);
        }

        public void Disconnect()
        {
            try
            {
                if (!_isConnected)
                    return;

                StopGrab();

                // 关闭相机
                if (_handle != 0)
                {
                    DvpCamera.dvpClose(_handle);
                    _handle = 0;
                }

                if (_callbackHandle.IsAllocated)
                {
                    _callbackHandle.Free();
                }

                _isConnected = false;
                FileLogger.Instance.Info($"度申相机已断开 (UserId: {UserId})", "DushenCamera");

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = false,
                    Message = "相机已断开"
                });
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机断开失败: {ex.Message}", ex, "DushenCamera");
            }
        }

        public bool StartGrab()
        {
            if (!_isConnected || _isGrabbing)
                return false;

            try
            {
                _grabCts = new CancellationTokenSource();

                // 根据触发源配置
                ConfigureTrigger(_currentTriggerSource);

                // 启动视频流
                var status = DvpCamera.dvpStart(_handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申相机启动视频流失败: {status}", null, "DushenCamera");
                    return false;
                }

                _isGrabbing = true;
                FileLogger.Instance.Info($"度申相机开始采集, 触发源: {_currentTriggerSource}", "DushenCamera");

                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机开始采集失败: {ex.Message}", ex, "DushenCamera");
                return false;
            }
        }

        private void ConfigureTrigger(TriggerSource source)
        {
            switch (source)
            {
                case TriggerSource.Continuous:
                    // 连续模式：关闭触发，内部连续出图
                    DvpCamera.dvpSetTriggerState(_handle, false);
                    break;

                case TriggerSource.Software:
                    // 软件触发模式
                    DvpCamera.dvpSetTriggerState(_handle, true);
                    DvpCamera.dvpSetTriggerSource(_handle, dvpTriggerSource.TRIGGER_SOURCE_SOFTWARE);
                    break;

                case TriggerSource.Line0:
                case TriggerSource.Line1:
                case TriggerSource.Line2:
                case TriggerSource.Line3:
                    // 硬件触发模式
                    DvpCamera.dvpSetTriggerState(_handle, true);
                    DvpCamera.dvpSetTriggerSource(_handle, ConvertTriggerSource(source));
                    DvpCamera.dvpSetTriggerInputType(_handle, ConvertTriggerEdge(_triggerEdge));
                    break;
            }
        }

        public void StopGrab()
        {
            if (!_isGrabbing)
                return;

            try
            {
                _grabCts?.Cancel();

                if (_continuousGrabTask != null)
                {
                    _continuousGrabTask.Wait(1000);
                    _continuousGrabTask = null;
                }

                // 停止视频流
                if (_handle != 0)
                {
                    DvpCamera.dvpStop(_handle);
                }

                _isGrabbing = false;
                FileLogger.Instance.Info($"度申相机停止采集", "DushenCamera");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机停止采集失败: {ex.Message}", ex, "DushenCamera");
            }
        }

        public bool SoftTrigger()
        {
            if (!_isConnected || !_isGrabbing)
                return false;

            if (_currentTriggerSource != TriggerSource.Software)
            {
                FileLogger.Instance.Warning("度申相机软触发失败: 当前不是软件触发模式", "DushenCamera");
                return false;
            }

            try
            {
                var status = DvpCamera.dvpTriggerFire(_handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Warning($"度申相机软触发失败: {status}", "DushenCamera");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机软触发失败: {ex.Message}", ex, "DushenCamera");
                return false;
            }
        }

        public bool SetExposure(int exposureUs)
        {
            try
            {
                _exposure = exposureUs;
                if (_isConnected && _handle != 0)
                {
                    var status = DvpCamera.dvpSetExposure(_handle, exposureUs);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"度申相机设置曝光失败: {status}", "DushenCamera");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机设置曝光失败: {ex.Message}", ex, "DushenCamera");
                return false;
            }
        }

        public int GetExposure()
        {
            if (_isConnected && _handle != 0)
            {
                double exposure = 0;
                var status = DvpCamera.dvpGetExposure(_handle, ref exposure);
                if (status == dvpStatus.DVP_STATUS_OK)
                {
                    _exposure = (int)exposure;
                }
            }
            return _exposure;
        }

        public bool SetGain(double gain)
        {
            try
            {
                _gain = gain;
                if (_isConnected && _handle != 0)
                {
                    var status = DvpCamera.dvpSetAnalogGain(_handle, gain);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"度申相机设置增益失败: {status}", "DushenCamera");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机设置增益失败: {ex.Message}", ex, "DushenCamera");
                return false;
            }
        }

        public double GetGain()
        {
            if (_isConnected && _handle != 0)
            {
                double gain = 0;
                var status = DvpCamera.dvpGetAnalogGain(_handle, ref gain);
                if (status == dvpStatus.DVP_STATUS_OK)
                {
                    _gain = gain;
                }
            }
            return _gain;
        }

        public bool SetTriggerSource(TriggerSource source)
        {
            try
            {
                _currentTriggerSource = source;

                // 如果正在采集，需要重新配置
                if (_isConnected && _isGrabbing)
                {
                    ConfigureTrigger(source);
                }

                FileLogger.Instance.Debug($"度申相机设置触发源: {source}", "DushenCamera");
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机设置触发源失败: {ex.Message}", ex, "DushenCamera");
                return false;
            }
        }

        public TriggerSource GetTriggerSource() => _currentTriggerSource;

        public bool SetTriggerEdge(TriggerEdge edge)
        {
            try
            {
                _triggerEdge = edge;
                if (_isConnected && _handle != 0 && IsHardwareTrigger(_currentTriggerSource))
                {
                    var status = DvpCamera.dvpSetTriggerInputType(_handle, ConvertTriggerEdge(edge));
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"度申相机设置触发边沿失败: {status}", "DushenCamera");
                        return false;
                    }
                }
                FileLogger.Instance.Debug($"度申相机设置触发边沿: {edge}", "DushenCamera");
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机设置触发边沿失败: {ex.Message}", ex, "DushenCamera");
                return false;
            }
        }

        public TriggerEdge GetTriggerEdge() => _triggerEdge;

        private bool IsHardwareTrigger(TriggerSource source)
        {
            return source == TriggerSource.Line0 || source == TriggerSource.Line1 ||
                   source == TriggerSource.Line2 || source == TriggerSource.Line3;
        }

        /// <summary>
        /// 流回调处理
        /// </summary>
        private void OnStreamCallback(uint handle, dvpStreamEvent eventType, IntPtr pContext, ref dvpFrame pFrame, IntPtr pBuffer)
        {
            try
            {
                if (pBuffer == IntPtr.Zero || pFrame.width <= 0 || pFrame.height <= 0)
                    return;

                // 创建Bitmap
                var pixelFormat = pFrame.bits == 8 ? PixelFormat.Format8bppIndexed : PixelFormat.Format24bppRgb;
                var stride = pFrame.width * (pFrame.bits / 8);
                if (pFrame.bits == 24)
                    stride = pFrame.width * 3;

                var bitmap = new Bitmap(pFrame.width, pFrame.height, stride, pixelFormat, pBuffer);

                // 如果是8位灰度图，设置调色板
                if (pixelFormat == PixelFormat.Format8bppIndexed)
                {
                    var palette = bitmap.Palette;
                    for (int i = 0; i < 256; i++)
                    {
                        palette.Entries[i] = Color.FromArgb(i, i, i);
                    }
                    bitmap.Palette = palette;
                }

                // 复制一份避免内存问题
                var clone = (Bitmap)bitmap.Clone();
                bitmap.Dispose();

                ImageReceived?.Invoke(this, new ImageReceivedEventArgs
                {
                    Image = clone,
                    Width = pFrame.width,
                    Height = pFrame.height,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申相机图像回调处理失败: {ex.Message}", ex, "DushenCamera");
            }
        }

        /// <summary>
        /// 事件回调处理
        /// </summary>
        private void OnEventCallback(uint handle, dvpEvent eventType, IntPtr pContext, int param)
        {
            if (eventType == dvpEvent.EVENT_DISCONNECTED)
            {
                FileLogger.Instance.Warning($"度申相机断开连接事件", "DushenCamera");
                _isConnected = false;
                _isGrabbing = false;

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = false,
                    Message = "相机意外断开"
                });
            }
        }

        private dvpTriggerSource ConvertTriggerSource(TriggerSource source)
        {
            switch (source)
            {
                case TriggerSource.Software:
                    return dvpTriggerSource.TRIGGER_SOURCE_SOFTWARE;
                case TriggerSource.Line0:
                    return dvpTriggerSource.TRIGGER_SOURCE_LINE0;
                case TriggerSource.Line1:
                    return dvpTriggerSource.TRIGGER_SOURCE_LINE1;
                case TriggerSource.Line2:
                    return dvpTriggerSource.TRIGGER_SOURCE_LINE2;
                case TriggerSource.Line3:
                    return dvpTriggerSource.TRIGGER_SOURCE_LINE3;
                default:
                    return dvpTriggerSource.TRIGGER_SOURCE_LINE0;
            }
        }

        private dvpTriggerInputType ConvertTriggerEdge(TriggerEdge edge)
        {
            switch (edge)
            {
                case TriggerEdge.RisingEdge:
                    return dvpTriggerInputType.TRIGGER_INPUT_RISING_EDGE;
                case TriggerEdge.FallingEdge:
                    return dvpTriggerInputType.TRIGGER_INPUT_FALLING_EDGE;
                case TriggerEdge.DoubleEdge:
                    return dvpTriggerInputType.TRIGGER_INPUT_DOUBLE_EDGE;
                default:
                    return dvpTriggerInputType.TRIGGER_INPUT_RISING_EDGE;
            }
        }

        public void Dispose()
        {
            Disconnect();
            _grabCts?.Dispose();
        }
    }
}
