using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Desalgoritmizacao.Scene
{
    public class DesalgoritmizacaoSceneRig : MonoBehaviour
    {
        [Header("Canvas in Scenario")]
        [SerializeField] private Transform canvasHost;
        [SerializeField] private Transform menuAnchor;
        [SerializeField] private Transform gameplayAnchor;
        [SerializeField] private Transform rankingAnchor;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector2 canvasSize = new Vector2(1600f, 900f);
        [SerializeField] private float canvasScale = 0.0012f;
        [SerializeField] private Vector3 menuCanvasLocalPosition = new Vector3(0f, 0f, -0.002f);
        [SerializeField] private Vector3 gameplayCanvasLocalPosition = new Vector3(0f, 0f, -0.004f);
        [SerializeField] private Vector3 rankingCanvasLocalPosition = new Vector3(0.42f, 0f, -0.004f);

        [Header("Temporary Camera Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField, Range(5f, 90f)] private float maxYawAngle = 90f;
        [SerializeField, Min(0.01f)] private float lookSensitivity = 0.12f;
        [SerializeField, Min(1f)] private float lookReturnSpeed = 180f;
        [SerializeField, Min(1f)] private float rotationSmoothing = 16f;

        private float yawOffset;
        private Quaternion initialPivotLocalRotation = Quaternion.identity;
        private bool pivotInitialized;

        public Transform MenuAnchor => menuAnchor;
        public Transform GameplayAnchor => gameplayAnchor;
        public Transform RankingAnchor => rankingAnchor;
        public Vector2 CanvasSize => canvasSize;

        public void EnsureRuntimeObjects()
        {
            if (canvasHost == null)
            {
                canvasHost = transform;
            }

            if (menuAnchor == null)
            {
                menuAnchor = EnsureChild("MenuAnchor");
            }

            if (gameplayAnchor == null)
            {
                gameplayAnchor = EnsureChild("GameplayAnchor");
            }

            if (rankingAnchor == null)
            {
                rankingAnchor = EnsureChild("RankingAnchor");
            }

            if (rankingCanvasLocalPosition == Vector3.zero)
            {
                rankingCanvasLocalPosition = new Vector3(0.42f, 0f, -0.004f);
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = Object.FindFirstObjectByType<Camera>();
                }
            }

            if (cameraPivot == null && targetCamera != null)
            {
                cameraPivot = targetCamera.transform.parent != null ? targetCamera.transform.parent : targetCamera.transform;
            }

            if (!pivotInitialized && cameraPivot != null)
            {
                initialPivotLocalRotation = cameraPivot.localRotation;
                pivotInitialized = true;
            }
        }

        public Camera ResolveCamera()
        {
            EnsureRuntimeObjects();
            return targetCamera;
        }

        public Canvas GetOrCreateCanvasInstance(string instanceName, string resourcePath, Transform parent, Color fallbackColor)
        {
            EnsureRuntimeObjects();

            Transform existing = parent != null ? parent.Find(instanceName) : null;
            Canvas canvas = existing != null ? existing.GetComponent<Canvas>() : null;
            if (canvas == null)
            {
                GameObject prefab = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<GameObject>(resourcePath);
                GameObject instance;
                if (prefab != null)
                {
                    instance = Instantiate(prefab, parent != null ? parent : canvasHost);
                    instance.name = instanceName;
                }
                else
                {
                    instance = new GameObject(instanceName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
                    instance.transform.SetParent(parent != null ? parent : canvasHost, false);
                }

                canvas = instance.GetComponent<Canvas>();
            }

            ConfigureWorldCanvas(canvas, parent, fallbackColor);
            return canvas;
        }

        public void ApplyCanvasPlacement(Canvas canvas, bool isMainCanvas, Color fallbackColor)
        {
            EnsureRuntimeObjects();
            ConfigureWorldCanvas(canvas, isMainCanvas ? gameplayAnchor : menuAnchor, fallbackColor);
        }

        private void ConfigureWorldCanvas(Canvas canvas, Transform anchor, Color fallbackColor)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = ResolveCamera();
            canvas.planeDistance = 1f;
            canvas.pixelPerfect = false;

            RectTransform rect = canvas.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.SetParent(anchor != null ? anchor : canvasHost, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = canvasSize;
                rect.localScale = Vector3.one * canvasScale;
                rect.localRotation = Quaternion.identity;
                if (anchor == menuAnchor)
                {
                    rect.localPosition = menuCanvasLocalPosition;
                }
                else if (anchor == gameplayAnchor)
                {
                    rect.localPosition = gameplayCanvasLocalPosition;
                }
                else if (anchor == rankingAnchor)
                {
                    rect.localPosition = rankingCanvasLocalPosition;
                }
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = canvasSize;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            Image background = canvas.GetComponent<Image>();
            if (background == null)
            {
                background = canvas.gameObject.AddComponent<Image>();
            }
            background.color = fallbackColor;
        }

        private void LateUpdate()
        {
            EnsureRuntimeObjects();
            if (cameraPivot == null)
            {
                return;
            }

            if (Object.FindFirstObjectByType<Desalgoritmizacao.World.DesalgoritmizacaoPlayerFlowController>() != null)
            {
                yawOffset = 0f;
                if (pivotInitialized)
                {
                    cameraPivot.localRotation = initialPivotLocalRotation;
                }
                return;
            }

            if (IsLookHeld())
            {
                yawOffset += GetMouseDeltaX() * lookSensitivity;
                yawOffset = Mathf.Clamp(yawOffset, -maxYawAngle, maxYawAngle);
            }
            else
            {
                yawOffset = Mathf.MoveTowards(yawOffset, 0f, lookReturnSpeed * Time.unscaledDeltaTime);
            }

            Quaternion targetRotation = initialPivotLocalRotation * Quaternion.Euler(0f, yawOffset, 0f);
            cameraPivot.localRotation = Quaternion.Slerp(cameraPivot.localRotation, targetRotation, Time.unscaledDeltaTime * rotationSmoothing);
        }

        private Transform EnsureChild(string childName)
        {
            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = new GameObject(childName, typeof(Transform));
            child.transform.SetParent(canvasHost != null ? canvasHost : transform, false);
            return child.transform;
        }

        private bool IsLookHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        private float GetMouseDeltaX()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue().x;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Mouse X") * 15f;
#else
            return 0f;
#endif
        }
    }
}
