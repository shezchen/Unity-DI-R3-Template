using Architecture;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace UI
{
    public class LanguageButton : MonoBehaviour,ISelectHandler
    {
        [SerializeField] private GameLanguageType languageType;
        
        [Inject] private EventBus _eventBus;
        
        public void OnSelect(BaseEventData eventData)
        {
            _eventBus.Publish<LanguageChangeEvent>(new LanguageChangeEvent(languageType));
        }
    }
}