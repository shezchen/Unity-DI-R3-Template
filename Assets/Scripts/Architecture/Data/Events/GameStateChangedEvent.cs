namespace Architecture.Data
{
    /// <summary>
    /// 游戏状态变化类型
    /// </summary>
    public enum GameStateChangeType
    {
        /// <summary>
        /// 新游戏开始
        /// </summary>
        NewGame,
        
        /// <summary>
        /// 游戏已加载
        /// </summary>
        Loaded,
        
        /// <summary>
        /// 游戏已保存
        /// </summary>
        Saved
    }
    
    /// <summary>
    /// 游戏状态变化事件
    /// 通过 EventBus 发布
    /// </summary>
    /// <param name="ChangeType">变化类型</param>
    /// <param name="Game">当前游戏状态</param>
    public record GameStateChangedEvent(
        GameStateChangeType ChangeType,
        GameDataRumtime Game
    );
}
