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
    /// 度申线扫相机实现
    /// </summary>
    public class DushenLineCamera : ILineCamera
    {
        private uint _handle;
        private bool _isConnected;
        private bool _isGrabbing;
        private int _exposure = 2000;
        private double _gain = 1.0;
        private int _lineRate = 10000;
        private int _lineCount = 4096;
        private TriggerSource _currentTriggerSource = TriggerSource.Continuous;
        private TriggerEdge _triggerEdge = TriggerEdge.RisingEdge;
        private CancellationTokenSource _grabCts;
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

        public DushenLineCamera()
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
                uint count = 0;
                var status = DvpCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK || count == 0)
                {
                    return cameras.ToArray();
                }

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
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申线扫相机搜索失败: {ex.Message}", ex, "DushenLineCamera");
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
                    FileLogger.Instance.Warning("度申线扫相机连接失败: UserId为空", "DushenLineCamera");
                    return false;
                }

                uint count = 0;
                var status = DvpCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK || count == 0)
                {
                    FileLogger.Instance.Warning($"度申线扫相机连接失败: 未找到任何设备", "DushenLineCamera");
                    return false;
                }

                uint handle = 0;
                status = DvpCamera.dvpOpenByUserId(userId, dvpOpenMode.OPEN_NORMAL, ref handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申线扫相机通过UserId '{userId}' 打开失败: {status}", null, "DushenLineCamera");
                    return false;
                }

                _handle = handle;
                UserId = userId;

                GetCameraInfo();
                InitializeCamera();

                _isConnected = true;
                FileLogger.Instance.Info($"度申线扫相机已连接 (UserId: {UserId}, FriendlyName: {FriendlyName})", "DushenLineCamera");

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = true,
                    Message = "度申线扫相机已连接"
                });

                return true;
            }
            catch (DllNotFoundException ex)
            {
                FileLogger.Instance.Error($"度申相机SDK未安装或DLL未找到: {ex.Message}", ex, "DushenLineCamera");
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申线扫相机连接失败: {ex.Message}", ex, "DushenLineCamera");
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
                uint count = 0;
                var status = DvpCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK || count == 0)
                {
                    FileLogger.Instance.Warning($"度申线扫相机连接失败: 未找到任何设备", "DushenLineCamera");
                    return false;
                }

                if (index < 0 || index >= count)
                {
                    FileLogger.Instance.Error($"度申线扫相机连接失败: 索引 {index} 超出范围", null, "DushenLineCamera");
                    return false;
                }

                dvpCameraInfo info = new dvpCameraInfo();
                status = DvpCamera.dvpEnum((uint)index, ref info);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申线扫相机枚举失败: {status}", null, "DushenLineCamera");
                    return false;
                }

                uint handle = 0;
                status = DvpCamera.dvpOpen((uint)index, dvpOpenMode.OPEN_NORMAL, ref handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申线扫相机打开失败: {status}", null, "DushenLineCamera");
                    return false;
                }

                _handle = handle;
                FriendlyName = info.FriendlyName;
                UserId = info.UserId;
                SerialNumber = info.SerialNumber;

                InitializeCamera();

                _isConnected = true;
                FileLogger.Instance.Info($"度申线扫相机已连接 (Index: {index}, FriendlyName: {FriendlyName})", "DushenLineCamera");

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = true,
                    Message = "度申线扫相机已连接"
                });

                return true;
            }
            catch (DllNotFoundException ex)
            {
                FileLogger.Instance.Error($"度申相机SDK未安装或DLL未找到: {ex.Message}", ex, "DushenLineCamera");
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申线扫相机连接失败: {ex.Message}", ex, "DushenLineCamera");
                return false;
            }
        }

        private void GetCameraInfo()
        {
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
                FileLogger.Instance.Warning($"度申线扫相机注册流回调失败: {status}", "DushenLineCamera");
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

            // 设置线扫参数
            DvpCamera.dvpSetLineRate(_handle, _lineRate);
            DvpCamera.dvpSetHeight(_handle, _lineCount);
        }

        public void Disconnect()
        {
            try
            {
                if (!_isConnected)
                    return;

                StopGrab();

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
                FileLogger.Instance.Info($"度申线扫相机已断开 (UserId: {UserId})", "DushenLineCamera");

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = false,
                    Message = "线扫相机已断开"
                });
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申线扫相机断开失败: {ex.Message}", ex, "DushenLineCamera");
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
                    FileLogger.Instance.Error($"度申线扫相机启动视频流失败: {status}", null, "DushenLineCamera");
                    return false;
                }

                _isGrabbing = true;
                FileLogger.Instance.Info($"度申线扫相机开始采集, 触发源: {_currentTriggerSource}", "DushenLineCamera");

                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申线扫相机开始采集失败: {ex.Message}", ex, "DushenLineCamera");
                return false;
            }
        }

        private void ConfigureTrigger(TriggerSource source)
        {
            switch (source)
            {
                case TriggerSource.Continuous:
                    DvpCamera.dvpSetTriggerState(_handle, false);
                    break;

                case TriggerSource.Software:
                    DvpCamera.dvpSetTriggerState(_handle, true);
                    DvpCamera.dvpSetTriggerSource(_handle, dvpTriggerSource.TRIGGER_SOURCE_SOFTWARE);
                    break;

                case TriggerSource.Line0:
                case TriggerSource.Line1:
                case TriggerSource.Line2:
                case TriggerSource.Line3:
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

                if (_handle != 0)
                {
                    DvpCamera.dvpStop(_handle);
                }

                _isGrabbing = false;
                FileLogger.Instance.Info($"度申线扫相机停止采集", "DushenLineCamera");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申线扫相机停止采集失败: {ex.Message}", ex, "DushenLineCamera");
            }
        }

        public bool SoftTrigger()
        {
            if (!_isConnected || !_isGrabbing)
                return false;

            if (_currentTriggerSource != TriggerSource.Software)
            {
                FileLogger.Instance.Warning("度申线扫相机软触发失败: 当前不是软件触发模式", "DushenLineCamera");
                return false;
            }

            try
            {
                var status = DvpCamera.dvpTriggerFire(_handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Warning($"度申线扫相机软触发失败: {status}", "DushenLineCamera");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申线扫相机软触发失败: {ex.Message}", ex, "DushenLineCamera");
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
                        FileLogger.Instance.Warning($"度申线扫相机设置曝光失败: {status}", "DushenLineCamera");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置曝光失败: {ex.Message}", ex, "DushenLineCamera");
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
                        FileLogger.Instance.Warning($"度申线扫相机设置增益失败: {status}", "DushenLineCamera");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置增益失败: {ex.Message}", ex, "DushenLineCamera");
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

                if (_isConnected && _isGrabbing)
                {
                    ConfigureTrigger(source);
                }

                FileLogger.Instance.Debug($"度申线扫相机设置触发源: {source}", "DushenLineCamera");
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置触发源失败: {ex.Message}", ex, "DushenLineCamera");
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
                        FileLogger.Instance.Warning($"度申线扫相机设置触发边沿失败: {status}", "DushenLineCamera");
                        return false;
                    }
                }
                FileLogger.Instance.Debug($"度申线扫相机设置触发边沿: {edge}", "DushenLineCamera");
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置触发边沿失败: {ex.Message}", ex, "DushenLineCamera");
                return false;
            }
        }

        public TriggerEdge GetTriggerEdge() => _triggerEdge;

        public bool SetLineRate(int lineRate)
        {
            try
            {
                _lineRate = lineRate;
                if (_isConnected && _handle != 0)
                {
                    var status = DvpCamera.dvpSetLineRate(_handle, lineRate);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"度申线扫相机设置行频失败: {status}", "DushenLineCamera");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置行频失败: {ex.Message}", ex, "DushenLineCamera");
                return false;
            }
        }

        public int GetLineRate()
        {
            if (_isConnected && _handle != 0)
            {
                double lineRate = 0;
                var status = DvpCamera.dvpGetLineRate(_handle, ref lineRate);
                if (status == dvpStatus.DVP_STATUS_OK)
                {
                    _lineRate = (int)lineRate;
                }
            }
            return _lineRate;
        }

        public bool SetLineCount(int lineCount)
        {
            try
            {
                _lineCount = lineCount;
                if (_isConnected && _handle != 0)
                {
                    var status = DvpCamera.dvpSetHeight(_handle, lineCount);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"度申线扫相机设置采集行数失败: {status}", "DushenLineCamera");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置采集行数失败: {ex.Message}", ex, "DushenLineCamera");
                return false;
            }
        }

        public int GetLineCount()
        {
            if (_isConnected && _handle != 0)
            {
                int height = 0;
                var status = DvpCamera.dvpGetHeight(_handle, ref height);
                if (status == dvpStatus.DVP_STATUS_OK)
                {
                    _lineCount = height;
                }
            }
            return _lineCount;
        }

        private bool IsHardwareTrigger(TriggerSource source)
        {
            return source == TriggerSource.Line0 || source == TriggerSource.Line1 ||
                   source == TriggerSource.Line2 || source == TriggerSource.Line3;
        }

        private void OnStreamCallback(uint handle, dvpStreamEvent eventType, IntPtr pContext, ref dvpFrame pFrame, IntPtr pBuffer)
        {
            try
            {
                if (pBuffer == IntPtr.Zero || pFrame.width <= 0 || pFrame.height <= 0)
                    return;

                var pixelFormat = pFrame.bits == 8 ? PixelFormat.Format8bppIndexed : PixelFormat.Format24bppRgb;
                var stride = pFrame.width * (pFrame.bits / 8);
                if (pFrame.bits == 24)
                    stride = pFrame.width * 3;

                var bitmap = new Bitmap(pFrame.width, pFrame.height, stride, pixelFormat, pBuffer);

                if (pixelFormat == PixelFormat.Format8bppIndexed)
                {
                    var palette = bitmap.Palette;
                    for (int i = 0; i < 256; i++)
                    {
                        palette.Entries[i] = Color.FromArgb(i, i, i);
                    }
                    bitmap.Palette = palette;
                }

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
                FileLogger.Instance.Error($"度申线扫相机图像回调处理失败: {ex.Message}", ex, "DushenLineCamera");
            }
        }

        private void OnEventCallback(uint handle, dvpEvent eventType, IntPtr pContext, int param)
        {
            if (eventType == dvpEvent.EVENT_DISCONNECTED)
            {
                FileLogger.Instance.Warning($"度申线扫相机断开连接事件", "DushenLineCamera");
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
