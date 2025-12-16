using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using VisionOTA.Common.Constants;
using VisionOTA.Common.Events;
using VisionOTA.Core.Interfaces;
using VisionOTA.Core.Models;
using VisionOTA.Hardware.Camera;
using VisionOTA.Hardware.Plc;
using VisionOTA.Hardware.Vision;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;
using VisionOTA.Infrastructure.Storage;

namespace VisionOTA.Core.Services
{
    /// <summary>
    /// 检测服务实现
    /// </summary>
    public class InspectionService : IInspectionService
    {
        private SystemState _currentState = SystemState.Idle;
        private readonly Dictionary<int, ICamera> _cameras;
        private readonly Dictionary<int, IVisionProcessor> _visionProcessors;
        private IPlcCommunication _plc;
        private readonly IStatisticsService _statisticsService;
        private CancellationTokenSource _runCts;
        private int _consecutiveFailures;
        private bool _isDisposed;

        public SystemState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    var oldState = _currentState;
                    _currentState = value;
                    StateChanged?.Invoke(this, value);
                    EventAggregator.Instance.Publish(new SystemStateChangedEvent
                    {
                        OldState = oldState.ToString(),
                        NewState = value.ToString()
                    });
                }
            }
        }

        public event EventHandler<InspectionCompletedEventArgs> InspectionCompleted;
        public event EventHandler<SystemState> StateChanged;
        public event EventHandler<string> ErrorOccurred;

        public InspectionService(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
            _cameras = new Dictionary<int, ICamera>();
            _visionProcessors = new Dictionary<int, IVisionProcessor>();

            // 订阅瓶身旋转命令
            EventAggregator.Instance.Subscribe<BottleRotateCommand>(OnBottleRotateCommand);
        }

        private async void OnBottleRotateCommand(BottleRotateCommand cmd)
        {
            if (_plc == null || !_plc.IsConnected)
            {
                FileLogger.Instance.Warning("PLC未连接，无法控制瓶身旋转", "Inspection");
                return;
            }

            try
            {
                var config = ConfigManager.Instance.Plc;
                // 使用PLC设置中配置的瓶身旋转地址（OutputValue）
                var addressConfig = config.OutputAddresses.OutputValue;
                var address = addressConfig.Address;
                var dataType = addressConfig.DataType;
                int value = cmd.Rotate ? 1 : 0;

                var success = await WriteByTypeAsync(address, dataType, value);
                if (success)
                {
                    FileLogger.Instance.Info($"瓶身旋转控制: {(cmd.Rotate ? "开始旋转" : "停止旋转")}, 地址: {address}, 类型: {dataType}", "Inspection");
                }
                else
                {
                    FileLogger.Instance.Warning($"瓶身旋转控制失败: 写入地址 {address} 失败", "Inspection");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"瓶身旋转控制异常: {ex.Message}", ex, "Inspection");
            }
        }

        /// <summary>
        /// 根据数据类型写入PLC
        /// </summary>
        private async Task<bool> WriteByTypeAsync(string address, string dataType, int value)
        {
            switch (dataType?.ToUpper())
            {
                case "BOOL":
                    return await _plc.WriteBitAsync(address, value != 0);
                case "INT":
                    return await _plc.WriteWordAsync(address, (short)value);
                case "UINT":
                    return await _plc.WriteUIntAsync(address, (ushort)value);
                case "DINT":
                    return await _plc.WriteDWordAsync(address, value);
                case "UDINT":
                    return await _plc.WriteUDIntAsync(address, (uint)value);
                case "REAL":
                    return await _plc.WriteFloatAsync(address, value);
                case "LREAL":
                    return await _plc.WriteLRealAsync(address, value);
                default:
                    // 默认使用REAL
                    return await _plc.WriteFloatAsync(address, value);
            }
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                FileLogger.Instance.Info("正在初始化检测服务...", "Inspection");

                // 初始化相机
                var cameraConfig = ConfigManager.Instance.Camera;
                _cameras[1] = CameraFactory.CreateAreaCamera();
                _cameras[2] = CameraFactory.CreateLineCamera();

                // 注册相机事件
                foreach (var camera in _cameras.Values)
                {
                    camera.ImageReceived += OnImageReceived;
                    camera.ConnectionChanged += OnCameraConnectionChanged;
                }

                // 初始化真实PLC连接
                var plcConfig = ConfigManager.Instance.Plc;
                _plc = new OmronFinsCommunication(plcConfig.Connection.IP, plcConfig.Connection.Port);

                // 监听PLC连接状态变化，转发到EventAggregator
                _plc.ConnectionChanged += OnPlcConnectionChanged;

                FileLogger.Instance.Info($"正在连接PLC: {plcConfig.Connection.IP}:{plcConfig.Connection.Port}", "Inspection");

                // 自动连接PLC
                var plcConnected = await _plc.ConnectAsync();

                // 发布初始连接状态
                PublishPlcConnectionState(plcConnected);

                if (plcConnected)
                {
                    FileLogger.Instance.Info("PLC连接成功", "Inspection");
                }
                else
                {
                    FileLogger.Instance.Warning("PLC连接失败，将在后台持续重试", "Inspection");
                    // 启动后台重连
                    StartPlcReconnect();
                }

                // 初始化视觉处理器（暂用模拟处理器，后续替换为VisionPro）
                _visionProcessors[1] = new MockVisionProcessor(1);
                _visionProcessors[2] = new MockVisionProcessor(2);

                // 加载视觉工具块配置
                var visionConfig = ConfigManager.Instance.Vision;
                FileLogger.Instance.Info($"视觉配置: 工位1={visionConfig.Station1VppPath}, 工位2={visionConfig.Station2VppPath}", "Inspection");

                FileLogger.Instance.Info("检测服务初始化完成", "Inspection");
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"检测服务初始化失败: {ex.Message}", ex, "Inspection");
                ErrorOccurred?.Invoke(this, ex.Message);
                return false;
            }
        }

        public async Task<bool> StartAsync()
        {
            if (CurrentState == SystemState.Running)
                return true;

            try
            {
                // 检查视觉工具块是否加载
                if (!_visionProcessors[1].IsLoaded || !_visionProcessors[2].IsLoaded)
                {
                    ErrorOccurred?.Invoke(this, "视觉工具块未加载");
                    return false;
                }

                _runCts = new CancellationTokenSource();
                _consecutiveFailures = 0;

                // 启动相机采集
                foreach (var camera in _cameras.Values)
                {
                    camera.StartGrab();
                }

                CurrentState = SystemState.Running;
                FileLogger.Instance.Info("检测已启动", "Inspection");

                // 启动触发监听（模拟模式下使用定时触发）
                _ = Task.Run(() => MonitorTriggerAsync(_runCts.Token));

                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"启动检测失败: {ex.Message}", ex, "Inspection");
                ErrorOccurred?.Invoke(this, ex.Message);
                return false;
            }
        }

        public async Task StopAsync()
        {
            if (CurrentState != SystemState.Running && CurrentState != SystemState.Paused)
                return;

            try
            {
                _runCts?.Cancel();

                // 停止相机采集
                foreach (var camera in _cameras.Values)
                {
                    camera.StopGrab();
                }

                CurrentState = SystemState.Idle;
                FileLogger.Instance.Info("检测已停止", "Inspection");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"停止检测失败: {ex.Message}", ex, "Inspection");
            }
        }

        public void Pause()
        {
            if (CurrentState == SystemState.Running)
            {
                CurrentState = SystemState.Paused;
                FileLogger.Instance.Info("检测已暂停", "Inspection");
            }
        }

        public void Resume()
        {
            if (CurrentState == SystemState.Paused)
            {
                CurrentState = SystemState.Running;
                FileLogger.Instance.Info("检测已恢复", "Inspection");
            }
        }

        public async Task<InspectionResult> ExecuteSingleAsync(int stationId, Bitmap image = null)
        {
            try
            {
                if (!_visionProcessors.ContainsKey(stationId) || !_visionProcessors[stationId].IsLoaded)
                {
                    return new InspectionResult
                    {
                        StationId = stationId,
                        Timestamp = DateTime.Now,
                        ResultType = InspectionResultType.Error,
                        ErrorMessage = "视觉工具块未加载"
                    };
                }

                // 如果没有提供图像，从相机采集
                if (image == null)
                {
                    image = await TriggerCaptureAsync(stationId);
                    if (image == null)
                    {
                        return new InspectionResult
                        {
                            StationId = stationId,
                            Timestamp = DateTime.Now,
                            ResultType = InspectionResultType.Error,
                            ErrorMessage = "图像采集失败"
                        };
                    }
                }

                // 执行视觉处理
                var visionResult = _visionProcessors[stationId].Execute(image);
                if (visionResult == null)
                {
                    return new InspectionResult
                    {
                        StationId = stationId,
                        Timestamp = DateTime.Now,
                        ResultType = InspectionResultType.Error,
                        ErrorMessage = "视觉处理失败"
                    };
                }

                var result = new InspectionResult
                {
                    StationId = stationId,
                    Timestamp = DateTime.Now,
                    ResultType = visionResult.Found ? InspectionResultType.Ok : InspectionResultType.Ng,
                    Score = visionResult.Score,
                    Angle = visionResult.Angle,
                    X = visionResult.X,
                    Y = visionResult.Y,
                    ProcessTimeMs = visionResult.ProcessTimeMs,
                    ResultImage = visionResult.ResultImage
                };

                return result;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"单次检测失败: {ex.Message}", ex, "Inspection");
                return new InspectionResult
                {
                    StationId = stationId,
                    Timestamp = DateTime.Now,
                    ResultType = InspectionResultType.Error,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<Bitmap> TriggerCaptureAsync(int stationId)
        {
            if (!_cameras.ContainsKey(stationId))
                return null;

            var camera = _cameras[stationId];
            if (!camera.IsConnected)
            {
                // 从配置获取UserId连接相机
                var cameraConfig = ConfigManager.Instance.Camera;
                var stationConfig = stationId == 1 ? cameraConfig.Station1 : cameraConfig.Station2;
                camera.Connect(stationConfig.UserId);
            }

            var tcs = new TaskCompletionSource<Bitmap>();
            var cts = new CancellationTokenSource(5000); // 5秒超时

            void handler(object s, ImageReceivedEventArgs e)
            {
                camera.ImageReceived -= handler;
                tcs.TrySetResult(e.Image);
            }

            camera.ImageReceived += handler;
            cts.Token.Register(() =>
            {
                camera.ImageReceived -= handler;
                tcs.TrySetCanceled();
            });

            camera.StartGrab();
            camera.SoftTrigger();

            try
            {
                return await tcs.Task;
            }
            catch (TaskCanceledException)
            {
                FileLogger.Instance.Warning($"工位{stationId}图像采集超时", "Inspection");
                return null;
            }
        }

        private async Task MonitorTriggerAsync(CancellationToken cancellationToken)
        {
            // 模拟模式下的触发监听
            // 实际生产中应该监听PLC触发信号
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(100, cancellationToken);

                    if (CurrentState != SystemState.Running)
                        continue;

                    // 检查PLC触发信号
                    var plcConfig = ConfigManager.Instance.Plc;

                    // 检查工位1触发
                    if (_plc.IsConnected)
                    {
                        var station1Trigger = await _plc.ReadBitAsync(plcConfig.InputAddresses.Station1Trigger.Address);
                        if (station1Trigger)
                        {
                            await ProcessStationAsync(1);
                        }

                        var station2Trigger = await _plc.ReadBitAsync(plcConfig.InputAddresses.Station2Trigger.Address);
                        if (station2Trigger)
                        {
                            await ProcessStationAsync(2);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Error($"触发监听错误: {ex.Message}", ex, "Inspection");
                }
            }
        }

        private async Task ProcessStationAsync(int stationId)
        {
            var result = await ExecuteSingleAsync(stationId);
            if (result == null)
                return;

            // 更新统计
            _statisticsService.AddResult(stationId, result.IsOk);

            // 写入PLC
            if (_plc.IsConnected)
            {
                var plcConfig = ConfigManager.Instance.Plc;

                // 写入结果 (1.0=OK, 0.0=NG) - REAL类型
                await _plc.WriteFloatAsync(plcConfig.OutputAddresses.Result.Address, result.IsOk ? 1.0f : 0.0f);

                // 写入角度值（仅OK时有效）- REAL类型
                if (result.IsOk)
                {
                    await _plc.WriteFloatAsync(plcConfig.OutputAddresses.RotationAngle.Address, (float)result.Angle);
                }
            }

            // 保存图片
            if (result.ResultImage != null)
            {
                result.ImagePath = ImageStorage.Instance.SaveImage(result.ResultImage, stationId, result.IsOk);
            }

            // 处理连续失败
            if (!result.IsOk)
            {
                _consecutiveFailures++;
                var threshold = ConfigManager.Instance.SystemCfg.ConsecutiveFailureAlarmThreshold;
                if (_consecutiveFailures >= threshold)
                {
                    ErrorOccurred?.Invoke(this, $"工位{stationId}连续{_consecutiveFailures}次检测失败");
                }
            }
            else
            {
                _consecutiveFailures = 0;
            }

            // 触发事件
            InspectionCompleted?.Invoke(this, new InspectionCompletedEventArgs { Result = result });

            EventAggregator.Instance.Publish(new InspectionCompletedEvent
            {
                StationId = stationId,
                IsOk = result.IsOk,
                Angle = result.Angle,
                Score = result.Score,
                Timestamp = result.Timestamp
            });
        }

        private void OnImageReceived(object sender, ImageReceivedEventArgs e)
        {
            // 根据相机实例确定工位ID
            int stationId = 0;
            foreach (var kvp in _cameras)
            {
                if (ReferenceEquals(kvp.Value, sender))
                {
                    stationId = kvp.Key;
                    break;
                }
            }

            // 图像接收处理
            EventAggregator.Instance.Publish(new ImageReceivedEvent
            {
                StationId = stationId,
                Width = e.Width,
                Height = e.Height,
                Timestamp = e.Timestamp
            });
        }

        private void OnCameraConnectionChanged(object sender, ConnectionChangedEventArgs e)
        {
            var camera = sender as ICamera;

            // 根据相机实例确定是Camera1还是Camera2
            string deviceType = "Camera";
            foreach (var kvp in _cameras)
            {
                if (ReferenceEquals(kvp.Value, sender))
                {
                    deviceType = $"Camera{kvp.Key}";
                    break;
                }
            }

            EventAggregator.Instance.Publish(new ConnectionChangedEvent
            {
                DeviceType = deviceType,
                DeviceName = camera?.FriendlyName ?? "Unknown",
                IsConnected = e.IsConnected
            });
        }

        private void OnPlcConnectionChanged(object sender, PlcConnectionChangedEventArgs e)
        {
            // PLC连接状态变化，转发到EventAggregator更新UI
            PublishPlcConnectionState(e.IsConnected);
            FileLogger.Instance.Info($"PLC连接状态变化: {(e.IsConnected ? "已连接" : "已断开")}", "Inspection");
        }

        private void PublishPlcConnectionState(bool isConnected)
        {
            var plcConfig = ConfigManager.Instance.Plc;
            EventAggregator.Instance.Publish(new ConnectionChangedEvent
            {
                DeviceType = "PLC",
                DeviceName = $"OmronPLC({plcConfig.Connection.IP})",
                IsConnected = isConnected
            });
        }

        private void StartPlcReconnect()
        {
            var plcConfig = ConfigManager.Instance.Plc;
            var reconnectInterval = plcConfig.Connection.ReconnectInterval;

            Task.Run(async () =>
            {
                while (!_isDisposed && !_plc.IsConnected)
                {
                    await Task.Delay(reconnectInterval);
                    if (_isDisposed) break;

                    FileLogger.Instance.Info("尝试重新连接PLC...", "Inspection");
                    var connected = await _plc.ConnectAsync();
                    if (connected)
                    {
                        PublishPlcConnectionState(true);
                        FileLogger.Instance.Info("PLC重连成功", "Inspection");
                        break;
                    }
                }
            });
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _runCts?.Cancel();
            _runCts?.Dispose();

            foreach (var camera in _cameras.Values)
            {
                camera.Dispose();
            }

            foreach (var vision in _visionProcessors.Values)
            {
                vision.Dispose();
            }

            _plc?.Dispose();
            _isDisposed = true;
        }
    }
}
