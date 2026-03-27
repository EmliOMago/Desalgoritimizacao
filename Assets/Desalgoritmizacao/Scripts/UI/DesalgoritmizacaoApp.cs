using System;
using System.Collections.Generic;
using Desalgoritmizacao.Data;
using Desalgoritmizacao.Runtime;
using Desalgoritmizacao.World;
using Desalgoritmizacao.Scene;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Desalgoritmizacao.UI
{
    public class DesalgoritmizacaoApp : MonoBehaviour
    {
        private enum ScreenState
        {
            Menu,
            Dashboard,
            Case,
            Result,
            Final
        }

        private sealed class ScrollBinding
        {
            public ScrollRect scrollRect;
            public RectTransform viewport;
            public RectTransform content;
            public Scrollbar scrollbar;
            public RectTransform handle;
        }

        private Canvas mainCanvas;
        private Canvas initialMenuCanvas;
        private Canvas rankingCanvas;
        private RectTransform headerRoot;
        private RectTransform contentRoot;
        private RectTransform initialMenuRoot;
        private RectTransform rankingRoot;
        private DesalgoritmizacaoScreenAnchor screenAnchor;
        private DesalgoritmizacaoSceneRig sceneRig;
        private Camera uiCamera;
        private Text headerTitleText;
        private Text headerSubtitleText;
        private Text analysisTimerText;
        private Button headerExitButton;
        private Transform currentScreenRoot;
        private InputField operatorNameInput;

        private AppThemeConfig theme;
        private readonly List<CandidateCaseDefinition> caseTemplates = new List<CandidateCaseDefinition>();
        private readonly List<CandidateCaseDefinition> masterCasePool = new List<CandidateCaseDefinition>();
        private readonly List<CandidateCaseDefinition> cases = new List<CandidateCaseDefinition>();
        private readonly DesalgoritmizacaoSessionState session = new DesalgoritmizacaoSessionState();

        private ScreenState currentScreen = ScreenState.Menu;
        private CandidateCaseDefinition activeCase;
        private CandidateCaseDefinition resolvedCase;
        private EvaluatedDecision lastEvaluatedDecision;
        private FinalEvaluation finalEvaluation;

        private float currentCaseStartedAt;
        private bool investigationUnlocked;
        private bool[] openedLayers = new bool[0];
        private int selectedLayerIndex = -1;
        private string currentOperatorName = "Operador";
        private int currentCycleNumber;
        private bool cycleRecordSaved;

        private void Awake()
        {
            ResolveSceneAnchor();
            LoadData();
            currentOperatorName = DesalgoritmizacaoPersistentProgress.LoadOperatorName(theme != null ? theme.defaultOperatorName : "Operador");
            session.ResetFromTheme(theme);
            BuildInitialMenuCanvas();
            BuildRankingCanvas();
            RefreshRankingCanvas();
            ShowMenu();
        }

        private void Update()
        {
            if (currentScreen == ScreenState.Case && analysisTimerText != null && activeCase != null)
            {
                float elapsed = Mathf.Max(0f, Time.unscaledTime - currentCaseStartedAt);
                float target = theme != null ? theme.recommendedAnalysisSeconds : 45f;
                analysisTimerText.text = "Tempo de análise: " + elapsed.ToString("0.0") + "s / meta " + target.ToString("0") + "s";
                analysisTimerText.color = elapsed > target ? theme.warningColor : theme.textSecondaryColor;
            }
        }

        private void LoadData()
        {
            theme = Resources.Load<AppThemeConfig>("Desalgoritmizacao/Config/AppThemeConfig");
            caseTemplates.Clear();
            cases.Clear();
            masterCasePool.Clear();

            CandidateCaseDefinition[] loadedCases = Resources.LoadAll<CandidateCaseDefinition>("Desalgoritmizacao/Cases");
            if (loadedCases != null)
            {
                for (int i = 0; i < loadedCases.Length; i++)
                {
                    if (loadedCases[i] != null)
                    {
                        caseTemplates.Add(loadedCases[i]);
                    }
                }
            }

            caseTemplates.Sort((a, b) => a.order.CompareTo(b.order));

            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<AppThemeConfig>();
                theme.gameTitle = "Desalgoritmizacao";
                theme.presentationLine = "Nem toda trajetória cabe no perfil que o sistema enxerga.";
                theme.menuIntro = "Tema temporário carregado em memória.";
                theme.dashboardSummary = "A central organiza casos, decisões e impacto acumulado.";
                theme.defaultOperatorName = "Operador";
                theme.victoryRuleLabel = "Vitória: os três indicadores precisam chegar a 100.";
                theme.defeatRuleLabel = "Derrota: qualquer indicador chegando a 0 encerra o ciclo.";
            }

            UIFactory.Fonts = new UIFactory.FontSettings
            {
                smallScale = theme.smallTextScale,
                bodyScale = theme.bodyTextScale,
                headingScale = theme.headingTextScale,
                titleScale = theme.titleTextScale,
                minimumFontSize = theme.minimumReadableFontSize,
                scrollbarWidth = theme.scrollbarWidth
            };

            if (caseTemplates.Count == 0)
            {
                CandidateCaseDefinition fallback = ScriptableObject.CreateInstance<CandidateCaseDefinition>();
                fallback.order = 1;
                fallback.caseId = "CASO-FALLBACK";
                fallback.candidateName = "Perfil de exemplo";
                fallback.opportunityTitle = "Programa de exemplo";
                fallback.profileSummary = "Os casos configuráveis não foram encontrados. Esta entrada temporária existe apenas para evitar uma cena vazia.";
                fallback.publicTraceSummary = "Baixa densidade digital e documentação incompleta.";
                fallback.applicationObjective = "Acessar formação básica.";
                fallback.algorithm.score = 42;
                fallback.algorithm.recommendedPriority = "Baixa";
                fallback.algorithm.automaticJustification = "Recomendação automática baseada em baixa legibilidade digital.";
                fallback.algorithm.suggestedDecisionLabel = "Validar leitura automática";
                fallback.algorithm.keywords.Add("dados incompletos");
                fallback.decisionOptions.Add(new DecisionOptionData
                {
                    optionId = "fallback_auto",
                    label = "Validar recomendação automática",
                    matchesAlgorithmRecommendation = true,
                    visuallyPrimary = true,
                    immediateFeedback = "Sem os dados reais, a central manteve a decisão padrão.",
                    impact = new ImpactDelta { operationalEfficiency = 1, communityTrust = -1, systemSensitivity = -1 }
                });
                caseTemplates.Add(fallback);
            }

            masterCasePool.AddRange(DesalgoritmizacaoCaseDeckBuilder.BuildExpandedPool(caseTemplates, Mathf.Max(theme.generatedCasePoolSize, theme.casesPerCycle * 6), theme.shuffleSeed));
        }


        private void PrepareNewCycle()
        {
            session.ResetFromTheme(theme);
            cases.Clear();
            activeCase = null;
            resolvedCase = null;
            cycleRecordSaved = false;
            investigationUnlocked = false;
            openedLayers = Array.Empty<bool>();
            selectedLayerIndex = -1;

            DeckReservationData reservation = DesalgoritmizacaoPersistentProgress.ReserveCycleDeck(masterCasePool.Count, theme != null ? theme.casesPerCycle : 20, theme != null ? theme.shuffleSeed : 41031);
            currentCycleNumber = reservation != null ? reservation.cycleNumber : 0;
            if (reservation == null || reservation.indices == null || reservation.indices.Count == 0)
            {
                return;
            }

            for (int i = 0; i < reservation.indices.Count; i++)
            {
                int poolIndex = reservation.indices[i];
                if (poolIndex < 0 || poolIndex >= masterCasePool.Count)
                {
                    continue;
                }

                CandidateCaseDefinition runtimeCase = Instantiate(masterCasePool[poolIndex]);
                runtimeCase.order = i + 1;
                runtimeCase.caseId = "CASO-" + (i + 1).ToString("00");
                runtimeCase.cycleId = "Rodada " + currentCycleNumber + " · Lote " + (i + 1).ToString("00");
                cases.Add(runtimeCase);
            }
        }

        private bool HasTerminalOutcome()
        {
            return DesalgoritmizacaoEvaluationEngine.IsVictoryState(session) || DesalgoritmizacaoEvaluationEngine.IsDefeatState(session);
        }

        private void ReturnToInitialMenu()
        {
            session.ResetFromTheme(theme);
            activeCase = null;
            resolvedCase = null;
            currentCycleNumber = 0;
            analysisTimerText = null;
            if (mainCanvas != null)
            {
                mainCanvas.enabled = false;
            }
            ShowMenu();
        }

        private void EnsureCycleSavedToRanking()
        {
            if (cycleRecordSaved)
            {
                return;
            }

            EndingDefinition ending = FindEnding(finalEvaluation.endingId);
            RankingEntryData entry = new RankingEntryData
            {
                cycleNumber = currentCycleNumber,
                playerName = string.IsNullOrWhiteSpace(currentOperatorName) ? (theme != null ? theme.defaultOperatorName : "Operador") : currentOperatorName,
                summary = (finalEvaluation.outcome == CycleOutcome.Victory ? "Vitória" : finalEvaluation.outcome == CycleOutcome.Defeat ? "Derrota" : "Ciclo encerrado") + " · " + (ending != null ? ending.title : finalEvaluation.endingId),
                endingId = finalEvaluation.endingId,
                operational = session.metrics.operationalEfficiency,
                communityTrust = session.metrics.communityTrust,
                systemSensitivity = session.metrics.systemSensitivity,
                recordedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            };

            DesalgoritmizacaoPersistentProgress.SaveRankingEntry(entry, theme != null ? theme.rankingMaxEntries : 12);
            cycleRecordSaved = true;
            RefreshRankingCanvas();
        }

        private void EnsureGameplayShell()
        {
            if (mainCanvas == null || contentRoot == null)
            {
                BuildShell();
            }
        }

        private void BuildShell()
        {
            GameObject canvasGo = null;
            if (sceneRig != null)
            {
                mainCanvas = sceneRig.GetOrCreateCanvasInstance(
                    "DesalgoritmizacaoCanvas",
                    "Desalgoritmizacao/Prefabs/DesalgoritmizacaoCanvasShell",
                    sceneRig.GameplayAnchor,
                    theme.backgroundColor);
                canvasGo = mainCanvas.gameObject;
            }
            else
            {
                GameObject prefab = Resources.Load<GameObject>("Desalgoritmizacao/Prefabs/DesalgoritmizacaoCanvasShell");
                Transform parent = screenAnchor != null ? screenAnchor.MainParent : transform;
                if (prefab != null)
                {
                    canvasGo = Instantiate(prefab, parent);
                    canvasGo.name = "DesalgoritmizacaoCanvas";
                }

                if (canvasGo == null)
                {
                    canvasGo = new GameObject("DesalgoritmizacaoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
                    canvasGo.transform.SetParent(parent, false);

                    GameObject headerGo = UIFactory.CreateUIObject("Header", canvasGo.transform);
                    headerRoot = headerGo.GetComponent<RectTransform>();
                    UIFactory.SetAnchors(headerRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -128f), new Vector2(-24f, -24f));
                    headerGo.AddComponent<Image>();

                    GameObject headerTitleGo = UIFactory.CreateUIObject("HeaderTitle", headerGo.transform);
                    headerTitleText = headerTitleGo.AddComponent<Text>();
                    headerTitleText.font = UIFactory.DefaultFont;
                    headerTitleText.alignment = TextAnchor.UpperLeft;
                    headerTitleText.fontStyle = FontStyle.Bold;
                    UIFactory.SetAnchors(headerTitleText.rectTransform, new Vector2(0f, 0.46f), new Vector2(1f, 1f), new Vector2(24f, 8f), new Vector2(-24f, -8f));

                    GameObject headerSubtitleGo = UIFactory.CreateUIObject("HeaderSubtitle", headerGo.transform);
                    headerSubtitleText = headerSubtitleGo.AddComponent<Text>();
                    headerSubtitleText.font = UIFactory.DefaultFont;
                    headerSubtitleText.alignment = TextAnchor.LowerLeft;
                    UIFactory.SetAnchors(headerSubtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.42f), new Vector2(24f, 8f), new Vector2(-24f, -8f));

                    GameObject contentGo = UIFactory.CreateUIObject("Content", canvasGo.transform);
                    contentRoot = contentGo.GetComponent<RectTransform>();
                    UIFactory.SetAnchors(contentRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -148f));
                }
            }

            mainCanvas = canvasGo.GetComponent<Canvas>();
            if (mainCanvas == null)
            {
                mainCanvas = canvasGo.AddComponent<Canvas>();
            }

            ConfigureCanvas(mainCanvas, canvasGo.GetComponent<RectTransform>(), true);

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasGo.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = sceneRig != null ? sceneRig.CanvasSize : (screenAnchor != null ? screenAnchor.canvasReferenceResolution : new Vector2(1600f, 900f));
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            ResolveShellNodes(canvasGo.transform);

            Image background = canvasGo.GetComponent<Image>();
            if (background == null)
            {
                background = canvasGo.AddComponent<Image>();
            }
            background.color = theme.backgroundColor;

            if (headerRoot != null)
            {
                Image headerImage = EnsureComponent<Image>(headerRoot.gameObject);
                headerImage.color = theme.surfaceColor;
            }

            if (headerTitleText != null)
            {
                headerTitleText.font = UIFactory.DefaultFont;
                headerTitleText.fontSize = theme.ScaleFont(34);
                headerTitleText.color = theme.textPrimaryColor;
                headerTitleText.alignment = TextAnchor.UpperLeft;
                headerTitleText.fontStyle = FontStyle.Bold;
            }

            if (headerSubtitleText != null)
            {
                headerSubtitleText.font = UIFactory.DefaultFont;
                headerSubtitleText.fontSize = theme.ScaleFont(16);
                headerSubtitleText.color = theme.textSecondaryColor;
                headerSubtitleText.alignment = TextAnchor.LowerLeft;
            }

            mainCanvas.enabled = false;
        }

        private void ResolveSceneAnchor()
        {
            sceneRig = Object.FindFirstObjectByType<DesalgoritmizacaoSceneRig>();
            if (sceneRig != null)
            {
                sceneRig.EnsureRuntimeObjects();
                screenAnchor = null;
                uiCamera = sceneRig.ResolveCamera();
                return;
            }

            screenAnchor = Object.FindFirstObjectByType<DesalgoritmizacaoScreenAnchor>();
            if (screenAnchor != null)
            {
                uiCamera = screenAnchor.ResolveCamera();
                return;
            }

            uiCamera = Camera.main;
            if (uiCamera == null)
            {
                uiCamera = Object.FindFirstObjectByType<Camera>();
            }
        }

        private void ConfigureCanvas(Canvas targetCanvas, RectTransform rectTransform, bool isMainCanvas)
        {
            if (targetCanvas == null || rectTransform == null)
            {
                return;
            }

            if (sceneRig != null)
            {
                sceneRig.ApplyCanvasPlacement(targetCanvas, isMainCanvas, theme != null ? theme.backgroundColor : Color.black);
                targetCanvas.sortingOrder = isMainCanvas ? 20 : 30;
            }
            else if (screenAnchor != null)
            {
                targetCanvas.renderMode = RenderMode.WorldSpace;
                targetCanvas.worldCamera = uiCamera;
                targetCanvas.sortingOrder = isMainCanvas ? 20 : 30;
                screenAnchor.ApplyTransform(rectTransform, isMainCanvas);
            }
            else
            {
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.worldCamera = null;
                targetCanvas.sortingOrder = isMainCanvas ? 1000 : 1010;
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
                rectTransform.localPosition = Vector3.zero;
                rectTransform.localRotation = Quaternion.identity;
            }
        }

        private void BuildInitialMenuCanvas()
        {
            if (initialMenuCanvas != null)
            {
                Destroy(initialMenuCanvas.gameObject);
                initialMenuCanvas = null;
            }

            GameObject canvasGo = new GameObject("InitialMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            canvasGo.transform.SetParent(transform, false);

            initialMenuCanvas = canvasGo.GetComponent<Canvas>();
            initialMenuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            initialMenuCanvas.worldCamera = null;
            initialMenuCanvas.sortingOrder = 2000;
            initialMenuCanvas.pixelPerfect = false;

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.localScale = Vector3.one;
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = canvasGo.GetComponent<Image>();
            background.color = theme != null ? theme.backgroundColor : new Color(0.07f, 0.08f, 0.11f, 1f);

            GameObject root = UIFactory.CreateUIObject("InitialMenuRoot", canvasGo.transform);
            initialMenuRoot = root.GetComponent<RectTransform>();
            UIFactory.SetAnchors(initialMenuRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            GameObject body = UIFactory.CreateUIObject("Body", initialMenuRoot);
            RectTransform bodyRect = body.GetComponent<RectTransform>();
            UIFactory.SetAnchors(bodyRect, new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.92f), Vector2.zero, Vector2.zero);
            UIFactory.AddVerticalLayout(body, 16, new RectOffset(24, 24, 24, 24), false);

            GameObject titlePanel = UIFactory.CreateUIObject("TitlePanel", body.transform);
            UIFactory.AddVerticalLayout(titlePanel, 6, new RectOffset(0, 0, 0, 0), false);
            UIFactory.AddLayoutElement(titlePanel, preferredHeight: 90f, minHeight: 90f);
            Text titleText = UIFactory.CreateText(titlePanel.transform, theme != null ? theme.gameTitle : "Desalgoritmizacao", 34, theme != null ? theme.textPrimaryColor : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.AddLayoutElement(titleText.gameObject, preferredHeight: 42f);
            Text subtitleText = UIFactory.CreateText(titlePanel.transform, theme != null ? theme.presentationLine : string.Empty, 16, theme != null ? theme.textSecondaryColor : Color.white, TextAnchor.MiddleCenter);
            UIFactory.AddLayoutElement(subtitleText.gameObject, preferredHeight: 34f);

            GameObject imageGo = UIFactory.CreateUIObject("MenuImage", body.transform);
            Image menuImage = imageGo.AddComponent<Image>();
            menuImage.color = screenAnchor != null ? screenAnchor.initialMenuTint : Color.white;
            menuImage.sprite = ResolveInitialMenuSprite();
            menuImage.preserveAspect = true;
            UIFactory.AddLayoutElement(imageGo, preferredHeight: 430f, minHeight: 220f, flexibleHeight: 1f);

            Image operatorPanel = UIFactory.CreatePanel(body.transform, theme != null ? theme.surfaceColor : new Color(0.11f, 0.13f, 0.18f, 1f), "OperatorPanel");
            UIFactory.AddVerticalLayout(operatorPanel.gameObject, 8, new RectOffset(16, 16, 12, 12), false);
            UIFactory.AddLayoutElement(operatorPanel.gameObject, preferredHeight: 92f, minHeight: 92f);
            Text operatorLabel = UIFactory.CreateText(operatorPanel.transform, "Operador identificado", 15, theme != null ? theme.textSecondaryColor : Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(operatorLabel.gameObject, preferredHeight: 18f);
            operatorNameInput = CreateInputField(operatorPanel.transform, currentOperatorName, theme != null ? theme.textPrimaryColor : Color.white, theme != null ? theme.backgroundColor : Color.black, theme != null ? theme.elevatedSurfaceColor : Color.gray);
            operatorNameInput.onEndEdit.AddListener(OnOperatorNameEdited);
            UIFactory.AddLayoutElement(operatorNameInput.gameObject, preferredHeight: 34f, minHeight: 34f);
            Text operatorHint = UIFactory.CreateText(operatorPanel.transform, "O nome será usado no painel de registros de ciclos.", 13, theme != null ? theme.textSecondaryColor : Color.white, TextAnchor.MiddleLeft, FontStyle.Italic);
            UIFactory.AddLayoutElement(operatorHint.gameObject, preferredHeight: 16f);

            Image rulesPanel = UIFactory.CreatePanel(body.transform, theme != null ? theme.elevatedSurfaceColor : new Color(0.15f, 0.18f, 0.24f, 1f), "RulesPanel");
            UIFactory.AddVerticalLayout(rulesPanel.gameObject, 4, new RectOffset(16, 16, 10, 10), false);
            UIFactory.AddLayoutElement(rulesPanel.gameObject, preferredHeight: 70f, minHeight: 70f);
            Text rulesTitle = UIFactory.CreateText(rulesPanel.transform, "Regras do ciclo", 15, theme != null ? theme.humanAccentColor : Color.yellow, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(rulesTitle.gameObject, preferredHeight: 18f);
            Text rulesBody = UIFactory.CreateText(rulesPanel.transform, (theme != null ? theme.victoryRuleLabel : "Vitória: os três indicadores precisam chegar a 100.") + "\n" + (theme != null ? theme.defeatRuleLabel : "Derrota: qualquer indicador chegando a 0 encerra o ciclo."), 13, theme != null ? theme.textSecondaryColor : Color.white, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(rulesBody.gameObject, preferredHeight: 34f);

            GameObject buttonsRow = UIFactory.CreateUIObject("ButtonsRow", body.transform);
            UIFactory.AddHorizontalLayout(buttonsRow, 20, new RectOffset(0, 0, 0, 0), true);
            UIFactory.AddLayoutElement(buttonsRow, preferredHeight: 78f, minHeight: 78f);

            Button startButton = UIFactory.CreateButton(buttonsRow.transform, theme != null ? theme.startButtonLabel : "Iniciar", theme != null ? theme.systemAccentColor : Color.cyan, theme != null ? theme.textPrimaryColor : Color.white, 22);
            UIFactory.AddLayoutElement(startButton.gameObject, preferredHeight: 78f, preferredWidth: 340f);
            startButton.onClick.AddListener(() =>
            {
                CommitOperatorNameFromInput();
                PrepareNewCycle();
                if (initialMenuCanvas != null)
                {
                    initialMenuCanvas.enabled = false;
                }
                ShowDashboard();
            });

            string quitLabel = theme != null && !string.IsNullOrWhiteSpace(theme.quitGameButtonLabel) ? theme.quitGameButtonLabel : "Sair do jogo";
            Button quitButton = UIFactory.CreateButton(buttonsRow.transform, quitLabel, theme != null ? theme.elevatedSurfaceColor : new Color(0.15f, 0.18f, 0.24f, 1f), theme != null ? theme.textPrimaryColor : Color.white, 22);
            UIFactory.AddLayoutElement(quitButton.gameObject, preferredHeight: 78f, preferredWidth: 340f);
            quitButton.onClick.AddListener(QuitGame);

            initialMenuCanvas.enabled = true;
        }

        private InputField CreateInputField(Transform parent, string initialValue, Color textColor, Color backgroundColor, Color frameColor)
        {
            GameObject root = UIFactory.CreateUIObject("InputField", parent);
            Image rootImage = root.AddComponent<Image>();
            rootImage.color = frameColor;
            InputField inputField = root.AddComponent<InputField>();

            GameObject textArea = UIFactory.CreateUIObject("TextArea", root.transform);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            UIFactory.Stretch(textAreaRect);
            textAreaRect.offsetMin = new Vector2(10f, 6f);
            textAreaRect.offsetMax = new Vector2(-10f, -6f);
            textArea.AddComponent<RectMask2D>();

            Text placeholder = UIFactory.CreateText(textArea.transform, "Digite o nome do operador", 15, new Color(textColor.r, textColor.g, textColor.b, 0.45f), TextAnchor.MiddleLeft, FontStyle.Italic);
            UIFactory.Stretch(placeholder.rectTransform);
            Text inputText = UIFactory.CreateText(textArea.transform, initialValue, 16, textColor, TextAnchor.MiddleLeft);
            UIFactory.Stretch(inputText.rectTransform);

            inputField.targetGraphic = rootImage;
            inputField.textViewport = textAreaRect;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.text = initialValue;
            return inputField;
        }

        private void OnOperatorNameEdited(string value)
        {
            string fallback = theme != null ? theme.defaultOperatorName : "Operador";
            currentOperatorName = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            DesalgoritmizacaoPersistentProgress.SaveOperatorName(currentOperatorName, fallback);
            RefreshRankingCanvas();
        }

        private void CommitOperatorNameFromInput()
        {
            string fallback = theme != null ? theme.defaultOperatorName : "Operador";
            string value = operatorNameInput != null ? operatorNameInput.text : currentOperatorName;
            currentOperatorName = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (operatorNameInput != null)
            {
                operatorNameInput.text = currentOperatorName;
            }
            DesalgoritmizacaoPersistentProgress.SaveOperatorName(currentOperatorName, fallback);
        }

        private void BuildRankingCanvas()
        {
            if (sceneRig == null && screenAnchor == null)
            {
                return;
            }

            Transform anchor = sceneRig != null ? sceneRig.RankingAnchor : (screenAnchor != null ? screenAnchor.MainParent : transform);
            GameObject canvasGo;
            if (sceneRig != null)
            {
                rankingCanvas = sceneRig.GetOrCreateCanvasInstance("DesalgoritmizacaoRankingCanvas", null, anchor, theme.elevatedSurfaceColor);
                canvasGo = rankingCanvas.gameObject;
            }
            else
            {
                Transform existing = anchor != null ? anchor.Find("DesalgoritmizacaoRankingCanvas") : null;
                canvasGo = existing != null ? existing.gameObject : new GameObject("DesalgoritmizacaoRankingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
                if (existing == null)
                {
                    canvasGo.transform.SetParent(anchor != null ? anchor : transform, false);
                }
                rankingCanvas = canvasGo.GetComponent<Canvas>();
                if (rankingCanvas == null)
                {
                    rankingCanvas = canvasGo.AddComponent<Canvas>();
                }
                ConfigureCanvas(rankingCanvas, canvasGo.GetComponent<RectTransform>(), true);
            }

            rankingCanvas.sortingOrder = 15;
            Image background = EnsureComponent<Image>(canvasGo);
            background.color = theme != null ? theme.surfaceColor : new Color(0.11f, 0.13f, 0.18f, 1f);

            rankingRoot = canvasGo.transform.Find("RankingRoot") as RectTransform;
            if (rankingRoot == null)
            {
                GameObject root = UIFactory.CreateUIObject("RankingRoot", canvasGo.transform);
                rankingRoot = root.GetComponent<RectTransform>();
                UIFactory.SetAnchors(rankingRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, 18f), new Vector2(-18f, -18f));
            }
        }

        private void RefreshRankingCanvas()
        {
            if (rankingRoot == null)
            {
                return;
            }

            ClearChildren(rankingRoot);
            VerticalLayoutGroup rootLayout = EnsureComponent<VerticalLayoutGroup>(rankingRoot.gameObject);
            rootLayout.spacing = 8;
            rootLayout.padding = new RectOffset(12, 12, 12, 12);
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = true;

            Text title = UIFactory.CreateText(rankingRoot, theme != null ? theme.rankingTitle : "Registro de ciclos", 22, theme != null ? theme.textPrimaryColor : Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(title.gameObject, preferredHeight: 26f);
            Text subtitle = UIFactory.CreateText(rankingRoot, theme != null ? theme.rankingSubtitle : "Confiança comunitária define a ordem do painel.", 13, theme != null ? theme.textSecondaryColor : Color.white, TextAnchor.MiddleLeft);
            UIFactory.AddLayoutElement(subtitle.gameObject, preferredHeight: 16f);

            ScrollRect scroll = UIFactory.CreateScrollView(rankingRoot, theme != null ? theme.backgroundColor : Color.black, theme != null ? theme.backgroundColor : Color.black, out RectTransform content);
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1f, preferredHeight: 560f);
            VerticalLayoutGroup layout = EnsureComponent<VerticalLayoutGroup>(content.gameObject);
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            List<RankingEntryData> rankingEntries = DesalgoritmizacaoPersistentProgress.LoadRankingEntries(theme != null ? theme.rankingMaxEntries : 12);
            if (rankingEntries.Count == 0)
            {
                Image empty = UIFactory.CreatePanel(content, theme != null ? theme.surfaceColor : new Color(0.11f, 0.13f, 0.18f, 1f), "EmptyRanking");
                UIFactory.AddLayoutElement(empty.gameObject, preferredHeight: 120f);
                UIFactory.AddVerticalLayout(empty.gameObject, 6, new RectOffset(12, 12, 12, 12), false);
                Text emptyTitle = UIFactory.CreateText(empty.transform, "Ainda não há ciclos registrados", 16, theme != null ? theme.textPrimaryColor : Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.AddLayoutElement(emptyTitle.gameObject, preferredHeight: 20f);
                Text emptyBody = UIFactory.CreateText(empty.transform, "Quando um ciclo termina, o resumo aparece aqui ordenado pela confiança comunitária.", 13, theme != null ? theme.textSecondaryColor : Color.white, TextAnchor.UpperLeft);
                UIFactory.AddLayoutElement(emptyBody.gameObject, preferredHeight: 60f);
                return;
            }

            for (int i = 0; i < rankingEntries.Count; i++)
            {
                RankingEntryData entry = rankingEntries[i];
                Image card = UIFactory.CreatePanel(content, theme != null ? theme.surfaceColor : new Color(0.11f, 0.13f, 0.18f, 1f), "RankingEntry");
                UIFactory.AddLayoutElement(card.gameObject, preferredHeight: 118f);
                UIFactory.AddVerticalLayout(card.gameObject, 4, new RectOffset(12, 12, 10, 10), false);

                Text head = UIFactory.CreateText(card.transform, "#" + (i + 1).ToString("00") + " · " + entry.playerName, 16, theme != null ? theme.textPrimaryColor : Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.AddLayoutElement(head.gameObject, preferredHeight: 20f);
                Text summary = UIFactory.CreateText(card.transform, entry.summary, 14, theme != null ? theme.humanAccentColor : Color.yellow, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.AddLayoutElement(summary.gameObject, preferredHeight: 18f);
                Text metrics = UIFactory.CreateText(card.transform, "Confiança " + entry.communityTrust.ToString("00") + " · Eficiência " + entry.operational.ToString("00") + " · Sensibilidade " + entry.systemSensitivity.ToString("00"), 13, theme != null ? theme.textSecondaryColor : Color.white, TextAnchor.MiddleLeft);
                UIFactory.AddLayoutElement(metrics.gameObject, preferredHeight: 16f);
                Text meta = UIFactory.CreateText(card.transform, "Rodada " + entry.cycleNumber + " · " + entry.recordedAt, 12, theme != null ? theme.textSecondaryColor : Color.white, TextAnchor.MiddleLeft, FontStyle.Italic);
                UIFactory.AddLayoutElement(meta.gameObject, preferredHeight: 14f);
            }
        }

        private Sprite ResolveInitialMenuSprite()
        {
            if (screenAnchor != null && screenAnchor.initialMenuImage != null)
            {
                return screenAnchor.initialMenuImage;
            }

            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            texture.name = "InitialMenuGenerated";
            Color a = theme != null ? theme.systemAccentColor : new Color(0.25f, 0.67f, 0.98f, 1f);
            Color b = theme != null ? theme.humanAccentColor : new Color(0.95f, 0.76f, 0.33f, 1f);
            Color bg = theme != null ? theme.surfaceColor : new Color(0.11f, 0.13f, 0.18f, 1f);
            for (int y = 0; y < texture.height; y++)
            {
                float v = y / (float)(texture.height - 1);
                for (int x = 0; x < texture.width; x++)
                {
                    float u = x / (float)(texture.width - 1);
                    Color c = Color.Lerp(a, b, Mathf.Abs(u - 0.5f) * 0.85f + v * 0.35f);
                    float frame = (x < 3 || y < 3 || x > texture.width - 4 || y > texture.height - 4) ? 0.75f : 0f;
                    texture.SetPixel(x, y, Color.Lerp(bg, c, 0.45f + frame));
                }
            }
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private void QuitGame()
        {
            Application.Quit();
        }

        private void ResolveShellNodes(Transform root)
        {
            headerRoot = root.Find("Header") as RectTransform;
            contentRoot = root.Find("Content") as RectTransform;

            Transform title = root.Find("Header/HeaderTitle");
            if (title != null)
            {
                headerTitleText = EnsureComponent<Text>(title.gameObject);
                headerTitleText.rectTransform.offsetMax = new Vector2(-200f, headerTitleText.rectTransform.offsetMax.y);
            }

            Transform subtitle = root.Find("Header/HeaderSubtitle");
            if (subtitle != null)
            {
                headerSubtitleText = EnsureComponent<Text>(subtitle.gameObject);
                headerSubtitleText.rectTransform.offsetMax = new Vector2(-200f, headerSubtitleText.rectTransform.offsetMax.y);
            }

            EnsureHeaderExitButton();
        }

        private void EnsureHeaderExitButton()
        {
            if (headerRoot == null)
            {
                return;
            }

            Transform existing = headerRoot.Find("HeaderExitButton");
            if (existing == null)
            {
                GameObject buttonGo = UIFactory.CreateUIObject("HeaderExitButton", headerRoot);
                RectTransform rect = buttonGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(154f, 42f);
                rect.anchoredPosition = new Vector2(-22f, -20f);
            }

            headerExitButton = PrepareButtonOnRect(headerRoot.Find("HeaderExitButton") as RectTransform, "Sair", theme != null ? theme.elevatedSurfaceColor : Color.gray, theme != null ? theme.textPrimaryColor : Color.white, 16, ReturnToInitialMenu);
            SetHeaderExitVisible(false);
        }

        private Button PrepareButtonOnRect(RectTransform rect, string label, Color backgroundColor, Color textColor, int baseSize, UnityAction action)
        {
            if (rect == null)
            {
                return null;
            }

            Image image = EnsureComponent<Image>(rect.gameObject);
            image.color = backgroundColor;

            Button button = EnsureComponent<Button>(rect.gameObject);
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = backgroundColor * 1.08f;
            colors.pressedColor = backgroundColor * 0.92f;
            colors.selectedColor = backgroundColor;
            colors.disabledColor = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, backgroundColor.a * 0.4f);
            button.colors = colors;

            Transform labelTransform = rect.Find("Label");
            Text labelText;
            if (labelTransform == null)
            {
                GameObject labelGo = UIFactory.CreateUIObject("Label", rect);
                RectTransform labelRect = labelGo.GetComponent<RectTransform>();
                UIFactory.Stretch(labelRect);
                labelText = labelGo.AddComponent<Text>();
            }
            else
            {
                labelText = EnsureComponent<Text>(labelTransform.gameObject);
            }

            labelText.font = UIFactory.DefaultFont;
            labelText.text = label;
            labelText.fontSize = theme.ScaleFont(baseSize);
            labelText.color = textColor;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.fontStyle = FontStyle.Bold;
            return button;
        }

        private void SetHeaderExitVisible(bool visible)
        {
            if (headerExitButton != null)
            {
                headerExitButton.gameObject.SetActive(visible);
            }
        }

        private void SetHeader(string title, string subtitle)
        {
            if (headerTitleText != null)
            {
                headerTitleText.text = title;
            }

            if (headerSubtitleText != null)
            {
                headerSubtitleText.text = subtitle;
            }
        }

        private void ClearContent()
        {
            analysisTimerText = null;
            currentScreenRoot = null;

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(contentRoot.GetChild(i).gameObject);
            }
        }

        private Transform LoadScreen(string resourcePath, string fallbackName)
        {
            EnsureGameplayShell();

            if (initialMenuCanvas != null)
            {
                initialMenuCanvas.enabled = false;
            }

            if (mainCanvas != null)
            {
                mainCanvas.enabled = true;
            }

            ClearContent();
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab != null)
            {
                currentScreenRoot = Instantiate(prefab, contentRoot).transform;
            }
            else
            {
                GameObject go = UIFactory.CreateUIObject(fallbackName, contentRoot);
                UIFactory.Stretch(go.GetComponent<RectTransform>());
                currentScreenRoot = go.transform;
            }

            RectTransform rootRect = currentScreenRoot as RectTransform;
            if (rootRect != null)
            {
                UIFactory.Stretch(rootRect);
            }

            return currentScreenRoot;
        }

        private RectTransform GetSlot(string path)
        {
            if (currentScreenRoot == null)
            {
                return null;
            }

            string[] parts = path.Split('/');
            Transform current = currentScreenRoot;
            for (int i = 0; i < parts.Length; i++)
            {
                Transform next = current.Find(parts[i]);
                if (next == null)
                {
                    GameObject created = UIFactory.CreateUIObject(parts[i], current);
                    next = created.transform;
                    if (i == parts.Length - 1)
                    {
                        UIFactory.Stretch(created.GetComponent<RectTransform>());
                    }
                }
                current = next;
            }

            return current as RectTransform;
        }

        private void SetSlotActive(string path, bool isActive)
        {
            RectTransform slot = GetSlot(path);
            if (slot != null)
            {
                slot.gameObject.SetActive(isActive);
            }
        }

        private void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }
            return component;
        }

        private Image PreparePanel(string path, Color color)
        {
            RectTransform slot = GetSlot(path);
            if (slot == null)
            {
                return null;
            }

            Image image = EnsureComponent<Image>(slot.gameObject);
            image.color = color;
            return image;
        }

        private Text PrepareText(string path, string value, int baseSize, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            RectTransform slot = GetSlot(path);
            if (slot == null)
            {
                return null;
            }

            Text text = EnsureComponent<Text>(slot.gameObject);
            text.font = UIFactory.DefaultFont;
            text.text = value;
            text.fontSize = theme.ScaleFont(baseSize);
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            return text;
        }

        private Button PrepareButton(string path, string label, Color backgroundColor, Color textColor, int baseSize, UnityAction action)
        {
            RectTransform slot = GetSlot(path);
            if (slot == null)
            {
                return null;
            }

            Image image = EnsureComponent<Image>(slot.gameObject);
            image.color = backgroundColor;

            Button button = EnsureComponent<Button>(slot.gameObject);
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = backgroundColor * 1.08f;
            colors.pressedColor = backgroundColor * 0.92f;
            colors.selectedColor = backgroundColor;
            colors.disabledColor = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, backgroundColor.a * 0.4f);
            button.colors = colors;

            PrepareText(path + "/Label", label, baseSize, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            return button;
        }

        private ScrollBinding PrepareScroll(string path, Color viewportColor, Color contentColor)
        {
            RectTransform root = GetSlot(path);
            if (root == null)
            {
                return null;
            }

            Image rootImage = EnsureComponent<Image>(root.gameObject);
            rootImage.color = viewportColor;

            ScrollRect scrollRect = EnsureComponent<ScrollRect>(root.gameObject);
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            RectTransform viewport = GetSlot(path + "/Viewport");
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-Mathf.Max(12, theme.scrollbarWidth + 6), 0f);

            Image viewportImage = EnsureComponent<Image>(viewport.gameObject);
            viewportImage.color = viewportColor;
            Mask mask = EnsureComponent<Mask>(viewport.gameObject);
            mask.showMaskGraphic = true;

            RectTransform content = GetSlot(path + "/Viewport/Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            Image contentImage = EnsureComponent<Image>(content.gameObject);
            contentImage.color = contentColor;
            ContentSizeFitter fitter = EnsureComponent<ContentSizeFitter>(content.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform scrollbarRect = GetSlot(path + "/Scrollbar");
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 1f);
            scrollbarRect.offsetMin = new Vector2(-Mathf.Max(12, theme.scrollbarWidth), 6f);
            scrollbarRect.offsetMax = new Vector2(0f, -6f);
            Image scrollbarBack = EnsureComponent<Image>(scrollbarRect.gameObject);
            scrollbarBack.color = new Color(1f, 1f, 1f, 0.08f);
            Scrollbar scrollbar = EnsureComponent<Scrollbar>(scrollbarRect.gameObject);
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            RectTransform slidingArea = GetSlot(path + "/Scrollbar/Sliding Area");
            UIFactory.Stretch(slidingArea);
            slidingArea.offsetMin = new Vector2(2f, 2f);
            slidingArea.offsetMax = new Vector2(-2f, -2f);

            RectTransform handle = GetSlot(path + "/Scrollbar/Sliding Area/Handle");
            UIFactory.Stretch(handle);
            Image handleImage = EnsureComponent<Image>(handle.gameObject);
            handleImage.color = new Color(1f, 1f, 1f, 0.55f);

            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.size = 0.2f;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 6f;

            return new ScrollBinding
            {
                scrollRect = scrollRect,
                viewport = viewport,
                content = content,
                scrollbar = scrollbar,
                handle = handle
            };
        }

        private void PrepareScrollHint(string path)
        {
            if (theme == null || string.IsNullOrWhiteSpace(theme.scrollIndicatorLabel))
            {
                return;
            }

            PrepareText(path, "↓ " + theme.scrollIndicatorLabel, 13, theme.textSecondaryColor, TextAnchor.MiddleRight, FontStyle.Italic);
        }

        private void PopulateMetricSlot(string path, string label, int value, Color accentColor)
        {
            PreparePanel(path, theme.elevatedSurfaceColor);
            PrepareText(path + "/Label", label, 16, theme.textSecondaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PrepareText(path + "/Value", value.ToString("00") + "/100", 28, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PreparePanel(path + "/BarBack", theme.surfaceColor);
            Image fill = PreparePanel(path + "/BarBack/BarFill", accentColor);
            if (fill != null)
            {
                RectTransform fillRect = fill.rectTransform;
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(Mathf.Clamp01(value / 100f), 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }
        }

        private void PopulateDeltaSlot(string path, string label, int delta, int currentValue, Color accentColor)
        {
            PreparePanel(path, theme.elevatedSurfaceColor);
            PrepareText(path + "/Label", label, 18, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            Color deltaColor = delta >= 0 ? accentColor : theme.negativeColor;
            PrepareText(path + "/Value", UIFactory.FormatDelta(delta) + " · total " + currentValue.ToString("00") + "/100", 20, deltaColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void ShowMenu()
        {
            currentScreen = ScreenState.Menu;
            if (mainCanvas != null)
            {
                mainCanvas.enabled = false;
            }

            if (initialMenuCanvas != null)
            {
                initialMenuCanvas.enabled = true;
            }

            if (operatorNameInput != null)
            {
                operatorNameInput.text = currentOperatorName;
            }
            SetHeader(theme.gameTitle, theme.presentationLine);
            SetHeaderExitVisible(false);
        }

        private void ShowDashboard()
        {
            if (cases.Count == 0)
            {
                PrepareNewCycle();
            }

            EnsureGameplayShell();
            currentScreen = ScreenState.Dashboard;
            activeCase = session.CurrentCase(cases);
            SetHeader("Central de triagem", theme.dashboardSummary + " · Rodada " + currentCycleNumber);
            LoadScreen("Desalgoritmizacao/Prefabs/Screens/DashboardScreen", "DashboardScreen");
            SetHeaderExitVisible(true);

            PreparePanel("LeftPanel", theme.surfaceColor);
            PreparePanel("RightPanel", theme.surfaceColor);
            PrepareText("LeftPanel/DashboardIntro", "A rodada atual usa um lote aleatório controlado e sem repetição imediata. Confiança ordena o ranking externo; equilíbrio decide a continuidade do ciclo.", 18, theme.textPrimaryColor, TextAnchor.UpperLeft);

            PopulateMetricSlot("LeftPanel/MetricsStrip/MetricOperational", "Eficiência operacional", session.metrics.operationalEfficiency, theme.systemAccentColor);
            PopulateMetricSlot("LeftPanel/MetricsStrip/MetricTrust", "Confiança comunitária", session.metrics.communityTrust, theme.humanAccentColor);
            PopulateMetricSlot("LeftPanel/MetricsStrip/MetricSensitivity", "Sensibilidade do sistema", session.metrics.systemSensitivity, theme.positiveColor);

            PreparePanel("LeftPanel/QueuePanel", theme.elevatedSurfaceColor);
            PrepareText("LeftPanel/QueuePanel/QueueTitle", theme.queueTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PrepareText("LeftPanel/QueuePanel/QueueHint", "Ao zerar qualquer indicador você perde. Ao levar os três a 100 você vence imediatamente.", 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            ScrollBinding queueScroll = PrepareScroll("LeftPanel/QueuePanel/QueueScroll", theme.surfaceColor, theme.surfaceColor);
            VerticalLayoutGroup queueLayout = EnsureComponent<VerticalLayoutGroup>(queueScroll.content.gameObject);
            queueLayout.spacing = 10;
            queueLayout.padding = new RectOffset(4, 4, 4, 4);
            queueLayout.childControlHeight = true;
            queueLayout.childControlWidth = true;
            queueLayout.childForceExpandHeight = false;
            queueLayout.childForceExpandWidth = true;
            ClearChildren(queueScroll.content);
            PrepareScrollHint("LeftPanel/QueuePanel/QueueScrollHint");

            for (int i = session.currentCaseIndex; i < cases.Count; i++)
            {
                CandidateCaseDefinition caseData = cases[i];
                Image card = UIFactory.CreatePanel(queueScroll.content, theme.surfaceColor, "CaseCard");
                UIFactory.AddLayoutElement(card.gameObject, preferredHeight: 112f);
                UIFactory.AddVerticalLayout(card.gameObject, 6, new RectOffset(16, 16, 14, 14), false);

                string line = (i + 1).ToString("00") + " · " + caseData.candidateName + " · " + caseData.opportunityTitle;
                Text caseTitle = UIFactory.CreateText(card.transform, line, 18, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.AddLayoutElement(caseTitle.gameObject, preferredHeight: 26f);

                Text caseBody = UIFactory.CreateText(card.transform, caseData.publicTraceSummary, 15, theme.textSecondaryColor, TextAnchor.UpperLeft);
                UIFactory.AddLayoutElement(caseBody.gameObject, preferredHeight: 38f);

                if (i == session.currentCaseIndex)
                {
                    Button analyzeButton = UIFactory.CreateButton(card.transform, theme.beginCaseButtonLabel, theme.systemAccentColor, theme.textPrimaryColor, 16);
                    UIFactory.AddLayoutElement(analyzeButton.gameObject, preferredHeight: 34f);
                    analyzeButton.onClick.AddListener(() => ShowCase(true));
                }
                else
                {
                    Text queued = UIFactory.CreateText(card.transform, "Aguardando casos anteriores", 14, theme.warningColor, TextAnchor.MiddleLeft, FontStyle.Italic);
                    UIFactory.AddLayoutElement(queued.gameObject, preferredHeight: 22f);
                }
            }

            PrepareText("RightPanel/ImpactTitle", theme.impactPanelTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PrepareText("RightPanel/ImpactHint", "Cada decisão altera os três indicadores centrais.", 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            ScrollBinding impactScroll = PrepareScroll("RightPanel/ImpactScroll", theme.elevatedSurfaceColor, theme.elevatedSurfaceColor);
            VerticalLayoutGroup impactLayout = EnsureComponent<VerticalLayoutGroup>(impactScroll.content.gameObject);
            impactLayout.spacing = 10;
            impactLayout.padding = new RectOffset(14, 14, 14, 14);
            impactLayout.childControlHeight = true;
            impactLayout.childControlWidth = true;
            impactLayout.childForceExpandHeight = false;
            impactLayout.childForceExpandWidth = true;
            ClearChildren(impactScroll.content);
            PrepareScrollHint("RightPanel/ImpactScrollHint");

            if (session.decisionHistory.Count == 0)
            {
                Image empty = UIFactory.CreatePanel(impactScroll.content, theme.surfaceColor, "EmptyState");
                UIFactory.AddLayoutElement(empty.gameObject, preferredHeight: 120f);
                UIFactory.AddVerticalLayout(empty.gameObject, 8, new RectOffset(16, 16, 16, 16), false);
                Text emptyTitle = UIFactory.CreateText(empty.transform, "Sem decisões registradas", 18, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.AddLayoutElement(emptyTitle.gameObject, preferredHeight: 24f);
                Text emptyBody = UIFactory.CreateText(empty.transform, "O histórico aparecerá aqui após a primeira decisão.", 15, theme.textSecondaryColor, TextAnchor.UpperLeft);
                UIFactory.AddLayoutElement(emptyBody.gameObject, preferredHeight: 56f);
            }
            else
            {
                for (int i = session.decisionHistory.Count - 1; i >= 0; i--)
                {
                    CreateDecisionRecordCard(impactScroll.content, session.decisionHistory[i]);
                }
            }
        }

        private void ShowCase(bool resetCaseState)
        {
            SetHeaderExitVisible(false);
            activeCase = session.CurrentCase(cases);
            if (activeCase == null)
            {
                ShowFinalSummary();
                return;
            }

            if (resetCaseState || openedLayers.Length != activeCase.investigationLayers.Count)
            {
                currentCaseStartedAt = Time.unscaledTime;
                investigationUnlocked = false;
                openedLayers = new bool[activeCase.investigationLayers.Count];
                selectedLayerIndex = activeCase.investigationLayers.Count > 0 ? 0 : -1;
            }

            currentScreen = ScreenState.Case;
            SetHeader(activeCase.cycleId + " · " + activeCase.caseId, "O algoritmo favorece rapidez. O contexto exige investigação.");
            LoadScreen("Desalgoritmizacao/Prefabs/Screens/CaseScreen", "CaseScreen");

            PreparePanel("ProfilePanel", theme.surfaceColor);
            PreparePanel("AlgorithmPanel", theme.elevatedSurfaceColor);
            PreparePanel("InvestigationPanel", theme.surfaceColor);

            PrepareText("ProfilePanel/ProfileTitle", theme.profilePanelTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PrepareText("AlgorithmPanel/AlgorithmTitle", theme.algorithmPanelTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            analysisTimerText = PrepareText("AlgorithmPanel/AlgorithmTimer", string.Empty, 15, theme.textSecondaryColor, TextAnchor.MiddleLeft);
            PrepareText("InvestigationPanel/InvestigationTitle", theme.investigationPanelTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PrepareText("InvestigationPanel/InvestigationHelper", "Estas camadas pedem leitura ativa e mudam a interpretação do caso.", 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            PrepareText("InvestigationPanel/DecisionTitle", theme.alternativesPanelTitle, 20, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PrepareScrollHint("ProfilePanel/ProfileScrollHint");
            PrepareScrollHint("InvestigationPanel/DecisionScrollHint");

            ScrollBinding profileScroll = PrepareScroll("ProfilePanel/ProfileScroll", theme.surfaceColor, theme.surfaceColor);
            VerticalLayoutGroup profileLayout = EnsureComponent<VerticalLayoutGroup>(profileScroll.content.gameObject);
            profileLayout.spacing = 10;
            profileLayout.padding = new RectOffset(18, 18, 18, 18);
            profileLayout.childControlHeight = true;
            profileLayout.childControlWidth = true;
            profileLayout.childForceExpandHeight = false;
            profileLayout.childForceExpandWidth = true;
            ClearChildren(profileScroll.content);
            BuildProfilePanel(profileScroll.content, activeCase);

            ScrollBinding algorithmScroll = PrepareScroll("AlgorithmPanel/AlgorithmScroll", theme.elevatedSurfaceColor, theme.elevatedSurfaceColor);
            VerticalLayoutGroup algorithmLayout = EnsureComponent<VerticalLayoutGroup>(algorithmScroll.content.gameObject);
            algorithmLayout.spacing = 10;
            algorithmLayout.padding = new RectOffset(18, 18, 18, 18);
            algorithmLayout.childControlHeight = true;
            algorithmLayout.childControlWidth = true;
            algorithmLayout.childForceExpandHeight = false;
            algorithmLayout.childForceExpandWidth = true;
            ClearChildren(algorithmScroll.content);
            BuildAlgorithmPanel(algorithmScroll.content, activeCase);

            DecisionOptionData recommended = FindRecommendedDecision(activeCase);
            PrepareButton("AlgorithmPanel/SuggestedButton", activeCase.algorithm.suggestedDecisionLabel, theme.systemAccentColor, theme.textPrimaryColor, 18, recommended != null ? (UnityAction)(() => CommitDecision(recommended)) : null);
            PrepareButton("AlgorithmPanel/UnlockButton", theme.unlockInvestigationLabel, theme.humanAccentColor, theme.backgroundColor, 18, () =>
            {
                investigationUnlocked = true;
                if (selectedLayerIndex < 0 && activeCase != null && activeCase.investigationLayers.Count > 0)
                {
                    selectedLayerIndex = 0;
                }
                ShowCase(false);
            });

            RenderInvestigationArea();
        }

        private void RenderInvestigationArea()
        {
            if (activeCase == null)
            {
                return;
            }

            SetSlotActive("InvestigationPanel/TabsArea", investigationUnlocked);
            SetSlotActive("InvestigationPanel/LayerPanel", investigationUnlocked);
            SetSlotActive("InvestigationPanel/DecisionTitle", investigationUnlocked);
            SetSlotActive("InvestigationPanel/DecisionScroll", investigationUnlocked);
            SetSlotActive("InvestigationPanel/DecisionScrollHint", investigationUnlocked);
            SetSlotActive("InvestigationPanel/LockedPanel", !investigationUnlocked);

            if (!investigationUnlocked)
            {
                PreparePanel("InvestigationPanel/LockedPanel", theme.elevatedSurfaceColor);
                PrepareText("InvestigationPanel/LockedPanel/LockedTitle", "Investigação ainda fechada", 22, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                PrepareText("InvestigationPanel/LockedPanel/LockedBody", "A investigação existe, mas fica fora do fluxo mais confortável da interface.", 17, theme.textSecondaryColor, TextAnchor.UpperLeft);
                string teaser = activeCase.investigationLayers.Count > 0 ? "Primeiro indício: " + activeCase.investigationLayers[0].shortHint : "Este perfil ainda não possui camadas contextuais configuradas.";
                PrepareText("InvestigationPanel/LockedPanel/LockedTeaser", teaser, 16, theme.textPrimaryColor, TextAnchor.UpperLeft, FontStyle.Italic);
                PrepareButton("InvestigationPanel/LockedPanel/LockedUnlockButton", theme.unlockInvestigationLabel, theme.humanAccentColor, theme.backgroundColor, 18, () =>
                {
                    investigationUnlocked = true;
                    if (selectedLayerIndex < 0 && activeCase.investigationLayers.Count > 0)
                    {
                        selectedLayerIndex = 0;
                    }
                    ShowCase(false);
                });
                return;
            }

            RectTransform tabsArea = GetSlot("InvestigationPanel/TabsArea");
            if (tabsArea != null)
            {
                PreparePanel("InvestigationPanel/TabsArea", new Color(0f, 0f, 0f, 0f));
                ClearChildren(tabsArea);
                HorizontalLayoutGroup tabsLayout = EnsureComponent<HorizontalLayoutGroup>(tabsArea.gameObject);
                tabsLayout.spacing = 8;
                tabsLayout.padding = new RectOffset(0, 0, 0, 0);
                tabsLayout.childAlignment = TextAnchor.MiddleLeft;
                tabsLayout.childControlHeight = true;
                tabsLayout.childControlWidth = false;
                tabsLayout.childForceExpandWidth = false;
                tabsLayout.childForceExpandHeight = false;

                for (int i = 0; i < activeCase.investigationLayers.Count; i++)
                {
                    int closureIndex = i;
                    InvestigationLayerData layer = activeCase.investigationLayers[i];
                    bool selected = closureIndex == selectedLayerIndex;
                    Color buttonColor = selected ? theme.humanAccentColor : theme.elevatedSurfaceColor;
                    Color textColor = selected ? theme.backgroundColor : theme.textPrimaryColor;
                    Button layerButton = UIFactory.CreateButton(tabsArea, layer.buttonLabel, buttonColor, textColor, 14);
                    UIFactory.AddLayoutElement(layerButton.gameObject, preferredWidth: 150f, preferredHeight: 38f);
                    layerButton.onClick.AddListener(() =>
                    {
                        selectedLayerIndex = closureIndex;
                        if (openedLayers != null && closureIndex >= 0 && closureIndex < openedLayers.Length)
                        {
                            openedLayers[closureIndex] = true;
                        }
                        ShowCase(false);
                    });
                }
            }

            PreparePanel("InvestigationPanel/LayerPanel", theme.elevatedSurfaceColor);
            if (selectedLayerIndex >= 0 && selectedLayerIndex < activeCase.investigationLayers.Count)
            {
                InvestigationLayerData layer = activeCase.investigationLayers[selectedLayerIndex];
                openedLayers[selectedLayerIndex] = true;
                PrepareText("InvestigationPanel/LayerPanel/LayerTitle", layer.buttonLabel + " · " + layer.revealTag, 20, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                PrepareText("InvestigationPanel/LayerPanel/LayerHint", layer.shortHint, 15, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Italic);
                PrepareText("InvestigationPanel/LayerPanel/LayerBody", layer.content, 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            }
            else
            {
                PrepareText("InvestigationPanel/LayerPanel/LayerTitle", "Sem camada selecionada", 20, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                PrepareText("InvestigationPanel/LayerPanel/LayerHint", string.Empty, 15, theme.humanAccentColor, TextAnchor.MiddleLeft);
                PrepareText("InvestigationPanel/LayerPanel/LayerBody", "Abra uma camada contextual para aprofundar a leitura do caso.", 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            }

            ScrollBinding decisionsScroll = PrepareScroll("InvestigationPanel/DecisionScroll", theme.surfaceColor, theme.surfaceColor);
            VerticalLayoutGroup decisionsLayout = EnsureComponent<VerticalLayoutGroup>(decisionsScroll.content.gameObject);
            decisionsLayout.spacing = 10;
            decisionsLayout.padding = new RectOffset(12, 12, 12, 12);
            decisionsLayout.childControlHeight = true;
            decisionsLayout.childControlWidth = true;
            decisionsLayout.childForceExpandHeight = false;
            decisionsLayout.childForceExpandWidth = true;
            ClearChildren(decisionsScroll.content);

            for (int i = 0; i < activeCase.decisionOptions.Count; i++)
            {
                DecisionOptionData option = activeCase.decisionOptions[i];
                float optionCardHeight = option.justification != null && option.justification.Length > 150 ? 176f : 150f;
                Image decisionCard = UIFactory.CreatePanel(decisionsScroll.content, theme.elevatedSurfaceColor, "DecisionOption");
                UIFactory.AddLayoutElement(decisionCard.gameObject, preferredHeight: optionCardHeight);
                UIFactory.AddVerticalLayout(decisionCard.gameObject, 8, new RectOffset(14, 14, 14, 14), false);

                string flag = option.matchesAlgorithmRecommendation ? "Reforça a leitura do sistema" : "Contraria ou recalibra a leitura automática";
                Text optionTitle = UIFactory.CreateText(decisionCard.transform, option.label, 18, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.AddLayoutElement(optionTitle.gameObject, preferredHeight: 24f);
                Text optionFlag = UIFactory.CreateText(decisionCard.transform, flag, 14, option.matchesAlgorithmRecommendation ? theme.systemAccentColor : theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.AddLayoutElement(optionFlag.gameObject, preferredHeight: 20f);
                Text optionBody = UIFactory.CreateText(decisionCard.transform, option.justification, 15, theme.textSecondaryColor, TextAnchor.UpperLeft);
                UIFactory.AddLayoutElement(optionBody.gameObject, preferredHeight: optionCardHeight - 92f);

                Color actionColor = option.matchesAlgorithmRecommendation ? theme.systemAccentColor : theme.humanAccentColor;
                Color textColor = option.matchesAlgorithmRecommendation ? theme.textPrimaryColor : theme.backgroundColor;
                Button decideButton = UIFactory.CreateButton(decisionCard.transform, "Registrar decisão", actionColor, textColor, 15);
                UIFactory.AddLayoutElement(decideButton.gameObject, preferredHeight: 34f);
                decideButton.onClick.AddListener(() => CommitDecision(option));
            }
        }

        private void BuildProfilePanel(Transform parent, CandidateCaseDefinition caseData)
        {
            Text name = UIFactory.CreateText(parent, caseData.candidateName + ", " + caseData.candidateAge + " anos", 28, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(name.gameObject, preferredHeight: 38f);

            Text context = UIFactory.CreateText(parent, caseData.district + " · " + caseData.interestArea, 16, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(context.gameObject, preferredHeight: 24f);

            UIFactory.CreateDivider(parent, theme.mutedOverlayColor, 1f);

            CreateDataRow(parent, "Oportunidade", caseData.opportunityTitle);
            CreateDataRow(parent, "Objetivo", caseData.applicationObjective);
            CreateDataRow(parent, "Resumo", caseData.profileSummary);
            CreateDataRow(parent, "Rastros visíveis", caseData.publicTraceSummary);

            UIFactory.CreateSpacer(parent, 8f);

            Image notePanel = UIFactory.CreatePanel(parent, theme.elevatedSurfaceColor, "HumanNote");
            UIFactory.AddLayoutElement(notePanel.gameObject, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(notePanel.gameObject, 8, new RectOffset(14, 14, 14, 14), false);
            Text noteTitle = UIFactory.CreateText(notePanel.transform, "Leitura crítica sugerida", 18, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(noteTitle.gameObject, preferredHeight: 24f);
            string suggestion = caseData.investigationLayers.Count > 0
                ? caseData.investigationLayers[0].shortHint
                : "Este perfil ainda não possui camadas contextuais configuradas.";
            Text noteBody = UIFactory.CreateText(notePanel.transform, suggestion, 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(noteBody.gameObject, preferredHeight: 120f);
        }

        private void BuildAlgorithmPanel(Transform parent, CandidateCaseDefinition caseData)
        {
            Image badge = UIFactory.CreatePanel(parent, theme.systemAccentColor, "ScoreBadge");
            UIFactory.AddLayoutElement(badge.gameObject, preferredHeight: 92f);
            UIFactory.AddVerticalLayout(badge.gameObject, 4, new RectOffset(16, 16, 14, 14), false);

            Text badgeKicker = UIFactory.CreateText(badge.transform, "NOTA ALGORÍTMICA", 14, theme.backgroundColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(badgeKicker.gameObject, preferredHeight: 18f);
            Text badgeScore = UIFactory.CreateText(badge.transform, caseData.algorithm.score.ToString("00") + "/100 · prioridade " + caseData.algorithm.recommendedPriority, 28, theme.backgroundColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(badgeScore.gameObject, preferredHeight: 34f);
            Text badgeFit = UIFactory.CreateText(badge.transform, caseData.algorithm.estimatedFit + " · risco " + caseData.algorithm.dropoutRisk, 14, theme.backgroundColor, TextAnchor.MiddleLeft);
            UIFactory.AddLayoutElement(badgeFit.gameObject, preferredHeight: 18f);

            CreateDataRow(parent, "Confiabilidade cadastral", caseData.algorithm.documentReliability);
            CreateDataRow(parent, "Padrão de engajamento", caseData.algorithm.engagementPattern);
            CreateDataRow(parent, "Justificativa automática", caseData.algorithm.automaticJustification);

            if (caseData.algorithm.keywords != null && caseData.algorithm.keywords.Count > 0)
            {
                CreateDataRow(parent, "Marcadores", string.Join(" · ", caseData.algorithm.keywords.ToArray()));
            }

            Image blindSpot = UIFactory.CreatePanel(parent, theme.surfaceColor, "BlindSpot");
            UIFactory.AddLayoutElement(blindSpot.gameObject, preferredHeight: 110f);
            UIFactory.AddVerticalLayout(blindSpot.gameObject, 6, new RectOffset(14, 14, 14, 14), false);
            Text blindTitle = UIFactory.CreateText(blindSpot.transform, "Ponto cego provável", 16, theme.warningColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(blindTitle.gameObject, preferredHeight: 22f);
            Text blindBody = UIFactory.CreateText(blindSpot.transform, caseData.algorithm.blindSpotHint, 15, theme.textSecondaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(blindBody.gameObject, preferredHeight: 54f);
        }

        private void CommitDecision(DecisionOptionData option)
        {
            if (activeCase == null || option == null)
            {
                return;
            }

            int layersRead = 0;
            if (openedLayers != null)
            {
                for (int i = 0; i < openedLayers.Length; i++)
                {
                    if (openedLayers[i])
                    {
                        layersRead++;
                    }
                }
            }

            resolvedCase = activeCase;
            float analysisSeconds = Mathf.Max(0f, Time.unscaledTime - currentCaseStartedAt);
            lastEvaluatedDecision = DesalgoritmizacaoEvaluationEngine.ApplyDecision(session, activeCase, option, analysisSeconds, layersRead, theme);
            ShowResult();
        }

        private void ShowResult()
        {
            currentScreen = ScreenState.Result;
            SetHeaderExitVisible(false);
            bool terminal = HasTerminalOutcome();
            SetHeader("Resultado imediato", "O resultado imediato aparece aqui. O impacto total continua acumulando.");
            LoadScreen("Desalgoritmizacao/Prefabs/Screens/ResultScreen", "ResultScreen");

            PreparePanel("LeftPanel", theme.surfaceColor);
            PreparePanel("RightPanel", theme.surfaceColor);
            PrepareText("LeftPanel/CaseTitle", resolvedCase.candidateName + " · " + lastEvaluatedDecision.option.label, 28, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PrepareText("LeftPanel/Feedback", lastEvaluatedDecision.option.immediateFeedback, 20, theme.textPrimaryColor, TextAnchor.UpperLeft);
            PreparePanel("LeftPanel/ClosingPanel", theme.elevatedSurfaceColor);
            PrepareText("LeftPanel/ClosingPanel/ClosingTitle", "Registro do caso", 20, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PrepareText("LeftPanel/ClosingPanel/ClosingBody", resolvedCase.afterCaseMessage, 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            PrepareText("LeftPanel/ClosingPanel/AnalysisMeta", "Tempo gasto: " + lastEvaluatedDecision.analysisSeconds.ToString("0.0") + "s · camadas abertas: " + lastEvaluatedDecision.layersRead, 16, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PrepareButton("LeftPanel/NextButton", (terminal || session.IsFinished(cases.Count)) ? theme.sessionSummaryTitle : theme.nextCaseButtonLabel, theme.systemAccentColor, theme.textPrimaryColor, 18, () =>
            {
                if (terminal || session.IsFinished(cases.Count))
                {
                    ShowFinalSummary();
                }
                else
                {
                    ShowDashboard();
                }
            });

            PrepareText("RightPanel/MetricsTitle", "Alteração nos indicadores", 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PopulateDeltaSlot("RightPanel/DeltaOperational", "Eficiência operacional", lastEvaluatedDecision.appliedOperationalDelta, session.metrics.operationalEfficiency, theme.systemAccentColor);
            PopulateDeltaSlot("RightPanel/DeltaTrust", "Confiança comunitária", lastEvaluatedDecision.appliedTrustDelta, session.metrics.communityTrust, theme.humanAccentColor);
            PopulateDeltaSlot("RightPanel/DeltaSensitivity", "Sensibilidade do sistema", lastEvaluatedDecision.appliedSensitivityDelta, session.metrics.systemSensitivity, theme.positiveColor);
            PrepareText("RightPanel/Rationale", lastEvaluatedDecision.option.justification, 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
        }

        private void ShowFinalSummary()
        {
            currentScreen = ScreenState.Final;
            finalEvaluation = DesalgoritmizacaoEvaluationEngine.EvaluateFinalState(session);
            EnsureCycleSavedToRanking();
            EndingDefinition ending = FindEnding(finalEvaluation.endingId);
            string headerSubtitle = finalEvaluation.outcome == CycleOutcome.Victory
                ? "Você venceu: os três indicadores chegaram a 100 antes do esgotamento do lote."
                : finalEvaluation.outcome == CycleOutcome.Defeat
                    ? "Você perdeu: um dos indicadores chegou a 0 e encerrou o ciclo."
                    : "O lote da rodada terminou e a síntese do ciclo foi registrada no ranking externo.";
            SetHeader(theme.sessionSummaryTitle, headerSubtitle);
            LoadScreen("Desalgoritmizacao/Prefabs/Screens/FinalScreen", "FinalScreen");
            SetHeaderExitVisible(false);

            PreparePanel("LeftPanel", theme.surfaceColor);
            PreparePanel("RightPanel", theme.surfaceColor);

            PrepareText("LeftPanel/EndingTitle", (finalEvaluation.outcome == CycleOutcome.Victory ? "Vitória · " : finalEvaluation.outcome == CycleOutcome.Defeat ? "Derrota · " : string.Empty) + (ending != null ? ending.title : "Síntese indisponível"), 30, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (ending != null)
            {
                string toneLine = (finalEvaluation.outcome == CycleOutcome.Victory ? "Resultado máximo" : finalEvaluation.outcome == CycleOutcome.Defeat ? "Encerramento crítico" : "Tonalidade do ciclo") + ": " + ending.tone;
                PrepareText("LeftPanel/EndingTone", toneLine, 16, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                PrepareText("LeftPanel/EndingBody", ending.description, 18, theme.textPrimaryColor, TextAnchor.UpperLeft);
            }
            else
            {
                PrepareText("LeftPanel/EndingTone", string.Empty, 16, theme.humanAccentColor, TextAnchor.MiddleLeft);
                PrepareText("LeftPanel/EndingBody", string.Empty, 18, theme.textPrimaryColor, TextAnchor.UpperLeft);
            }

            PopulateMetricSlot("LeftPanel/MetricsStrip/MetricOperational", "Eficiência operacional", session.metrics.operationalEfficiency, theme.systemAccentColor);
            PopulateMetricSlot("LeftPanel/MetricsStrip/MetricTrust", "Confiança comunitária", session.metrics.communityTrust, theme.humanAccentColor);
            PopulateMetricSlot("LeftPanel/MetricsStrip/MetricSensitivity", "Sensibilidade do sistema", session.metrics.systemSensitivity, theme.positiveColor);

            PreparePanel("LeftPanel/SynthesisPanel", theme.elevatedSurfaceColor);
            PrepareText("LeftPanel/SynthesisPanel/SynthesisTitle", "Leitura do comportamento da central", 20, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            PrepareText("LeftPanel/SynthesisPanel/SynthesisBody",
                "Concordância com o algoritmo: " + Mathf.RoundToInt(session.AlgorithmAgreementRatio * 100f) + "%\n" +
                "Camadas contextuais abertas: " + session.totalLayersRead + "\n" +
                "Tempo total investido: " + session.totalAnalysisSeconds.ToString("0.0") + "s",
                17,
                theme.textSecondaryColor,
                TextAnchor.UpperLeft);
            PrepareButton("LeftPanel/RestartButton", theme.restartButtonLabel, theme.systemAccentColor, theme.textPrimaryColor, 18, () =>
            {
                session.ResetFromTheme(theme);
                cases.Clear();
                ShowMenu();
            });

            PrepareText("RightPanel/LogTitle", theme.decisionLogTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            ScrollBinding logScroll = PrepareScroll("RightPanel/LogScroll", theme.elevatedSurfaceColor, theme.elevatedSurfaceColor);
            VerticalLayoutGroup logLayout = EnsureComponent<VerticalLayoutGroup>(logScroll.content.gameObject);
            logLayout.spacing = 10;
            logLayout.padding = new RectOffset(12, 12, 12, 12);
            logLayout.childControlHeight = true;
            logLayout.childControlWidth = true;
            logLayout.childForceExpandHeight = false;
            logLayout.childForceExpandWidth = true;
            ClearChildren(logScroll.content);
            PrepareScrollHint("RightPanel/LogScrollHint");

            for (int i = 0; i < session.decisionHistory.Count; i++)
            {
                CreateDecisionRecordCard(logScroll.content, session.decisionHistory[i]);
            }
        }

        private void CreateDecisionRecordCard(Transform parent, DecisionRecord record)
        {
            float cardHeight = 142f;
            if (!string.IsNullOrEmpty(record.immediateFeedback) && record.immediateFeedback.Length > 140)
            {
                cardHeight = 174f;
            }

            Image card = UIFactory.CreatePanel(parent, theme.surfaceColor, "RecordCard");
            UIFactory.AddLayoutElement(card.gameObject, preferredHeight: cardHeight);
            UIFactory.AddVerticalLayout(card.gameObject, 6, new RectOffset(14, 14, 14, 14), false);

            string title = record.candidateName + " · " + record.decisionLabel;
            Text titleText = UIFactory.CreateText(card.transform, title, 17, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(titleText.gameObject, preferredHeight: 22f);

            Text metaText = UIFactory.CreateText(card.transform,
                record.caseId + " · " +
                "tempo " + record.analysisSeconds.ToString("0.0") + "s" + " · " +
                "camadas " + record.layersRead + (record.matchedAlgorithm ? " · seguiu algoritmo" : " · contrariou algoritmo"),
                13,
                theme.textSecondaryColor,
                TextAnchor.MiddleLeft);
            UIFactory.AddLayoutElement(metaText.gameObject, preferredHeight: 18f);

            Text bodyText = UIFactory.CreateText(card.transform, record.immediateFeedback, 14, theme.textSecondaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(bodyText.gameObject, preferredHeight: cardHeight - 78f);

            string deltas = "Eficiência " + UIFactory.FormatDelta(record.operationalDelta) +
                " · Confiança " + UIFactory.FormatDelta(record.trustDelta) +
                " · Sensibilidade " + UIFactory.FormatDelta(record.sensitivityDelta);
            Text deltaText = UIFactory.CreateText(card.transform, deltas, 14, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(deltaText.gameObject, preferredHeight: 20f);
        }

        private void CreateDataRow(Transform parent, string label, string value)
        {
            float rowHeight = 92f;
            int length = string.IsNullOrEmpty(value) ? 0 : value.Length;
            if (length > 180)
            {
                rowHeight = 152f;
            }
            else if (length > 95)
            {
                rowHeight = 124f;
            }

            Image row = UIFactory.CreatePanel(parent, theme.elevatedSurfaceColor, label + "Row");
            UIFactory.AddLayoutElement(row.gameObject, preferredHeight: rowHeight);
            UIFactory.AddVerticalLayout(row.gameObject, 6, new RectOffset(14, 14, 12, 12), false);

            Text labelText = UIFactory.CreateText(row.transform, label, 14, theme.textSecondaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(labelText.gameObject, preferredHeight: 18f);
            Text valueText = UIFactory.CreateText(row.transform, value, 16, theme.textPrimaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(valueText.gameObject, preferredHeight: rowHeight - 34f);
        }

        private DecisionOptionData FindRecommendedDecision(CandidateCaseDefinition caseData)
        {
            if (caseData == null || caseData.decisionOptions == null)
            {
                return null;
            }

            for (int i = 0; i < caseData.decisionOptions.Count; i++)
            {
                if (caseData.decisionOptions[i] != null && caseData.decisionOptions[i].matchesAlgorithmRecommendation)
                {
                    return caseData.decisionOptions[i];
                }
            }

            return caseData.decisionOptions.Count > 0 ? caseData.decisionOptions[0] : null;
        }

        private EndingDefinition FindEnding(string endingId)
        {
            if (theme != null && theme.endings != null)
            {
                for (int i = 0; i < theme.endings.Count; i++)
                {
                    EndingDefinition ending = theme.endings[i];
                    if (ending != null && ending.endingId == endingId)
                    {
                        return ending;
                    }
                }
            }

            EndingDefinition fallback = new EndingDefinition();
            fallback.endingId = endingId;
            fallback.title = "Leitura final provisória";
            fallback.description = "A configuração do final não foi localizada. Ainda assim, o sistema registrou as métricas do ciclo e deixou a fundação pronta para novas conclusões.";
            fallback.tone = "provisório";
            return fallback;
        }
    }
}
