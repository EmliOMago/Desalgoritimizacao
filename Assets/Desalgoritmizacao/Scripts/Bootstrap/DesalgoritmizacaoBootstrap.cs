using UnityEngine;
using UnityEngine.EventSystems;
using Desalgoritmizacao.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Desalgoritmizacao.Bootstrap
{
    public static class DesalgoritmizacaoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (Object.FindObjectOfType<DesalgoritmizacaoApp>() != null)
            {
                return;
            }

            GameObject root = new GameObject("DesalgoritmizacaoApp");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<DesalgoritmizacaoApp>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystemGo = new GameObject("EventSystem");
                Object.DontDestroyOnLoad(eventSystemGo);
                eventSystemGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
#else
                eventSystemGo.AddComponent<StandaloneInputModule>();
#endif
            }
        }
    }
}
