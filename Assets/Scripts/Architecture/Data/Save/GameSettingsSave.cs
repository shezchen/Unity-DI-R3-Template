using System;
using Architecture;

namespace Architecture.Data
{
    /// <summary>
    /// 游戏设置的持久化数据模型
    /// 纯粹的可序列化 POD 数据，与 GameSettingsData 通过 Adapter 转换
    /// </summary>
    [Serializable]
    public class GameSettingsSave
    {
        /// <summary>
        /// 背景音乐音量 (0-100)
        /// </summary>
        public int bgmVolume = 100;
        
        /// <summary>
        /// 音效音量 (0-100)
        /// </summary>
        public int sfxVolume = 100;
        
        /// <summary>
        /// 游戏分辨率
        /// </summary>
        public GameResolution gameResolution = GameResolution.Res_3840x2160;
        
        /// <summary>
        /// 窗口模式
        /// </summary>
        public GameWindow gameWindow = GameWindow.FullScreenWindow;
    }
}
