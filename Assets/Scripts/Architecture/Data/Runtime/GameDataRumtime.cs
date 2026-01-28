using System;

namespace Architecture.Data
{
    /// <summary>
    /// 游戏状态的运行时数据模型
    /// 与 GameSave 通过 Adapter 转换
    /// 包含游戏进度、玩家数据等运行时状态
    /// </summary>
    public class GameDataRumtime
    {
        /// <summary>
        /// 上次保存时间
        /// </summary>
        public DateTime LastSaveTime { get; set; }
        
        // TODO: 根据游戏需求添加更多运行时数据
        // 例如：
        // public int CurrentLevel { get; set; }
        // public PlayerData Player { get; set; }
        // public List<QuestData> ActiveQuests { get; set; }
        
        /// <summary>
        /// 创建新游戏的默认状态
        /// </summary>
        public static GameDataRumtime CreateNew()
        {
            return new GameDataRumtime
            {
                LastSaveTime = DateTime.MinValue
            };
        }
    }
}
