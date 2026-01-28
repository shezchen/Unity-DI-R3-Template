namespace Architecture.Data
{
    /// <summary>
    /// 设置变化类型
    /// </summary>
    public enum SettingsChangeType
    {
        /// <summary>
        /// 所有设置（用于初始化时）
        /// </summary>
        AllSettings,
        
        /// <summary>
        /// 背景音乐音量
        /// </summary>
        BgmVolume,
        
        /// <summary>
        /// 音效音量
        /// </summary>
        SfxVolume,
        
        /// <summary>
        /// 屏幕分辨率
        /// </summary>
        Resolution,
        
        /// <summary>
        /// 窗口模式
        /// </summary>
        WindowMode
    }
    
    /// <summary>
    /// 设置变化事件
    /// 通过 EventBus 发布，供 AudioService、ScreenManager 等订阅
    /// </summary>
    /// <param name="ChangeType">变化的设置类型</param>
    /// <param name="Settings">当前设置数据（只读副本）</param>
    public record SettingsChangedEvent(
        SettingsChangeType ChangeType,
        GameSettingsRuntime Settings
    );
}
