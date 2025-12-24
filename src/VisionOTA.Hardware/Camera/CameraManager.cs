using System;
using System.Collections.Generic;
using VisionOTA.Common.Events;

namespace VisionOTA.Hardware.Camera
{
    /// <summary>
    /// 相机管理器 - 单例模式，管理所有相机实例
    /// </summary>
    public class CameraManager : IDisposable
    {
        private static readonly Lazy<CameraManager> _instance = new Lazy<CameraManager>(() => new CameraManager());
        public static CameraManager Instance => _instance.Value;

        private readonly Dictionary<int, ICamera> _cameras = new Dictionary<int, ICamera>();
        private readonly object _lock = new object();
        private bool _isDisposed;

        private CameraManager() { }

        /// <summary>
        /// 获取或创建工位相机
        /// </summary>
        /// <param name="stationId">工位ID (1=面阵, 2=线扫)</param>
        /// <returns>相机实例</returns>
        public ICamera GetCamera(int stationId)
        {
            lock (_lock)
            {
                if (!_cameras.ContainsKey(stationId))
                {
                    _cameras[stationId] = stationId == 1
                        ? CameraFactory.CreateAreaCamera()
                        : CameraFactory.CreateLineCamera();
                }
                return _cameras[stationId];
            }
        }

        /// <summary>
        /// 检查相机是否已连接
        /// </summary>
        public bool IsConnected(int stationId)
        {
            lock (_lock)
            {
                return _cameras.ContainsKey(stationId) && _cameras[stationId].IsConnected;
            }
        }

        /// <summary>
        /// 检查相机是否正在采集
        /// </summary>
        public bool IsGrabbing(int stationId)
        {
            lock (_lock)
            {
                return _cameras.ContainsKey(stationId) && _cameras[stationId].IsGrabbing;
            }
        }

        /// <summary>
        /// 获取相机连接状态信息
        /// </summary>
        public (bool IsConnected, string FriendlyName, string SerialNumber) GetCameraStatus(int stationId)
        {
            lock (_lock)
            {
                if (_cameras.ContainsKey(stationId))
                {
                    var camera = _cameras[stationId];
                    return (camera.IsConnected, camera.FriendlyName, camera.SerialNumber);
                }
                return (false, null, null);
            }
        }

        /// <summary>
        /// 发布相机连接状态事件
        /// </summary>
        public void PublishConnectionState(int stationId)
        {
            lock (_lock)
            {
                if (_cameras.ContainsKey(stationId))
                {
                    var camera = _cameras[stationId];
                    EventAggregator.Instance.Publish(new ConnectionChangedEvent
                    {
                        DeviceType = $"Camera{stationId}",
                        DeviceName = camera.FriendlyName ?? $"Camera{stationId}",
                        IsConnected = camera.IsConnected
                    });
                }
            }
        }

        /// <summary>
        /// 释放所有相机资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            lock (_lock)
            {
                foreach (var camera in _cameras.Values)
                {
                    try
                    {
                        camera?.Dispose();
                    }
                    catch { }
                }
                _cameras.Clear();
            }
        }
    }
}
