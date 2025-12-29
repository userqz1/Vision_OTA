using System;
using System.Windows;
using VisionOTA.Hardware.Camera;
using VisionOTA.Infrastructure.Logging;

namespace VisionOTA.Main
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            FileLogger.Instance.Info("VisionOTA 启动", "App");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                FileLogger.Instance.Info("VisionOTA 退出", "App");

                // 释放相机资源
                CameraManager.Instance.Dispose();

                FileLogger.Instance.Dispose();
            }
            catch { }

            base.OnExit(e);

            // 强制终止进程，确保所有后台线程都被终止
            Environment.Exit(0);
        }
    }
}
