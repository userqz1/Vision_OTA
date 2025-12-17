namespace VisionOTA.Hardware.Camera
{
    /// <summary>
    /// 度申面阵相机实现
    /// </summary>
    public class DushenAreaCamera : DushenCameraBase
    {
        protected override string CameraTypeName => "度申面阵相机";
        protected override int DefaultExposure => 5000;

        public DushenAreaCamera()
        {
        }
    }
}
