using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DVPCameraType;
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
        private float _gain = 1.0f;
        private TriggerSource _currentTriggerSource = TriggerSource.Continuous;
        private TriggerEdge _triggerEdge = TriggerEdge.RisingEdge;
        private CancellationTokenSource _grabCts;
        private DVPCamera.dvpStreamCallback _streamCallback;

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
                FileLogger.Instance.Error($"度申面阵相机搜索失败: {ex.Message}", ex, "DushenAreaCamera");
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
                    FileLogger.Instance.Warning("度申面阵相机连接失败: UserId为空", "DushenAreaCamera");
                    return false;
                }

                uint count = 0;
                var status = DVPCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK || count == 0)
                {
                    FileLogger.Instance.Warning($"度申面阵相机连接失败: 未找到任何设备", "DushenAreaCamera");
                    return false;
                }

                uint handle = 0;
                status = DVPCamera.dvpOpenByUserId(userId, dvpOpenMode.OPEN_NORMAL, ref handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申面阵相机通过UserId '{userId}' 打开失败: {status}", null, "DushenAreaCamera");
                    return false;
                }

                _handle = handle;
                UserId = userId;

                GetCameraInfo();
                InitializeCamera();

                _isConnected = true;
                FileLogger.Instance.Info($"度申面阵相机已连接 (UserId: {UserId}, FriendlyName: {FriendlyName})", "DushenAreaCamera");

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = true,
                    Message = "度申面阵相机已连接"
                });

                return true;
            }
            catch (DllNotFoundException ex)
            {
                FileLogger.Instance.Error($"度申相机SDK未安装或DLL未找到: {ex.Message}", ex, "DushenAreaCamera");
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申面阵相机连接失败: {ex.Message}", ex, "DushenAreaCamera");
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
                    FileLogger.Instance.Warning($"度申面阵相机连接失败: 未找到任何设备", "DushenAreaCamera");
                    return false;
                }

                if (index < 0 || index >= count)
                {
                    FileLogger.Instance.Error($"度申面阵相机连接失败: 索引 {index} 超出范围", null, "DushenAreaCamera");
                    return false;
                }

                dvpCameraInfo info = new dvpCameraInfo();
                status = DVPCamera.dvpEnum((uint)index, ref info);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申面阵相机枚举失败: {status}", null, "DushenAreaCamera");
                    return false;
                }

                uint handle = 0;
                status = DVPCamera.dvpOpen((uint)index, dvpOpenMode.OPEN_NORMAL, ref handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申面阵相机打开失败: {status}", null, "DushenAreaCamera");
                    return false;
                }

                _handle = handle;
                FriendlyName = info.FriendlyName;
                UserId = info.UserID;
                SerialNumber = info.SerialNumber;

                InitializeCamera();

                _isConnected = true;
                FileLogger.Instance.Info($"度申面阵相机已连接 (Index: {index}, FriendlyName: {FriendlyName})", "DushenAreaCamera");

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = true,
                    Message = "度申面阵相机已连接"
                });

                return true;
            }
            catch (DllNotFoundException ex)
            {
                FileLogger.Instance.Error($"度申相机SDK未安装或DLL未找到: {ex.Message}", ex, "DushenAreaCamera");
                return false;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申面阵相机连接失败: {ex.Message}", ex, "DushenAreaCamera");
                return false;
            }
        }

        private void GetCameraInfo()
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

        private void InitializeCamera()
        {
            // 注册流回调
            _streamCallback = OnStreamCallback;
            var status = DVPCamera.dvpRegisterStreamCallback(_handle, _streamCallback, dvpStreamEvent.STREAM_EVENT_FRAME_THREAD, IntPtr.Zero);
            if (status != dvpStatus.DVP_STATUS_OK)
            {
                FileLogger.Instance.Warning($"度申面阵相机注册流回调失败: {status}", "DushenAreaCamera");
            }

            // 设置初始参数
            DVPCamera.dvpSetFloatValue(_handle, "ExposureTime", _exposure);
            DVPCamera.dvpSetFloatValue(_handle, "Gain", _gain);
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
                FileLogger.Instance.Info($"度申面阵相机已断开 (UserId: {UserId})", "DushenAreaCamera");

                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                {
                    IsConnected = false,
                    Message = "面阵相机已断开"
                });
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申面阵相机断开失败: {ex.Message}", ex, "DushenAreaCamera");
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
                var status = DVPCamera.dvpStart(_handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"度申面阵相机启动视频流失败: {status}", null, "DushenAreaCamera");
                    return false;
                }

                _isGrabbing = true;
                FileLogger.Instance.Info($"度申面阵相机开始采集, 触发源: {_currentTriggerSource}", "DushenAreaCamera");

                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申面阵相机开始采集失败: {ex.Message}", ex, "DushenAreaCamera");
                return false;
            }
        }

        private void ConfigureTrigger(TriggerSource source)
        {
            switch (source)
            {
                case TriggerSource.Continuous:
                    // 关闭触发模式，使用连续采集
                    DVPCamera.dvpSetTriggerState(_handle, false);
                    break;

                case TriggerSource.Software:
                    // 开启触发模式，软件触发
                    DVPCamera.dvpSetTriggerState(_handle, true);
                    DVPCamera.dvpSetTriggerSource(_handle, dvpTriggerSource.TRIGGER_SOURCE_SOFTWARE);
                    break;

                case TriggerSource.Line0:
                case TriggerSource.Line1:
                    DVPCamera.dvpSetTriggerState(_handle, true);
                    DVPCamera.dvpSetTriggerSource(_handle, dvpTriggerSource.TRIGGER_SOURCE_LINE1);
                    DVPCamera.dvpSetTriggerInputType(_handle, ConvertTriggerEdge(_triggerEdge));
                    break;

                case TriggerSource.Line2:
                    DVPCamera.dvpSetTriggerState(_handle, true);
                    DVPCamera.dvpSetTriggerSource(_handle, dvpTriggerSource.TRIGGER_SOURCE_LINE2);
                    DVPCamera.dvpSetTriggerInputType(_handle, ConvertTriggerEdge(_triggerEdge));
                    break;

                case TriggerSource.Line3:
                    DVPCamera.dvpSetTriggerState(_handle, true);
                    DVPCamera.dvpSetTriggerSource(_handle, dvpTriggerSource.TRIGGER_SOURCE_LINE3);
                    DVPCamera.dvpSetTriggerInputType(_handle, ConvertTriggerEdge(_triggerEdge));
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
                FileLogger.Instance.Info($"度申面阵相机停止采集", "DushenAreaCamera");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申面阵相机停止采集失败: {ex.Message}", ex, "DushenAreaCamera");
            }
        }

        public bool SoftTrigger()
        {
            if (!_isConnected || !_isGrabbing)
                return false;

            if (_currentTriggerSource != TriggerSource.Software)
            {
                FileLogger.Instance.Warning("度申面阵相机软触发失败: 当前不是软件触发模式", "DushenAreaCamera");
                return false;
            }

            try
            {
                // 使用官方API发送软触发命令
                var status = DVPCamera.dvpSetCommandValue(_handle, "TriggerSoftware");
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    // 备用方法
                    status = DVPCamera.dvpTriggerFire(_handle);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"度申面阵相机软触发失败: {status}", "DushenAreaCamera");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申面阵相机软触发失败: {ex.Message}", ex, "DushenAreaCamera");
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
                        FileLogger.Instance.Warning($"度申面阵相机设置曝光失败: {status}", "DushenAreaCamera");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置曝光失败: {ex.Message}", ex, "DushenAreaCamera");
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
                        FileLogger.Instance.Warning($"度申面阵相机设置增益失败: {status}", "DushenAreaCamera");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置增益失败: {ex.Message}", ex, "DushenAreaCamera");
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

                FileLogger.Instance.Debug($"度申面阵相机设置触发源: {source}", "DushenAreaCamera");
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置触发源失败: {ex.Message}", ex, "DushenAreaCamera");
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
                        FileLogger.Instance.Warning($"度申面阵相机设置触发边沿失败: {status}", "DushenAreaCamera");
                        return false;
                    }
                }
                FileLogger.Instance.Debug($"度申面阵相机设置触发边沿: {edge}", "DushenAreaCamera");
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置触发边沿失败: {ex.Message}", ex, "DushenAreaCamera");
                return false;
            }
        }

        public TriggerEdge GetTriggerEdge() => _triggerEdge;

        private bool IsHardwareTrigger(TriggerSource source)
        {
            return source == TriggerSource.Line0 || source == TriggerSource.Line1 ||
                   source == TriggerSource.Line2 || source == TriggerSource.Line3;
        }

        private int OnStreamCallback(uint handle, dvpStreamEvent eventType, IntPtr pContext, ref dvpFrame refFrame, IntPtr pBuffer)
        {
            try
            {
                if (pBuffer == IntPtr.Zero || refFrame.iWidth <= 0 || refFrame.iHeight <= 0)
                    return 0;

                int width = refFrame.iWidth;
                int height = refFrame.iHeight;

                // 根据图像格式确定像素格式和字节数
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
                        // 默认按RGB24处理
                        pixelFormat = PixelFormat.Format24bppRgb;
                        bytesPerPixel = 3;
                        break;
                }

                // 创建新的Bitmap并复制数据（stride需要4字节对齐）
                var bitmap = new Bitmap(width, height, pixelFormat);

                // 设置灰度调色板
                if (pixelFormat == PixelFormat.Format8bppIndexed)
                {
                    var palette = bitmap.Palette;
                    for (int i = 0; i < 256; i++)
                    {
                        palette.Entries[i] = Color.FromArgb(i, i, i);
                    }
                    bitmap.Palette = palette;
                }

                // 锁定位图内存并复制数据
                var bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    pixelFormat);

                int srcStride = width * bytesPerPixel;
                int dstStride = bitmapData.Stride;

                // 逐行复制数据
                for (int y = 0; y < height; y++)
                {
                    IntPtr srcRow = new IntPtr(pBuffer.ToInt64() + y * srcStride);
                    IntPtr dstRow = new IntPtr(bitmapData.Scan0.ToInt64() + y * dstStride);
                    CopyMemory(dstRow, srcRow, srcStride);
                }

                bitmap.UnlockBits(bitmapData);

                ImageReceived?.Invoke(this, new ImageReceivedEventArgs
                {
                    Image = bitmap,
                    Width = width,
                    Height = height,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"度申面阵相机图像回调处理失败: {ex.Message}", ex, "DushenAreaCamera");
            }

            return 0;
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, int count);

        private dvpTriggerInputType ConvertTriggerEdge(TriggerEdge edge)
        {
            switch (edge)
            {
                case TriggerEdge.RisingEdge:
                    return dvpTriggerInputType.TRIGGER_POS_EDGE;
                case TriggerEdge.FallingEdge:
                    return dvpTriggerInputType.TRIGGER_NEG_EDGE;
                case TriggerEdge.DoubleEdge:
                    return dvpTriggerInputType.TRIGGER_POS_EDGE; // 没有双边沿，使用上升沿
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
