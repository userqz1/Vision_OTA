using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using DVPCameraType;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Hardware.Camera
{
    /// <summary>
    /// 度申线扫相机实现 - 使用轮询方式获取图像（官方推荐）
    /// </summary>
    public class DushenLineCamera : DushenCameraBase, ILineCamera
    {
        private int _lineRate = 0;
        private Thread _grabThread;
        private volatile bool _stopGrabThread = false;
        private AutoResetEvent _threadEvent = new AutoResetEvent(false);

        protected override string CameraTypeName => "度申线扫相机";
        protected override int DefaultExposure => 150;

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        public DushenLineCamera()
        {
        }

        /// <summary>
        /// 初始化相机 - 线扫相机不注册回调，使用轮询方式
        /// </summary>
        protected override void InitializeCamera()
        {
            // 不调用base.InitializeCamera()，因为不需要注册回调
            // 只设置基本参数
            var status = DVPCamera.dvpSetFloatValue(_handle, "ExposureTime", _exposure);
            FileLogger.Instance.Debug($"{CameraTypeName}设置曝光时间 {_exposure}: {status}", CameraTypeName);

            status = DVPCamera.dvpSetFloatValue(_handle, "Gain", _gain);
            FileLogger.Instance.Debug($"{CameraTypeName}设置增益 {_gain}: {status}", CameraTypeName);

            FileLogger.Instance.Info($"{CameraTypeName}初始化完成（使用轮询模式）", CameraTypeName);
        }

        /// <summary>
        /// 启动采集 - 使用轮询线程
        /// </summary>
        public new bool StartGrab()
        {
            if (!_isConnected || _isGrabbing)
            {
                FileLogger.Instance.Warning($"{CameraTypeName}无法开始采集: IsConnected={_isConnected}, IsGrabbing={_isGrabbing}", CameraTypeName);
                return false;
            }

            try
            {
                _grabCts = new CancellationTokenSource();

                // 配置触发
                FileLogger.Instance.Info($"{CameraTypeName}配置触发源: {_currentTriggerSource}", CameraTypeName);
                ConfigureTrigger(_currentTriggerSource);

                // 线扫相机特有：设置行触发使能（从配置读取）
                var lineTrigStatus = DVPCamera.dvpSetBoolValue(_handle, "LineTrigEnable", _lineTrigEnable);
                FileLogger.Instance.Info($"{CameraTypeName}设置行触发使能(LineTrigEnable={_lineTrigEnable}): {lineTrigStatus}", CameraTypeName);

                // 启动视频流
                FileLogger.Instance.Info($"{CameraTypeName}启动视频流...", CameraTypeName);
                var status = DVPCamera.dvpStart(_handle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    FileLogger.Instance.Error($"{CameraTypeName}启动视频流失败: {status}", null, CameraTypeName);
                    return false;
                }

                // 启动轮询线程
                _stopGrabThread = false;
                _threadEvent.Reset();
                _grabThread = new Thread(GrabThreadProc);
                _grabThread.IsBackground = true;
                _grabThread.Start();

                _isGrabbing = true;
                FileLogger.Instance.Info($"{CameraTypeName}视频流启动成功（轮询模式）, 触发源: {_currentTriggerSource}", CameraTypeName);

                // 输出触发参数
                LogTriggerParameters();

                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}开始采集失败: {ex.Message}", ex, CameraTypeName);
                return false;
            }
        }

        /// <summary>
        /// 停止采集
        /// </summary>
        public new void StopGrab()
        {
            if (!_isGrabbing)
                return;

            try
            {
                // 停止轮询线程
                _stopGrabThread = true;
                _threadEvent.Set();

                if (_grabThread != null && _grabThread.IsAlive)
                {
                    if (!_grabThread.Join(2000))
                    {
                        _grabThread.Abort();
                    }
                    _grabThread = null;
                }

                _grabCts?.Cancel();

                if (_handle != 0)
                {
                    DVPCamera.dvpStop(_handle);
                }

                _isGrabbing = false;
                FileLogger.Instance.Info($"{CameraTypeName}已停止采集", CameraTypeName);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}停止采集失败: {ex.Message}", ex, CameraTypeName);
            }
        }

        /// <summary>
        /// 轮询线程 - 使用dvpGetFrame获取图像
        /// </summary>
        private void GrabThreadProc()
        {
            FileLogger.Instance.Info($"{CameraTypeName}轮询线程启动", CameraTypeName);
            int frameCount = 0;

            while (!_stopGrabThread)
            {
                try
                {
                    // 等待信号或超时
                    if (_threadEvent.WaitOne(50))
                    {
                        // 收到停止信号
                        break;
                    }

                    // 尝试获取帧，超时4秒
                    IntPtr pBuffer = IntPtr.Zero;
                    dvpFrame frame = new dvpFrame();

                    var status = DVPCamera.dvpGetFrame(_handle, ref frame, ref pBuffer, 4000);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        // 超时或错误，继续等待
                        continue;
                    }

                    frameCount++;
                    var now = DateTime.Now;
                    FileLogger.Instance.Info($"{CameraTypeName}轮询获取帧 #{frameCount} | 时间: {now:HH:mm:ss.fff} | 帧ID: {frame.uFrameID}", CameraTypeName);

                    // 转换图像
                    var bitmap = ConvertFrameToBitmap(ref frame, pBuffer);
                    if (bitmap != null)
                    {
                        // 触发事件
                        RaiseImageReceived(new ImageReceivedEventArgs
                        {
                            Image = bitmap,
                            Width = frame.iWidth,
                            Height = frame.iHeight,
                            Timestamp = now
                        });

                        FileLogger.Instance.Info($"{CameraTypeName}图像已发送, 尺寸: {frame.iWidth}x{frame.iHeight}", CameraTypeName);
                    }
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Warning($"{CameraTypeName}轮询线程异常: {ex.Message}", CameraTypeName);
                }
            }

            FileLogger.Instance.Info($"{CameraTypeName}轮询线程退出", CameraTypeName);
        }

        /// <summary>
        /// 将dvpFrame转换为Bitmap
        /// </summary>
        private Bitmap ConvertFrameToBitmap(ref dvpFrame frame, IntPtr pBuffer)
        {
            try
            {
                int width = frame.iWidth;
                int height = frame.iHeight;

                PixelFormat pixelFormat;
                int bytesPerPixel;

                switch (frame.format)
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

                if (srcStride == dstStride)
                {
                    CopyMemory(bitmapData.Scan0, pBuffer, (uint)(srcStride * height));
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        IntPtr srcRow = new IntPtr(pBuffer.ToInt64() + y * srcStride);
                        IntPtr dstRow = new IntPtr(bitmapData.Scan0.ToInt64() + y * dstStride);
                        CopyMemory(dstRow, srcRow, (uint)srcStride);
                    }
                }

                bitmap.UnlockBits(bitmapData);
                return bitmap;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"{CameraTypeName}图像转换失败: {ex.Message}", ex, CameraTypeName);
                return null;
            }
        }

        /// <summary>
        /// 输出触发参数
        /// </summary>
        private void LogTriggerParameters()
        {
            try
            {
                FileLogger.Instance.Info($"========== {CameraTypeName} 触发参数 ==========", CameraTypeName);

                // 基本触发参数
                bool triggerState = false;
                DVPCamera.dvpGetTriggerState(_handle, ref triggerState);
                FileLogger.Instance.Info($"帧触发模式(TriggerMode): {triggerState}", CameraTypeName);

                dvpTriggerSource triggerSource = dvpTriggerSource.TRIGGER_SOURCE_SOFTWARE;
                DVPCamera.dvpGetTriggerSource(_handle, ref triggerSource);
                FileLogger.Instance.Info($"帧触发源: {triggerSource}", CameraTypeName);

                dvpStreamState streamState = dvpStreamState.STATE_STOPED;
                DVPCamera.dvpGetStreamState(_handle, ref streamState);
                FileLogger.Instance.Info($"流状态: {streamState}", CameraTypeName);

                FileLogger.Instance.Info($"曝光时间: {_exposure}us, 增益: {_gain}", CameraTypeName);

                // ========== 线扫相机特有参数 ==========
                FileLogger.Instance.Info($"---------- 线扫特有参数 ----------", CameraTypeName);

                // 行触发使能
                bool lineTrigEnable = false;
                var st = DVPCamera.dvpGetBoolValue(_handle, "LineTrigEnable", ref lineTrigEnable);
                FileLogger.Instance.Info($"行触发使能(LineTrigEnable): {lineTrigEnable} (status={st})", CameraTypeName);

                // 行频
                int lineRate = 0;
                dvpIntDescr intDescr = new dvpIntDescr();
                st = DVPCamera.dvpGetIntValue(_handle, "LineRate", ref lineRate, ref intDescr);
                FileLogger.Instance.Info($"行频(LineRate): {lineRate} Hz (范围:{intDescr.iMin}-{intDescr.iMax}, status={st})", CameraTypeName);

                // 预分频
                int preDiv = 0;
                st = DVPCamera.dvpGetIntValue(_handle, "LineTrigFreqPreDiv", ref preDiv, ref intDescr);
                FileLogger.Instance.Info($"预分频(LineTrigFreqPreDiv): {preDiv} (status={st})", CameraTypeName);

                // 倍频
                int mult = 0;
                st = DVPCamera.dvpGetIntValue(_handle, "LineTrigFreqMult", ref mult, ref intDescr);
                FileLogger.Instance.Info($"倍频(LineTrigFreqMult): {mult} (status={st})", CameraTypeName);

                // 分频
                int div = 0;
                st = DVPCamera.dvpGetIntValue(_handle, "LineTrigFreqDiv", ref div, ref intDescr);
                FileLogger.Instance.Info($"分频(LineTrigFreqDiv): {div} (status={st})", CameraTypeName);

                // 触发过滤
                float filter = 0;
                dvpFloatDescr floatDescr = new dvpFloatDescr();
                st = DVPCamera.dvpGetFloatValue(_handle, "LineTrigFilter", ref filter, ref floatDescr);
                FileLogger.Instance.Info($"触发过滤(LineTrigFilter): {filter}us (status={st})", CameraTypeName);

                // 触发延迟
                float delay = 0;
                st = DVPCamera.dvpGetFloatValue(_handle, "LineTrigDelay", ref delay, ref floatDescr);
                FileLogger.Instance.Info($"触发延迟(LineTrigDelay): {delay}us (status={st})", CameraTypeName);

                // 帧超时
                int frameTimeout = 0;
                st = DVPCamera.dvpGetIntValue(_handle, "FrameTimeout", ref frameTimeout, ref intDescr);
                FileLogger.Instance.Info($"帧超时(FrameTimeout): {frameTimeout}ms (status={st})", CameraTypeName);

                // 图像高度（帧行数）
                int height = 0;
                st = DVPCamera.dvpGetIntValue(_handle, "Height", ref height, ref intDescr);
                FileLogger.Instance.Info($"图像高度(Height): {height} 行 (status={st})", CameraTypeName);

                // 计算理论采集时间
                if (lineRate > 0 && height > 0)
                {
                    double theoreticalTime = (double)height / lineRate * 1000;
                    FileLogger.Instance.Info($"理论采集时间: {theoreticalTime:F1}ms ({height}行 / {lineRate}Hz)", CameraTypeName);
                }

                FileLogger.Instance.Info($"========== 参数输出完成 ==========", CameraTypeName);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"读取触发参数失败: {ex.Message}", CameraTypeName);
            }
        }

        public bool SetLineRate(int lineRate)
        {
            try
            {
                if (lineRate <= 0)
                {
                    _lineRate = 0;
                    return true;
                }

                _lineRate = lineRate;
                if (_isConnected && _handle != 0)
                {
                    var status = DVPCamera.dvpSetIntValue(_handle, "LineRate", lineRate);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"{CameraTypeName}设置行频失败: {status}", CameraTypeName);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置行频失败: {ex.Message}", ex, CameraTypeName);
                return false;
            }
        }

        public int GetLineRate()
        {
            if (_isConnected && _handle != 0)
            {
                int lineRate = 0;
                dvpIntDescr descr = new dvpIntDescr();
                var status = DVPCamera.dvpGetIntValue(_handle, "LineRate", ref lineRate, ref descr);
                if (status == dvpStatus.DVP_STATUS_OK)
                {
                    _lineRate = lineRate;
                }
            }
            return _lineRate;
        }

        private bool _lineTrigEnable = false;

        /// <summary>
        /// 设置行触发使能
        /// </summary>
        public bool SetLineTrigEnable(bool enable)
        {
            try
            {
                _lineTrigEnable = enable;
                if (_isConnected && _handle != 0)
                {
                    var status = DVPCamera.dvpSetBoolValue(_handle, "LineTrigEnable", enable);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"{CameraTypeName}设置行触发使能失败: {status}", CameraTypeName);
                        return false;
                    }
                    FileLogger.Instance.Info($"{CameraTypeName}行触发使能设置为: {enable}", CameraTypeName);
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置行触发使能失败: {ex.Message}", ex, CameraTypeName);
                return false;
            }
        }

        /// <summary>
        /// 获取行触发使能状态
        /// </summary>
        public bool GetLineTrigEnable()
        {
            if (_isConnected && _handle != 0)
            {
                bool enable = false;
                var status = DVPCamera.dvpGetBoolValue(_handle, "LineTrigEnable", ref enable);
                if (status == dvpStatus.DVP_STATUS_OK)
                {
                    _lineTrigEnable = enable;
                }
            }
            return _lineTrigEnable;
        }
    }
}
