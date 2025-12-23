using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using DVPCameraType;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Hardware.Camera
{
    /// <summary>
    /// 度申相机基类 - 封装面阵/线扫相机的公共功能
    /// </summary>
    public abstract class DushenCameraBase : ICamera
    {
        protected uint _handle;
        protected bool _isConnected;
        protected bool _isGrabbing;
        protected int _exposure;
        protected float _gain = 1.0f;
        protected TriggerSource _currentTriggerSource = TriggerSource.Continuous;
        protected TriggerEdge _triggerEdge = TriggerEdge.RisingEdge;
        protected CancellationTokenSource _grabCts;
        protected DVPCamera.dvpStreamCallback _streamCallback;

        /// <summary>
        /// 相机类型名称，用于日志
        /// </summary>
        protected abstract string CameraTypeName { get; }

        /// <summary>
        /// 默认曝光值
        /// </summary>
        protected abstract int DefaultExposure { get; }

        public string FriendlyName { get; protected set; }
        public string UserId { get; protected set; }
        public string SerialNumber { get; protected set; }
        public string IPAddress { get; protected set; }
        public bool IsConnected => _isConnected;
        public bool IsGrabbing => _isGrabbing;
        public TriggerSource CurrentTriggerSource => _currentTriggerSource;

        public event EventHandler<ImageReceivedEventArgs> ImageReceived;
        public event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;

        protected DushenCameraBase()
        {
            FriendlyName = "";
            UserId = "";
            SerialNumber = "";
            IPAddress = "";
            _exposure = DefaultExposure;
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
                var status = DVPCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK || count == 0)
                {
                    return cameras.ToArray();
                }

                for (uint i = 0; i < count; i++)
                {
                    dvpCameraInfo info = new dvpCameraInfo();
                    status = DVPCamera.dvpEnum(i, ref info);
                    if (status == dvpStatus.DVP_STATUS_OK)
                    {
                        cameras.Add(new CameraInfo
                        {
                            FriendlyName = info.FriendlyName,
                            UserId = info.UserID,
                            SerialNumber = info.SerialNumber,
                            Index = (int)i
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}搜索失败: {ex.Message}", ex, CameraTypeName);
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
                    FileLogger.Instance.Warning($"{CameraTypeName}连接失败: UserId为空", CameraTypeName);
                    return false;
                }

                uint count = 0;
                var status = DVPCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK || count == 0)
                {
                    FileLogger.Instance.Warning($"{CameraTypeName}连接失败: 未找到任何设备", CameraTypeName);
                    return false;
                }

                uint handle = 0;
                status = DVPCamera.dvpOpenByUserId(userId, dvpOpenMode.OPEN_NORMAL, ref handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"{CameraTypeName}通过UserId '{userId}' 打开失败: {status}", null, CameraTypeName);
                    return false;
                }

                _handle = handle;
                UserId = userId;

                GetCameraInfo();
                InitializeCamera();

                _isConnected = true;
                FileLogger.Instance.Info($"{CameraTypeName}已连接 (UserId: {UserId}, FriendlyName: {FriendlyName})", CameraTypeName);

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = true,
                    Message = $"{CameraTypeName}已连接"
                });

                return true;
            }
            catch (DllNotFoundException ex)
            {
                FileLogger.Instance.Error($"度申相机SDK未安装或DLL未找到: {ex.Message}", ex, CameraTypeName);
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}连接失败: {ex.Message}", ex, CameraTypeName);
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
                var status = DVPCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK || count == 0)
                {
                    FileLogger.Instance.Warning($"{CameraTypeName}连接失败: 未找到任何设备", CameraTypeName);
                    return false;
                }

                if (index < 0 || index >= count)
                {
                    FileLogger.Instance.Error($"{CameraTypeName}连接失败: 索引 {index} 超出范围", null, CameraTypeName);
                    return false;
                }

                dvpCameraInfo info = new dvpCameraInfo();
                status = DVPCamera.dvpEnum((uint)index, ref info);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"{CameraTypeName}枚举失败: {status}", null, CameraTypeName);
                    return false;
                }

                uint handle = 0;
                status = DVPCamera.dvpOpen((uint)index, dvpOpenMode.OPEN_NORMAL, ref handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"{CameraTypeName}打开失败: {status}", null, CameraTypeName);
                    return false;
                }

                _handle = handle;
                FriendlyName = info.FriendlyName;
                UserId = info.UserID;
                SerialNumber = info.SerialNumber;

                InitializeCamera();

                _isConnected = true;
                FileLogger.Instance.Info($"{CameraTypeName}已连接 (Index: {index}, FriendlyName: {FriendlyName})", CameraTypeName);

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = true,
                    Message = $"{CameraTypeName}已连接"
                });

                return true;
            }
            catch (DllNotFoundException ex)
            {
                FileLogger.Instance.Error($"度申相机SDK未安装或DLL未找到: {ex.Message}", ex, CameraTypeName);
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}连接失败: {ex.Message}", ex, CameraTypeName);
                return false;
            }
        }

        protected void GetCameraInfo()
        {
            try
            {
                uint count = 0;
                DVPCamera.dvpRefresh(ref count);
                for (uint i = 0; i < count; i++)
                {
                    dvpCameraInfo info = new dvpCameraInfo();
                    if (DVPCamera.dvpEnum(i, ref info) == dvpStatus.DVP_STATUS_OK)
                    {
                        if (info.UserID == UserId)
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

        /// <summary>
        /// 初始化相机 - 子类可重写以添加特定参数
        /// </summary>
        protected virtual void InitializeCamera()
        {
            // 注册流回调（必须保持委托引用防止被GC）
            _streamCallback = OnStreamCallback;
            var status = DVPCamera.dvpRegisterStreamCallback(_handle, _streamCallback, dvpStreamEvent.STREAM_EVENT_FRAME_THREAD, IntPtr.Zero);
            if (status != dvpStatus.DVP_STATUS_OK)
            {
                FileLogger.Instance.Warning($"{CameraTypeName}注册流回调失败: {status}", CameraTypeName);
            }
            else
            {
                FileLogger.Instance.Info($"{CameraTypeName}注册流回调成功", CameraTypeName);
            }

            // 设置初始参数（使用dvpParam中的参数名）
            status = DVPCamera.dvpSetFloatValue(_handle, "ExposureTime", _exposure);
            FileLogger.Instance.Debug($"{CameraTypeName}设置曝光时间 {_exposure}: {status}", CameraTypeName);

            status = DVPCamera.dvpSetFloatValue(_handle, "Gain", _gain);
            FileLogger.Instance.Debug($"{CameraTypeName}设置增益 {_gain}: {status}", CameraTypeName);
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
                    DVPCamera.dvpClose(_handle);
                    _handle = 0;
                }

                _isConnected = false;
                FileLogger.Instance.Info($"{CameraTypeName}已断开 (UserId: {UserId})", CameraTypeName);

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = false,
                    Message = $"{CameraTypeName}已断开"
                });
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}断开失败: {ex.Message}", ex, CameraTypeName);
            }
        }

        public bool StartGrab()
        {
            if (!_isConnected || _isGrabbing)
            {
                FileLogger.Instance.Warning($"{CameraTypeName}无法开始采集: IsConnected={_isConnected}, IsGrabbing={_isGrabbing}", CameraTypeName);
                return false;
            }

            try
            {
                _grabCts = new CancellationTokenSource();

                // 根据触发源配置
                FileLogger.Instance.Info($"{CameraTypeName}配置触发源: {_currentTriggerSource}", CameraTypeName);
                ConfigureTrigger(_currentTriggerSource);

                // 启动视频流
                FileLogger.Instance.Info($"{CameraTypeName}启动视频流...", CameraTypeName);
                var status = DVPCamera.dvpStart(_handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"{CameraTypeName}启动视频流失败: {status}", null, CameraTypeName);
                    return false;
                }

                _isGrabbing = true;
                FileLogger.Instance.Info($"{CameraTypeName}视频流启动成功, 触发源: {_currentTriggerSource}", CameraTypeName);

                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}开始采集失败: {ex.Message}", ex, CameraTypeName);
                return false;
            }
        }

        protected void ConfigureTrigger(TriggerSource source)
        {
            dvpStatus status;

            switch (source)
            {
                case TriggerSource.Continuous:
                    // 使用官方示例的API: dvpSetBoolValue(handle, "TriggerMode", false)
                    status = DVPCamera.dvpSetBoolValue(_handle, "TriggerMode", false);
                    FileLogger.Instance.Debug($"设置连续采集模式: {status}", CameraTypeName);
                    break;

                case TriggerSource.Software:
                    // 先启用触发模式，再设置触发源
                    status = DVPCamera.dvpSetBoolValue(_handle, "TriggerMode", true);
                    FileLogger.Instance.Debug($"设置触发模式启用: {status}", CameraTypeName);

                    status = DVPCamera.dvpSetEnumValueByString(_handle, "TriggerSource", "Software");
                    FileLogger.Instance.Debug($"设置软件触发源: {status}", CameraTypeName);
                    break;

                case TriggerSource.Line0:
                    status = DVPCamera.dvpSetBoolValue(_handle, "TriggerMode", true);
                    status = DVPCamera.dvpSetEnumValueByString(_handle, "TriggerSource", "Line0");
                    status = DVPCamera.dvpSetEnumValueByString(_handle, "TriggerActivation", _triggerEdge == TriggerEdge.RisingEdge ? "RisingEdge" : "FallingEdge");
                    break;

                case TriggerSource.Line1:
                    status = DVPCamera.dvpSetBoolValue(_handle, "TriggerMode", true);
                    status = DVPCamera.dvpSetEnumValueByString(_handle, "TriggerSource", "Line1");
                    status = DVPCamera.dvpSetEnumValueByString(_handle, "TriggerActivation", _triggerEdge == TriggerEdge.RisingEdge ? "RisingEdge" : "FallingEdge");
                    break;

                case TriggerSource.Line2:
                    status = DVPCamera.dvpSetBoolValue(_handle, "TriggerMode", true);
                    status = DVPCamera.dvpSetEnumValueByString(_handle, "TriggerSource", "Line2");
                    status = DVPCamera.dvpSetEnumValueByString(_handle, "TriggerActivation", _triggerEdge == TriggerEdge.RisingEdge ? "RisingEdge" : "FallingEdge");
                    break;

                case TriggerSource.Line3:
                    status = DVPCamera.dvpSetBoolValue(_handle, "TriggerMode", true);
                    status = DVPCamera.dvpSetEnumValueByString(_handle, "TriggerSource", "Line3");
                    status = DVPCamera.dvpSetEnumValueByString(_handle, "TriggerActivation", _triggerEdge == TriggerEdge.RisingEdge ? "RisingEdge" : "FallingEdge");
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
                    DVPCamera.dvpStop(_handle);
                }

                _isGrabbing = false;
                FileLogger.Instance.Info($"{CameraTypeName}停止采集", CameraTypeName);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}停止采集失败: {ex.Message}", ex, CameraTypeName);
            }
        }

        public bool SoftTrigger()
        {
            if (!_isConnected || !_isGrabbing)
                return false;

            if (_currentTriggerSource != TriggerSource.Software)
            {
                FileLogger.Instance.Warning($"{CameraTypeName}软触发失败: 当前不是软件触发模式", CameraTypeName);
                return false;
            }

            try
            {
                var status = DVPCamera.dvpSetCommandValue(_handle, "TriggerSoftware");
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    status = DVPCamera.dvpTriggerFire(_handle);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"{CameraTypeName}软触发失败: {status}", CameraTypeName);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}软触发失败: {ex.Message}", ex, CameraTypeName);
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
                    var status = DVPCamera.dvpSetFloatValue(_handle, "ExposureTime", exposureUs);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"{CameraTypeName}设置曝光失败: {status}", CameraTypeName);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置曝光失败: {ex.Message}", ex, CameraTypeName);
                return false;
            }
        }

        public int GetExposure()
        {
            if (_isConnected && _handle != 0)
            {
                float exposure = 0;
                dvpFloatDescr descr = new dvpFloatDescr();
                var status = DVPCamera.dvpGetFloatValue(_handle, "ExposureTime", ref exposure, ref descr);
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
                _gain = (float)gain;
                if (_isConnected && _handle != 0)
                {
                    var status = DVPCamera.dvpSetFloatValue(_handle, "Gain", _gain);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"{CameraTypeName}设置增益失败: {status}", CameraTypeName);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置增益失败: {ex.Message}", ex, CameraTypeName);
                return false;
            }
        }

        public double GetGain()
        {
            if (_isConnected && _handle != 0)
            {
                float gain = 0;
                dvpFloatDescr descr = new dvpFloatDescr();
                var status = DVPCamera.dvpGetFloatValue(_handle, "Gain", ref gain, ref descr);
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

                FileLogger.Instance.Debug($"{CameraTypeName}设置触发源: {source}", CameraTypeName);
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置触发源失败: {ex.Message}", ex, CameraTypeName);
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
                    var status = DVPCamera.dvpSetTriggerInputType(_handle, ConvertTriggerEdge(edge));
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"{CameraTypeName}设置触发边沿失败: {status}", CameraTypeName);
                        return false;
                    }
                }
                FileLogger.Instance.Debug($"{CameraTypeName}设置触发边沿: {edge}", CameraTypeName);
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置触发边沿失败: {ex.Message}", ex, CameraTypeName);
                return false;
            }
        }

        public TriggerEdge GetTriggerEdge() => _triggerEdge;

        protected bool IsHardwareTrigger(TriggerSource source)
        {
            return source == TriggerSource.Line0 || source == TriggerSource.Line1 ||
                   source == TriggerSource.Line2 || source == TriggerSource.Line3;
        }

        private int _frameCount = 0;

        protected int OnStreamCallback(uint handle, dvpStreamEvent eventType, IntPtr pContext, ref dvpFrame refFrame, IntPtr pBuffer)
        {
            try
            {
                _frameCount++;
                if (_frameCount % 30 == 1) // 每30帧打印一次日志，避免日志过多
                {
                    FileLogger.Instance.Debug($"{CameraTypeName}回调被调用, 帧数: {_frameCount}, 宽: {refFrame.iWidth}, 高: {refFrame.iHeight}, 格式: {refFrame.format}", CameraTypeName);
                }

                if (pBuffer == IntPtr.Zero || refFrame.iWidth <= 0 || refFrame.iHeight <= 0)
                {
                    FileLogger.Instance.Warning($"{CameraTypeName}回调数据无效: pBuffer={(pBuffer == IntPtr.Zero ? "null" : "valid")}, Width={refFrame.iWidth}, Height={refFrame.iHeight}", CameraTypeName);
                    return 0;
                }

                int width = refFrame.iWidth;
                int height = refFrame.iHeight;

                PixelFormat pixelFormat;
                int bytesPerPixel;

                switch (refFrame.format)
                {
                    case dvpImageFormat.FORMAT_MONO:
                        pixelFormat = PixelFormat.Format8bppIndexed;
                        bytesPerPixel = 1;
                        break;
                    case dvpImageFormat.FORMAT_RGB24:
                    case dvpImageFormat.FORMAT_BGR24:
                        pixelFormat = PixelFormat.Format24bppRgb;
                        bytesPerPixel = 3;
                        break;
                    default:
                        pixelFormat = PixelFormat.Format24bppRgb;
                        bytesPerPixel = 3;
                        break;
                }

                var bitmap = new Bitmap(width, height, pixelFormat);

                if (pixelFormat == PixelFormat.Format8bppIndexed)
                {
                    var palette = bitmap.Palette;
                    for (int i = 0; i < 256; i++)
                    {
                        palette.Entries[i] = Color.FromArgb(i, i, i);
                    }
                    bitmap.Palette = palette;
                }

                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    pixelFormat);

                int srcStride = width * bytesPerPixel;
                int dstStride = bitmapData.Stride;

                for (int y = 0; y < height; y++)
                {
                    IntPtr srcRow = new IntPtr(pBuffer.ToInt64() + y * srcStride);
                    IntPtr dstRow = new IntPtr(bitmapData.Scan0.ToInt64() + y * dstStride);
                    CopyMemory(dstRow, srcRow, srcStride);
                }

                bitmap.UnlockBits(bitmapData);

                RaiseImageReceived(new ImageReceivedEventArgs
                {
                    Image = bitmap,
                    Width = width,
                    Height = height,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}图像回调处理失败: {ex.Message}", ex, CameraTypeName);
            }

            return 0;
        }

        protected void RaiseImageReceived(ImageReceivedEventArgs e)
        {
            ImageReceived?.Invoke(this, e);
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        protected static extern void CopyMemory(IntPtr dest, IntPtr src, int count);

        protected dvpTriggerInputType ConvertTriggerEdge(TriggerEdge edge)
        {
            switch (edge)
            {
                case TriggerEdge.RisingEdge:
                    return dvpTriggerInputType.TRIGGER_POS_EDGE;
                case TriggerEdge.FallingEdge:
                    return dvpTriggerInputType.TRIGGER_NEG_EDGE;
                case TriggerEdge.DoubleEdge:
                    return dvpTriggerInputType.TRIGGER_POS_EDGE;
                default:
                    return dvpTriggerInputType.TRIGGER_POS_EDGE;
            }
        }

        public void Dispose()
        {
            Disconnect();
            _grabCts?.Dispose();
        }
    }
}
