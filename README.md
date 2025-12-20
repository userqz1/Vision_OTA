# Vision OTA - 工业视觉检测系统

WPF工业视觉检测系统，双工位（面阵+线扫）独立运行。

## 技术栈

- **语言**: C#
- **框架**: .NET Framework 4.8
- **UI**: WPF (MVVM)
- **视觉**: 海康 VisionMaster
- **相机**: 度申 SDK
- **PLC**: 欧姆龙 FINS/TCP

## 项目架构

```
VisionOTA/
├── src/
│   ├── VisionOTA.Main/              # WPF主程序
│   │   ├── Views/                   # 视图层
│   │   ├── ViewModels/              # 视图模型层
│   │   ├── Controls/                # 自定义控件
│   │   ├── Converters/              # 值转换器
│   │   └── Themes/                  # 主题样式
│   │
│   ├── VisionOTA.Core/              # 核心业务层
│   │   ├── Interfaces/              # 服务接口
│   │   ├── Models/                  # 业务模型
│   │   └── Services/                # 业务服务
│   │
│   ├── VisionOTA.Hardware/          # 硬件通讯层
│   │   ├── Camera/                  # 相机控制
│   │   ├── Plc/                     # PLC通讯
│   │   └── Vision/                  # 视觉处理
│   │
│   ├── VisionOTA.Infrastructure/    # 基础设施层
│   │   ├── Config/                  # 配置管理
│   │   ├── Logging/                 # 日志系统
│   │   ├── Permission/              # 权限管理
│   │   └── Storage/                 # 存储服务
│   │
│   └── VisionOTA.Common/            # 公共组件
│       ├── Constants/               # 常量定义
│       ├── Events/                  # 事件聚合器
│       ├── Extensions/              # 扩展方法
│       └── Mvvm/                    # MVVM基础类
│
└── Config/                          # 配置文件目录
```

## 核心文件说明

### VisionOTA.Main（主程序）

#### Views（视图）
| 文件 | 说明 |
|------|------|
| `MainWindow.xaml` | 主界面，包含工位显示、统计、状态栏 |
| `CameraSettingsWindow.xaml` | 相机设置，双工位相机配置和预览 |
| `PlcSettingsWindow.xaml` | PLC设置，地址配置和连接测试 |
| `VisionMasterSettingsWindow.xaml` | 算法设置，VisionMaster方案加载和调试 |
| `LogWindow.xaml` | 日志查看，支持模块筛选 |
| `LoginWindow.xaml` | 登录界面 |
| `UserManagementWindow.xaml` | 用户管理 |

#### ViewModels（视图模型）
| 文件 | 说明 |
|------|------|
| `MainViewModel.cs` | 主界面逻辑，管理工位状态和检测流程 |
| `StationViewModel.cs` | 工位通用ViewModel，封装相机控制和统计 |
| `CameraSettingsViewModel.cs` | 相机设置逻辑 |
| `PlcSettingsViewModel.cs` | PLC设置逻辑 |
| `PlcAddressViewModel.cs` | PLC地址项ViewModel，用于动态绑定 |
| `LogViewModel.cs` | 日志查看逻辑，支持模块筛选 |
| `LoginViewModel.cs` | 登录逻辑 |

#### Controls（自定义控件）
| 文件 | 说明 |
|------|------|
| `ZoomableImageControl.xaml` | 可缩放图像控件，支持鼠标滚轮缩放、拖拽平移、十字准线 |

### VisionOTA.Core（核心业务）

| 文件 | 说明 |
|------|------|
| `InspectionService.cs` | 检测服务，协调相机、视觉、PLC完成检测流程 |
| `StatisticsService.cs` | 统计服务，管理各工位的OK/NG计数 |
| `ImageStorageService.cs` | 图片存储服务，循环存储和NG保存 |

### VisionOTA.Hardware（硬件层）

#### Camera（相机）
| 文件 | 说明 |
|------|------|
| `ICamera.cs` | 相机接口定义 |
| `DushenCameraBase.cs` | 度申相机基类 |
| `DushenAreaCamera.cs` | 面阵相机实现（工位1） |
| `DushenLineCamera.cs` | 线扫相机实现（工位2） |
| `CameraFactory.cs` | 相机工厂，根据配置创建相机实例 |

#### Plc（PLC通讯）
| 文件 | 说明 |
|------|------|
| `IPlcCommunication.cs` | PLC通讯接口 |
| `OmronFinsCommunication.cs` | 欧姆龙FINS/TCP协议实现 |
| `MockPlc.cs` | 模拟PLC，用于离线测试 |

#### Vision（视觉处理）
| 文件 | 说明 |
|------|------|
| `IVisionProcessor.cs` | 视觉处理器接口 |
| `VisionMasterProcessor.cs` | VisionMaster实现，包含方案管理器单例 |
| `MockVisionProcessor.cs` | 模拟处理器，用于离线测试 |

### VisionOTA.Infrastructure（基础设施）

#### Config（配置）
| 文件 | 说明 |
|------|------|
| `ConfigManager.cs` | 配置管理器，统一加载和保存配置 |
| `CameraConfig.cs` | 相机配置模型 |
| `PlcConfig.cs` | PLC配置模型 |
| `VisionConfig.cs` | 视觉配置模型 |

#### Logging（日志）
| 文件 | 说明 |
|------|------|
| `FileLogger.cs` | 文件日志，按模块分文件存储 |
| `LogExtensions.cs` | 日志扩展方法 |

#### Permission（权限）
| 文件 | 说明 |
|------|------|
| `PermissionService.cs` | 权限服务，三级权限管理 |
| `PermissionLevel.cs` | 权限级别枚举 |

### VisionOTA.Common（公共组件）

#### Mvvm
| 文件 | 说明 |
|------|------|
| `ViewModelBase.cs` | ViewModel基类，实现INotifyPropertyChanged |
| `RelayCommand.cs` | 命令实现 |
| `CommandFactory.cs` | 命令工厂，简化命令创建 |
| `ObservableObject.cs` | 可观察对象基类 |

#### Events
| 文件 | 说明 |
|------|------|
| `EventAggregator.cs` | 事件聚合器，跨模块通信 |

## 功能特性

- **双工位检测**: 工位1(面阵) + 工位2(线扫) 独立运行
- **图案匹配**: 检测产品图案，输出匹配角度
- **PLC通讯**: 欧姆龙 FINS/TCP 协议，输出角度和结果
- **三级权限**: 操作员/工程师/管理员
- **数据统计**: 各工位独立统计，支持导出
- **图片存储**: 最近图片循环存储，NG单独保存
- **模块化日志**: 不同模块保存到不同日志文件

## 配置文件

```
Config/
├── CameraConfig.json   # 相机参数（曝光、增益、触发模式）
├── PlcConfig.json      # PLC地址配置（可在界面编辑）
├── VisionConfig.json   # 算法配置（方案路径、流程名称）
├── SystemConfig.json   # 系统参数
└── Users.json          # 用户账户
```

## 开发中遇到的问题及解决方案

### 1. VisionMaster加载方案时UI卡住

**问题**: 点击"加载方案"按钮后界面卡死，底部一直显示"正在加载方案..."

**原因**:
- `VmSolution.Load()` 是耗时操作，在UI线程执行会阻塞界面
- `SetRotationAngleFromPlc()` 中使用 `.Result` 同步等待异步操作，阻塞了UI线程，导致 `Dispatcher.BeginInvoke` 的委托无法执行

**解决方案**:
```csharp
// 1. 使用 Task.Run 将耗时操作放到后台线程
await Task.Run(() => {
    VmSolution.Load(solutionPath);
});

// 2. 将同步方法改为异步，避免 .Result 阻塞
private async Task SetRotationAngleFromPlcAsync()
{
    await Task.Run(async () => {
        var connected = await plc.ConnectAsync();  // 不用 .Result
        var value = await plc.ReadFloatAsync(address);
    });
}

// 3. 在加载方案时不等待PLC操作，让其后台执行
_ = SetRotationAngleFromPlcAsync();
```

### 2. VisionMaster状态灯不亮

**问题**: 算法加载成功后，主界面的算法状态灯没有变绿

**原因**: 加载方案后没有发布 `ConnectionChangedEvent` 事件

**解决方案**:
```csharp
// 在方案加载成功后发布事件
EventAggregator.Instance.Publish(new ConnectionChangedEvent
{
    DeviceType = "Vision",
    DeviceName = "VisionMaster",
    IsConnected = true
});
```

### 3. 切换方案时卡住

**问题**: 在已加载方案的情况下，重新选择方案加载会卡住

**原因**: 检查旧方案是否加载时使用了窗口内的局部变量 `_isSolutionLoaded`，而方案可能是由 `InspectionService` 在启动时加载的

**解决方案**:
```csharp
// 改为检查 VmSolution.Instance 而非局部变量
if (VmSolution.Instance != null)
{
    VmSolution.Instance.CloseSolution();
}
```

### 4. 相机设置界面按钮过多

**问题**: 每个工位有7个按钮，界面显得拥挤

**解决方案**:
- 合并互斥按钮：连接/断开 → 1个切换按钮，采集/停止 → 1个切换按钮
- 按钮文字动态变化：根据状态显示"连接"或"断开"
- 使用分隔符分组：连接控制 | 采集控制 | 工具按钮

```csharp
public string ConnectionButtonText => IsConnected ? "断开" : "连接";
public ICommand ToggleConnectionCommand { get; }
```

### 5. 代码重复问题

**问题**: `CameraSettingsViewModel` 和 `MainViewModel` 中工位相关代码大量重复

**解决方案**:
- 提取 `StationViewModel` 封装工位通用逻辑（相机控制、统计数据）
- 提取 `PlcAddressViewModel` 封装PLC地址项
- 使用组合代替继承，减少代码量约50%

### 6. 日志混乱

**问题**: 所有模块的日志都写入同一个文件，难以定位问题

**解决方案**:
```csharp
// 按日期和模块分文件存储
// logs/2024-01-15/All.log      - 所有日志
// logs/2024-01-15/Camera.log   - 相机模块
// logs/2024-01-15/PLC.log      - PLC模块
// logs/2024-01-15/Vision.log   - 视觉模块

public void Log(LogLevel level, string message, string category)
{
    var module = GetModuleFromCategory(category);
    WriteToFile($"logs/{date}/All.log", message);
    WriteToFile($"logs/{date}/{module}.log", message);
}
```

## 开发环境

- Visual Studio 2019/2022
- .NET Framework 4.8
- VisionMaster SDK 4.x
- 度申相机 SDK

## 许可证

私有项目
