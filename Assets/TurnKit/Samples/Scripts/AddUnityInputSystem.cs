using UnityEngine;
using UnityEngine.EventSystems;

namespace TurnKit.Example
{
    public class AddUnityInputSystem : MonoBehaviour
    {
        private void Awake()
        { // enables demo to work on different Unity Input systems and versions
#if ENABLE_INPUT_SYSTEM
            gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            gameObject.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}