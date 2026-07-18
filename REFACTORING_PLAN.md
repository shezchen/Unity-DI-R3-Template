# DI-R3 Template 全项目重构计划

> 状态：实施收尾 1.1  
> 适用项目：`DI-R3-Template`  
> 核心策略：保留有效骨架，按运行链重建边界；每个批次均可验证、可回退，不进行一次性推倒重写。

## 1. 文档目的

本计划用于指导项目从当前“功能原型可运行”的状态，演进为一个边界清晰、生命周期明确、可测试、可构建、适合中小型独立游戏扩展的 Unity 模板。

计划重点不是统一代码风格或增加抽象层，而是解决以下结构性问题：

- 初始化正确性依赖对象构造和事件订阅顺序。
- 状态、命令、通知和持久化职责混在同一个模块中。
- 异步请求缺少取消、顺序语义和失败结果。
- Addressables handle、Tween、订阅和页面实例的所有权不完整。
- Editor 作者工具混入 Player 运行时代码。
- 缺少自动化测试、Packed Addressables 和 Player Build 门禁。

本计划以当前 checkout 的代码、场景和 ProjectSettings 为事实来源。README 和 `Assets/Doc` 仅作为意图参考。

## 2. 重构目标

### 2.1 必须达成

- 启动流程显式、有序、可等待、可取消，并能报告失败。
- 每份持久状态只有一个写入所有者，对外只暴露不可变快照。
- 命令直接调用服务并等待结果，不通过 EventBus 间接执行。
- 删除全局 EventBus；命令直接调用服务，状态变化由所有者通过 R3 Observable 暴露。
- Audio、UI、Persistence、Localization 的 Unity/第三方实现被稳定接口隔离。
- 所有 Addressables handle、Tween、R3 订阅和异步任务都有明确生命周期。
- 第一方代码具备可重复的 Editor 菜单、手工 Play Mode 和 Player 构建验证闭环；自动化测试按当前项目决策延期。
- 保留场景、Prefab、ScriptableObject 和音频资源的 GUID，避免破坏序列化引用。

### 2.2 明确非目标

- 不重写 VContainer、R3、UniTask、DOTween、Addressables 或 Unity Localization。
- 不引入第二套 Service Locator、全局单例或消息框架。
- 不为尚未出现的游戏业务建立通用 ECS、通用 Repository 或复杂领域框架。
- 不在首轮加入 3D 音频、对白、环境音、声部优先级、Audio Bank 等未确认需求。
- 不把仓库级格式化、命名清理与核心运行链重构混在同一批次。
- 不直接大范围手改 `.unity`、`.prefab`、`.asset` 或 `.meta` YAML。

## 3. 当前基线与主要问题

### 3.1 当前保留价值较高的骨架

- `ProjectLifetimeScope` 作为单一组合根。
- VContainer 构造/字段注入和 resolver 创建页面的工作流。
- `IBasePage` 的 `OnEnter / OnPause / OnResume / OnExit` 生命周期模型。
- Runtime / Save / Adapter 的数据分层方向。
- Addressables 管理 UI 和 Audio 资源的方向。
- UniTask 表达异步流程，R3 表达响应式通知。

### 3.2 当前高优先级问题

| 优先级 | 问题 | 证据位置 |
|---|---|---|
| P0 | AudioCatalog 运行时文件无条件引用 UnityEditor，Player 构建存在阻断风险 | `Assets/Scripts/Architecture/GameSound/AudioCatalog.cs:4-5` |
| P0 | 没有第一方自动化测试、Packed Addressables 和 Player Build 门禁 | `Assets/Scripts/Test/SoundTest.cs` 仅为手工测试组件 |
| P1 | Audio 同时承担设置订阅、查表、加载、缓存、播放、Tween 和释放 | `Assets/Scripts/Architecture/GameSound/AudioService.cs:19-223` |
| P1 | 同一 Clip 正在加载时，第二个请求会直接得到 null | `AudioService.cs:193-198` |
| P1 | BGM Play/Stop 没有 latest-command-wins 或取消语义 | `AudioService.cs:67-135` |
| P1 | BGM Fade 与设置音量竞争写入 `AudioSource.volume` | `AudioService.cs:92-109,154-161` |
| P1 | 初始设置通过不重放的 EventBus 发布，正确性依赖服务提前构造 | `DataManager.cs:67-71`、`EventBus.cs:15-22` |
| P1 | DataManager 同时拥有设置、游戏存档、JSON、路径、默认值和事件 | `Assets/Scripts/Architecture/Data/DataManager.cs:14-274` |
| P1 | `GameSettings` 和设置事件暴露同一个可变对象 | `DataManager.cs:35`、`SettingsChangedEvent.cs:40-43` |
| P1 | 分辨率和窗口模式只有设置页交互时才应用，启动时不恢复 | `SettingsPage.cs:47-98` |
| P1 | 语言确认通过事件触发 `.Forget()`，页面未等待 Locale 真正切换 | `LanguagePage.cs:70-76`、`LanguageManager.cs:33-36` |
| P1 | UI Push/Pop 无串行化、失败回滚和资源 lease | `UIPageStack.cs:42-118`、`UIManager.cs:94-155` |
| P2 | 生成器输出不稳定，缺少碰撞、转义和 freshness 检查 | `Assets/Editor/AddressableKeyGenerator.cs:18-83` |
| P2 | `DOTweenTool` 体积远大于实际使用面，另有未使用 StateMachine/RectTransform 工具 | `Assets/Scripts/Tools` |
| P2 | README 和模块文档已与 Unity 版本、菜单、依赖及真实代码行为漂移 | `README.md`、`Assets/Doc` |

## 4. 全局设计原则

### 4.1 状态所有权

- 持久状态由对应服务唯一拥有。
- 对外暴露不可变 snapshot/read model。
- 修改必须经过服务命令，以保证校验、通知和保存不会被绕过。
- EventBus 消息不得携带可被外部修改的内部状态引用。

### 4.2 命令与事件

- 需要返回值、失败信息、严格顺序或可靠执行的操作使用直接接口调用。
- EventBus 只发布已经成功发生的事实，例如 `SettingsSaved` 或 `GameLoaded`。
- 初始化状态通过显式读取或初始化参数传递，不依赖订阅先后。

### 4.3 异步语义

- 每个异步 API 必须定义“任务完成”具体代表什么。
- 长生命周期操作接受 `CancellationToken`。
- 无法等待的 Unity 事件边界使用统一的安全 Forget/log 策略。
- 同类命令必须定义并发策略：拒绝、排队、合并或 latest-wins。

### 4.4 资源所有权

- 获得 Addressables handle 的对象负责恰好释放一次。
- 页面实例持有自己的资源 lease，销毁页面后才能释放资源。
- Audio shutdown 先停止播放并解除 Clip 引用，再释放加载句柄。
- Tween 和输入订阅绑定到所属页面、播放器或 Scope 生命周期。

### 4.5 抽象约束

- 只有真实替换点、测试缝或跨模块边界才引入接口。
- 不创建只转发一次调用的 Provider/Wrapper。
- 优先使用少量清晰接口，而不是为每个类创建接口。

## 5. 目标架构

```text
Bootstrap / Composition Root
├─ Application
│  ├─ GameFlow
│  ├─ Settings
│  ├─ GameSave
│  └─ Navigation Contracts
├─ Presentation
│  └─ UI Pages / Components
└─ Infrastructure
   ├─ Audio + Addressables
   ├─ Persistence + FileSystem
   ├─ Localization
   ├─ Unity Screen
   └─ UI Addressables Provider

Presentation UI ──────> Application Contracts
Application ──────────> Models / Ports
Infrastructure ───────> Application Ports
Bootstrap ────────────> 组装全部实现，不承载业务规则
```

程序集边界不会在第一步一次拆完。先建立逻辑边界和测试缝，循环依赖清除后再让 asmdef 强制依赖方向。

## 6. Audio 模块重写规格

Audio 是本轮重构的第一个垂直切片，用于建立后续模块共同遵循的边界、异步和测试规范。

### 6.1 已确认功能需求

- 全局 2D Audio 分为 Music 和 SFX 两类。
- 同一时刻只播放一首循环 BGM。
- 切换 BGM 时先淡出旧曲，再换曲并淡入新曲。
- 相同 BGM 已播放时重复调用不重启。
- SFX 使用 OneShot 语义并允许重叠。
- Music/SFX 各自拥有持久化总音量。
- AudioClip 通过 Addressables 按需加载并缓存。
- 未知 Cue、资源加载失败、被新命令取代和取消都有明确结果。
- Scope 关闭时停止播放、取消操作并释放所有资源。

### 6.2 默认决策

以下决策在实现前可调整；若没有新需求，则按默认值执行：

- BGM 转场采用单 Source 的 fade-through-silence，不实现双 Source 真 crossfade。
- 全局 BGM/UI 转场使用 independent update，暂停菜单下不会因 `timeScale == 0` 卡住。
- 资源默认按需加载；不保留无调用证据的 `PreloadAllClipsAsync` 公共 API。
- 如后续出现明确加载尖峰，再增加按标签或场景定义的预加载集。
- SFX 暂不提供单实例停止、声部池、优先级和并发上限。
- 用户音量由 AudioMixer Bus 控制；播放器转场增益由 Source 控制。

### 6.3 公开接口

```text
GameFlow ───────────> IMusicPlayer
UI / Presentation ─> ISfxPlayer
Settings Binding ──> IAudioLevelsControl
Loading Flow ──────> IAudioPreloader（只有确认需要时才加入）
```

建议接口职责：

- `IMusicPlayer`
  - `PlayAsync(MusicCueId, MusicTransition, CancellationToken)`
  - `StopAsync(MusicTransition, CancellationToken)`
- `ISfxPlayer`
  - `PlayAsync(SfxCueId, float gain, CancellationToken)`
- `IAudioLevelsControl`
  - `Apply(AudioLevels)`
- `AudioPlayResult`
  - `Started`
  - `UnknownCue`
  - `LoadFailed`
  - `Superseded`
  - `Cancelled`
  - `ShuttingDown`

接口不暴露 `AudioSource`、Addressables handle、DOTween Tween、`GameSettingsRuntime` 或 EventBus。

### 6.4 内部组件

#### AudioCueDefinition / AudioCatalog

- Cue 包含稳定 ID、类别、`AssetReferenceT<AudioClip>`、默认增益和已确认的播放策略。
- Runtime Catalog 只负责只读查找，不引用 UnityEditor。
- 使用 Unity 可序列化 Entry 列表建立运行时索引，避免运行时核心依赖 Odin 字典序列化。
- ID 在全局唯一，或类型系统明确区分 Music/SFX。

#### AddressableAudioClipStore

- 以稳定 Cue identity 或资源 GUID 为缓存键，不使用分类内裸字符串。
- 同一资源的并发请求共享同一个 in-flight task。
- 加载失败释放 handle，并允许后续重试。
- shutdown 后拒绝新请求。
- 每个 handle 恰好释放一次。

#### MusicPlayer

- 维护单独的播放状态机。
- 每个 Play/Stop 增加 request generation 或取消旧命令。
- A 后 B 即使加载逆序完成，最终也只能播放 B。
- Pending Play 后 Stop，加载完成不得重新开播。
- “await Play 完成”默认表示资源加载成功且播放已启动；是否等待完整淡入由接口文档固定。

#### SfxPlayer

- 当前需求下使用一个 SFX AudioSource 的 `PlayOneShot`。
- 保留每次播放的 gain 参数。
- 不为尚不存在的停止/优先级需求提前创建 Source Pool。

#### UnityAudioOutput

- 内部持有 BGM/SFX AudioSource 和 AudioMixer 参数。
- 负责 Unity API 边界，不向上暴露裸 Source。
- Music/SFX 用户音量写入 Mixer Bus。
- Music transition gain 只由 MusicPlayer 控制，避免与设置音量竞争。

### 6.5 设置集成

Audio 核心不依赖 `DataManager`、`GameSettingsRuntime` 或 `SettingsChangedEvent`。

当前 `AudioSettingsBinding` 直接依赖 `ISettingsService`：

1. Settings 初始化后主动读取当前 BGM/SFX 音量。
2. 转换为 `AudioLevels` 并调用 `IAudioLevelsControl.Apply`。
3. 订阅后续设置 snapshot 变化。
4. Audio 核心始终不依赖持久化实现。

### 6.6 作者工具

Editor 工具统一放入 `Assets/Editor/Audio`：

- 扫描音频目录或明确的 Catalog Entry。
- 验证 AssetReference、Addressables 类型和地址。
- 检查空 ID、重复 ID、标识符碰撞和失效资源。
- 以 ordinal 顺序稳定生成 typed cue 常量。
- 提供 check-only/freshness 模式，生成结果过期时让验证失败。

保留现有 AudioClip 和 Catalog asset 的 `.meta` GUID，但 Catalog 内容直接按新结构重新生成，不读取或迁移旧序列化数据。

### 6.7 Shutdown 顺序

1. 标记 ShuttingDown，拒绝新命令。
2. 取消 pending Play/Stop/Load。
3. 终止 Music transition Tween。
4. Stop BGM/SFX，并清空 Source.clip。
5. 收束仍在进行的加载任务。
6. 释放所有 Addressables handle。
7. 释放订阅、CancellationTokenSource 和其他所有者资源。

### 6.8 Audio 验收矩阵

- 同 ID 并发加载只发起一次真实加载，所有调用者获得确定结果。
- 快速连续播放 SFX 不因首次加载而静默丢请求。
- A 后 B、加载逆序完成，最终播放 B。
- Play 加载中调用 Stop，资源完成后仍保持停止。
- Fade 中修改用户音量，最终 Mixer 保持新值。
- 相同 BGM 重复调用不重启。
- 未知 Cue、类型错误和加载失败返回明确结果。
- 失败加载允许重试且不泄漏 handle。
- Dispose/Shutdown 发生在加载中时，不再触碰已释放 AudioSource。
- TimeScale 为 0 时 Stop/Fade 不挂起。
- Catalog 中每个 Cue 都能在 PlayMode 真实加载为 AudioClip。
- 目标平台 Player 中能够初始化、播放并正确关闭。

## 7. Settings 与 Persistence 目标设计

### 7.1 拆分职责

当前 `DataManager` 拆为：

- `ISettingsService`
  - 拥有当前设置 snapshot。
  - 提供受控修改命令。
  - 提供初始化后可读取的当前值和后续变化流。
- `IGameSaveService`
  - 新游戏、存档槽、加载和保存。
- `ISettingsRepository`
  - 设置 DTO 的读取、校验和原子写入。
- `IGameSaveRepository`
  - 游戏存档 DTO 的读取、校验和原子写入。
- `IFileStore`、`IClock`
  - 隔离 `File`、`Application.persistentDataPath` 和 `DateTime.Now`，形成测试缝。

### 7.2 SettingsSnapshot

- 使用不可变 snapshot。
- 外部无法直接修改内部状态。
- 变更流程固定为：校验/归一化 → 更新 snapshot → 应用系统设置 → 保存 → 发布事实通知。
- 是否保存每个 Slider tick 由 UI 交互策略决定；推荐预览期间只应用，拖动结束或 debounce 后保存。

### 7.3 设置应用器

- `AudioSettingsBinding`：Settings → AudioLevels。
- `DisplaySettingsApplier`：Settings → `Screen.SetResolution` / FullScreenMode。
- `LocalizationSettingsBinding`：Settings → Locale。
- 设置页不再直接操作 Unity Screen API。

### 7.4 存档格式

- Settings 和 GameSave 均包含 schema version。
- 版本升级直接切换新 schema；旧格式文件不读取。
- 写入采用临时文件 + 替换的原子策略。
- 明确缺字段、旧版本、损坏 JSON、写入失败和备份恢复行为。
- `SaveGame` 等操作返回结果，不只记录日志。

## 8. Bootstrap 与 GameFlow 目标设计

### 8.1 启动顺序

```text
1. 建立 Composition Root
2. 加载 Settings
3. 初始化 Localization
4. 应用保存的 Locale
5. 应用 Display Settings
6. 应用 Audio Levels
7. 初始化 UI Navigation
8. 根据 FirstLaunch / Language 状态决定入口页面
9. 进入可交互状态
```

- 启动流程由可等待的 application bootstrapper 表达。
- Unity `Start` 只作为框架入口，立即委托给可测试的异步流程。
- 每个步骤定义失败策略：阻断、使用默认值、重试或进入错误页面。
- 删除仅用于强制构造服务的未使用注入字段。

### 8.2 关闭顺序

```text
1. 停止接受输入和新导航
2. 退出并销毁页面
3. 停止 Audio
4. 完成必要的设置/存档写入
5. 释放 Addressables、Tween、订阅和容器资源
```

## 9. Localization 目标设计

- 以实例状态替代静态 `CurrentLanguage`。
- `SetLanguageAsync` 直接返回成功、UnsupportedLocale、InitializationFailed 等结果。
- LanguagePage 等待 Locale 成功切换后再导航。
- 当前语言写入 Settings schema。
- 首次启动才显示语言选择页；后续启动直接应用已保存语言。
- 预览选择文本等页面内部状态优先使用本地回调，不绕 EventBus。
- 找不到 Locale 时不得更新“当前语言”副本。

## 10. UI Navigation 与资源生命周期

### 10.1 目标组件

- `IPageNavigator`
  - Push、Pop、Replace、Clear。
  - 串行化所有页面转场。
- `IUiPrefabProvider`
  - Addressables 加载、失败结果和资源 lease。
- `PageStack`
  - 只管理页面实例、栈和生命周期。
- `PageLease`
  - 页面实例与对应资源引用共同释放。

### 10.2 页面转场规则

- Push 先加载并验证新页面，再暂停旧页面。
- OnEnter 失败时销毁新页面并恢复旧页面。
- Pop 必须完成 OnExit 后才销毁和恢复下层页面。
- 同时发生的 Push/Pop 使用 async gate 排队或明确拒绝。
- 页面生命周期接受与页面销毁绑定的 CancellationToken。
- Scope shutdown 通过 Clear 正常执行所有 OnExit。

### 10.3 页面职责

- 页面负责渲染、收集用户输入和展示服务结果。
- 页面不负责跨系统初始化或存档细节。
- 页面依赖 `IPageNavigator`、`ISfxPlayer`、`ISettingsService` 等最小应用接口，不依赖 concrete Manager。
- `UIBinder` 首轮保留，但增加 Editor 校验，阻止重复 ID、空引用和缺失组件进入运行时。

## 11. 跨模块通信决策

全局 EventBus 已 clean cutover 删除，不保留兼容层。需要结果、失败信息或严格顺序的操作直接调用服务接口；状态所有者通过 `Current` 提供初始 snapshot，并可通过 R3 Observable 暴露后续变化。页面内部瞬时交互使用局部回调或 R3 订阅。

## 12. Tools、生成器与依赖清理

### 12.1 生成器

- AddressableKeys 和 Audio Cue 生成器共享标识符清洗、关键字处理、字符串转义和稳定排序逻辑。
- 检测原始 key 重复与清洗后变量名碰撞。
- 提供批处理入口和 check-only 模式。
- 生成前验证，失败时不覆盖现有文件。
- 生成文件带明确命名空间和“禁止手改”头部。

### 12.2 通用工具

- 统计真实调用面。
- 将 `DOTweenTool` 收缩到项目实际使用、行为已验证的扩展。
- 未使用的 `StateMachine`、`RectTransformExtensions` 和 Editor 小工具先移出 Runtime 或删除，不在核心重构中顺手扩展。
- 若决定保留为模板示例，应放入明确的 Samples/Optional 区域并有测试。

### 12.3 Odin 决策

第一方运行时代码已移除所有 Odin 类型和特性，Inspector 作者操作改用 Unity CustomEditor、Header 与 ContextMenu。Odin 不再是模板必需依赖。

## 13. 分批实施路线图

### 批次 0：建立可比较基线

任务：

- [x] 记录并持续避开批次开始前的 dirty worktree，不将用户已有改动混入重构范围。
- [x] 记录 SampleScene 当前启动、语言、页面和音频行为。
- [x] 将 AudioCatalog 的 Editor-only 引用和生成逻辑移出 Runtime，先恢复 Player 可构建边界。
- [x] 当前不引入第一方 asmdef；逻辑边界保持在 Assembly-CSharp，除非后续出现明确收益再评估程序集隔离。
- [x] 以现有 Assembly-CSharp 边界建立 Editor 验证与构建入口；按用户要求，正式测试程序集延期。
- [x] 建立 Packed Addressables 构建入口。
- [x] 选择首个支持平台；以 Windows Standalone x64 建立 Player Build 基线。
- [x] 将 `SoundTest` 限定为 Editor diagnostics；后续 Audio 迁移完成时从场景移除。

退出条件：

- Unity 冷导入和脚本编译零 error。
- EditMode/PlayMode 测试可批处理运行。
- Packed Addressables 构建成功。
- Player 构建并启动 SampleScene 成功。

### 批次 1：Audio 垂直切片

任务：

- [x] 冻结 6.2 节 Audio 默认决策。
- [x] 为 AudioCue、ClipStore、MusicPlayer、SfxPlayer、LevelsControl 建立新实现。
- [x] 按用户要求将 fake ClipStore/Output 单元测试移出本轮范围；保留验收矩阵供未来恢复测试工作。
- [x] clean cutover 删除旧 `IAudioService`，调用方直接依赖新接口。
- [x] 增加 AudioSettingsBinding，消除初始设置事件时序依赖。
- [x] 创建 Music/SFX AudioMixer Bus 并迁移输出。
- [x] 迁移 MainScenePage 和测试调用方。
- [x] 更新 Audio Catalog 和代码生成流程。
- [x] 删除旧 AudioService 和空壳 Source Provider。

退出条件：

- 6.8 节 Audio 验收矩阵全部通过。
- 所有现有 Cue 均能真实 Addressables 加载。
- Audio 核心不引用 Data、EventBus、UnityEditor 或具体页面。
- 场景退出后无残留 Tween、订阅或 handle。

### 批次 2：Settings 与 Persistence

任务：

- [x] 引入不可变 SettingsSnapshot。
- [x] 拆出 SettingsService、GameSaveService、Repository、FileStore 和 Clock。
- [x] 增加 schema version、原子写入和失败结果；不提供旧格式 migration。
- [x] 增加 DisplaySettingsApplier。
- [x] 将语言加入设置持久化。
- [x] 将 AudioSettingsBinding 切换到新 SettingsService。
- [x] 修复 `GameDataRumtime` 命名；旧存档不做兼容。

退出条件：

- 默认、旧档、缺字段、损坏 JSON、保存失败和往返测试通过。
- 启动后无需打开设置页，显示、音量和语言设置即生效。
- 外部无法绕过服务直接修改设置状态。

### 批次 3：Bootstrap 与 Localization

任务：

- [x] 建立显式可取消启动流程。
- [x] 为每个初始化阶段定义失败策略。
- [x] LanguagePage 改为直接等待 Localization 服务结果。
- [x] 首次启动和后续启动采用不同入口路由。
- [x] 移除 Language 命令型 EventBus 用法和静态 CurrentLanguage。
- [x] 建立退出顺序：受控页面清理可等待；Scope teardown 跳过动画，先销毁页面、再释放 Addressables，随后由容器释放 Audio 等服务。

退出条件：

- 启动顺序不再依赖未使用字段或构造副作用。
- 页面导航发生前 Locale 已完成切换。
- 初始化失败能够记录明确上下文并进入可恢复路径。

### 批次 4：UI Navigation 与资源

任务：

- [x] 拆分 PageNavigator、UiPrefabProvider 和内部 PageStack。
- [x] 增加 async gate、失败回滚和重复导航策略。
- [x] 页面与 Addressables lease 绑定。
- [x] 页面生命周期增加 CancellationToken。
- [x] 受控 Clear 执行 OnExit；Scope Dispose 采用取消命令、销毁实例、释放 lease 的同步快速路径。
- [x] 将页面对 concrete UIManager 的依赖迁移到 IPageNavigator。
- [x] 增加 UIBinder Editor 校验与 Player Build 前置门禁。

退出条件：

- Push/Pause/Pop/Resume、加载失败和重复点击测试通过。
- 页面存活期间对应资源不会被提前释放。
- Scope 关闭后页面、Tween、输入和订阅全部释放。

### 批次 5：EventBus 与工具收缩

任务：

- [x] 统计所有 Publish/Receive，并 clean cutover 删除全局 EventBus 与全部事件定义。
- [x] 使用 `ForgetLogged` 统一无法 await 的 Unity/R3 回调边界，忽略预期生命周期取消并记录意外异常。
- [x] 合并并加固 Addressables/Audio 代码生成公共逻辑。
- [x] 增加生成文件 freshness 菜单和 Player Build 前置门禁。
- [x] DOTweenTool 只保留真实调用扩展；删除未使用的 StateMachine、RectTransformExtensions 和行数统计 EditorWindow。
- [x] 第一方运行时代码移除 Odin 依赖；Odin 不再是模板必需依赖。

退出条件：

- EventBus 不再影响核心状态正确性。
- 生成代码稳定、可批处理校验且不会无意义漂移。
- Runtime 不包含无调用证据的大型工具面。

### 批次 6：最终程序集边界与交付文档

如果后续确认需要程序集隔离，建议最终粗粒度程序集：

```text
Template.Application
Template.Infrastructure
Template.Presentation
Template.Bootstrap
Template.Editor
Template.Tests.EditMode
Template.Tests.PlayMode
```

只有当纯模型数量和复用价值足够时，再增加 `Template.Core`；不为层次完整而创建空程序集。

任务：

- [x] 重新评估 asmdef 的收益与维护成本；当前明确不引入第一方 asmdef。
- [x] 清除旧 Manager/EventBus 引用、未使用注入和零调用工具。
- [x] 更新 README、GameSound、重构计划和协作文档；删除已不存在的 EventBus 文档。
- [x] 修正 Unity 版本、菜单、依赖和示例代码漂移；移除仓库中无 LICENSE 文件支撑的许可证声明。
- [x] 固化 UIBinder、Persistence、Audio/生成文件 freshness、Packed Addressables 和 Windows Player 验证入口。

退出条件：

- Presentation 不被 Application/Infrastructure 反向依赖。
- Editor API 不进入 Player 程序集。
- README 和模块文档与真实运行时行为一致。
- 完整构建门禁可在干净工作区重复通过。

## 14. 验证门禁

### 每个批次必须执行

1. 检查并保留批次开始前的工作树状态。
2. Unity 冷编译，Console 无新增 error。
3. 第一方自动化测试当前延期；恢复测试工作后再执行 EditMode tests。
4. 用户手动执行 PlayMode 核心流程 smoke tests。
5. 生成代码 freshness check。
6. 涉及资源时执行真实 Addressables 加载。
7. Packed Addressables build。
8. 目标平台 Player build。
9. 启动 Player 并扫描日志中的 exception/error。
10. 更新本计划中的完成状态和相关模块文档。

### 测试覆盖矩阵

当前阶段按用户要求暂缓单元测试与测试 asmdef；下表保留为未来恢复测试工作时的覆盖目标，不阻塞现阶段重构推进。

| 模块 | EditMode | PlayMode / Integration |
|---|---|---|
| Audio | 状态机、加载去重、取消、结果、释放 | 真实 AudioClip、Mixer、场景关闭 |
| Settings | 默认值、校验、快照、schema 拒绝、损坏文件 | 启动恢复和系统应用器 |
| GameSave | Adapter 往返、槽位、失败、原子写入 | 新游戏、保存、重启、加载 |
| Bootstrap | 阶段顺序、失败策略、取消 | SampleScene 完整启动 |
| Localization | Locale 映射和失败结果 | 真实表初始化与页面导航 |
| UI | 栈状态机、并发策略、失败回滚 | Prefab 加载、注入、动画、资源释放 |
| EventBus | 订阅、Dispose、发布语义 | 仅保留必要 smoke coverage |
| Codegen | 排序、转义、碰撞、stale 检查 | Unity 批处理入口 |

## 15. Clean Cutover 安全策略

- 每个批次独立提交，不混入无关清理。
- 新核心接入时同步删除旧 API 和旧实现，不保留兼容适配器。
- 迁移 ScriptableObject 时保留原 `.meta` GUID。
- 场景/Prefab 引用优先通过 Unity Editor 重接并验证，不直接大范围修改 YAML。
- 删除旧类前先全仓搜索代码引用和序列化 GUID 引用。
- Addressables key 变更必须在同一批次更新全部调用方，不保留兼容映射。
- 设置和存档格式改变后直接使用新格式；旧本地文件由用户删除或由开发构建统一重置。
- 当前工作树已有用户修改；实施时必须避开或先由用户单独提交这些变更。

## 16. 全项目完成定义

只有同时满足以下条件，才能认为本轮重构完成：

- 启动、导航、Audio、Settings、Localization 和 Save 均有明确状态所有者。
- 不再使用 EventBus 执行命令或传递初始化状态。
- 不存在依靠未使用注入触发构造的运行时行为。
- 所有公共异步 API 的成功、失败、取消和并发语义已文档化；自动化测试覆盖按当前决策延期。
- 所有 Addressables handle、Tween、订阅和 CancellationTokenSource 有清晰释放链。
- Editor-only 代码不进入 Player 程序集。
- 设置和游戏存档具备版本、旧 schema 拒绝、损坏处理和原子写入。
- UI 转场具备串行化、失败回滚和资源 lease。
- Audio 满足 6.8 节全部验收场景。
- Editor 验证菜单、用户手动 PlayMode、Packed Addressables 和 Player Build 门禁稳定通过；第一方测试程序集延期。
- 正式构建场景不包含手工测试夹具。
- 文档、生成代码和实际项目版本保持一致。

## 17. 建议立即开始的下一步

按以下顺序启动实施：

1. 完成批次 0 的工作树隔离、Player 构建边界修复和测试骨架。
2. 冻结 Audio 默认决策。
3. 先用 fake output/store 实现并测试新 MusicPlayer 和 ClipStore。
4. 同批切换全部调用方并删除旧 Audio API/实现。
5. 通过真实 Addressables、Mixer、SampleScene 和 Player Build 验证新实现。

Audio 试点通过后，以相同的“明确状态所有者 → 隔离基础设施 → 定义异步语义 → 建立验收门禁”方法依次推进 Settings、Localization 和 UI。
