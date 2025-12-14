using System;
using System.Collections.Generic;
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
        private IPlcCommunication _plc;
        private bool _isConnected;

        private string _ipAddress;
        private int _port;
        private int _timeout;

        private string _outputValueAddress;
        private string _outputValueType;
        private string _rotationAngleAddress;
        private string _rotationAngleType;
        private string _resultAddress;
        private string _resultType;

        // 当前读取值
        private string _outputValueCurrent = "--";
        private string _rotationAngleCurrent = "--";
        private string _resultCurrent = "--";

        // 写入值
        private string _outputValueWrite = "0";
        private string _rotationAngleWrite = "0";
        private string _resultWrite = "0";

        /// <summary>
        /// 支持的数据类型
        /// </summary>
        public List<string> DataTypes { get; } = new List<string> { "WORD", "DWORD", "REAL", "BIT" };

        public event EventHandler<bool> RequestClose;

        #region Properties

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

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

        // 当前值
        public string OutputValueCurrent
        {
            get => _outputValueCurrent;
            set => SetProperty(ref _outputValueCurrent, value);
        }

        public string RotationAngleCurrent
        {
            get => _rotationAngleCurrent;
            set => SetProperty(ref _rotationAngleCurrent, value);
        }

        public string ResultCurrent
        {
            get => _resultCurrent;
            set => SetProperty(ref _resultCurrent, value);
        }

        // 写入值
        public string OutputValueWrite
        {
            get => _outputValueWrite;
            set => SetProperty(ref _outputValueWrite, value);
        }

        public string RotationAngleWrite
        {
            get => _rotationAngleWrite;
            set => SetProperty(ref _rotationAngleWrite, value);
        }

        public string ResultWrite
        {
            get => _resultWrite;
            set => SetProperty(ref _resultWrite, value);
        }

        #endregion

        #region Commands

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }

        public ICommand ReadOutputValueCommand { get; }
        public ICommand WriteOutputValueCommand { get; }
        public ICommand ReadRotationAngleCommand { get; }
        public ICommand WriteRotationAngleCommand { get; }
        public ICommand ReadResultCommand { get; }
        public ICommand WriteResultCommand { get; }
        public ICommand ReadAllCommand { get; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        public PlcSettingsViewModel()
        {
            Title = "PLC设置";

            ConnectCommand = new RelayCommand(async _ => await ConnectAsync(), _ => !IsConnected);
            DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected);

            ReadOutputValueCommand = new RelayCommand(async _ => await ReadOutputValueAsync(), _ => IsConnected);
            WriteOutputValueCommand = new RelayCommand(async _ => await WriteOutputValueAsync(), _ => IsConnected);
            ReadRotationAngleCommand = new RelayCommand(async _ => await ReadRotationAngleAsync(), _ => IsConnected);
            WriteRotationAngleCommand = new RelayCommand(async _ => await WriteRotationAngleAsync(), _ => IsConnected);
            ReadResultCommand = new RelayCommand(async _ => await ReadResultAsync(), _ => IsConnected);
            WriteResultCommand = new RelayCommand(async _ => await WriteResultAsync(), _ => IsConnected);
            ReadAllCommand = new RelayCommand(async _ => await ReadAllAsync(), _ => IsConnected);

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

        private async Task ConnectAsync()
        {
            try
            {
                _plc = new OmronFinsCommunication(IpAddress, Port);
                var connected = await _plc.ConnectAsync();
                if (connected)
                {
                    IsConnected = true;
                    FileLogger.Instance.Info($"PLC已连接: {IpAddress}:{Port}", "PlcSettings");
                    CommandManager.InvalidateRequerySuggested();
                }
                else
                {
                    MessageBox.Show("PLC连接失败", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"PLC连接失败: {ex.Message}", ex, "PlcSettings");
            }
        }

        private void Disconnect()
        {
            try
            {
                _plc?.Dispose();
                _plc = null;
                IsConnected = false;
                OutputValueCurrent = "--";
                RotationAngleCurrent = "--";
                ResultCurrent = "--";
                FileLogger.Instance.Info("PLC已断开", "PlcSettings");
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error($"PLC断开失败: {ex.Message}", ex, "PlcSettings");
            }
        }

        private async Task ReadOutputValueAsync()
        {
            try
            {
                OutputValueCurrent = await ReadByTypeAsync(OutputValueAddress, OutputValueType);
                FileLogger.Instance.Debug($"读取 {OutputValueAddress} ({OutputValueType}) = {OutputValueCurrent}", "PlcSettings");
            }
            catch (Exception ex)
            {
                OutputValueCurrent = "ERR";
                FileLogger.Instance.Error($"读取失败: {ex.Message}", ex, "PlcSettings");
            }
        }

        private async Task WriteOutputValueAsync()
        {
            try
            {
                await WriteByTypeAsync(OutputValueAddress, OutputValueType, OutputValueWrite);
                FileLogger.Instance.Info($"写入 {OutputValueAddress} ({OutputValueType}) = {OutputValueWrite}", "PlcSettings");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"写入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"写入失败: {ex.Message}", ex, "PlcSettings");
            }
        }

        private async Task ReadRotationAngleAsync()
        {
            try
            {
                RotationAngleCurrent = await ReadByTypeAsync(RotationAngleAddress, RotationAngleType);
                FileLogger.Instance.Debug($"读取 {RotationAngleAddress} ({RotationAngleType}) = {RotationAngleCurrent}", "PlcSettings");
            }
            catch (Exception ex)
            {
                RotationAngleCurrent = "ERR";
                FileLogger.Instance.Error($"读取失败: {ex.Message}", ex, "PlcSettings");
            }
        }

        private async Task WriteRotationAngleAsync()
        {
            try
            {
                await WriteByTypeAsync(RotationAngleAddress, RotationAngleType, RotationAngleWrite);
                FileLogger.Instance.Info($"写入 {RotationAngleAddress} ({RotationAngleType}) = {RotationAngleWrite}", "PlcSettings");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"写入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"写入失败: {ex.Message}", ex, "PlcSettings");
            }
        }

        private async Task ReadResultAsync()
        {
            try
            {
                ResultCurrent = await ReadByTypeAsync(ResultAddress, ResultType);
                FileLogger.Instance.Debug($"读取 {ResultAddress} ({ResultType}) = {ResultCurrent}", "PlcSettings");
            }
            catch (Exception ex)
            {
                ResultCurrent = "ERR";
                FileLogger.Instance.Error($"读取失败: {ex.Message}", ex, "PlcSettings");
            }
        }

        private async Task WriteResultAsync()
        {
            try
            {
                await WriteByTypeAsync(ResultAddress, ResultType, ResultWrite);
                FileLogger.Instance.Info($"写入 {ResultAddress} ({ResultType}) = {ResultWrite}", "PlcSettings");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"写入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"写入失败: {ex.Message}", ex, "PlcSettings");
            }
        }

        private async Task<string> ReadByTypeAsync(string address, string dataType)
        {
            switch (dataType?.ToUpper())
            {
                case "WORD":
                    var wordVal = await _plc.ReadWordAsync(address);
                    return wordVal.ToString();
                case "DWORD":
                    var dwordVal = await _plc.ReadDWordAsync(address);
                    return dwordVal.ToString();
                case "BIT":
                    var bitVal = await _plc.ReadBitAsync(address);
                    return bitVal ? "1" : "0";
                case "REAL":
                default:
                    var floatVal = await _plc.ReadFloatAsync(address);
                    return floatVal.ToString("F2");
            }
        }

        private async Task WriteByTypeAsync(string address, string dataType, string value)
        {
            switch (dataType?.ToUpper())
            {
                case "WORD":
                    await _plc.WriteWordAsync(address, short.Parse(value));
                    break;
                case "DWORD":
                    await _plc.WriteDWordAsync(address, int.Parse(value));
                    break;
                case "BIT":
                    await _plc.WriteBitAsync(address, value == "1" || value.ToLower() == "true");
                    break;
                case "REAL":
                default:
                    await _plc.WriteFloatAsync(address, float.Parse(value));
                    break;
            }
        }

        private async Task ReadAllAsync()
        {
            await ReadOutputValueAsync();
            await ReadRotationAngleAsync();
            await ReadResultAsync();
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

                // 更新输出地址和类型
                config.OutputAddresses.OutputValue.Address = OutputValueAddress;
                config.OutputAddresses.OutputValue.DataType = OutputValueType;
                config.OutputAddresses.RotationAngle.Address = RotationAngleAddress;
                config.OutputAddresses.RotationAngle.DataType = RotationAngleType;
                config.OutputAddresses.Result.Address = ResultAddress;
                config.OutputAddresses.Result.DataType = ResultType;

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

        public override void Cleanup()
        {
            Disconnect();
            base.Cleanup();
        }
    }
}
