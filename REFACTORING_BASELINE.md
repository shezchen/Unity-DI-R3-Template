# Refactoring Baseline

> Snapshot date: 2026-07-18  
> Unity version: 6000.3.5f2  
> First target platform: Windows Standalone x64

This file records the observable starting point for the phased refactor. It is not evidence that the current baseline has passed its build gates; unchecked items still require verification in Unity.

## Worktree boundary

The following changes existed before refactoring implementation started and must remain outside refactoring commits:

```text
 D .cursor/rules/unity-vcontainer-r3.mdc
 M Assets/Font/TMP Font/NotoSansSC-Regular SDF.asset
 M Packages/manifest.json
 M Packages/packages-lock.json
 M ProjectSettings/ProjectSettings.asset
 M ProjectSettings/ProjectVersion.txt
?? AGENTS.md
?? REFACTORING_PLAN.md
```

New refactoring changes must be reviewed and committed separately from this baseline set.

## Current runtime behavior

### Startup

1. `Assets/Scenes/SampleScene.unity` is the only enabled build scene.
2. `ProjectLifetimeScope` builds the VContainer graph from scene references.
3. `GameFlowController.Start()` awaits Language, Data, and UI initialization in that order.
4. Startup always navigates to `LanguagePage`; first-launch state is not used for routing.

### Language and pages

1. The language selection page is always displayed on startup.
2. A language selection is sent through EventBus and locale switching is not awaited by navigation.
3. Confirming language replaces the page with `MainScenePage`.
4. `SettingsPage` is pushed above the main page and popped to resume it.

### Audio

1. One looping BGM source and one overlapping SFX source are registered from the scene.
2. Clips are loaded on demand from Addressables through `AudioCatalog` and cached until service disposal.
3. BGM switches by fading through silence; SFX uses `PlayOneShot`.
4. BGM/SFX volumes are updated from settings events.
5. `SoundTest` is attached under `TestRoot` in SampleScene and is retained only as an Editor diagnostic during migration.

## Baseline verification checklist

- [x] Unity completes asset import and script compilation with zero errors. (2026-07-18)
- [ ] SampleScene starts and reaches LanguagePage.
- [ ] English, Simplified Chinese, and Japanese selection each switch locale before main-page interaction.
- [ ] MainScenePage opens SettingsPage and returns through Pop/Resume.
- [ ] Test BGM plays, switches, fades, and stops.
- [ ] Test SFX overlaps and respects SFX volume.
- [ ] 第一方 EditMode tests（用户要求现阶段暂缓）。
- [ ] 第一方 PlayMode tests（用户要求现阶段暂缓）。
- [x] Persistence Editor checks pass（29 assertions，2026-07-18）。
- [x] UIBinder Editor validation passes（2026-07-18）。
- [x] Packed Addressables build succeeds. (2026-07-18)
- [x] Windows Standalone x64 Player build succeeds. (2026-07-18)
- [x] Windows Standalone x64 Player launches SampleScene successfully. (2026-07-18)
- [x] Player log contains no project exception or error. (2026-07-18; one Unity/graphics cleanup warning remains)

## Batch-mode entry points

After Unity has imported the Editor scripts, these methods are the stable automation entry points:

```text
Template.Editor.Build.ProjectBuildCommands.BuildPackedAddressables
Template.Editor.Build.ProjectBuildCommands.BuildWindowsPlayer
Template.Editor.Build.ProjectBuildCommands.BuildPackedAddressablesAndWindowsPlayer
```

The corresponding Editor menu is `Tools/Template/Build`.
