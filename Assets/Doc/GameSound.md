# GameSound

GameSound 将业务调用、Addressables 加载、播放状态和 Unity 输出拆成独立边界。调用方不接触 `AudioSource`、`AudioMixer` 或 Addressables handle。

## 公开接口

- `IMusicPlayer`：播放或停止唯一循环 BGM；新命令会取代旧命令。
- `ISfxPlayer`：使用 `PlayOneShot` 播放可重叠 SFX。
- `IAudioLevelsControl`：把 Music/SFX 用户音量写入 AudioMixer。
- `AudioPlayResult`：明确表示 Started、UnknownCue、LoadFailed、Superseded、Cancelled 或 ShuttingDown。

## 内部所有权

- `AudioCatalog` 保存 typed cue ID 到 `AssetReferenceT<AudioClip>` 的只读映射。
- `AddressableAudioClipStore` 合并同一资源的并发加载，并拥有、释放所有 Clip handle。
- `MusicPlayer` 管理 latest-command-wins 转场和 Source transition gain。
- `SfxPlayer` 负责 OneShot 播放。
- `UnityAudioOutput` 是 AudioSource、AudioMixer 与 DOTween 的唯一 Unity 边界。
- `AudioSettingsBinding` 从 `ISettingsService.Current` 应用初始音量，并订阅后续设置变化。

容器释放时，播放器先停止操作，Output 清空播放引用，ClipStore 最后释放 Addressables handle。

## 添加音频

1. 将 AudioClip 放入 `Assets/Audio/BGM` 或 `Assets/Audio/SFX`。
2. 将资源标记为 Addressable。
3. 选择 `AudioCatalog.asset`，点击 `Auto Generate Index`。
4. 运行 `Tools/Template/Audio/Validate Catalog Freshness`。

生成会保留已有 cue 的 Default Gain；空 ID、重复 ID、生成标识符碰撞、非 AudioClip 引用及非 Addressable 资源都会使验证失败。`Assets/Scripts/Generated/AudioClipName.cs` 是生成文件，不要手改。

## 调用示例

```csharp
using Architecture.Audio;
using Generated;

var music = await musicPlayer.PlayAsync(
    new MusicCueId(AudioClipName.BGM.TestBGM),
    MusicTransition.Default,
    cancellationToken);

var sfx = await sfxPlayer.PlayAsync(
    new SfxCueId(AudioClipName.SFX.ClickSound),
    cancellationToken: cancellationToken);
```

页面或流程应根据 `AudioPlayResult` 处理失败；只有无法等待的 Unity 事件边界才使用 `.Forget()`。
