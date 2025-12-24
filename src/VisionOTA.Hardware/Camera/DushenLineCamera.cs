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
        private int _lineRate = 0; // 0 表示使用默认值

        protected override string CameraTypeName => "度申线扫相机";
        protected override int DefaultExposure => 2000;

        public DushenLineCamera()
        {
        }

        /// <summary>
        /// 初始化相机 - 使用默认参数
        /// </summary>
        protected override void InitializeCamera()
        {
            base.InitializeCamera();
            // 使用相机默认的图像尺寸和行频，不额外设置
        }

        public bool SetLineRate(int lineRate)
        {
            try
            {
                if (lineRate <= 0)
                {
                    // 0 或负数表示使用默认值，不设置
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

        // LineCount 相关方法保留空实现以兼容接口
        public bool SetLineCount(int lineCount)
        {
            // 使用相机默认配置，不设置行数
            return true;
        }

        public int GetLineCount()
        {
            // 返回0表示使用默认值
            return 0;
        }
    }
}
