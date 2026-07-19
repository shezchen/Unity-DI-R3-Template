# DI-R3-Template 协作指南

## 适用范围

本文件适用于整个仓库。目标是把项目维持为可复用、可替换、适合中小型独立游戏扩展的 Unity 模板，而不是把某个具体游戏的业务规则塞进基础设施。

开始修改前先阅读目标模块及其调用方。以当前 checkout 的代码、场景和 ProjectSettings 为准；README 与 `Assets/Doc` 用于理解意图，但可能滞后。

## 当前技术基线

- Unity 版本以 `ProjectSettings/ProjectVersion.txt` 为准，当前为 `6000.3.5f2`。
- 核心依赖：VContainer、R3、UniTask、DOTween、Addressables、Unity Localization、Input System、URP 2D。
- 第一方运行时代码目前没有 asmdef，统一进入 `Assembly-CSharp`；`Assets/Editor` 进入编辑器程序集。不要假设已有模块级程序集隔离。
- `Assets/Plugins`、`Assets/NuGet` 和大部分包代码视为第三方依赖，除非任务明确要求，不直接修改。
- `Library`、`Temp`、`Logs`、`obj`、生成的 `.csproj`/`.sln` 不是源码，不提交也不手工维护。

## 运行时主链

唯一启用的构建场景是 `Assets/Scenes/SampleScene.unity`。

1. 场景中的 `ProjectLifetimeScope` 是组合根，持有并注册 `GameFlowController`、`UIRoot`、`AudioCatalog` 和两个 `AudioSource`，再注册全局服务。
2. `GameFlowController.Start()` 是当前游戏入口，依次初始化 `ISettingsService`、`ILanguageService`，应用保存的 Locale、Display 与 Audio 设置，再通过 `IPageNavigator` 进入首个页面。
3. 初始化完成后通过 `IPageNavigator` 加载首个页面；首次启动进入 `LanguagePage`，确认语言后 Replace 为 `MainScenePage`，设置页作为其上层页面 Push/Pop。
4. UI Prefab 由 Addressables 加载，并由 `IObjectResolver.Instantiate` 实例化，因此页面上的 VContainer 注入才能生效。

启动任务绑定 `GameFlowController` 的销毁 CancellationToken；场景或 Scope 退出导致的取消不是启动失败，不应记录为 error。

## 模块职责与依赖方向

### `Assets/Scripts/Architecture/DependencyInjection`

- `ProjectLifetimeScope` 是依赖注册的唯一默认入口。
- 新增长生命周期服务时在这里显式注册；优先暴露接口并使用构造函数注入。
- 场景对象或 Prefab 组件使用 `RegisterComponent`/resolver 注入，不新增静态 Service Locator 或第二套全局单例。

### `Assets/Scripts/Architecture/GameFlow`

- 只编排启动、阶段切换和跨系统流程，不承载存档细节、页面内部交互或具体音频加载逻辑。
- 异步阶段使用 `UniTask` 并保持可读的顺序；不要用 `Update` 轮询初始化状态。

### `Assets/Scripts/Architecture/Data`

- `ISettingsService` 是设置状态唯一所有者，只暴露不可变 `SettingsSnapshot`；外部通过受控 Set 方法修改。
- `IGameSaveService` 是当前游戏存档状态唯一所有者；未 NewGame/Load 前 Save 会返回 `NoActiveGame`。
- `ISettingsRepository`、`IGameSaveRepository` 负责 schema 校验与 JSON 映射，`IFileStore` 负责原子替换，`IClock` 提供存档时间测试缝。
- 设置修改当前语义是：校验/归一化 -> 原子保存候选 snapshot -> 替换 Current -> 发布 Changes。保存失败时内存状态不变。
- 新增持久字段时必须同步 snapshot/runtime、document、repository 校验与映射、默认值及服务命令，不能只改 JSON 模型。
- 存档使用 Newtonsoft.Json，路径位于 `Application.persistentDataPath`。当前重构变更格式时直接切换新结构，并覆盖默认值、缺字段和损坏 JSON 的行为；旧档不做兼容。
- 当前整项目重构采用 clean cutover：不保留旧 API、旧序列化结构、兼容适配器或一次性 migration；除非用户之后明确要求兼容。

### 跨模块通信

- 当前项目已删除全局 `EventBus`。需要返回值、严格顺序或可靠交付的操作直接调用服务接口。
- 状态所有者可暴露 R3 Observable 作为后续变化流，但初始状态必须通过显式 Current/snapshot 读取，不依赖事件重放。
- MonoBehaviour 订阅必须 `.AddTo(this)`；纯 C# 单例持有 `DisposableBag` 并在 `Dispose` 释放。

### `Assets/Scripts/UI`

- `IPageNavigator` 串行化 Push/Pop/Replace/Clear；`IUiPrefabProvider` 加载 Addressables，`PageStack` 管理页面实例、生命周期和资源 lease。
- 新页面实现带 `CancellationToken` 的 `IBasePage` 生命周期。被覆盖时禁用交互，恢复时刷新可能过期的数据，退出完成后由页面 lease 销毁实例并释放句柄。
- Replace 为保留失败回滚，会等待旧页 `OnPause` 后进入新页，成功后才对旧页执行 `OnExit`；不允许与替换页叠加显示时，旧页必须在 `OnPause` 完成隐藏，并在 `OnResume` 恢复。
- 需要注入的页面必须经 `IPageNavigator` 创建，禁止改回 `Object.Instantiate`；页面只依赖导航接口，不依赖 concrete navigator。
- Push 同类型同 key 页面返回 `AlreadyCurrent`；所有转场经过 async gate 排队，加载或 OnEnter 失败必须恢复旧页面。
- 页面 Prefab 保持 `UIBinder` 和需要的 `CanvasGroup`/`GraphicRaycaster` 组件。自动绑定对象按 `Button_`、`Text_`、`Image_`、`Slider_`、`Toggle_`、`Input_`、`Panel_`、`Object_` 前缀命名，ID 在同一页面内唯一。
- `Tools/Template/UI/Validate UIBinders` 与 Player Build 前置门禁检查重复 ID、空引用、层级漂移、名称漂移和前缀对应组件。
- UI 可以调用应用服务，但业务状态的最终写入归对应服务所有。

### `Assets/Scripts/Architecture/GameSound`

- 调用方分别依赖 `IMusicPlayer`、`ISfxPlayer` 和 `IAudioLevelsControl`，不要直接操作场景中的 BGM/SFX AudioSource。
- `AudioCatalog` 维护 typed cue ID 到 Addressable AudioClip 引用的映射；ClipStore、Player 与 Output 分别负责加载缓存、播放状态和 Unity 输出。
- 添加音频后同步 Addressable 标记、Catalog 与生成常量，并验证重复 ID 和加载失败分支。

### `Assets/Scripts/Architecture/Language`

- `LanguageManager` 封装 Unity Localization 的初始化与 Locale 切换。
- Locale 代码集中维护，当前为 `en`、`zh-Hans`、`ja`；扩展语言时同时更新 enum、Localization Settings/表、选择 UI 和字体资源。
- `SettingsSnapshot` 包含语言字段；启动时先恢复保存的 Locale，并依据 `IsFirstLaunch` 在 `LanguagePage` 与 `MainScenePage` 之间分流。

### `Assets/Editor`、`Assets/Scripts/Generated`

- UnityEditor API 只能放在 `Assets/Editor`，或完整包在 `#if UNITY_EDITOR` 内，避免 Player 构建引用编辑器程序集。
- `AddressableKeys.cs` 由 `Tools/Template/Code Generation/Generate Addressable Keys` 生成；`AudioClipName.cs` 由 `AudioCatalog` Inspector 的 `Auto Generate Index` 生成。不要手工编辑生成文件，应修改来源后重新生成。
- 两套生成器共享标识符清洗、关键字、碰撞、转义和稳定排序规则；Player Build 前置门禁会拒绝过期生成文件。

## 编码与生命周期约束

- 默认沿用现有命名空间：基础设施为 `Architecture`/`Architecture.Data`，UI 为 `UI`，通用扩展为 `Tools`，生成音频常量为 `Generated`。
- 异步接口优先 `UniTask`/`UniTask<T>`。只允许 Unity 消息等框架边界使用 `async void`；其他地方返回任务供调用方等待。
- `.Forget()` 只用于确实无法等待的事件边界，并确保异常有明确日志或处理路径。
- DOTween Tween、R3 订阅、Addressables handle 都必须有清晰所有者，并在禁用、销毁或容器释放时清理。
- 不用静默 `catch` 掩盖失败。日志至少包含模块名、操作和关键 ID/路径；可恢复失败返回明确结果。
- 不为“以后也许会用”提前增加抽象。新接口应对应真实替换点、测试缝或跨模块边界。

## 资源与序列化改动

- 场景、Prefab、ScriptableObject 和 Addressables 配置优先通过 Unity Editor 修改，避免手工大范围编辑 YAML。
- 移动或重命名 Unity 资源时保留 `.meta` 与 GUID；不要删除后重建来完成普通移动。
- 新增 Addressable 资源后重新生成 `AddressableKeys.cs`，并检查引用的 key 与组配置一致。
- 修改 `ProjectLifetimeScope` 的序列化字段后，同时检查 `SampleScene` 的 Inspector 引用是否完整。
- 第一方运行时代码不依赖 Odin；不要为 Inspector 美化重新引入必需的 Odin 运行时依赖。

## 验证要求

按改动风险选择最小但真实的验证闭环：

1. 先检查 `git status --short`，保留并避开用户已有改动。
2. 让 Unity 完成导入和脚本编译，确认 Console 没有新增 error。禁止运行 `dotnet build`（包括对 Unity 生成的 `.csproj`/`.sln`）；本项目只接受 Unity Editor 或 Unity batchmode 给出的编译结果。
3. 禁止由 Codex 或 Unity MCP 进入、暂停或退出 Play Mode，也不通过 MCP 触发运行时流程；Play Mode 与实际游玩验证全部由用户执行。Unity MCP 仅用于 Edit Mode 下的只读状态、Console、资源和 Inspector 检查，除非用户另行明确授权。
4. 数据改动验证默认创建、保存、加载、损坏文件回退，以及 Runtime/Save 往返一致性。
5. UI/流程改动在 `SampleScene` 实际走一遍 Push、Pause、Pop、Resume，并观察注入、交互开关和动画结束状态。
6. Addressables/音频/本地化改动验证真实资源加载；涉及 Player 行为时再做对应平台构建。

项目当前不保留第一方单元测试、测试程序集、手工测试组件或测试场景，也不为测试引入 asmdef。验证依靠 Unity 编译、Editor 作者门禁、Addressables/Player 构建和用户手动 Play Mode；除非用户之后明确改变该决策，不新增测试代码。

交付时说明修改文件、验证信号和仍未验证的边界。不要把“代码看起来能编译”表述成已通过 Unity 或 Player 验证。

## 当前已知边界

- Scope 释放时 `PageNavigator` 采用同步快速退出：取消排队命令，跳过页面退出动画，并按页面实例后资源 lease 的顺序清理。
