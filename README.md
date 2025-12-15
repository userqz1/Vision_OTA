# Vision OTA - 工业视觉检测系统

WPF工业视觉检测系统，双工位（面阵+线扫）独立运行。

## 技术栈

- **语言**: C#
- **框架**: .NET Framework 4.8
- **UI**: WPF (MVVM)
- **视觉**: 海康 VisionMaster
- **相机**: 度申 SDK
- **PLC**: 欧姆龙 FINS/TCP

## 系统架构

```
VisionOTA/
├── VisionOTA.Main          # WPF主程序
├── VisionOTA.Vision        # 视觉算法层
├── VisionOTA.Hardware      # 硬件通讯层 (相机/PLC)
├── VisionOTA.Infrastructure# 基础设施 (配置/日志)
└── VisionOTA.Common        # 公共组件
```

## 功能特性

- **双工位检测**: 工位1(面阵) + 工位2(线扫) 独立运行
- **图案匹配**: 检测产品图案，输出匹配角度
- **PLC通讯**: 欧姆龙 FINS/TCP 协议，输出角度和结果
- **三级权限**: 操作员/工程师/管理员
- **数据统计**: 各工位独立统计，支持导出
- **图片存储**: 最近图片循环存储，NG单独保存

## VisionMaster 视觉模块

### 为什么选择 VisionMaster

| 对比项 | VisionPro | VisionMaster |
|--------|-----------|--------------|
| 厂商 | Cognex | 海康威视 |
| 授权费用 | 高 | 较低 |
| 功能完整性 | 成熟 | 齐全 |
| 本地化支持 | 一般 | 优秀 |

### 集成架构

```
┌──────────────────────────────────────┐
│  VisionOTA.Vision                    │
│    ├── IVisionProcessor (接口)       │
│    └── VisionMasterProcessor (实现)  │
├──────────────────────────────────────┤
│  VisionMaster SDK                    │
│    ├── VM.Core.dll                   │
│    └── VM.PlatformSDKCS.dll          │
└──────────────────────────────────────┘
```

### 检测配置

| 工位 | 相机类型 | 检测任务 | VM工具 |
|------|----------|----------|--------|
| 工位1 | 面阵 | 图案匹配 | 形状匹配 |
| 工位2 | 线扫 | 图案匹配 | 形状匹配 |

## 配置文件

```
Config/
├── CameraConfig.json   # 相机参数
├── PlcConfig.json      # PLC地址配置
├── VisionConfig.json   # 算法配置
├── SystemConfig.json   # 系统参数
└── Users.json          # 用户账户
```

## 开发环境

- Visual Studio 2019/2022
- .NET Framework 4.8
- VisionMaster SDK 4.x
- 度申相机 SDK

## 许可证

私有项目
