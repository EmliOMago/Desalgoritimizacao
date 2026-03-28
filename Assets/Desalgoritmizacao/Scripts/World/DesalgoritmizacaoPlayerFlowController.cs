using System;
using System.Collections.Generic;
using System.IO;
using Desalgoritmizacao.Data;
using Desalgoritmizacao.Runtime;
using Desalgoritmizacao.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Desalgoritmizacao.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class DesalgoritmizacaoPlayerFlowController : MonoBehaviour
    {
        private enum StationType
        {
            None,
            Pc,
            Impressora
        }

        private sealed class PrinterRunState
        {
            public readonly List<PrinterDirection> sequence = new List<PrinterDirection>();
            public int currentIndex;
            public float inputWindowSeconds;
            public float stepStartedAt;
            public bool completed;
            public bool failed;
        }

        private Rigidbody playerBody;
        private Camera targetCamera;
        private DesalgoritmizacaoApp app;
        private DesalgoritmizacaoInteractionConfig config;
        private DesalgoritmizacaoPrinterQuickTimeConfig printerConfig;

        private InputActionAsset runtimeInputActions;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction stationLookModifierAction;
        private InputAction printerUpAction;
        private InputAction printerDownAction;
        private InputAction printerLeftAction;
        private InputAction printerRightAction;

        private Transform localCamPc;
        private Transform localCamImpre;
        private Transform localCamInicio;
        private Collider triggerPc;
        private Collider triggerImpressora;
        private Collider triggerPorta;
        private Collider triggerAtendimento;

        private Canvas overlayCanvas;
        private Text topBannerText;
        private Image modalPanel;
        private Text modalTitleText;
        private Text modalBodyText;
        private Button modalConfirmButton;
        private Button modalCancelButton;
        private Image printerPanel;
        private Text printerTitleText;
        private Text printerSubtitleText;
        private Text printerProgressText;
        private Text printerFeedbackText;
        private RawImage printerCurrentIcon;
        private readonly List<RawImage> printerPreviewIcons = new List<RawImage>();

        private StationType currentStation = StationType.None;
        private Collider currentStationTrigger;
        private Transform currentStationAnchor;
        private bool stationAllowsUiInteraction;
        private bool stationAllowsTemporaryLook;
        private bool printerAwaitingFreshEntry;
        private bool isExitPromptVisible;
        private bool isPrinterPending;
        private bool isPrinterRunning;
        private bool isAtendimentoRequested;
        private bool gameplayStarted;
        private float nextAtendimentoRequestAt;
        private float temporaryBannerUntil;
        private string temporaryBannerMessage = string.Empty;
        private int completedPrinterRuns;
        private float printerRestartAt;

        private float bodyYaw;
        private float cameraPitch;
        private Vector3 desiredMovement;
        private PrinterRunState activePrinterRun;

        public string CurrentStatusMessage
        {
            get
            {
                if (isAtendimentoRequested)
                {
                    return "Atendimento solicitado";
                }

                if (isPrinterPending)
                {
                    return "Imprimindo registro";
                }

                return string.Empty;
            }
        }

        private bool CanProgressOperatorWorkflow
        {
            get
            {
                return !isAtendimentoRequested && !isPrinterPending && !isPrinterRunning && !isExitPromptVisible;
            }
        }

        private string OperatorBlockReason
        {
            get
            {
                if (isAtendimentoRequested)
                {
                    return "Atendimento solicitado";
                }

                if (isPrinterPending || isPrinterRunning)
                {
                    return "Imprimindo registro";
                }

                if (isExitPromptVisible)
                {
                    return "Saída aguardando confirmação";
                }

                return string.Empty;
            }
        }

        private void Awake()
        {
            playerBody = GetComponent<Rigidbody>();
            targetCamera = GetComponentInChildren<Camera>(true);
            app = UnityEngine.Object.FindFirstObjectByType<DesalgoritmizacaoApp>();
            config = Resources.Load<DesalgoritmizacaoInteractionConfig>("Desalgoritmizacao/Config/InteractionConfig");
            printerConfig = config != null ? config.printerQuickTimeConfig : null;

            ConfigurePhysics();
            CacheSceneReferences();
            CreateOverlayCanvas();
            BindBridge();
            DisableLegacyCameraLook();
            LoadInputActions();
            SnapToInitialCamera();
            ScheduleNextAtendimento();
        }

        private void OnDestroy()
        {
            DesalgoritmizacaoGameplayBridge.ExternalStatusProvider = null;
            DesalgoritmizacaoGameplayBridge.CanOpenOperatorWorkflowProvider = null;
            DesalgoritmizacaoGameplayBridge.OperatorWorkflowBlockReasonProvider = null;
            DesalgoritmizacaoGameplayBridge.RegisterAndReturnToCentralRequested = null;

            runtimeInputActions?.Disable();
            if (overlayCanvas != null)
            {
                Destroy(overlayCanvas.gameObject);
            }
        }

        private void Update()
        {
            app = app != null ? app : UnityEngine.Object.FindFirstObjectByType<DesalgoritmizacaoApp>();
            UpdateGameplayStartedFlag();
            UpdateCursorAndUiState();
            UpdateTopBanner();
            UpdateExitPrompt();
            UpdateAtendimentoScheduling();
            UpdatePendingPrinterEntryState();

            if (isExitPromptVisible)
            {
                desiredMovement = Vector3.zero;
                return;
            }

            if (currentStation == StationType.Pc)
            {
                UpdatePcDock();
                desiredMovement = Vector3.zero;
                return;
            }

            if (currentStation == StationType.Impressora)
            {
                UpdatePrinterDock();
                desiredMovement = Vector3.zero;
                return;
            }

            if (!CanDriveFreeRoam())
            {
                desiredMovement = Vector3.zero;
                return;
            }

            UpdateFreeLook();
            UpdateFreeMovement();
        }

        private void FixedUpdate()
        {
            if (desiredMovement.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 targetPosition = playerBody.position + desiredMovement * Time.fixedDeltaTime;
            playerBody.MovePosition(targetPosition);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
            {
                return;
            }

            if (other == triggerPc)
            {
                if (!isAtendimentoRequested && !isExitPromptVisible && currentStation == StationType.None)
                {
                    DockAtStation(StationType.Pc, triggerPc, localCamPc, true, true);
                }
                return;
            }

            if (other == triggerImpressora)
            {
                if (isPrinterPending && !printerAwaitingFreshEntry && !isAtendimentoRequested && !isExitPromptVisible && currentStation == StationType.None)
                {
                    DockAtStation(StationType.Impressora, triggerImpressora, localCamImpre, false, false);
                    BeginPrinterRun();
                }
                return;
            }

            if (other == triggerPorta)
            {
                if (!isExitPromptVisible)
                {
                    ShowExitPrompt();
                }
                return;
            }

            if (other == triggerAtendimento && isAtendimentoRequested)
            {
                CompleteAtendimento();
            }
        }

        private void ConfigurePhysics()
        {
            playerBody.interpolation = RigidbodyInterpolation.Interpolate;
            playerBody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void CacheSceneReferences()
        {
            localCamPc = FindTransformByName("LocalCamPC");
            localCamImpre = FindTransformByName("LocalCamImpre");
            localCamInicio = FindTransformByName("LocalCamInicio") ?? FindTransformByName("LocalCaminicio");
            triggerPc = FindColliderByName("TriggerPC");
            triggerImpressora = FindColliderByName("TriggerImpressora");
            triggerPorta = FindColliderByName("TriggerPorta");
            triggerAtendimento = FindColliderByName("TriggerAtendimento");

            if (triggerPorta is BoxCollider portaBox)
            {
                portaBox.isTrigger = true;
            }

            if (triggerImpressora != null)
            {
                triggerImpressora.enabled = false;
            }
        }

        private void DisableLegacyCameraLook()
        {
            DesalgoritmizacaoTemporaryCameraLook temporaryLook = targetCamera != null ? targetCamera.GetComponent<DesalgoritmizacaoTemporaryCameraLook>() : null;
            if (temporaryLook != null)
            {
                temporaryLook.enabled = false;
            }
        }

        private void BindBridge()
        {
            DesalgoritmizacaoGameplayBridge.ExternalStatusProvider = () => CurrentStatusMessage;
            DesalgoritmizacaoGameplayBridge.CanOpenOperatorWorkflowProvider = () => CanProgressOperatorWorkflow;
            DesalgoritmizacaoGameplayBridge.OperatorWorkflowBlockReasonProvider = () => OperatorBlockReason;
            DesalgoritmizacaoGameplayBridge.RegisterAndReturnToCentralRequested = HandleRegisterAndReturnToCentral;
        }

        private void LoadInputActions()
        {
            string json = TryLoadInputJsonFromProject();
            if (string.IsNullOrWhiteSpace(json) && config != null && !string.IsNullOrWhiteSpace(config.runtimeInputJsonResourcePath))
            {
                TextAsset runtimeJson = Resources.Load<TextAsset>(config.runtimeInputJsonResourcePath);
                json = runtimeJson != null ? runtimeJson.text : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError("DesalgoritmizacaoPlayerFlowController não encontrou o JSON do InputSystem_Actions.");
                enabled = false;
                return;
            }

            runtimeInputActions = InputActionAsset.FromJson(json);
            runtimeInputActions.Enable();

            string mapName = config != null && !string.IsNullOrWhiteSpace(config.playerActionMap) ? config.playerActionMap : "Player";
            InputActionMap playerMap = runtimeInputActions.FindActionMap(mapName, true);
            moveAction = playerMap.FindAction(config != null ? config.moveActionName : "Move", true);
            lookAction = playerMap.FindAction(config != null ? config.lookActionName : "Look", true);
            stationLookModifierAction = playerMap.FindAction(config != null ? config.stationLookModifierActionName : "StationLook", true);
            printerUpAction = playerMap.FindAction(config != null ? config.printerUpActionName : "PrinterUp", true);
            printerDownAction = playerMap.FindAction(config != null ? config.printerDownActionName : "PrinterDown", true);
            printerLeftAction = playerMap.FindAction(config != null ? config.printerLeftActionName : "PrinterLeft", true);
            printerRightAction = playerMap.FindAction(config != null ? config.printerRightActionName : "PrinterRight", true);
        }

        private string TryLoadInputJsonFromProject()
        {
            string relativePath = config != null && !string.IsNullOrWhiteSpace(config.editorSourceRelativePath)
                ? config.editorSourceRelativePath
                : "InputSystem_Actions.inputactions";
            string absolutePath = Path.Combine(Application.dataPath, relativePath);
            return File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
        }

        private void SnapToInitialCamera()
        {
            Transform startAnchor = localCamInicio != null ? localCamInicio : localCamPc;
            if (startAnchor == null || targetCamera == null)
            {
                return;
            }

            MatchCameraToAnchor(startAnchor);
            bodyYaw = transform.eulerAngles.y;
            cameraPitch = NormalizeAngle(targetCamera.transform.localEulerAngles.x);
            desiredMovement = Vector3.zero;
        }

        private void UpdateGameplayStartedFlag()
        {
            bool wasStarted = gameplayStarted;
            gameplayStarted = app != null && !app.IsInInitialMenu;
            if (!wasStarted && gameplayStarted)
            {
                SnapToInitialCamera();
            }
        }

        private bool CanDriveFreeRoam()
        {
            return gameplayStarted && currentStation == StationType.None && !isPrinterRunning && !isExitPromptVisible;
        }

        private void UpdateFreeMovement()
        {
            Vector2 moveValue = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 worldMove = (right * moveValue.x + forward * moveValue.y);
            if (worldMove.sqrMagnitude > 1f)
            {
                worldMove.Normalize();
            }

            float speed = config != null ? config.moveSpeed : 2.9f;
            desiredMovement = worldMove * speed;
        }

        private void UpdateFreeLook()
        {
            Vector2 lookValue = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
            if (lookValue.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float sensitivity = config != null ? config.lookSensitivity : 0.1f;
            bodyYaw += lookValue.x * sensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch - (lookValue.y * sensitivity), config != null ? config.minimumPitch : -70f, config != null ? config.maximumPitch : 72f);
            Quaternion targetRotation = Quaternion.Euler(0f, bodyYaw, 0f);
            playerBody.MoveRotation(targetRotation);
            transform.rotation = targetRotation;
            targetCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }

        private void UpdatePcDock()
        {
            bool isUsingTemporaryLook = stationAllowsTemporaryLook && stationLookModifierAction != null && stationLookModifierAction.IsPressed();
            if (isUsingTemporaryLook)
            {
                ApplyDockLook(config != null ? config.stationLookSensitivity : 0.08f);
            }
            else
            {
                MaintainDockAlignment();
            }

            Vector2 moveValue = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (Mathf.Abs(moveValue.x) > 0.2f || moveValue.y < -0.2f)
            {
                ReleaseCurrentStation();
            }
        }

        private void UpdatePrinterDock()
        {
            MaintainDockAlignment();

            if (isPrinterRunning)
            {
                UpdatePrinterQuickTime();
                return;
            }

            if (isPrinterPending && printerAwaitingFreshEntry && !IsPlayerInsideTrigger(triggerImpressora))
            {
                printerAwaitingFreshEntry = false;
            }

            if (isPrinterPending && activePrinterRun == null && printerRestartAt > Time.unscaledTime)
            {
                RefreshPrinterUi();
                return;
            }

            if (isPrinterPending && activePrinterRun == null && printerRestartAt > 0f && Time.unscaledTime >= printerRestartAt)
            {
                printerRestartAt = 0f;
                BeginPrinterRun();
                return;
            }

            Vector2 moveValue = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (Mathf.Abs(moveValue.x) > 0.2f || moveValue.y < -0.2f)
            {
                ReleaseCurrentStation();
            }
        }

        private void ApplyDockLook(float sensitivity)
        {
            Vector2 lookValue = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
            if (lookValue.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            bodyYaw += lookValue.x * sensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch - (lookValue.y * sensitivity), config != null ? config.minimumPitch : -70f, config != null ? config.maximumPitch : 72f);
            Quaternion targetRotation = Quaternion.Euler(0f, bodyYaw, 0f);
            playerBody.MoveRotation(targetRotation);
            transform.rotation = targetRotation;
            targetCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }

        private void DockAtStation(StationType stationType, Collider stationTrigger, Transform stationAnchor, bool allowUiInteraction, bool allowTemporaryLook)
        {
            if (stationAnchor == null || targetCamera == null)
            {
                return;
            }

            currentStation = stationType;
            currentStationTrigger = stationTrigger;
            currentStationAnchor = stationAnchor;
            stationAllowsUiInteraction = allowUiInteraction;
            stationAllowsTemporaryLook = allowTemporaryLook;
            desiredMovement = Vector3.zero;
            MatchCameraToAnchor(stationAnchor);
            bodyYaw = transform.eulerAngles.y;
            cameraPitch = NormalizeAngle(targetCamera.transform.localEulerAngles.x);
        }

        private void ReleaseCurrentStation()
        {
            if (currentStation == StationType.None)
            {
                return;
            }

            if (isPrinterRunning)
            {
                return;
            }

            PushPlayerOutOfTrigger(currentStationTrigger);
            currentStation = StationType.None;
            currentStationTrigger = null;
            currentStationAnchor = null;
            stationAllowsUiInteraction = false;
            stationAllowsTemporaryLook = false;
            bodyYaw = transform.eulerAngles.y;
            cameraPitch = NormalizeAngle(targetCamera.transform.localEulerAngles.x);
            RefreshPrinterUi();
        }

        private void MatchCameraToAnchor(Transform anchor)
        {
            if (anchor == null || targetCamera == null)
            {
                return;
            }

            Quaternion playerRotation = anchor.rotation * Quaternion.Inverse(targetCamera.transform.localRotation);
            Vector3 playerPosition = anchor.position - (playerRotation * targetCamera.transform.localPosition);
            playerBody.position = playerPosition;
            playerBody.rotation = playerRotation;
            transform.SetPositionAndRotation(playerPosition, playerRotation);
            Physics.SyncTransforms();
        }

        private void MaintainDockAlignment()
        {
            if (currentStationAnchor == null)
            {
                return;
            }

            MatchCameraToAnchor(currentStationAnchor);
            bodyYaw = transform.eulerAngles.y;
            cameraPitch = NormalizeAngle(targetCamera.transform.localEulerAngles.x);
        }

        private void PushPlayerOutOfTrigger(Collider trigger)
        {
            if (trigger == null)
            {
                return;
            }

            Vector3 escapeDirection = targetCamera.transform.position - trigger.bounds.center;
            escapeDirection.y = 0f;
            if (escapeDirection.sqrMagnitude < 0.0001f)
            {
                escapeDirection = -trigger.transform.forward;
                escapeDirection.y = 0f;
            }

            escapeDirection.Normalize();
            float releaseDistance = Mathf.Max(trigger.bounds.extents.x, trigger.bounds.extents.z) + (config != null ? config.stationReleaseDistance : 1.35f);
            Vector3 desiredCameraPosition = trigger.bounds.center + (escapeDirection * releaseDistance);
            desiredCameraPosition.y = targetCamera.transform.position.y;

            Quaternion playerRotation = transform.rotation;
            Vector3 desiredPlayerPosition = desiredCameraPosition - (playerRotation * targetCamera.transform.localPosition);
            playerBody.position = desiredPlayerPosition;
        }

        private bool IsPlayerInsideTrigger(Collider trigger)
        {
            if (trigger == null || targetCamera == null)
            {
                return false;
            }

            Vector3 closestPoint = trigger.ClosestPoint(targetCamera.transform.position);
            return (closestPoint - targetCamera.transform.position).sqrMagnitude <= 0.0001f;
        }

        private void HandleRegisterAndReturnToCentral()
        {
            if (isAtendimentoRequested || isExitPromptVisible)
            {
                return;
            }

            isPrinterPending = true;
            printerAwaitingFreshEntry = true;
            printerRestartAt = 0f;
            activePrinterRun = null;
            isPrinterRunning = false;
            if (triggerImpressora != null)
            {
                triggerImpressora.enabled = true;
            }

            if (currentStation == StationType.Pc)
            {
                ReleaseCurrentStation();
            }

            if (!IsPlayerInsideTrigger(triggerImpressora))
            {
                printerAwaitingFreshEntry = false;
            }
        }

        private void BeginPrinterRun()
        {
            if (printerConfig == null)
            {
                CompletePrinterRun();
                return;
            }

            isPrinterRunning = true;
            activePrinterRun = new PrinterRunState();

            int minLength = Mathf.Max(2, printerConfig.minimumSequenceLength);
            int maxLength = Mathf.Max(minLength, printerConfig.maximumSequenceLength);
            int bonusSteps = Mathf.Min(2, completedPrinterRuns / 2);
            int sequenceLength = UnityEngine.Random.Range(minLength + bonusSteps, maxLength + 1);
            float difficulty = Mathf.Clamp01(0.18f + (completedPrinterRuns * 0.11f) + UnityEngine.Random.Range(0f, 0.35f));
            activePrinterRun.inputWindowSeconds = Mathf.Lerp(printerConfig.maximumInputWindowSeconds, printerConfig.minimumInputWindowSeconds, difficulty);

            for (int i = 0; i < sequenceLength; i++)
            {
                activePrinterRun.sequence.Add((PrinterDirection)UnityEngine.Random.Range(0, 4));
            }

            activePrinterRun.currentIndex = 0;
            activePrinterRun.stepStartedAt = Time.unscaledTime;
            activePrinterRun.failed = false;
            activePrinterRun.completed = false;
            printerRestartAt = 0f;
            RefreshPrinterUi();
        }

        private void UpdatePrinterQuickTime()
        {
            if (activePrinterRun == null)
            {
                return;
            }

            if (Time.unscaledTime - activePrinterRun.stepStartedAt > activePrinterRun.inputWindowSeconds)
            {
                FailPrinterRun();
                return;
            }

            PrinterDirection? pressedDirection = ReadPressedPrinterDirection();
            if (!pressedDirection.HasValue)
            {
                RefreshPrinterUi();
                return;
            }

            PrinterDirection expectedDirection = activePrinterRun.sequence[activePrinterRun.currentIndex];
            if (pressedDirection.Value != expectedDirection)
            {
                FailPrinterRun();
                return;
            }

            activePrinterRun.currentIndex++;
            activePrinterRun.stepStartedAt = Time.unscaledTime;
            if (activePrinterRun.currentIndex >= activePrinterRun.sequence.Count)
            {
                CompletePrinterRun();
                return;
            }

            RefreshPrinterUi();
        }

        private PrinterDirection? ReadPressedPrinterDirection()
        {
            if (printerUpAction != null && printerUpAction.WasPressedThisFrame())
            {
                return PrinterDirection.Up;
            }

            if (printerDownAction != null && printerDownAction.WasPressedThisFrame())
            {
                return PrinterDirection.Down;
            }

            if (printerLeftAction != null && printerLeftAction.WasPressedThisFrame())
            {
                return PrinterDirection.Left;
            }

            if (printerRightAction != null && printerRightAction.WasPressedThisFrame())
            {
                return PrinterDirection.Right;
            }

            return null;
        }

        private void CompletePrinterRun()
        {
            isPrinterRunning = false;
            isPrinterPending = false;
            printerAwaitingFreshEntry = false;
            completedPrinterRuns++;
            activePrinterRun = null;
            printerRestartAt = 0f;
            if (triggerImpressora != null)
            {
                triggerImpressora.enabled = false;
            }

            ShowTemporaryBanner(printerConfig != null ? printerConfig.successMessage : "Registro impresso. Retorne ao terminal.");
            if (currentStation == StationType.Impressora)
            {
                ReleaseCurrentStation();
            }
            else
            {
                RefreshPrinterUi();
            }
        }

        private void FailPrinterRun()
        {
            if (printerConfig != null)
            {
                ShowTemporaryBanner(printerConfig.failureMessage);
            }

            isPrinterRunning = false;
            activePrinterRun = null;
            printerRestartAt = Time.unscaledTime + (printerConfig != null ? printerConfig.restartPauseSeconds : 0.65f);
            RefreshPrinterUi();
        }

        private void RefreshPrinterUi()
        {
            bool showPrinterUi = currentStation == StationType.Impressora;
            if (printerPanel != null)
            {
                printerPanel.gameObject.SetActive(showPrinterUi);
            }

            if (!showPrinterUi || printerConfig == null)
            {
                return;
            }

            printerTitleText.text = printerConfig.title;
            printerSubtitleText.text = printerConfig.subtitle;

            if (activePrinterRun == null)
            {
                printerProgressText.text = printerRestartAt > Time.unscaledTime ? "Reiniciando sequência..." : (isPrinterPending ? "Aproxime-se da impressora para iniciar." : string.Empty);
                printerFeedbackText.text = string.Empty;
                printerCurrentIcon.texture = null;
                for (int i = 0; i < printerPreviewIcons.Count; i++)
                {
                    printerPreviewIcons[i].texture = null;
                    printerPreviewIcons[i].color = new Color(1f, 1f, 1f, 0.12f);
                }
                return;
            }

            printerProgressText.text = "Etapa " + (activePrinterRun.currentIndex + 1) + " de " + activePrinterRun.sequence.Count +
                                       " · janela " + Mathf.Max(0f, activePrinterRun.inputWindowSeconds - (Time.unscaledTime - activePrinterRun.stepStartedAt)).ToString("0.00") + "s";
            printerFeedbackText.text = "Use apenas as setas do teclado.";
            printerCurrentIcon.texture = printerConfig.ResolveTexture(activePrinterRun.sequence[activePrinterRun.currentIndex]);
            printerCurrentIcon.color = Color.white;

            for (int i = 0; i < printerPreviewIcons.Count; i++)
            {
                int sequenceIndex = activePrinterRun.currentIndex + i;
                if (sequenceIndex < activePrinterRun.sequence.Count)
                {
                    printerPreviewIcons[i].texture = printerConfig.ResolveTexture(activePrinterRun.sequence[sequenceIndex]);
                    printerPreviewIcons[i].color = i == 0 ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                }
                else
                {
                    printerPreviewIcons[i].texture = null;
                    printerPreviewIcons[i].color = new Color(1f, 1f, 1f, 0.12f);
                }
            }
        }

        private void UpdateCursorAndUiState()
        {
            bool allowMenuCursor = gameplayStarted && currentStation == StationType.Pc && stationAllowsUiInteraction && !isAtendimentoRequested && !isExitPromptVisible;
            bool forceVisibleCursor = isExitPromptVisible || (!gameplayStarted && app != null && app.IsInInitialMenu) || allowMenuCursor;

            Cursor.visible = forceVisibleCursor;
            Cursor.lockState = forceVisibleCursor ? CursorLockMode.None : CursorLockMode.Locked;

            Canvas mainCanvas = app != null ? app.MainCanvas : null;
            if (mainCanvas != null)
            {
                GraphicRaycaster appRaycaster = mainCanvas.GetComponent<GraphicRaycaster>();
                if (appRaycaster != null)
                {
                    appRaycaster.enabled = allowMenuCursor;
                }
            }
        }

        private void CreateOverlayCanvas()
        {
            GameObject canvasGo = new GameObject("DesalgoritmizacaoWorldOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlayCanvas = canvasGo.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 2600;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            DontDestroyOnLoad(canvasGo);

            RectTransform root = canvasGo.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject bannerGo = CreateUiObject("TopBanner", root);
            RectTransform bannerRect = bannerGo.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.sizeDelta = new Vector2(640f, 72f);
            bannerRect.anchoredPosition = new Vector2(0f, -18f);
            Image bannerImage = bannerGo.AddComponent<Image>();
            bannerImage.color = new Color(0.08f, 0.11f, 0.17f, 0.92f);
            topBannerText = CreateText("Label", bannerRect, font, 28, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(topBannerText.rectTransform, new Vector2(18f, 10f), new Vector2(-18f, -10f));
            bannerGo.SetActive(false);

            GameObject modalGo = CreateUiObject("ExitModal", root);
            RectTransform modalRect = modalGo.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(540f, 280f);
            modalPanel = modalGo.AddComponent<Image>();
            modalPanel.color = new Color(0.06f, 0.08f, 0.12f, 0.97f);
            modalTitleText = CreateText("Title", modalRect, font, 30, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            Stretch(modalTitleText.rectTransform, new Vector2(24f, -28f), new Vector2(-24f, -168f));
            modalBodyText = CreateText("Body", modalRect, font, 20, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.86f, 0.9f, 0.97f, 1f));
            Stretch(modalBodyText.rectTransform, new Vector2(24f, -92f), new Vector2(-24f, -92f));
            modalConfirmButton = CreateButton(modalRect, font, "Sair", new Vector2(24f, 24f), new Vector2(236f, 72f), new Color(0.84f, 0.26f, 0.26f, 1f), Color.white);
            modalCancelButton = CreateButton(modalRect, font, "Continuar", new Vector2(280f, 24f), new Vector2(236f, 72f), new Color(0.22f, 0.52f, 0.78f, 1f), Color.white);
            modalConfirmButton.onClick.AddListener(ConfirmExit);
            modalCancelButton.onClick.AddListener(CancelExit);
            modalGo.SetActive(false);

            GameObject printerGo = CreateUiObject("PrinterQuickTime", root);
            RectTransform printerRect = printerGo.GetComponent<RectTransform>();
            printerRect.anchorMin = new Vector2(0.5f, 0.5f);
            printerRect.anchorMax = new Vector2(0.5f, 0.5f);
            printerRect.pivot = new Vector2(0.5f, 0.5f);
            printerRect.sizeDelta = new Vector2(760f, 360f);
            printerPanel = printerGo.AddComponent<Image>();
            printerPanel.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
            printerTitleText = CreateText("Title", printerRect, font, 30, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            Stretch(printerTitleText.rectTransform, new Vector2(26f, -24f), new Vector2(-26f, -286f));
            printerSubtitleText = CreateText("Subtitle", printerRect, font, 18, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.82f, 0.87f, 0.95f, 1f));
            Stretch(printerSubtitleText.rectTransform, new Vector2(26f, -74f), new Vector2(-26f, -226f));
            printerCurrentIcon = CreateRawImage("CurrentIcon", printerRect, new Vector2(26f, 84f), new Vector2(180f, 180f));
            printerProgressText = CreateText("Progress", printerRect, font, 20, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            Stretch(printerProgressText.rectTransform, new Vector2(230f, -138f), new Vector2(-26f, -148f));
            printerFeedbackText = CreateText("Feedback", printerRect, font, 18, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.98f, 0.82f, 0.42f, 1f));
            Stretch(printerFeedbackText.rectTransform, new Vector2(230f, -178f), new Vector2(-26f, -98f));

            int previewCount = printerConfig != null ? Mathf.Max(1, printerConfig.previewIconCount) : 4;
            for (int i = 0; i < previewCount; i++)
            {
                float x = 230f + (i * 116f);
                RawImage preview = CreateRawImage("Preview" + i, printerRect, new Vector2(x, 84f), new Vector2(92f, 92f));
                printerPreviewIcons.Add(preview);
            }

            printerGo.SetActive(false);
        }

        private void UpdateTopBanner()
        {
            string bannerMessage = isAtendimentoRequested ? "Atendimento solicitado" : string.Empty;
            if (string.IsNullOrWhiteSpace(bannerMessage) && Time.unscaledTime < temporaryBannerUntil)
            {
                bannerMessage = temporaryBannerMessage;
            }

            bool show = !string.IsNullOrWhiteSpace(bannerMessage);
            if (topBannerText != null)
            {
                topBannerText.transform.parent.gameObject.SetActive(show);
                topBannerText.text = bannerMessage;
            }
        }

        private void ShowTemporaryBanner(string message)
        {
            temporaryBannerMessage = message;
            temporaryBannerUntil = Time.unscaledTime + (config != null ? config.feedbackBannerSeconds : 2f);
        }

        private void UpdatePendingPrinterEntryState()
        {
            if (isPrinterPending && printerAwaitingFreshEntry && !IsPlayerInsideTrigger(triggerImpressora))
            {
                printerAwaitingFreshEntry = false;
            }
        }

        private void UpdateExitPrompt()
        {
            if (modalPanel != null)
            {
                modalPanel.gameObject.SetActive(isExitPromptVisible);
            }
        }

        private void ShowExitPrompt()
        {
            isExitPromptVisible = true;
            if (modalTitleText != null)
            {
                modalTitleText.text = "Sair do jogo";
            }

            if (modalBodyText != null)
            {
                modalBodyText.text = "Deseja encerrar a operação agora?";
            }
        }

        private void ConfirmExit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void CancelExit()
        {
            isExitPromptVisible = false;
            if (triggerPorta != null)
            {
                PushPlayerOutOfTrigger(triggerPorta);
            }
        }

        private void UpdateAtendimentoScheduling()
        {
            if (isAtendimentoRequested || !gameplayStarted)
            {
                return;
            }

            bool eligible = app != null && app.IsDashboardScreen && currentStation == StationType.None && !isPrinterPending && !isExitPromptVisible;
            if (!eligible)
            {
                return;
            }

            if (Time.unscaledTime >= nextAtendimentoRequestAt)
            {
                isAtendimentoRequested = true;
                ShowTemporaryBanner("Atendimento solicitado");
            }
        }

        private void CompleteAtendimento()
        {
            isAtendimentoRequested = false;
            ShowTemporaryBanner("Atendimento concluído");
            ScheduleNextAtendimento();
        }

        private void ScheduleNextAtendimento()
        {
            float minDelay = config != null ? config.atendimentoMinDelaySeconds : 25f;
            float maxDelay = config != null ? config.atendimentoMaxDelaySeconds : 55f;
            nextAtendimentoRequestAt = Time.unscaledTime + UnityEngine.Random.Range(minDelay, Mathf.Max(minDelay, maxDelay));
        }

        private static Transform FindTransformByName(string objectName)
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static Collider FindColliderByName(string objectName)
        {
            Collider[] colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (string.Equals(colliders[i].name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return colliders[i];
                }
            }

            return null;
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f)
            {
                angle -= 360f;
            }

            while (angle < -180f)
            {
                angle += 360f;
            }

            return angle;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Text CreateText(string name, RectTransform parent, Font font, int fontSize, FontStyle style, TextAnchor anchor, Color color)
        {
            GameObject go = CreateUiObject(name, parent);
            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static RawImage CreateRawImage(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject go = CreateUiObject(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            RawImage image = go.AddComponent<RawImage>();
            image.color = new Color(1f, 1f, 1f, 0.12f);
            return image;
        }

        private static Button CreateButton(RectTransform parent, Font font, string label, Vector2 anchoredPosition, Vector2 size, Color backgroundColor, Color textColor)
        {
            GameObject go = CreateUiObject(label + "Button", parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = backgroundColor;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = backgroundColor * 1.08f;
            colors.pressedColor = backgroundColor * 0.92f;
            colors.selectedColor = backgroundColor;
            button.colors = colors;

            Text text = CreateText(label + "Label", rect, font, 22, FontStyle.Bold, TextAnchor.MiddleCenter, textColor);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            text.text = label;
            return button;
        }
    }
}
