using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VisionOTA.Common.Events;
using VisionOTA.Common.Mvvm;
using VisionOTA.Hardware.Camera;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// 相机设置视图模型 - 使用 StationViewModel 简化重复代码
    /// </summary>
    public class CameraSettingsViewModel : ViewModelBase
    {
        #region 工位视图模型

        private StationViewModel _station1;
        private StationViewModel _station2;

        public StationViewModel Station1
        {
            get => _station1;
            set => SetProperty(ref _station1, value);
        }

        public StationViewModel Station2
        {
            get => _station2;
            set => SetProperty(ref _station2, value);
        }

        #endregion

        #region 相机选择

        private CameraInfo _station1SelectedCamera;
        private CameraInfo _station2SelectedCamera;

        public ObservableCollection<CameraInfo> Station1Cameras { get; } = new ObservableCollection<CameraInfo>();
        public ObservableCollection<CameraInfo> Station2Cameras { get; } = new ObservableCollection<CameraInfo>();

        public CameraInfo Station1SelectedCamera
        {
            get => _station1SelectedCamera;
            set
            {
                if (SetProperty(ref _station1SelectedCamera, value) && value != null)
                {
                    Station1.UserId = value.UserId;
                    Station1.FriendlyName = value.FriendlyName;
                }
            }
        }

        public CameraInfo Station2SelectedCamera
        {
            get => _station2SelectedCamera;
            set
            {
                if (SetProperty(ref _station2SelectedCamera, value) && value != null)
                {
                    Station2.UserId = value.UserId;
                    Station2.FriendlyName = value.FriendlyName;
                }
            }
        }

        #endregion

        #region 通用配置

        private bool _isBottleRotating;
        private bool _showCrosshair;

        public bool ShowCrosshair
        {
            get => _showCrosshair;
            set => SetProperty(ref _showCrosshair, value);
        }

        public bool IsBottleRotating
        {
            get => _isBottleRotating;
            set => SetProperty(ref _isBottleRotating, value);
        }

        public string BottleRotationButtonText => _isBottleRotating ? "停止旋转" : "旋转瓶身";

        public List<string> TriggerModes { get; } = new List<string>
        {
            "连续采集", "软件触发", "硬件触发"
        };

        public List<string> HardwareTriggerSources { get; } = new List<string>
        {
            "Line1", "Line2", "Line3"
        };

        public List<string> TriggerEdges { get; } = new List<string>
        {
            "上升沿", "下降沿"
        };

        #endregion

        #region Commands

        public ICommand SearchStation1Command { get; }
        public ICommand SearchStation2Command { get; }
        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ToggleBottleRotationCommand { get; }

        #endregion

        public event EventHandler<bool> RequestClose;

        public CameraSettingsViewModel()
        {
            Title = "相机设置";

            // 创建工位视图模型
            Station1 = new StationViewModel(1, () => CameraFactory.CreateAreaCamera());
            Station2 = new StationViewModel(2, () => CameraFactory.CreateLineCamera());

            // 订阅图像接收事件以在UI线程更新
            Station1.ImageReceived += OnStation1ImageReceived;
            Station2.ImageReceived += OnStation2ImageReceived;

            // 搜索命令
            SearchStation1Command = CommandFactory.Create(SearchStation1Cameras);
            SearchStation2Command = CommandFactory.Create(SearchStation2Cameras);

            // 通用命令
            SaveCommand = CommandFactory.Create(Save);
            CloseCommand = CommandFactory.Create(Close);
            ToggleBottleRotationCommand = CommandFactory.Create(ToggleBottleRotation);

            LoadConfig();

            // 窗口加载后自动搜索相机
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchStation1Cameras();
                SearchStation2Cameras();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        #region 相机搜索

        private void SearchStation1Cameras()
        {
            try
            {
                Station1Cameras.Clear();
                using (var camera = CameraFactory.CreateAreaCamera())
                {
                    var cameras = camera.SearchCameras();
                    foreach (var cam in cameras)
                    {
                        Station1Cameras.Add(cam);
                    }

                    if (cameras.Length == 0)
                    {
                        MessageBox.Show("未找到相机", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        this.LogInfo($"搜索到 {cameras.Length} 个面阵相机");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                this.LogError($"搜索工位1相机失败", ex);
            }
        }

        private void SearchStation2Cameras()
        {
            try
            {
                Station2Cameras.Clear();
                using (var camera = CameraFactory.CreateLineCamera())
                {
                    var cameras = camera.SearchCameras();
                    foreach (var cam in cameras)
                    {
                        Station2Cameras.Add(cam);
                    }

                    if (cameras.Length == 0)
                    {
                        MessageBox.Show("未找到相机", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        this.LogInfo($"搜索到 {cameras.Length} 个线扫相机");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                this.LogError($"搜索工位2相机失败", ex);
            }
        }

        #endregion

        #region 配置加载/保存

        private void LoadConfig()
        {
            var config = ConfigManager.Instance.Camera;

            // 工位1
            Station1.UserId = config.Station1.UserId ?? "";
            Station1.Exposure = config.Station1.Exposure;
            Station1.Gain = config.Station1.Gain;
            LoadTriggerConfig(config.Station1.TriggerSource, config.Station1.TriggerEdge, Station1);

            // 工位2
            Station2.UserId = config.Station2.UserId ?? "";
            Station2.Exposure = config.Station2.Exposure;
            Station2.Gain = config.Station2.Gain;
            Station2.LineRate = config.Station2.LineRate;
            Station2.LineCount = config.Station2.LineCount;
            LoadTriggerConfig(config.Station2.TriggerSource, config.Station2.TriggerEdge, Station2);
        }

        private void LoadTriggerConfig(string triggerSource, string triggerEdge, StationViewModel station)
        {
            switch (triggerSource)
            {
                case "Software":
                    station.TriggerMode = "软件触发";
                    station.HardwareTriggerSource = "Line1";
                    break;
                case "Line1":
                case "Line2":
                case "Line3":
                    station.TriggerMode = "硬件触发";
                    station.HardwareTriggerSource = triggerSource;
                    break;
                default:
                    station.TriggerMode = "连续采集";
                    station.HardwareTriggerSource = "Line1";
                    break;
            }

            station.TriggerEdge = ConvertTriggerEdgeToDisplay(triggerEdge);
        }

        private string GetTriggerSourceFromMode(StationViewModel station)
        {
            switch (station.TriggerMode)
            {
                case "软件触发": return "Software";
                case "硬件触发": return station.HardwareTriggerSource;
                default: return "Continuous";
            }
        }

        private string ConvertTriggerEdgeToDisplay(string edge)
        {
            switch (edge)
            {
                case "FallingEdge": return "下降沿";
                case "DoubleEdge": return "双边沿";
                default: return "上升沿";
            }
        }

        private string ConvertTriggerEdgeToConfig(string display)
        {
            switch (display)
            {
                case "下降沿": return "FallingEdge";
                case "双边沿": return "DoubleEdge";
                default: return "RisingEdge";
            }
        }

        private void Save()
        {
            try
            {
                var config = ConfigManager.Instance.Camera;

                config.Station1.UserId = Station1.UserId;
                config.Station1.Exposure = Station1.Exposure;
                config.Station1.Gain = Station1.Gain;
                config.Station1.TriggerSource = GetTriggerSourceFromMode(Station1);
                config.Station1.TriggerEdge = ConvertTriggerEdgeToConfig(Station1.TriggerEdge);

                config.Station2.UserId = Station2.UserId;
                config.Station2.Exposure = Station2.Exposure;
                config.Station2.Gain = Station2.Gain;
                config.Station2.LineRate = Station2.LineRate;
                config.Station2.LineCount = Station2.LineCount;
                config.Station2.TriggerSource = GetTriggerSourceFromMode(Station2);
                config.Station2.TriggerEdge = ConvertTriggerEdgeToConfig(Station2.TriggerEdge);

                ConfigManager.Instance.SaveCameraConfig();

                this.LogInfo("相机配置已保存");
                MessageBox.Show("配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                this.LogError("保存相机配置失败", ex);
            }
        }

        #endregion

        #region 瓶身旋转

        private void ToggleBottleRotation()
        {
            IsBottleRotating = !IsBottleRotating;
            OnPropertyChanged(nameof(BottleRotationButtonText));

            EventAggregator.Instance.Publish(new BottleRotateCommand
            {
                Rotate = IsBottleRotating
            });

            this.LogInfo($"瓶身旋转: {(IsBottleRotating ? "开始" : "停止")}");
        }

        #endregion

        #region 图像事件处理

        private void OnStation1ImageReceived(object sender, ImageReceivedEventArgs e)
        {
            // 图像已在 StationViewModel 中更新
            e.Image?.Dispose();
        }

        private void OnStation2ImageReceived(object sender, ImageReceivedEventArgs e)
        {
            // 图像已在 StationViewModel 中更新
            e.Image?.Dispose();
        }

        #endregion

        private void Close()
        {
            if (_isBottleRotating)
            {
                ToggleBottleRotation();
            }
            RequestClose?.Invoke(this, true);
        }

        public override void Cleanup()
        {
            Station1?.Dispose();
            Station2?.Dispose();
            base.Cleanup();
        }
    }
}
