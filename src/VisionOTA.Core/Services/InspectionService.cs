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
        private CancellationTokenSource _reconnectCts;
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
        /// 发送测试触发信号到PLC
        /// </summary>
        /// <param name="stationId">工位ID (1=面阵, 2=线扫)</param>
        /// <returns>是否成功</returns>
        public async Task<bool> SendTestTriggerAsync(int stationId)
        {
            if (_plc == null || !_plc.IsConnected)
            {
                FileLogger.Instance.Warning($"PLC未连接，无法发送工位{stationId}测试触发", "Inspection");
                return false;
            }

            try
            {
                var config = ConfigManager.Instance.Plc;
                var triggerConfig = stationId == 1
                    ? config.TestTrigger.Station1Trigger
                    : config.TestTrigger.Station2Trigger;

                var address = triggerConfig.Address;

                // 发送脉冲信号：置1，延时100ms，置0
                var success = await _plc.WriteBitAsync(address, true);
                if (success)
                {
                    FileLogger.Instance.Info($"工位{stationId}测试触发已发送: {address} = 1", "Inspection");
                    await Task.Delay(100);
                    await _plc.WriteBitAsync(address, false);
                    FileLogger.Instance.Debug($"工位{stationId}测试触发复位: {address} = 0", "Inspection");
                }
                else
                {
                    FileLogger.Instance.Warning($"工位{stationId}测试触发失败: 写入 {address} 失败", "Inspection");
                }

                return success;
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{stationId}测试触发异常: {ex.Message}", ex, "Inspection");
                return false;
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

                // 从CameraManager获取共享相机实例
                var cameraConfig = ConfigManager.Instance.Camera;
                _cameras[1] = CameraManager.Instance.GetCamera(1);
                _cameras[2] = CameraManager.Instance.GetCamera(2);

                // 注册相机事件
                foreach (var kvp in _cameras)
                {
                    var stationId = kvp.Key;
                    var camera = kvp.Value;
                    camera.ImageReceived += OnImageReceived;
                    camera.ConnectionChanged += OnCameraConnectionChanged;
                    FileLogger.Instance.Info($"工位{stationId}相机事件已注册, 实例HashCode={camera.GetHashCode()}", "Inspection");
                }

                // 使用配置的userId连接相机，并应用触发源设置
                await ConnectAndConfigureCameraAsync(1, cameraConfig.Station1);
                await ConnectAndConfigureCameraAsync(2, cameraConfig.Station2);

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

                // 初始化VisionMaster方案
                var visionConfig = ConfigManager.Instance.Vision;
                if (!string.IsNullOrEmpty(visionConfig.VisionMaster?.SolutionPath))
                {
                    var solutionLoaded = VisionMasterSolutionManager.Instance.LoadSolution(
                        visionConfig.VisionMaster.SolutionPath,
                        visionConfig.VisionMaster.Password ?? "");

                    // 发布Vision状态事件
                    EventAggregator.Instance.Publish(new ConnectionChangedEvent
                    {
                        DeviceType = "Vision",
                        DeviceName = "VisionMaster",
                        IsConnected = solutionLoaded
                    });

                    if (!solutionLoaded)
                    {
                        FileLogger.Instance.Warning("VisionMaster方案加载失败，将使用模拟处理器", "Inspection");
                    }
                }

                // 初始化视觉处理器
                var processor1 = new VisionMasterProcessor(1);
                var processor2 = new VisionMasterProcessor(2);

                // 配置工位1（瓶底定位）
                processor1.ConfigureInputImageSource(visionConfig.Station1.InputImageSourceName);
                processor1.ConfigureOutputs(
                    visionConfig.Station1.AngleOutputName,
                    visionConfig.Station1.ResultImageOutputName);
                processor1.LoadToolBlock(visionConfig.Station1.ProcedureName);

                // 配置工位2（瓶身定位）
                processor2.ConfigureInputImageSource(visionConfig.Station2.InputImageSourceName);
                processor2.ConfigureOutputs(
                    visionConfig.Station2.AngleOutputName,
                    visionConfig.Station2.ResultImageOutputName);
                processor2.LoadToolBlock(visionConfig.Station2.ProcedureName);

                _visionProcessors[1] = processor1;
                _visionProcessors[2] = processor2;

                FileLogger.Instance.Info($"视觉配置: 工位1={visionConfig.Station1.ProcedureName}, 工位2={visionConfig.Station2.ProcedureName}", "Inspection");

                // 读取PLC旋转角度并写入VisionMaster瓶身工位
                FileLogger.Instance.Info(">>> 准备调用 InitializeRotationAngleAsync", "Inspection");
                await InitializeRotationAngleAsync(processor2, plcConfig);
                FileLogger.Instance.Info(">>> InitializeRotationAngleAsync 调用完成", "Inspection");

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

        /// <summary>
        /// 检测前设置旋转角度（从PLC读取并写入VisionMaster全局变量）
        /// </summary>
        private async Task SetRotationAngleBeforeInspectionAsync()
        {
            try
            {
                if (_plc == null || !_plc.IsConnected)
                {
                    return;
                }

                var plcConfig = ConfigManager.Instance.Plc;
                var rotationAngleConfig = plcConfig.InputAddresses.RotationAngle;
                if (rotationAngleConfig == null || string.IsNullOrEmpty(rotationAngleConfig.Address))
                {
                    return;
                }

                // 从PLC读取旋转角度
                var rotationAngle = await _plc.ReadFloatAsync(rotationAngleConfig.Address);

                // 写入VisionMaster瓶身工位的全局变量
                if (_visionProcessors.ContainsKey(2))
                {
                    var processor = _visionProcessors[2] as VisionMasterProcessor;
                    if (processor != null && processor.IsLoaded)
                    {
                        processor.SetInputParameter("旋转角度", rotationAngle);
                        FileLogger.Instance.Debug($"瓶身工位检测前设置旋转角度: {rotationAngle:F2}", "Inspection");
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Warning($"设置旋转角度失败: {ex.Message}", "Inspection");
            }
        }

        /// <summary>
        /// 初始化旋转角度参数（从PLC读取并写入VisionMaster）
        /// </summary>
        private async Task InitializeRotationAngleAsync(VisionMasterProcessor processor, PlcConfig plcConfig)
        {
            FileLogger.Instance.Info(">>> InitializeRotationAngleAsync 开始执行", "Inspection");
            try
            {
                FileLogger.Instance.Debug($"PLC状态: _plc={(_plc != null ? "存在" : "null")}, IsConnected={_plc?.IsConnected}", "Inspection");

                if (_plc == null || !_plc.IsConnected)
                {
                    FileLogger.Instance.Warning("PLC未连接，无法读取旋转角度参数", "Inspection");
                    return;
                }

                var rotationAngleConfig = plcConfig.InputAddresses.RotationAngle;
                if (rotationAngleConfig == null || string.IsNullOrEmpty(rotationAngleConfig.Address))
                {
                    FileLogger.Instance.Warning("旋转角度地址未配置", "Inspection");
                    return;
                }

                // 从PLC读取旋转角度
                var rotationAngle = await _plc.ReadFloatAsync(rotationAngleConfig.Address);
                FileLogger.Instance.Info($"从PLC读取旋转角度: {rotationAngle:F2}, 地址: {rotationAngleConfig.Address}", "Inspection");

                // 写入VisionMaster瓶身工位的输入参数
                if (processor != null && processor.IsLoaded)
                {
                    var success = processor.SetInputParameter("旋转角度", rotationAngle);
                    if (success)
                    {
                        FileLogger.Instance.Info($"旋转角度已写入VisionMaster瓶身工位: {rotationAngle:F2}", "Inspection");
                    }
                    else
                    {
                        FileLogger.Instance.Warning($"旋转角度写入VisionMaster失败", "Inspection");
                    }
                }
                else
                {
                    FileLogger.Instance.Warning("瓶身工位视觉处理器未加载，无法设置旋转角度", "Inspection");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"初始化旋转角度失败: {ex.Message}", ex, "Inspection");
            }
        }

        /// <summary>
        /// 连接相机并配置触发源
        /// </summary>
        private async Task ConnectAndConfigureCameraAsync(int stationId, StationCameraConfig config)
        {
            if (!_cameras.ContainsKey(stationId))
                return;

            var camera = _cameras[stationId];

            // 检查UserId是否配置
            if (string.IsNullOrEmpty(config.UserId))
            {
                FileLogger.Instance.Warning($"工位{stationId}相机UserId未配置，跳过自动连接", "Inspection");
                return;
            }

            try
            {
                // 使用UserId连接相机
                FileLogger.Instance.Info($"工位{stationId}正在连接相机: UserId={config.UserId}", "Inspection");
                var connected = camera.Connect(config.UserId);

                if (connected)
                {
                    FileLogger.Instance.Info($"工位{stationId}相机连接成功: {camera.FriendlyName}", "Inspection");

                    // 应用曝光和增益
                    camera.SetExposure(config.Exposure);
                    camera.SetGain(config.Gain);

                    // 从配置读取并应用触发源
                    var triggerSource = ConvertConfigToTriggerSource(config.TriggerSource);
                    camera.SetTriggerSource(triggerSource);
                    FileLogger.Instance.Info($"工位{stationId}触发源设置为: {config.TriggerSource}", "Inspection");

                    // 线扫相机额外配置
                    if (camera is ILineCamera lineCamera && config is LineCameraConfig lineConfig)
                    {
                        lineCamera.SetLineRate(lineConfig.LineRate);
                    }

                    // 发布连接状态
                    EventAggregator.Instance.Publish(new ConnectionChangedEvent
                    {
                        DeviceType = $"Camera{stationId}",
                        DeviceName = camera.FriendlyName,
                        IsConnected = true
                    });
                }
                else
                {
                    FileLogger.Instance.Warning($"工位{stationId}相机连接失败: UserId={config.UserId}", "Inspection");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{stationId}相机连接异常: {ex.Message}", ex, "Inspection");
            }
        }

        /// <summary>
        /// 将配置文件触发源转换为枚举
        /// </summary>
        private TriggerSource ConvertConfigToTriggerSource(string configValue)
        {
            switch (configValue)
            {
                case "Software": return TriggerSource.Software;
                case "Line1": return TriggerSource.Line1;
                case "Line2": return TriggerSource.Line2;
                case "Line3": return TriggerSource.Line3;
                case "Line4": return TriggerSource.Line4;
                case "Line5": return TriggerSource.Line5;
                case "Line6": return TriggerSource.Line6;
                case "Line7": return TriggerSource.Line7;
                case "Line8": return TriggerSource.Line8;
                default: return TriggerSource.Continuous;
            }
        }

        public async Task<bool> StartAsync()
        {
            if (CurrentState == SystemState.Running)
                return true;

            try
            {
                _runCts = new CancellationTokenSource();
                _consecutiveFailures = 0;

                // 检查并重连相机，然后启动采集（两工位独立运行）
                var cameraConfig = ConfigManager.Instance.Camera;
                int connectedCount = 0;
                var failedStations = new List<int>();

                foreach (var kvp in _cameras)
                {
                    var stationId = kvp.Key;
                    var camera = kvp.Value;
                    var stationConfig = stationId == 1 ? cameraConfig.Station1 : cameraConfig.Station2;

                    // 检查视觉处理器是否加载
                    if (!_visionProcessors.ContainsKey(stationId) || !_visionProcessors[stationId].IsLoaded)
                    {
                        FileLogger.Instance.Warning($"工位{stationId}视觉处理器未加载，跳过", "Inspection");
                        failedStations.Add(stationId);
                        continue;
                    }

                    // 如果相机未连接，尝试重连
                    if (!camera.IsConnected)
                    {
                        FileLogger.Instance.Info($"工位{stationId}相机未连接，尝试重连...", "Inspection");

                        if (string.IsNullOrEmpty(stationConfig.UserId))
                        {
                            FileLogger.Instance.Warning($"工位{stationId}相机UserId未配置，跳过该工位", "Inspection");
                            failedStations.Add(stationId);
                            continue;
                        }

                        if (!camera.Connect(stationConfig.UserId))
                        {
                            FileLogger.Instance.Warning($"工位{stationId}相机重连失败，跳过该工位", "Inspection");
                            failedStations.Add(stationId);
                            continue;
                        }

                        FileLogger.Instance.Info($"工位{stationId}相机重连成功", "Inspection");

                        // 从配置读取触发源
                        var triggerSource = ConvertConfigToTriggerSource(stationConfig.TriggerSource);
                        camera.SetTriggerSource(triggerSource);
                        FileLogger.Instance.Debug($"工位{stationId}触发源设置为: {stationConfig.TriggerSource}", "Inspection");
                    }

                    // 启动采集
                    if (!camera.IsGrabbing)
                    {
                        camera.StartGrab();
                        FileLogger.Instance.Debug($"工位{stationId}相机开始采集", "Inspection");
                    }

                    connectedCount++;
                }

                // 如果所有工位都失败，返回错误
                if (connectedCount == 0)
                {
                    ErrorOccurred?.Invoke(this, "所有工位相机均未连接");
                    return false;
                }

                // 部分工位失败时发出警告
                if (failedStations.Count > 0)
                {
                    var msg = $"工位{string.Join(",", failedStations)}未就绪，其他工位正常运行";
                    FileLogger.Instance.Warning(msg, "Inspection");
                    ErrorOccurred?.Invoke(this, msg);
                }

                CurrentState = SystemState.Running;
                FileLogger.Instance.Info("检测已启动 (硬件触发模式)", "Inspection");

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

        /// <summary>
        /// 使用图片文件路径执行单次检测
        /// </summary>
        public async Task<InspectionResult> ExecuteSingleWithFilePathAsync(int stationId, string imagePath)
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

                // 瓶身工位（工位2）检测前，先设置旋转角度
                if (stationId == 2)
                {
                    await SetRotationAngleBeforeInspectionAsync();
                }

                // 使用文件路径执行视觉处理
                var visionResult = _visionProcessors[stationId].ExecuteWithFilePath(imagePath);
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
                FileLogger.Instance.Error($"文件路径检测失败: {ex.Message}", ex, "Inspection");
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


        private async void OnImageReceived(object sender, ImageReceivedEventArgs e)
        {
            // 如果已释放，直接返回
            if (_isDisposed)
            {
                FileLogger.Instance.Debug("InspectionService已释放，忽略图像", "Inspection");
                return;
            }

            FileLogger.Instance.Debug($"InspectionService收到图像事件: {e.Width}x{e.Height}", "Inspection");

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

            if (stationId == 0)
            {
                FileLogger.Instance.Warning("收到图像但无法确定工位ID（相机实例不匹配）", "Inspection");
                return;
            }

            FileLogger.Instance.Debug($"工位{stationId}收到图像, 当前状态: {CurrentState}", "Inspection");

            // 发布图像接收事件（UI更新）
            EventAggregator.Instance.Publish(new ImageReceivedEvent
            {
                StationId = stationId,
                Width = e.Width,
                Height = e.Height,
                Timestamp = e.Timestamp
            });

            // 如果系统正在运行且是硬件触发模式，直接执行检测
            if (!_isDisposed && CurrentState == SystemState.Running && e.Image != null)
            {
                FileLogger.Instance.Info($"工位{stationId}开始执行检测流程", "Inspection");
                await ProcessImageAsync(stationId, e.Image);
            }
            else
            {
                FileLogger.Instance.Debug($"工位{stationId}图像不处理: State={CurrentState}, Image={e.Image != null}", "Inspection");
            }
        }

        /// <summary>
        /// 处理图像（硬件触发模式）
        /// </summary>
        private async Task ProcessImageAsync(int stationId, Bitmap image)
        {
            try
            {
                // 瓶身工位（工位2）检测前，先设置旋转角度
                if (stationId == 2)
                {
                    await SetRotationAngleBeforeInspectionAsync();
                }

                // 1. 执行视觉处理（只提取角度，不获取结果图）
                if (!_visionProcessors.ContainsKey(stationId) || !_visionProcessors[stationId].IsLoaded)
                {
                    FileLogger.Instance.Warning($"工位{stationId}视觉处理器未加载", "Inspection");
                    return;
                }

                var visionResult = _visionProcessors[stationId].Execute(image);

                var result = new InspectionResult
                {
                    StationId = stationId,
                    Timestamp = DateTime.Now,
                    ResultType = visionResult.Found ? InspectionResultType.Ok : InspectionResultType.Ng,
                    Score = visionResult.Score,
                    Angle = visionResult.Angle,
                    X = visionResult.X,
                    Y = visionResult.Y,
                    ProcessTimeMs = visionResult.ProcessTimeMs
                };

                // 2. 优先写入PLC（角度和结果）
                if (_plc != null && _plc.IsConnected)
                {
                    await WritePlcResultAsync(result);
                }

                // 3. 统计更新
                _statisticsService.AddResult(stationId, result.IsOk);

                // 4. 获取结果图并保存（在PLC写入后执行）
                var processor = _visionProcessors[stationId] as VisionMasterProcessor;
                if (processor != null)
                {
                    processor.FillResultImage(visionResult, image);
                    result.ResultImage = visionResult.ResultImage;
                }

                // 5. 图片保存（异步）
                if (result.ResultImage != null)
                {
                    _ = Task.Run(() =>
                    {
                        result.ImagePath = ImageStorage.Instance.SaveImage(result.ResultImage, stationId, result.IsOk);
                    });
                }

                // 6. 处理连续失败
                HandleConsecutiveFailures(stationId, result.IsOk);

                // 7. 触发完成事件（UI更新）
                InspectionCompleted?.Invoke(this, new InspectionCompletedEventArgs { Result = result });

                EventAggregator.Instance.Publish(new InspectionCompletedEvent
                {
                    StationId = stationId,
                    IsOk = result.IsOk,
                    Angle = result.Angle,
                    Score = result.Score,
                    Timestamp = result.Timestamp
                });

                FileLogger.Instance.Debug($"工位{stationId}检测完成: {(result.IsOk ? "OK" : "NG")}, 角度={result.Angle:F2}, 耗时={result.ProcessTimeMs:F0}ms", "Inspection");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"工位{stationId}检测处理失败: {ex.Message}", ex, "Inspection");
            }
        }

        /// <summary>
        /// 写入PLC结果
        /// </summary>
        private async Task WritePlcResultAsync(InspectionResult result)
        {
            try
            {
                var plcConfig = ConfigManager.Instance.Plc;

                // 写入角度值 - OK时写实际角度，NG时写0
                float angleToWrite = result.IsOk ? (float)result.Angle : 0.0f;
                await _plc.WriteFloatAsync(plcConfig.OutputAddresses.RotationAngle.Address, angleToWrite);

                // 写入检测结果 (2=合格, 3=不合格)
                float resultValue = result.IsOk ? 2.0f : 3.0f;
                await _plc.WriteFloatAsync(plcConfig.OutputAddresses.Result.Address, resultValue);

                FileLogger.Instance.Info($"工位{result.StationId}PLC写入: 角度={angleToWrite:F2}, 结果={resultValue}({(result.IsOk ? "合格" : "不合格")})", "Inspection");
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"写入PLC失败: {ex.Message}", ex, "Inspection");
            }
        }

        /// <summary>
        /// 处理连续失败
        /// </summary>
        private void HandleConsecutiveFailures(int stationId, bool isOk)
        {
            if (!isOk)
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
            // 取消之前的重连任务
            _reconnectCts?.Cancel();
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            var plcConfig = ConfigManager.Instance.Plc;
            var reconnectInterval = plcConfig.Connection.ReconnectInterval;

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested && !_isDisposed && !_plc.IsConnected)
                    {
                        await Task.Delay(reconnectInterval, token);
                        if (token.IsCancellationRequested || _isDisposed) break;

                        FileLogger.Instance.Info("尝试重新连接PLC...", "Inspection");
                        var connected = await _plc.ConnectAsync();
                        if (connected)
                        {
                            PublishPlcConnectionState(true);
                            FileLogger.Instance.Info("PLC重连成功", "Inspection");
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，忽略
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Debug($"PLC重连任务结束: {ex.Message}", "Inspection");
                }
            }, token);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true; // 先设置标志，防止异步操作继续

            // 取消所有后台任务
            try
            {
                _runCts?.Cancel();
                _reconnectCts?.Cancel();
            }
            catch { }

            // 给异步操作一点时间完成
            Task.Delay(100).Wait();

            try
            {
                _runCts?.Dispose();
                _reconnectCts?.Dispose();
            }
            catch { }

            // 取消相机事件订阅（相机实例由CameraManager管理，不在此释放）
            foreach (var camera in _cameras.Values)
            {
                try
                {
                    camera.ImageReceived -= OnImageReceived;
                    camera.ConnectionChanged -= OnCameraConnectionChanged;
                }
                catch { }
            }
            _cameras.Clear();

            foreach (var vision in _visionProcessors.Values)
            {
                try { vision.Dispose(); } catch { }
            }

            // 关闭VisionMaster方案（在后台线程，带超时）
            try
            {
                var disposeTask = Task.Run(() =>
                {
                    try
                    {
                        VisionMasterSolutionManager.Instance.Dispose();
                    }
                    catch { }
                });
                disposeTask.Wait(3000); // 最多等待3秒
            }
            catch { }

            try { _plc?.Dispose(); } catch { }

            FileLogger.Instance.Info("检测服务已释放", "Inspection");
        }
    }
}
