using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VisionOTA.Hardware.Camera;
using VisionOTA.Hardware.Vision;
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
                FileLogger.Instance.Info("App.OnExit 开始...", "App");

                // 释放VisionMaster资源（带超时）
                var visionTask = Task.Run(() =>
                {
                    try
                    {
                        VisionMasterSolutionManager.Instance.Dispose();
                        FileLogger.Instance.Info("VisionMaster资源已释放", "App");
                    }
                    catch { }
                });

                visionTask.Wait(2000);

                FileLogger.Instance.Info("VisionOTA 退出完成", "App");
                FileLogger.Instance.Dispose();
            }
            catch { }

            base.OnExit(e);

            // 强制终止当前进程（确保所有native线程都被终止）
            try
            {
                Process.GetCurrentProcess().Kill();
            }
            catch
            {
                Environment.Exit(0);
            }
        }
    }
}
