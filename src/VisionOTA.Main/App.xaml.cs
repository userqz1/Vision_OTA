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

                // 第一步：停止所有相机采集
                try
                {
                    CameraManager.Instance.Dispose();
                    FileLogger.Instance.Info("相机资源已释放", "App");
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Warning($"相机释放异常: {ex.Message}", "App");
                }

                // 等待相机完全停止
                Thread.Sleep(300);

                // 第二步：释放VisionMaster资源（带超时）
                var visionTask = Task.Run(() =>
                {
                    try
                    {
                        VisionMasterSolutionManager.Instance.Dispose();
                        FileLogger.Instance.Info("VisionMaster资源已释放", "App");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Instance.Warning($"VisionMaster释放异常: {ex.Message}", "App");
                    }
                });

                if (!visionTask.Wait(3000))
                {
                    FileLogger.Instance.Warning("VisionMaster释放超时", "App");
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
