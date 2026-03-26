using UnityEngine;
using UnityEngine.EventSystems;
using Desalgoritmizacao.UI;
using Desalgoritmizacao.World;
using Desalgoritmizacao.Scene;
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
            if (Object.FindFirstObjectByType<DesalgoritmizacaoApp>() == null)
            {
                GameObject root = new GameObject("DesalgoritmizacaoApp");
                Object.DontDestroyOnLoad(root);
                root.AddComponent<DesalgoritmizacaoApp>();
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
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

            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                sceneCamera = Object.FindFirstObjectByType<Camera>();
            }

            if (Object.FindFirstObjectByType<DesalgoritmizacaoSceneRig>() == null &&
                sceneCamera != null &&
                sceneCamera.GetComponent<DesalgoritmizacaoTemporaryCameraLook>() == null)
            {
                sceneCamera.gameObject.AddComponent<DesalgoritmizacaoTemporaryCameraLook>();
            }
        }
    }
}
