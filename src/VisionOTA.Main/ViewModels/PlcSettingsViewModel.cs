using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using VisionOTA.Common.Mvvm;
using VisionOTA.Hardware.Plc;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// PLC设置视图模型
    /// </summary>
    public class PlcSettingsViewModel : ViewModelBase
    {
        private string _ipAddress;
        private int _port;
        private int _timeout;

        private string _outputValueAddress;
        private string _outputValueType;
        private string _rotationAngleAddress;
        private string _rotationAngleType;
        private string _resultAddress;
        private string _resultType;

        public event EventHandler<bool> RequestClose;

        #region Properties

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public int Timeout
        {
            get => _timeout;
            set => SetProperty(ref _timeout, value);
        }

        public string OutputValueAddress
        {
            get => _outputValueAddress;
            set => SetProperty(ref _outputValueAddress, value);
        }

        public string OutputValueType
        {
            get => _outputValueType;
            set => SetProperty(ref _outputValueType, value);
        }

        public string RotationAngleAddress
        {
            get => _rotationAngleAddress;
            set => SetProperty(ref _rotationAngleAddress, value);
        }

        public string RotationAngleType
        {
            get => _rotationAngleType;
            set => SetProperty(ref _rotationAngleType, value);
        }

        public string ResultAddress
        {
            get => _resultAddress;
            set => SetProperty(ref _resultAddress, value);
        }

        public string ResultType
        {
            get => _resultType;
            set => SetProperty(ref _resultType, value);
        }

        #endregion

        #region Commands

        public ICommand TestConnectionCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        public PlcSettingsViewModel()
        {
            Title = "PLC设置";
            TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync());
            SaveCommand = new RelayCommand(_ => Save());
            CancelCommand = new RelayCommand(_ => Cancel());

            LoadConfig();
        }

        private void LoadConfig()
        {
            var config = ConfigManager.Instance.Plc;

            // 连接配置
            IpAddress = config.Connection.IP;
            Port = config.Connection.Port;
            Timeout = config.Connection.Timeout;

            // 输出地址
            OutputValueAddress = config.OutputAddresses.OutputValue.Address;
            OutputValueType = config.OutputAddresses.OutputValue.DataType;
            RotationAngleAddress = config.OutputAddresses.RotationAngle.Address;
            RotationAngleType = config.OutputAddresses.RotationAngle.DataType;
            ResultAddress = config.OutputAddresses.Result.Address;
            ResultType = config.OutputAddresses.Result.DataType;
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                using (var plc = new OmronFinsCommunication(IpAddress, Port))
                {
                    var connected = await plc.ConnectAsync();
                    if (connected)
                    {
                        MessageBox.Show("PLC连接成功", "测试结果", MessageBoxButton.OK, MessageBoxImage.Information);
                        FileLogger.Instance.Info($"PLC连接测试成功: {IpAddress}:{Port}", "PlcSettings");
                    }
                    else
                    {
                        MessageBox.Show("PLC连接失败", "测试结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                        FileLogger.Instance.Warning($"PLC连接测试失败: {IpAddress}:{Port}", "PlcSettings");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接测试出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"PLC连接测试异常: {ex.Message}", ex, "PlcSettings");
            }
        }

        private void Save()
        {
            try
            {
                var config = ConfigManager.Instance.Plc;

                // 更新连接配置
                config.Connection.IP = IpAddress;
                config.Connection.Port = Port;
                config.Connection.Timeout = Timeout;

                // 更新输出地址
                config.OutputAddresses.OutputValue.Address = OutputValueAddress;
                config.OutputAddresses.RotationAngle.Address = RotationAngleAddress;
                config.OutputAddresses.Result.Address = ResultAddress;

                // 保存到文件
                ConfigManager.Instance.SavePlcConfig();

                FileLogger.Instance.Info("PLC配置已保存", "PlcSettings");
                MessageBox.Show("配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                RequestClose?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"保存PLC配置失败: {ex.Message}", ex, "PlcSettings");
            }
        }

        private void Cancel()
        {
            RequestClose?.Invoke(this, false);
        }
    }
}
