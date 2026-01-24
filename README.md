# Unity DI-R3 Template | Unity DI-R3 游戏模板

<div align="center">

[![Unity](https://img.shields.io/badge/Unity-2022.3+-black.svg?style=flat&logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![VContainer](https://img.shields.io/badge/VContainer-1.17.0-green.svg)](https://github.com/hadashiA/VContainer)
[![R3](https://img.shields.io/badge/R3-latest-orange.svg)](https://github.com/Cysharp/R3)

*A modern Unity indie game template featuring clean architecture with dependency injection and reactive programming*

*一个现代化的Unity独立游戏模板，采用清晰的架构设计、依赖注入和反应式编程*

[English](#english) | [简体中文](#简体中文)

</div>

---

## English

### ✨ Features

- **🎯 Dependency Injection**: Built on [VContainer](https://github.com/hadashiA/VContainer) for clean, testable code
- **⚡ Reactive Programming**: Powered by [R3](https://github.com/Cysharp/R3) for elegant event handling
- **🎵 Audio System**: Complete audio service with BGM/SFX management and fade transitions
- **💾 Save System**: Flexible save/load system with settings and game state persistence
- **🌍 Localization**: Multi-language support via Unity Localization
- **🎨 UI Management**: Page-based UI system with async lifecycle management
- **📦 Addressables**: Resource management using Unity Addressables
- **🔄 State Machine**: Generic state machine for complex game flow
- **🎭 Event Bus**: Type-safe event system with R3 Observables

### 📁 Project Structure

```
Assets/Scripts/
├── Architecture/          # Core systems
│   ├── DependencyInjection/   # VContainer setup
│   ├── EventBus/              # R3-based event system
│   ├── GameFlow/              # Game initialization flow
│   ├── GameSound/             # Audio service
│   ├── Language/              # Localization manager
│   └── Data/                  # Save/load system
├── UI/                    # UI pages and components
├── Tools/                 # Utilities (StateMachine, Extensions)
└── Generated/             # Auto-generated code
```

### 🛠️ Core Systems

#### Dependency Injection (VContainer)
```csharp
// Register in ProjectLifetimeScope.cs
builder.Register<UIManager>(Lifetime.Singleton);
builder.Register<EventBus>(Lifetime.Singleton);

// Inject anywhere
[Inject] private UIManager _uiManager;
```

#### Event System (R3)
```csharp
// Define events as records
public record PlayerDamagedEvent(int Damage);

// Publish
_eventBus.Publish(new PlayerDamagedEvent(10));

// Subscribe with lifecycle binding
_eventBus.Receive<PlayerDamagedEvent>()
    .Subscribe(evt => HandleDamage(evt.Damage))
    .AddTo(this);
```

#### Audio Service
```csharp
await _audioService.PlayBgmAsync("menu_theme", fadeDuration: 1.0f);
await _audioService.PlaySfxAsync("button_click");
```

### 📦 Dependencies

- **VContainer** 1.17.0 - Dependency Injection
- **R3** - Reactive Extensions
- **UniTask** - Async/Await for Unity
- **DOTween** - Tweening animations
- **Unity Addressables** - Asset management
- **Unity Localization** - Multi-language support
- **Odin Inspector** (Optional) - Enhanced editor

### 📖 Documentation

For detailed architecture patterns and best practices, see `.cursor/rules/unity-vcontainer-r3.mdc`

### 🤝 Contributing

Contributions are welcome! Feel free to submit issues and pull requests.

### 📄 License

This project is licensed under the MIT License.

---

## 简体中文

### ✨ 特性

- **🎯 依赖注入**: 基于 [VContainer](https://github.com/hadashiA/VContainer) 实现清晰、可测试的代码
- **⚡ 反应式编程**: 使用 [R3](https://github.com/Cysharp/R3) 实现优雅的事件处理
- **🎵 音频系统**: 完整的音频服务，支持BGM/SFX管理和淡入淡出
- **💾 存档系统**: 灵活的存档/读档系统，支持设置和游戏状态持久化
- **🌍 本地化**: 通过Unity Localization实现多语言支持
- **🎨 UI管理**: 基于页面的UI系统，支持异步生命周期管理
- **📦 Addressables**: 使用Unity Addressables进行资源管理
- **🔄 状态机**: 泛型状态机，用于复杂游戏流程
- **🎭 事件总线**: 类型安全的事件系统，基于R3 Observables

### 📁 项目结构

```
Assets/Scripts/
├── Architecture/          # 核心系统
│   ├── DependencyInjection/   # VContainer配置
│   ├── EventBus/              # 基于R3的事件系统
│   ├── GameFlow/              # 游戏初始化流程
│   ├── GameSound/             # 音频服务
│   ├── Language/              # 本地化管理器
│   └── Data/                  # 存档/读档系统
├── UI/                    # UI页面和组件
├── Tools/                 # 工具类（状态机、扩展方法）
└── Generated/             # 自动生成的代码
```

### 🛠️ 核心系统

#### 依赖注入 (VContainer)
```csharp
// 在 ProjectLifetimeScope.cs 中注册
builder.Register<UIManager>(Lifetime.Singleton);
builder.Register<EventBus>(Lifetime.Singleton);

// 在任意位置注入
[Inject] private UIManager _uiManager;
```

#### 事件系统 (R3)
```csharp
// 使用 record 定义事件
public record PlayerDamagedEvent(int Damage);

// 发布事件
_eventBus.Publish(new PlayerDamagedEvent(10));

// 订阅事件（绑定生命周期）
_eventBus.Receive<PlayerDamagedEvent>()
    .Subscribe(evt => HandleDamage(evt.Damage))
    .AddTo(this);
```

#### 音频服务
```csharp
await _audioService.PlayBgmAsync("menu_theme", fadeDuration: 1.0f);
await _audioService.PlaySfxAsync("button_click");
```

### 📦 依赖项

- **VContainer** 1.17.0 - 依赖注入框架
- **R3** - 反应式扩展
- **UniTask** - Unity异步/等待
- **DOTween** - 补间动画
- **Unity Addressables** - 资源管理
- **Unity Localization** - 多语言支持
- **Odin Inspector**（可选）- 增强编辑器

### 🤝 贡献

欢迎贡献！请随时提交问题和拉取请求。

### 📄 许可证

本项目采用 MIT 许可证。