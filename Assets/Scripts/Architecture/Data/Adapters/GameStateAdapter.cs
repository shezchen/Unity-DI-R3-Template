namespace Architecture.Data
{
    /// <summary>
    /// GameStateData (Runtime) 和 GameSaveData (持久化) 之间的适配器
    /// </summary>
    public static class GameStateAdapter
    {
        /// <summary>
        /// 将运行时数据转换为持久化数据
        /// </summary>
        public static GameDataSave ToSave(GameDataRumtime runtime)
        {
            return new GameDataSave
            {
                LastSaveTime = runtime.LastSaveTime
                // TODO: 添加更多字段的转换
            };
        }
        
        /// <summary>
        /// 将持久化数据转换为运行时数据
        /// </summary>
        public static GameDataRumtime ToRuntime(GameDataSave dataSave)
        {
            return new GameDataRumtime
            {
                LastSaveTime = dataSave.LastSaveTime,
                // TODO: 添加更多字段的转换
            };
        }
    }
}
