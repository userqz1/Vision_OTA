using System;
using System.IO;
using Newtonsoft.Json;

namespace VisionOTA.Infrastructure.Config
{
    /// <summary>
    /// 配置管理器
    /// </summary>
    public class ConfigManager
    {
        private static readonly Lazy<ConfigManager> _instance =
            new Lazy<ConfigManager>(() => new ConfigManager());

        private readonly string _configDirectory;

        public static ConfigManager Instance => _instance.Value;

        /// <summary>
        /// 相机配置
        /// </summary>
        public CameraConfig Camera { get; private set; }

        /// <summary>
        /// PLC配置
        /// </summary>
        public PlcConfig Plc { get; private set; }

        /// <summary>
        /// 视觉配置
        /// </summary>
        public VisionConfig Vision { get; private set; }

        /// <summary>
        /// 系统配置
        /// </summary>
        public SystemConfig SystemCfg { get; private set; }

        private ConfigManager()
        {
            _configDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs");
            EnsureDirectoryExists();
            LoadAllConfigs();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
            }
        }

        /// <summary>
        /// 加载所有配置
        /// </summary>
        public void LoadAllConfigs()
        {
            Camera = LoadConfig<CameraConfig>("CameraConfig.json");
            Plc = LoadConfig<PlcConfig>("PlcConfig.json");
            Vision = LoadConfig<VisionConfig>("VisionConfig.json");
            SystemCfg = LoadConfig<SystemConfig>("SystemConfig.json");
        }

        /// <summary>
        /// 保存所有配置
        /// </summary>
        public void SaveAllConfigs()
        {
            SaveConfig("CameraConfig.json", Camera);
            SaveConfig("PlcConfig.json", Plc);
            SaveConfig("VisionConfig.json", Vision);
            SaveConfig("SystemConfig.json", SystemCfg);
        }

        /// <summary>
        /// 加载指定配置文件
        /// </summary>
        /// <typeparam name="T">配置类型</typeparam>
        /// <param name="fileName">文件名</param>
        /// <returns>配置对象</returns>
        public T LoadConfig<T>(string fileName) where T : class, new()
        {
            var filePath = Path.Combine(_configDirectory, fileName);
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var result = JsonConvert.DeserializeObject<T>(json);
                    if (result != null)
                        return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载配置文件失败 {fileName}: {ex.Message}");
            }

            // 配置文件不存在或加载失败，创建默认配置
            var defaultConfig = new T();
            SaveConfig(fileName, defaultConfig);
            return defaultConfig;
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        /// <typeparam name="T">配置类型</typeparam>
        /// <param name="fileName">文件名</param>
        /// <param name="config">配置对象</param>
        public void SaveConfig<T>(string fileName, T config)
        {
            var filePath = Path.Combine(_configDirectory, fileName);
            try
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置文件失败 {fileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存相机配置
        /// </summary>
        public void SaveCameraConfig()
        {
            SaveConfig("CameraConfig.json", Camera);
        }

        /// <summary>
        /// 保存PLC配置
        /// </summary>
        public void SavePlcConfig()
        {
            SaveConfig("PlcConfig.json", Plc);
        }

        /// <summary>
        /// 保存视觉配置
        /// </summary>
        public void SaveVisionConfig()
        {
            SaveConfig("VisionConfig.json", Vision);
        }

        /// <summary>
        /// 保存系统配置
        /// </summary>
        public void SaveSystemConfig()
        {
            SaveConfig("SystemConfig.json", SystemCfg);
        }
    }
}
