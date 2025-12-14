using VisionOTA.Common.Constants;
using VisionOTA.Common.Mvvm;

namespace VisionOTA.Core.Models
{
    /// <summary>
    /// 系统状态模型
    /// </summary>
    public class SystemStateModel : ObservableObject
    {
        private SystemState _currentState = SystemState.Idle;
        private bool _isCameraConnected;
        private bool _isPlcConnected;
        private bool _isVisionLoaded;
        private int _consecutiveFailures;
        private string _lastError;

        /// <summary>
        /// 当前系统状态
        /// </summary>
        public SystemState CurrentState
        {
            get => _currentState;
            set => SetProperty(ref _currentState, value);
        }

        /// <summary>
        /// 相机是否已连接
        /// </summary>
        public bool IsCameraConnected
        {
            get => _isCameraConnected;
            set => SetProperty(ref _isCameraConnected, value);
        }

        /// <summary>
        /// PLC是否已连接
        /// </summary>
        public bool IsPlcConnected
        {
            get => _isPlcConnected;
            set => SetProperty(ref _isPlcConnected, value);
        }

        /// <summary>
        /// 视觉工具块是否已加载
        /// </summary>
        public bool IsVisionLoaded
        {
            get => _isVisionLoaded;
            set => SetProperty(ref _isVisionLoaded, value);
        }

        /// <summary>
        /// 连续失败次数
        /// </summary>
        public int ConsecutiveFailures
        {
            get => _consecutiveFailures;
            set => SetProperty(ref _consecutiveFailures, value);
        }

        /// <summary>
        /// 最后错误信息
        /// </summary>
        public string LastError
        {
            get => _lastError;
            set => SetProperty(ref _lastError, value);
        }

        /// <summary>
        /// 是否可以启动运行
        /// </summary>
        public bool CanStart => CurrentState == SystemState.Idle && IsVisionLoaded;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning => CurrentState == SystemState.Running;
    }
}
