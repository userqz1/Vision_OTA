using System;
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
                FileLogger.Instance.Info("VisionOTA 开始退出清理...", "App");

                // 使用超时机制清理资源
                var cleanupTask = Task.Run(() =>
                {
                    try
                    {
                        // 释放相机资源
                        CameraManager.Instance.Dispose();
                        FileLogger.Instance.Info("相机资源已释放", "App");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Instance.Warning($"相机释放异常: {ex.Message}", "App");
                    }

                    try
                    {
                        // 释放VisionMaster资源
                        VisionMasterSolutionManager.Instance.Dispose();
                        FileLogger.Instance.Info("VisionMaster资源已释放", "App");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Instance.Warning($"VisionMaster释放异常: {ex.Message}", "App");
                    }
                });

                // 最多等待5秒
                if (!cleanupTask.Wait(5000))
                {
                    FileLogger.Instance.Warning("资源清理超时，强制退出", "App");
                }

                FileLogger.Instance.Info("VisionOTA 退出完成", "App");
                FileLogger.Instance.Dispose();
            }
            catch { }

            base.OnExit(e);

            // 强制终止进程，确保所有后台线程都被终止
            Environment.Exit(0);
        }
    }
}
