# DI-R3 Template

Unity 6 中小型独立游戏模板。项目以 VContainer 作为唯一组合根，以 UniTask 表达可取消的异步流程，以 R3 暴露状态变化，并通过 Addressables 管理 UI 与音频资源。

当前编辑器版本：`6000.3.5f2`。

## 主要能力

- 显式启动链：设置、本地化、显示/音频应用和首页面路由均可等待、可取消。
- 设置与存档：不可变设置快照、独立 Repository、schema version、损坏文件回退和原子写入。
- 本地化：支持 `en`、`zh-Hans`、`ja`，保存 Locale，首次启动才进入语言选择页。
- UI 导航：串行 Push/Pop/Replace/Clear、失败回滚、页面生命周期 CancellationToken 和 Addressables lease。
- 音频：Music/SFX 分离接口、Addressable Clip 缓存、latest-command-wins BGM 转场和 AudioMixer 音量。
- 作者门禁：UIBinder 校验、Audio Catalog freshness、Addressable Keys freshness、Packed Addressables 与 Windows Player 构建入口。

## 运行时结构

```text
Assets/Scripts/
├─ Architecture/
│  ├─ DependencyInjection/   # ProjectLifetimeScope 组合根
│  ├─ GameFlow/              # 启动编排
│  ├─ Data/                  # Settings、GameSave、Persistence
│  ├─ GameSound/             # Audio contracts、players、output、clip store
│  └─ Language/              # Localization contracts 与实现
├─ UI/
│  ├─ Navigation/            # Navigator、PageStack、Prefab lease
│  └─ Page/                  # 页面与 UIBinder
├─ Tools/                    # 有真实调用方的运行时扩展
└─ Generated/                # 禁止手改的生成常量
```

`ProjectLifetimeScope` 是默认的唯一组合根。运行时不使用全局 EventBus、Service Locator 或第二套单例系统；需要结果和顺序的操作通过接口直接调用。

## 最小用法

页面导航：

```csharp
var result = await navigator.PushAsync<SettingsPage>(
    AddressableKeys.Assets.SettingsPagePrefab,
    cancellationToken);
```

播放音频：

```csharp
await musicPlayer.PlayAsync(new MusicCueId(AudioClipName.BGM.TestBGM));
await sfxPlayer.PlayAsync(new SfxCueId(AudioClipName.SFX.ClickSound));
```

修改设置：

```csharp
var result = settingsService.SetMusicVolume(80);
var snapshot = settingsService.Current;
```

## Editor 菜单

- `Tools/Template/UI/Validate UIBinders`
- `Tools/Template/Audio/Validate Catalog Freshness`
- `Tools/Template/Code Generation/Generate Addressable Keys`
- `Tools/Template/Code Generation/Validate Addressable Keys`
- `Tools/Template/Code Generation/Validate Generated Code`
- `Tools/Template/Build/Packed Addressables`
- `Tools/Template/Build/Windows Player`
- `Tools/Template/Build/Packed Addressables + Windows Player`

Player Build 前会自动执行 UIBinder、Audio Catalog 与生成文件 freshness 检查。

## 资源约定

- UI Prefab 由 `IUiPrefabProvider` 加载，并由 `IObjectResolver.Instantiate` 实例化以完成 VContainer 注入。
- UIBinder 对象使用 `Button_`、`Text_`、`Image_`、`Slider_`、`Toggle_`、`Input_`、`Panel_`、`Object_` 前缀。
- AudioClip 放入 `Assets/Audio/BGM` 或 `Assets/Audio/SFX`，标记为 Addressable 后再生成 Audio Catalog。
- `AddressableKeys.cs` 与 `AudioClipName.cs` 是生成文件，不应手工编辑。

## Dependencies

- VContainer
- R3
- UniTask
- DOTween
- Unity Addressables
- Unity Localization
- Unity Input System
- URP 2D

第一方运行时代码不依赖 Odin Inspector。当前第一方代码保持在 `Assembly-CSharp`；项目暂不引入模块 asmdef。

## Verification

项目只接受 Unity Editor/Player 构建结果作为编译证据，不使用普通 `dotnet build` 验证 Unity 工程。完整手工流程见 [REFACTORING_PLAN.md](REFACTORING_PLAN.md)。
