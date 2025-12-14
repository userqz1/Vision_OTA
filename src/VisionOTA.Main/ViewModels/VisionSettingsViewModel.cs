using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using VisionOTA.Common.Mvvm;
using VisionOTA.Infrastructure.Config;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Main.ViewModels
{
    /// <summary>
    /// 算法设置视图模型
    /// </summary>
    public class VisionSettingsViewModel : ViewModelBase
    {
        private string _station1VppPath;
        private string _station2VppPath;
        private double _scoreThreshold;
        private int _timeout;
        private bool _showIntermediateResults;
        private bool _enableGraphicOverlay;

        public event EventHandler<bool> RequestClose;

        #region Properties

        public string Station1VppPath
        {
            get => _station1VppPath;
            set => SetProperty(ref _station1VppPath, value);
        }

        public string Station2VppPath
        {
            get => _station2VppPath;
            set => SetProperty(ref _station2VppPath, value);
        }

        public double ScoreThreshold
        {
            get => _scoreThreshold;
            set => SetProperty(ref _scoreThreshold, value);
        }

        public int Timeout
        {
            get => _timeout;
            set => SetProperty(ref _timeout, value);
        }

        public bool ShowIntermediateResults
        {
            get => _showIntermediateResults;
            set => SetProperty(ref _showIntermediateResults, value);
        }

        public bool EnableGraphicOverlay
        {
            get => _enableGraphicOverlay;
            set => SetProperty(ref _enableGraphicOverlay, value);
        }

        #endregion

        #region Commands

        public ICommand BrowseStation1Command { get; }
        public ICommand BrowseStation2Command { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        public VisionSettingsViewModel()
        {
            Title = "算法设置";
            BrowseStation1Command = new RelayCommand(_ => BrowseStation1());
            BrowseStation2Command = new RelayCommand(_ => BrowseStation2());
            SaveCommand = new RelayCommand(_ => Save());
            CancelCommand = new RelayCommand(_ => Cancel());

            LoadConfig();
        }

        private void LoadConfig()
        {
            var config = ConfigManager.Instance.Vision;

            Station1VppPath = config.Station1VppPath;
            Station2VppPath = config.Station2VppPath;
            ScoreThreshold = config.ScoreThreshold;
            Timeout = config.Timeout;
            ShowIntermediateResults = config.ShowIntermediateResults;
            EnableGraphicOverlay = config.EnableGraphicOverlay;
        }

        private void BrowseStation1()
        {
            var path = BrowseVppFile();
            if (!string.IsNullOrEmpty(path))
            {
                Station1VppPath = path;
            }
        }

        private void BrowseStation2()
        {
            var path = BrowseVppFile();
            if (!string.IsNullOrEmpty(path))
            {
                Station2VppPath = path;
            }
        }

        private string BrowseVppFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择VisionPro工具块",
                Filter = "VisionPro工具块 (*.vpp)|*.vpp|所有文件 (*.*)|*.*",
                DefaultExt = ".vpp"
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        private void Save()
        {
            try
            {
                var config = ConfigManager.Instance.Vision;

                config.Station1VppPath = Station1VppPath;
                config.Station2VppPath = Station2VppPath;
                config.ScoreThreshold = ScoreThreshold;
                config.Timeout = Timeout;
                config.ShowIntermediateResults = ShowIntermediateResults;
                config.EnableGraphicOverlay = EnableGraphicOverlay;

                ConfigManager.Instance.SaveVisionConfig();

                FileLogger.Instance.Info("算法配置已保存", "VisionSettings");
                MessageBox.Show("配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                RequestClose?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileLogger.Instance.Error($"保存算法配置失败: {ex.Message}", ex, "VisionSettings");
            }
        }

        private void Cancel()
        {
            RequestClose?.Invoke(this, false);
        }
    }
}
