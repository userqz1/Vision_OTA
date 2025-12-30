using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VisionOTA.Common.Constants;
using VisionOTA.Common.Events;
using VisionOTA.Common.Mvvm;
using VisionOTA.Core.Interfaces;
using VisionOTA.Core.Models;
using VisionOTA.Core.Services;
using VisionOTA.Infrastructure.Logging;
using VisionOTA.Infrastructure.Permission;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// 主视图模型 - 使用 StationViewModel 简化重复代码
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly IInspectionService _inspectionService;
        private readonly IStatisticsService _statisticsService;
        private readonly DispatcherTimer _timer;

        #region 工位视图模型

        public StationViewModel Station1 { get; }
        public StationViewModel Station2 { get; }

        #endregion

        #region 系统状态

        private string _systemStatus = "空闲";
        private SolidColorBrush _systemStatusColor = new SolidColorBrush(Colors.Gray);
        private string _currentUser = "未登录";
        private string _statusMessage = "系统就绪";
        private DateTime _currentTime;

        public string SystemStatus
        {
            get => _systemStatus;
            set => SetProperty(ref _systemStatus, value);
        }

        public SolidColorBrush SystemStatusColor
        {
            get => _systemStatusColor;
            set => SetProperty(ref _systemStatusColor, value);
        }

        public string CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public DateTime CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        #endregion

        #region 登录相关

        private bool _isLoginPanelVisible;
        private string _selectedUserType;
        private string _loginPassword = string.Empty;
        private string _loginError = string.Empty;
        private bool _isLoggedIn;

        public string[] UserTypes { get; } = { "操作员", "工程师", "管理员" };

        public bool IsAdmin => PermissionService.Instance.HasPermission(PermissionLevel.Administrator);

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set
            {
                if (SetProperty(ref _isLoggedIn, value))
                {
                    OnPropertyChanged(nameof(IsAdmin));
                    OnPropertyChanged(nameof(CanOperate));
                }
            }
        }

        public bool CanOperate => IsLoggedIn && PermissionService.Instance.HasPermission(PermissionLevel.Operator);

        public bool IsLoginPanelVisible
        {
            get => _isLoginPanelVisible;
            set => SetProperty(ref _isLoginPanelVisible, value);
        }

        public string SelectedUserType
        {
            get => _selectedUserType;
            set => SetProperty(ref _selectedUserType, value);
        }

        public string LoginPassword
        {
            get => _loginPassword;
            set => SetProperty(ref _loginPassword, value);
        }

        public string LoginError
        {
            get => _loginError;
            set => SetProperty(ref _loginError, value);
        }

        #endregion

        #region 设备状态

        private SolidColorBrush _plcStatusColor = new SolidColorBrush(Colors.Gray);
        private SolidColorBrush _camera1StatusColor = new SolidColorBrush(Colors.Gray);
        private SolidColorBrush _camera2StatusColor = new SolidColorBrush(Colors.Gray);
        private SolidColorBrush _visionStatusColor = new SolidColorBrush(Colors.Gray);
        private bool _showCrosshair;

        public SolidColorBrush PlcStatusColor
        {
            get => _plcStatusColor;
            set => SetProperty(ref _plcStatusColor, value);
        }

        public SolidColorBrush Camera1StatusColor
        {
            get => _camera1StatusColor;
            set => SetProperty(ref _camera1StatusColor, value);
        }

        public SolidColorBrush Camera2StatusColor
        {
            get => _camera2StatusColor;
            set => SetProperty(ref _camera2StatusColor, value);
        }

        public SolidColorBrush VisionStatusColor
        {
            get => _visionStatusColor;
            set => SetProperty(ref _visionStatusColor, value);
        }

        public bool ShowCrosshair
        {
            get => _showCrosshair;
            set => SetProperty(ref _showCrosshair, value);
        }

        #endregion

        #region Commands

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ShowLoginCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand CancelLoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand OpenCameraSettingsCommand { get; }
        public ICommand OpenPlcSettingsCommand { get; }
        public ICommand OpenVisionSettingsCommand { get; }
        public ICommand OpenStatisticsCommand { get; }
        public ICommand OpenLogCommand { get; }
        public ICommand OpenUserManagementCommand { get; }
        public ICommand TestTriggerStation1Command { get; }
        public ICommand TestTriggerStation2Command { get; }
        public ICommand TestImageStation2Command { get; }

        #endregion

        public MainViewModel()
        {
            _statisticsService = new StatisticsService();
            _inspectionService = new InspectionService(_statisticsService);

            // 创建工位视图模型（无相机控制）
            Station1 = new StationViewModel(1, null);
            Station2 = new StationViewModel(2, null);

            // 初始化命令
            StartCommand = new RelayCommand(async _ => await StartInspection(), _ => CanOperate && _inspectionService.CurrentState != SystemState.Running);
            StopCommand = new RelayCommand(async _ => await StopInspection(), _ => CanOperate && _inspectionService.CurrentState == SystemState.Running);
            ShowLoginCommand = new RelayCommand(_ => ShowLoginPanel(), _ => !IsLoggedIn);
            LoginCommand = new RelayCommand(_ => DoLogin());
            CancelLoginCommand = new RelayCommand(_ => CancelLogin());
            LogoutCommand = new RelayCommand(_ => Logout(), _ => IsLoggedIn);
            OpenCameraSettingsCommand = new RelayCommand(_ => OpenCameraSettings(), _ => CanOperate);
            OpenPlcSettingsCommand = new RelayCommand(_ => OpenPlcSettings(), _ => CanOperate);
            OpenVisionSettingsCommand = new RelayCommand(_ => OpenVisionSettings(), _ => CanOperate);
            OpenStatisticsCommand = new RelayCommand(_ => OpenStatistics());
            OpenLogCommand = new RelayCommand(_ => OpenLog());
            OpenUserManagementCommand = new RelayCommand(_ => OpenUserManagement(), _ => IsAdmin);
            TestTriggerStation1Command = new RelayCommand(async _ => await SendTestTrigger(1), _ => CanOperate);
            TestTriggerStation2Command = new RelayCommand(async _ => await SendTestTrigger(2), _ => CanOperate);
            TestImageStation2Command = new RelayCommand(_ => TestImageWithFile(2));

            // 订阅事件
            _inspectionService.InspectionCompleted += OnInspectionCompleted;
            _inspectionService.StateChanged += OnStateChanged;
            _inspectionService.ErrorOccurred += OnErrorOccurred;

            EventAggregator.Instance.Subscribe<ConnectionChangedEvent>(OnConnectionChanged);

            // 初始化定时器
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                CurrentTime = DateTime.Now;
                UpdateStatistics();
            };
            _timer.Start();

            UpdateLoginState();
        }

        #region 登录操作

        private void ShowLoginPanel()
        {
            SelectedUserType = UserTypes[0];
            LoginPassword = string.Empty;
            LoginError = string.Empty;
            IsLoginPanelVisible = true;
        }

        private void DoLogin()
        {
            if (string.IsNullOrWhiteSpace(SelectedUserType))
            {
                LoginError = "请选择用户类型";
                return;
            }

            if (string.IsNullOrWhiteSpace(LoginPassword))
            {
                LoginError = "请输入密码";
                return;
            }

            var success = PermissionService.Instance.Login(SelectedUserType, LoginPassword);
            if (success)
            {
                IsLoginPanelVisible = false;
                UpdateLoginState();
                StatusMessage = $"{CurrentUser} 已登录";
                this.LogInfo($"{CurrentUser} 登录成功");
            }
            else
            {
                LoginError = "密码错误";
                LoginPassword = string.Empty;
                this.LogWarning($"{SelectedUserType} 登录失败");
            }
        }

        private void CancelLogin()
        {
            IsLoginPanelVisible = false;
            SelectedUserType = null;
            LoginPassword = string.Empty;
            LoginError = string.Empty;
        }

        private void Logout()
        {
            var username = CurrentUser;
            PermissionService.Instance.Logout();
            UpdateLoginState();
            StatusMessage = $"用户 {username} 已登出";
            this.LogInfo($"用户 {username} 登出");
        }

        private void UpdateLoginState()
        {
            var user = PermissionService.Instance.CurrentUser;
            IsLoggedIn = user != null;
            CurrentUser = user?.Username ?? "未登录";
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion

        #region 检测操作

        public async Task InitializeAsync()
        {
            StatusMessage = "正在初始化...";

            PlcStatusColor = new SolidColorBrush(Colors.Gray);
            Camera1StatusColor = new SolidColorBrush(Colors.Gray);
            Camera2StatusColor = new SolidColorBrush(Colors.Gray);
            VisionStatusColor = new SolidColorBrush(Colors.Gray);

            var success = await _inspectionService.InitializeAsync();

            StatusMessage = success ? "初始化完成" : "初始化失败，部分功能不可用";
            UpdateStatistics();
        }

        private async Task StartInspection()
        {
            var success = await _inspectionService.StartAsync();
            if (success)
            {
                StatusMessage = "检测已启动";
            }
        }

        private async Task StopInspection()
        {
            await _inspectionService.StopAsync();
            StatusMessage = "检测已停止";
        }

        #endregion

        #region 设置窗口

        private void OpenCameraSettings()
        {
            var window = new Views.CameraSettingsWindow { Owner = Application.Current.MainWindow };
            window.ShowDialog();
        }

        private void OpenPlcSettings()
        {
            var window = new Views.PlcSettingsWindow { Owner = Application.Current.MainWindow };
            window.ShowDialog();
        }

        private void OpenVisionSettings()
        {
            var window = new Views.VisionMasterSettingsWindow { Owner = Application.Current.MainWindow };
            window.ShowDialog();
        }

        private void OpenStatistics()
        {
            StatusMessage = "统计报表 - 功能开发中";
        }

        private void OpenLog()
        {
            var window = new Views.LogWindow { Owner = Application.Current.MainWindow };
            window.ShowDialog();
        }

        private void OpenUserManagement()
        {
            var window = new Views.UserManagementWindow { Owner = Application.Current.MainWindow };
            window.ShowDialog();
        }

        private async Task SendTestTrigger(int stationId)
        {
            var success = await _inspectionService.SendTestTriggerAsync(stationId);
            if (!success)
            {
                MessageBox.Show($"工位{stationId}测试触发发送失败，请检查PLC连接", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 使用本地图片文件测试VisionMaster
        /// </summary>
        private void TestImageWithFile(int stationId)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = $"选择工位{stationId}测试图片",
                    Filter = "图片文件|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = $"正在测试图片: {System.IO.Path.GetFileName(dialog.FileName)}";
                    FileLogger.Instance.Info($"工位{stationId}测试图片(文件路径): {dialog.FileName}", "Test");

                    // 使用文件路径方式调用InspectionService执行单次检测
                    Task.Run(async () =>
                    {
                        try
                        {
                            // 直接使用文件路径，让VisionMaster加载图片
                            var result = await _inspectionService.ExecuteSingleWithFilePathAsync(stationId, dialog.FileName);

                            RunOnUIThread(() =>
                            {
                                if (result.ResultType == InspectionResultType.Ok)
                                {
                                    StatusMessage = $"测试完成: OK, 角度={result.Angle:F2}°";
                                    MessageBox.Show($"检测结果: OK\n角度: {result.Angle:F2}°\n耗时: {result.ProcessTimeMs:F0}ms", "测试结果", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                else if (result.ResultType == InspectionResultType.Ng)
                                {
                                    StatusMessage = $"测试完成: NG";
                                    MessageBox.Show($"检测结果: NG\n耗时: {result.ProcessTimeMs:F0}ms", "测试结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                                else
                                {
                                    StatusMessage = $"测试失败: {result.ErrorMessage}";
                                    MessageBox.Show($"检测失败: {result.ErrorMessage}", "测试结果", MessageBoxButton.OK, MessageBoxImage.Error);
                                }

                                // 更新工位显示
                                if (stationId == 2)
                                {
                                    Station2.UpdateResult(result.IsOk, result.Angle);
                                    if (result.ResultImage != null)
                                    {
                                        Station2.SetImage(result.ResultImage);
                                    }
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            FileLogger.Instance.Error($"测试图片处理失败: {ex.Message}", ex, "Test");
                            RunOnUIThread(() =>
                            {
                                StatusMessage = $"测试失败: {ex.Message}";
                                MessageBox.Show($"测试失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"打开测试图片失败: {ex.Message}", ex, "Test");
                MessageBox.Show($"打开测试图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 事件处理

        private void OnInspectionCompleted(object sender, InspectionCompletedEventArgs e)
        {
            RunOnUIThread(() =>
            {
                var result = e.Result;
                var station = result.StationId == 1 ? Station1 : Station2;

                station.UpdateResult(result.IsOk, result.Angle);
                station.StatusColor = result.IsOk
                    ? new SolidColorBrush(Colors.LimeGreen)
                    : new SolidColorBrush(Colors.Red);

                if (result.ResultImage != null)
                {
                    station.Image = ConvertToBitmapSource(result.ResultImage);
                }

                UpdateStatistics();
            });
        }

        private void OnStateChanged(object sender, SystemState state)
        {
            RunOnUIThread(() =>
            {
                switch (state)
                {
                    case SystemState.Idle:
                        SystemStatus = "空闲";
                        SystemStatusColor = new SolidColorBrush(Colors.Gray);
                        break;
                    case SystemState.Running:
                        SystemStatus = "运行中";
                        SystemStatusColor = new SolidColorBrush(Colors.LimeGreen);
                        break;
                    case SystemState.Paused:
                        SystemStatus = "已暂停";
                        SystemStatusColor = new SolidColorBrush(Colors.Orange);
                        break;
                    case SystemState.Error:
                        SystemStatus = "错误";
                        SystemStatusColor = new SolidColorBrush(Colors.Red);
                        break;
                }
            });
        }

        private void OnErrorOccurred(object sender, string error)
        {
            RunOnUIThread(() =>
            {
                StatusMessage = error;
                this.LogError(error);
            });
        }

        private void OnConnectionChanged(ConnectionChangedEvent e)
        {
            RunOnUIThread(() =>
            {
                var color = e.IsConnected
                    ? new SolidColorBrush(Colors.LimeGreen)
                    : new SolidColorBrush(Colors.Red);

                switch (e.DeviceType)
                {
                    case "Camera1":
                        Camera1StatusColor = color;
                        break;
                    case "Camera2":
                        Camera2StatusColor = color;
                        break;
                    case "PLC":
                        PlcStatusColor = color;
                        break;
                    case "Vision":
                        VisionStatusColor = color;
                        break;
                }
            });
        }

        #endregion

        #region 辅助方法

        private void UpdateStatistics()
        {
            var stat1 = _statisticsService.GetStationStatistics(1);
            var stat2 = _statisticsService.GetStationStatistics(2);

            if (stat1 != null)
            {
                Station1.UpdateStatistics(stat1.TotalCount, stat1.OkCount, stat1.NgCount);
            }

            if (stat2 != null)
            {
                Station2.UpdateStatistics(stat2.TotalCount, stat2.OkCount, stat2.NgCount);
            }
        }

        private BitmapSource ConvertToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            if (bitmap == null) return null;

            // 克隆图像避免多线程访问冲突
            System.Drawing.Bitmap clonedBitmap = null;
            System.Drawing.Imaging.BitmapData bitmapData = null;
            try
            {
                clonedBitmap = (System.Drawing.Bitmap)bitmap.Clone();

                // 根据像素格式选择对应的WPF格式
                System.Windows.Media.PixelFormat wpfFormat;
                switch (clonedBitmap.PixelFormat)
                {
                    case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                        wpfFormat = PixelFormats.Gray8;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                        wpfFormat = PixelFormats.Bgr24;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                        wpfFormat = PixelFormats.Bgr32;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                        wpfFormat = PixelFormats.Bgra32;
                        break;
                    default:
                        // 不支持的格式，转换为24位RGB
                        using (var convertedBitmap = new System.Drawing.Bitmap(clonedBitmap.Width, clonedBitmap.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                        {
                            using (var g = System.Drawing.Graphics.FromImage(convertedBitmap))
                            {
                                g.DrawImage(clonedBitmap, 0, 0, clonedBitmap.Width, clonedBitmap.Height);
                            }
                            clonedBitmap.Dispose();
                            return ConvertToBitmapSource(convertedBitmap);
                        }
                }

                bitmapData = clonedBitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, clonedBitmap.Width, clonedBitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    clonedBitmap.PixelFormat);

                var bitmapSource = BitmapSource.Create(
                    bitmapData.Width, bitmapData.Height,
                    96, 96,
                    wpfFormat, null,
                    bitmapData.Scan0,
                    bitmapData.Stride * bitmapData.Height,
                    bitmapData.Stride);

                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch (Exception ex)
            {
                Infrastructure.Logging.FileLogger.Instance.Warning($"图像转换失败: {ex.Message}", "MainViewModel");
                return null;
            }
            finally
            {
                if (bitmapData != null && clonedBitmap != null)
                {
                    clonedBitmap.UnlockBits(bitmapData);
                }
                clonedBitmap?.Dispose();
            }
        }

        #endregion

        public override void Cleanup()
        {
            _timer.Stop();
            _inspectionService?.Dispose();
            (_statisticsService as IDisposable)?.Dispose();
            Station1?.Dispose();
            Station2?.Dispose();
            base.Cleanup();
        }
    }
}
