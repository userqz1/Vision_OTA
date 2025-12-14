using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Hardware.Camera
{
    /// <summary>
    /// 相机类型
    /// </summary>
    public enum CameraType
    {
        /// <summary>
        /// 面阵相机
        /// </summary>
        AreaScan,

        /// <summary>
        /// 线扫相机
        /// </summary>
        LineScan
    }

    /// <summary>
    /// 相机工厂
    /// </summary>
    public static class CameraFactory
    {
        /// <summary>
        /// 创建面阵相机
        /// </summary>
        public static ICamera CreateAreaCamera()
        {
            FileLogger.Instance.Info("创建度申面阵相机", "CameraFactory");
            return new DushenAreaCamera();
        }

        /// <summary>
        /// 创建线扫相机
        /// </summary>
        public static ILineCamera CreateLineCamera()
        {
            FileLogger.Instance.Info("创建度申线扫相机", "CameraFactory");
            return new DushenLineCamera();
        }
    }
}
