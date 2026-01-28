using System;

namespace Architecture.Data
{
    /// <summary>
    /// 游戏存档的持久化数据模型
    /// 纯粹的可序列化 POD 数据，与 GameStateData 通过 Adapter 转换
    /// </summary>
    [Serializable]
    public struct GameDataSave
    {
        /// <summary>
        /// 上次保存时间
        /// </summary>
        public DateTime LastSaveTime;
        
        // TODO: 根据游戏需求添加更多需要持久化的数据
        // 例如：
        // public int currentLevel;
        // public PlayerSaveData player;
        // public List<QuestSaveData> quests;
    }
}
