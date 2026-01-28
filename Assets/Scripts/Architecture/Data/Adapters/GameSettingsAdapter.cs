using Architecture;

namespace Architecture.Data
{
    /// <summary>
    /// GameSettingsData (Runtime) 和 GameSettingsSave (持久化) 之间的适配器
    /// </summary>
    public static class GameSettingsAdapter
    {
        /// <summary>
        /// 将运行时数据转换为持久化数据
        /// </summary>
        public static GameSettingsSave ToSave(GameSettingsRuntime runtime)
        {
            return new GameSettingsSave
            {
                bgmVolume = runtime.BgmVolume,
                sfxVolume = runtime.SfxVolume,
                gameResolution = runtime.Resolution,
                gameWindow = runtime.WindowMode
            };
        }
        
        /// <summary>
        /// 将持久化数据转换为运行时数据
        /// </summary>
        public static GameSettingsRuntime ToRuntime(GameSettingsSave save)
        {
            return new GameSettingsRuntime
            {
                BgmVolume = save.bgmVolume,
                SfxVolume = save.sfxVolume,
                Resolution = save.gameResolution,
                WindowMode = save.gameWindow
            };
        }
    }
}
