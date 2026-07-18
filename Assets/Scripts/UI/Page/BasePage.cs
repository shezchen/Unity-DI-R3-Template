using System.Threading;
using Cysharp.Threading.Tasks;

namespace UI
{
    /// <summary>
    /// 页面生命周期接口
    /// </summary>
    public interface IBasePage
    {
        /// <summary>
        /// 页面首次显示时调用（成为栈顶）
        /// 用于初始化界面、播放入场动画
        /// </summary>
        UniTask OnEnter(CancellationToken cancellationToken);

        /// <summary>
        /// 页面被新页面覆盖时调用（从栈顶变为非栈顶）
        /// 用于禁用 GraphicRaycaster、隐藏外观等
        /// </summary>
        UniTask OnPause(CancellationToken cancellationToken);

        /// <summary>
        /// 页面重新成为栈顶时调用（上层页面被 Pop）
        /// 用于刷新过期数据、恢复交互
        /// </summary>
        UniTask OnResume(CancellationToken cancellationToken);

        /// <summary>
        /// 页面即将销毁时调用
        /// 用于播放出场动画、清理资源
        /// </summary>
        UniTask OnExit(CancellationToken cancellationToken);
    }
}
