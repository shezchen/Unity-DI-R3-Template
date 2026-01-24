using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UI
{
    public abstract class BasePage:MonoBehaviour
    {
        public abstract UniTask Display();
        public abstract UniTask Hide();
    }
}