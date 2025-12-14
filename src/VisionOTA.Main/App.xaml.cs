using System.Windows;
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
            FileLogger.Instance.Info("VisionOTA 退出", "App");
            FileLogger.Instance.Dispose();
            base.OnExit(e);
        }
    }
}
