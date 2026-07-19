# DI-R3 Template 重构交付记录

> 状态：代码收尾完成，等待当前批次 Unity 验证
> Unity：`6000.3.5f2`
> 目标平台：Windows Standalone x64
> 更新日期：2026-07-19

## 1. 目标与范围

本轮重构把原型式模板收敛为边界清晰、生命周期明确、可以直接扩展中小型独立游戏的框架项目。

当前明确决策：

- clean cutover，不保留旧 API、旧存档兼容层或 migration。
- 第一方代码不引入 asmdef，继续使用 `Assembly-CSharp` 与 Editor 程序集。
- 不保留或新增第一方单元测试、测试程序集、手工测试组件和测试场景。
- Codex 不运行 `dotnet build`，不进入 Play Mode；运行时验证由用户完成。
- 第一方运行时代码不依赖 Odin Inspector。

## 2. 最终运行时结构

```text
ProjectLifetimeScope
├─ GameFlowController
├─ Settings / GameSave / Persistence
├─ Localization
├─ Audio
│  ├─ IMusicPlayer / MusicPlayer
│  ├─ ISfxPlayer / SfxPlayer
│  ├─ AddressableAudioClipStore
│  └─ UnityAudioOutput
└─ UI
   ├─ IPageNavigator / PageNavigator
   ├─ PageStack
   └─ AddressableUiPrefabProvider
```

启动顺序固定为：

1. 初始化设置并恢复默认值或存档。
2. 初始化 Localization，并应用保存语言。
3. 应用 Display 与 Audio 设置。
4. 首次启动进入 `LanguagePage`，其余启动进入 `MainScenePage`。

## 3. 已完成模块

### Audio

- Music、SFX 与音量控制拆为独立接口。
- Catalog 只保存 typed cue 到 Addressable `AudioClip` 的映射。
- ClipStore 合并并发加载并拥有 handle 生命周期。
- Music 采用 latest-command-wins，切曲和停止具有明确取消结果。
- AudioMixer 负责用户音量，AudioSource 只负责播放与转场增益。
- Runtime 不引用 Data、UI、EventBus 或 UnityEditor。

### Settings、Save 与 Persistence

- `SettingsSnapshot` 与 `GameDataRuntime` 均为不可变状态。
- Service 是唯一写入所有者；持久化成功后才替换内存状态并发布变化。
- Repository、FileStore 与 Clock 分离。
- schema v1、损坏文件拒绝、备份恢复和原子替换已接入。
- Save slot 的主文件或备份任一存在时均可被发现。

### Bootstrap 与 Localization

- 启动流程显式、可等待、可取消。
- 初始化不依赖构造副作用或事件订阅顺序。
- 语言切换先成功应用 Locale，再保存设置并导航。
- 生命周期取消不记录为启动或页面错误。

### UI Navigation

- Push、Pop、Replace 与 Clear 统一经过异步 gate。
- 重复 Push 当前页面返回 `AlreadyCurrent`。
- Prefab、页面实例与 Addressables lease 具有同一所有权链。
- 页面生命周期支持 CancellationToken；加载或进入失败会回滚旧页面。
- Replace 在旧页 `OnPause` 完成后才进入新页；语言选择页会先完整淡出，避免两个页面交叠。
- Scope teardown 使用同步快速清理，并释放输入订阅与 Tween。
- 设置页保存失败会回显持久化中的真实状态，不再留下假 UI 状态。

### 作者工具与交付门禁

- UIBinder 提供自动绑定、项目校验与 Player Build 前置门禁。
- Addressable Keys 与 Audio cue 常量生成采用稳定排序、标识符碰撞检查和 freshness 校验。
- Packed Addressables 与 Windows Player 提供统一菜单入口。
- Windows 构建命令会拒绝错误的 Active Build Target，避免为错误平台生成内容。

## 4. 已删除内容

- 旧 `AudioService`、DataManager、UIManager、UIPageStack 与全局 EventBus。
- 旧事件定义、兼容 facade、Source Provider 和迁移代码。
- 未使用的 StateMachine、RectTransform 扩展和大面积 DOTween 包装。
- `SoundTest`、Persistence 断言工具、Test 场景和测试音频。
- 无引用 UI 图片、旧 Data 空目录、重复 DOTween Settings 与 Addressables 构建状态文件。
- 未使用的 project-wide Input Actions 资产、C# wrapper 与误导性的通用 R3/EventBus 教程。

## 5. 必须保持的架构约束

- 组合只发生在 `ProjectLifetimeScope`，不新增 Service Locator 或第二套全局单例。
- 需要结果、顺序或可靠交付的命令直接调用服务接口。
- R3 Observable 只表达已成功发生的状态变化；初始状态通过 `Current` 读取。
- Addressables handle、Tween、订阅与 CancellationTokenSource 必须有明确 owner。
- UnityEditor API 只能进入 `Assets/Editor` 或完整 Editor 条件编译块。
- 生成文件不得手工维护；修改来源后重新生成并通过 freshness 校验。

## 6. 已取得的验证证据

以下证据来自本轮较早批次，证明主重构链路曾经通过，但不能替代最新清理后的最终复验：

- Unity 脚本编译多次通过。
- Persistence 检查曾通过 29 个断言；该临时检查工具现已按范围要求删除。
- UIBinder 与 Generated Code 校验通过。
- Packed Addressables 构建通过。
- Windows Standalone x64 Player 构建和启动通过。
- 首次启动、后续启动、语言保存、主页面、Settings Push/Pop 和重复点击流程正常。

## 7. 当前批次最终验证

最新清理涉及 C#、Addressables、Audio Catalog、生成常量和 Player Settings，因此交付前需要在 Unity 中依次完成：

1. 等待资源刷新和脚本编译，确认 Console 无 error。
2. 运行 `Tools/Template/Code Generation/Validate Generated Code`。
3. 运行 `Tools/Template/UI/Validate UIBinders`。
4. 确认 Active Build Target 为 `StandaloneWindows64`。
5. 运行 `Tools/Template/Build/Packed Addressables + Windows Player`。
6. 用户按需手动验证首次启动与正常启动流程；Codex 不进入 Play Mode。

只有本节全部通过，当前 checkout 才具备最终 Player 交付证据。

## 8. 完成定义

- 启动、导航、Audio、Settings、Localization 与 Save 均有唯一状态所有者。
- 不存在旧 Manager、EventBus、测试夹具或无引用第一方工具代码。
- 所有异步边界、资源与页面实例都具有明确生命周期。
- Editor-only 代码不进入 Player 程序集。
- 生成代码、资源索引、文档和项目版本一致。
- Unity 编译、生成校验、UIBinder 校验、Packed Addressables 与 Windows Player 构建全部通过。
