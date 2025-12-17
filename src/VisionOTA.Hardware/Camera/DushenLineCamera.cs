using System;
using DVPCameraType;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Hardware.Camera
{
    /// <summary>
    /// 度申线扫相机实现
    /// </summary>
    public class DushenLineCamera : DushenCameraBase, ILineCamera
    {
        private int _lineRate = 10000;
        private int _lineCount = 4096;

        protected override string CameraTypeName => "度申线扫相机";
        protected override int DefaultExposure => 2000;

        public DushenLineCamera()
        {
        }

        /// <summary>
        /// 初始化相机 - 添加线扫特有参数
        /// </summary>
        protected override void InitializeCamera()
        {
            base.InitializeCamera();

            // 线扫特有参数
            DVPCamera.dvpSetIntValue(_handle, "LineRate", _lineRate);
            DVPCamera.dvpSetIntValue(_handle, "Height", _lineCount);
        }

        public bool SetLineRate(int lineRate)
        {
            try
            {
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

        public bool SetLineCount(int lineCount)
        {
            try
            {
                _lineCount = lineCount;
                if (_isConnected && _handle != 0)
                {
                    var status = DVPCamera.dvpSetIntValue(_handle, "Height", lineCount);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        FileLogger.Instance.Warning($"{CameraTypeName}设置采集行数失败: {status}", CameraTypeName);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"设置采集行数失败: {ex.Message}", ex, CameraTypeName);
                return false;
            }
        }

        public int GetLineCount()
        {
            if (_isConnected && _handle != 0)
            {
                int height = 0;
                dvpIntDescr descr = new dvpIntDescr();
                var status = DVPCamera.dvpGetIntValue(_handle, "Height", ref height, ref descr);
                if (status == dvpStatus.DVP_STATUS_OK)
                {
                    _lineCount = height;
                }
            }
            return _lineCount;
        }
    }
}
