using Architecture;

namespace Architecture.Data
{
    /// <summary>
    /// 游戏设置的运行时数据模型
    /// 与 GameSettingsSave 通过 Adapter 转换
    /// </summary>
    public class GameSettingsRuntime
    {
        /// <summary>
        /// 背景音乐音量 (0-100)
        /// </summary>
        public int BgmVolume { get; set; } = 100;
        
        /// <summary>
        /// 音效音量 (0-100)
        /// </summary>
        public int SfxVolume { get; set; } = 100;
        
        /// <summary>
        /// 游戏分辨率
        /// </summary>
        public GameResolution Resolution { get; set; } = GameResolution.Res_3840x2160;
        
        /// <summary>
        /// 窗口模式
        /// </summary>
        public GameWindow WindowMode { get; set; } = GameWindow.FullScreenWindow;
        
        /// <summary>
        /// 创建默认设置
        /// </summary>
        public static GameSettingsRuntime CreateDefault()
        {
            return new GameSettingsRuntime();
        }
    }
}
