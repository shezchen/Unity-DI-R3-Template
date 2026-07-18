using System;
using Architecture;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    public sealed class LanguageButton : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private GameLanguageType languageType;

        public event Action<GameLanguageType> Selected;

        public void OnSelect(BaseEventData eventData) => Selected?.Invoke(languageType);
    }
}
