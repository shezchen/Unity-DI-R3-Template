using System;
using System.IO;
using Architecture.Data;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using Newtonsoft.Json;

namespace Architecture
{
    /// <summary>
    /// 数据中心 - 管理游戏的所有运行时数据和持久化
    /// </summary>
    public class DataManager
    {
        [Inject] private EventBus _eventBus;
        
        #region 文件路径
        
        private const string SettingsSaveName = "GameSettings.json";
        private readonly string _settingsSavePath = Path.Combine(Application.persistentDataPath, SettingsSaveName);
        
        #endregion
        
        #region 状态
        
        /// <summary>
        /// 是否是首次启动游戏
        /// </summary>
        public bool IsFirstLaunch { get; private set; }
        
        /// <summary>
        /// 当前游戏设置（运行时数据）
        /// </summary>
        public GameSettingsRuntime GameSettings { get; private set; }
        
        /// <summary>
        /// 当前游戏状态（运行时数据）
        /// </summary>
        public GameDataRumtime GameData { get; private set; }
        
        #endregion

        #region 初始化
        
        /// <summary>
        /// 初始化数据中心
        /// </summary>
        public async UniTask Init()
        {
            // 检查首次启动
            IsFirstLaunch = !File.Exists(_settingsSavePath);
            
            // 加载或创建 Settings
            if (IsFirstLaunch)
            {
                GameSettings = GameSettingsRuntime.CreateDefault();
                SaveSettingsInternal();
                Debug.Log("[DataManager] 首次启动，已创建默认设置");
            }
            else
            {
                LoadSettingsInternal();
                Debug.Log("[DataManager] 已加载设置: " + _settingsSavePath);
            }
            
            // 发布初始化事件，让其他模块应用设置
            _eventBus.Publish(new SettingsChangedEvent(
                SettingsChangeType.AllSettings,
                GameSettings
            ));
            
            await UniTask.CompletedTask;
        }
        
        #endregion

        #region 设置修改方法 (自动保存)
        
        /// <summary>
        /// 设置背景音乐音量
        /// </summary>
        /// <param name="volume">音量值 (0-100)</param>
        public void SetBgmVolume(int volume)
        {
            volume = Mathf.Clamp(volume, 0, 100);
            if (GameSettings.BgmVolume == volume) return;
            
            GameSettings.BgmVolume = volume;
            PublishAndSaveSettings(SettingsChangeType.BgmVolume);
        }
        
        /// <summary>
        /// 设置音效音量
        /// </summary>
        /// <param name="volume">音量值 (0-100)</param>
        public void SetSfxVolume(int volume)
        {
            volume = Mathf.Clamp(volume, 0, 100);
            if (GameSettings.SfxVolume == volume) return;
            
            GameSettings.SfxVolume = volume;
            PublishAndSaveSettings(SettingsChangeType.SfxVolume);
        }
        
        /// <summary>
        /// 设置游戏分辨率
        /// </summary>
        public void SetResolution(GameResolution resolution)
        {
            if (GameSettings.Resolution == resolution) return;
            
            GameSettings.Resolution = resolution;
            PublishAndSaveSettings(SettingsChangeType.Resolution);
        }
        
        /// <summary>
        /// 设置窗口模式
        /// </summary>
        public void SetWindowMode(GameWindow windowMode)
        {
            if (GameSettings.WindowMode == windowMode) return;
            
            GameSettings.WindowMode = windowMode;
            PublishAndSaveSettings(SettingsChangeType.WindowMode);
        }
        
        /// <summary>
        /// 发布设置变化事件并自动保存
        /// </summary>
        private void PublishAndSaveSettings(SettingsChangeType changeType)
        {
            _eventBus.Publish(new SettingsChangedEvent(changeType, GameSettings));
            SaveSettingsInternal();
        }
        
        #endregion

        #region 游戏内Public数据修改方法

        // 在此处实现Runtime数据修改方法        

        #endregion

        #region 存档方法
        
        /// <summary>
        /// 在slotIndex存档位开始新游戏
        /// </summary>
        public void NewGame(int slotIndex)
        {
            GameData = GameDataRumtime.CreateNew();
            _eventBus.Publish(new GameStateChangedEvent(
                GameStateChangeType.NewGame,
                GameData
            ));
            SaveGame(GetDefaultSavePath(slotIndex));
        }
        
        /// <summary>
        /// 获取默认存档路径
        /// </summary>
        public string GetDefaultSavePath(int saveSlot)
        {
            return Path.Combine(Application.persistentDataPath, $"SaveSlot{saveSlot}.json");
        }
        
        /// <summary>
        /// 保存游戏（手动调用）
        /// </summary>
        public void SaveGame(string savePath)
        {
            try
            {
                GameData.LastSaveTime = DateTime.Now;
                var saveData = GameStateAdapter.ToSave(GameData);
                
                string json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
                File.WriteAllText(savePath, json);
                
                _eventBus.Publish(new GameStateChangedEvent(
                    GameStateChangeType.Saved,
                    GameData
                ));
                
                Debug.Log("[DataManager] 游戏已保存: " + savePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataManager] 保存游戏失败: {e.Message}");
            }
        }
        
        /// <summary>
        /// 加载游戏
        /// </summary>
        public bool LoadGame(string savePath)
        {
            if (!File.Exists(savePath))
            {
                Debug.LogWarning("[DataManager] 存档文件不存在: " + savePath);
                return false;
            }

            try
            {
                string json = File.ReadAllText(savePath);
                var saveData = JsonConvert.DeserializeObject<GameDataSave>(json);
                GameData = GameStateAdapter.ToRuntime(saveData);
                
                _eventBus.Publish(new GameStateChangedEvent(
                    GameStateChangeType.Loaded,
                    GameData
                ));
                
                Debug.Log("[DataManager] 游戏已加载: " + savePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataManager] 加载游戏失败: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 检查存档是否存在
        /// </summary>
        public bool SaveExists(string savePath)
        {
            return File.Exists(savePath);
        }
        
        /// <summary>
        /// 检查存档是否存在
        /// </summary>
        public bool SaveExists(int saveSlot)
        {
            return File.Exists(GetDefaultSavePath(saveSlot));
        }
        
        #endregion

        #region 内部方法
        
        private void LoadSettingsInternal()
        {
            try
            {
                string json = File.ReadAllText(_settingsSavePath);
                var saveData = JsonConvert.DeserializeObject<GameSettingsSave>(json);
                GameSettings = GameSettingsAdapter.ToRuntime(saveData);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DataManager] 加载设置失败，使用默认设置: {e.Message}");
                GameSettings = GameSettingsRuntime.CreateDefault();
                SaveSettingsInternal();
            }
        }
        
        private void SaveSettingsInternal()
        {
            try
            {
                var saveData = GameSettingsAdapter.ToSave(GameSettings);
                string json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
                File.WriteAllText(_settingsSavePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataManager] 保存设置失败: {e.Message}");
            }
        }
        
        #endregion
    }
}
