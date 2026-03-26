using System.Collections.Generic;
using Desalgoritmizacao.Data;
using Desalgoritmizacao.Runtime;
using UnityEngine;
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

        private Canvas canvas;
        private RectTransform headerRoot;
        private RectTransform contentRoot;
        private Text headerTitleText;
        private Text headerSubtitleText;
        private Text analysisTimerText;

        private AppThemeConfig theme;
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

        private void Awake()
        {
            LoadData();
            session.ResetFromTheme(theme);
            BuildShell();
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
            cases.Clear();
            CandidateCaseDefinition[] loadedCases = Resources.LoadAll<CandidateCaseDefinition>("Desalgoritmizacao/Cases");
            if (loadedCases != null)
            {
                for (int i = 0; i < loadedCases.Length; i++)
                {
                    if (loadedCases[i] != null)
                    {
                        cases.Add(loadedCases[i]);
                    }
                }
            }

            cases.Sort((a, b) => a.order.CompareTo(b.order));

            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<AppThemeConfig>();
                theme.gameTitle = "Desalgoritmizacao";
                theme.presentationLine = "Nem toda trajetória cabe no perfil que o sistema enxerga.";
                theme.menuIntro = "Tema temporário carregado em memória.";
                theme.dashboardSummary = "A central organiza casos, decisões e impacto acumulado.";
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

            if (cases.Count == 0)
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
                cases.Add(fallback);
            }
        }

        private void BuildShell()
        {
            GameObject canvasGo = null;
            GameObject prefab = Resources.Load<GameObject>("Desalgoritmizacao/Prefabs/DesalgoritmizacaoCanvasShell");
            if (prefab != null)
            {
                canvasGo = Instantiate(prefab, transform);
                canvasGo.name = "DesalgoritmizacaoCanvas";
            }

            if (canvasGo == null)
            {
                canvasGo = new GameObject("DesalgoritmizacaoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
                canvasGo.transform.SetParent(transform, false);

                GameObject headerGo = UIFactory.CreateUIObject("Header", canvasGo.transform);
                headerRoot = headerGo.GetComponent<RectTransform>();
                UIFactory.SetAnchors(headerRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -108f), new Vector2(-24f, -24f));
                headerGo.AddComponent<Image>();

                GameObject headerTitleGo = UIFactory.CreateUIObject("HeaderTitle", headerGo.transform);
                headerTitleText = headerTitleGo.AddComponent<Text>();
                headerTitleText.font = UIFactory.DefaultFont;
                headerTitleText.alignment = TextAnchor.UpperLeft;
                headerTitleText.fontStyle = FontStyle.Bold;
                UIFactory.SetAnchors(headerTitleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.65f, 1f), new Vector2(24f, 16f), new Vector2(-24f, -16f));

                GameObject headerSubtitleGo = UIFactory.CreateUIObject("HeaderSubtitle", headerGo.transform);
                headerSubtitleText = headerSubtitleGo.AddComponent<Text>();
                headerSubtitleText.font = UIFactory.DefaultFont;
                headerSubtitleText.alignment = TextAnchor.LowerLeft;
                UIFactory.SetAnchors(headerSubtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 16f), new Vector2(-24f, -18f));

                GameObject contentGo = UIFactory.CreateUIObject("Content", canvasGo.transform);
                contentRoot = contentGo.GetComponent<RectTransform>();
                UIFactory.SetAnchors(contentRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -124f));
            }

            canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasGo.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasGo.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
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
                Image headerImage = headerRoot.GetComponent<Image>();
                if (headerImage == null)
                {
                    headerImage = headerRoot.gameObject.AddComponent<Image>();
                }
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
        }

        private void ResolveShellNodes(Transform root)
        {
            headerRoot = root.Find("Header") as RectTransform;
            contentRoot = root.Find("Content") as RectTransform;

            Transform title = root.Find("Header/HeaderTitle");
            if (title != null)
            {
                headerTitleText = title.GetComponent<Text>();
            }

            Transform subtitle = root.Find("Header/HeaderSubtitle");
            if (subtitle != null)
            {
                headerSubtitleText = subtitle.GetComponent<Text>();
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

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(contentRoot.GetChild(i).gameObject);
            }
        }

        private void ShowMenu()
        {
            currentScreen = ScreenState.Menu;
            SetHeader(theme.gameTitle, theme.presentationLine);
            ClearContent();

            Image shell = UIFactory.CreatePanel(contentRoot, theme.surfaceColor, "MenuShell");
            UIFactory.Stretch(shell.rectTransform);

            GameObject layoutGo = UIFactory.CreateUIObject("MenuLayout", shell.transform);
            RectTransform layoutRect = layoutGo.GetComponent<RectTransform>();
            UIFactory.SetAnchors(layoutRect, new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.88f), Vector2.zero, Vector2.zero);
            UIFactory.AddVerticalLayout(layoutGo, 18, new RectOffset(36, 36, 36, 36), false);

            Text kicker = UIFactory.CreateText(layoutGo.transform, "Central de triagem local", 18, theme.systemAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(kicker.gameObject, preferredHeight: 28f);

            Text intro = UIFactory.CreateText(layoutGo.transform, theme.menuIntro, 22, theme.textPrimaryColor, TextAnchor.UpperLeft, FontStyle.Normal);
            UIFactory.AddLayoutElement(intro.gameObject, preferredHeight: 260f);

            Image callout = UIFactory.CreatePanel(layoutGo.transform, theme.elevatedSurfaceColor, "Callout");
            UIFactory.AddLayoutElement(callout.gameObject, preferredHeight: 124f);
            UIFactory.AddVerticalLayout(callout.gameObject, 8, new RectOffset(20, 20, 18, 18), false);
            Text calloutTitle = UIFactory.CreateText(callout.transform, "Leitura inicial", 18, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(calloutTitle.gameObject, preferredHeight: 26f);
            Text calloutBody = UIFactory.CreateText(callout.transform, "O algoritmo aparece primeiro. O contexto fica disponível para quem decide investigar.", 17, theme.textSecondaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(calloutBody.gameObject, preferredHeight: 72f);

            UIFactory.CreateSpacer(layoutGo.transform, 6f);

            Button startButton = UIFactory.CreateButton(layoutGo.transform, theme.startButtonLabel, theme.systemAccentColor, theme.textPrimaryColor, 20);
            UIFactory.AddLayoutElement(startButton.gameObject, preferredHeight: 58f);
            startButton.onClick.AddListener(() =>
            {
                session.ResetFromTheme(theme);
                ShowDashboard();
            });
        }

        private void ShowDashboard()
        {
            currentScreen = ScreenState.Dashboard;
            activeCase = session.CurrentCase(cases);
            SetHeader("Central de triagem", theme.dashboardSummary);
            ClearContent();

            GameObject root = UIFactory.CreateUIObject("DashboardRoot", contentRoot);
            UIFactory.Stretch(root.GetComponent<RectTransform>());
            UIFactory.AddHorizontalLayout(root, 18, new RectOffset(0, 0, 0, 0), true);

            Image left = UIFactory.CreatePanel(root.transform, theme.surfaceColor, "LeftPanel");
            UIFactory.AddLayoutElement(left.gameObject, preferredWidth: 1020f, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(left.gameObject, 14, new RectOffset(22, 22, 22, 22), false);

            Image right = UIFactory.CreatePanel(root.transform, theme.surfaceColor, "RightPanel");
            UIFactory.AddLayoutElement(right.gameObject, preferredWidth: 480f, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(right.gameObject, 14, new RectOffset(22, 22, 22, 22), false);

            Text intro = UIFactory.CreateText(left.transform, "A fila desta etapa foi organizada em torno de perfis que parecem simples para o sistema, mas exigem leitura mais cuidadosa para não reproduzir exclusões invisíveis.", 18, theme.textPrimaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(intro.gameObject, preferredHeight: 72f);

            CreateMetricsStrip(left.transform);

            Image queuePanel = UIFactory.CreatePanel(left.transform, theme.elevatedSurfaceColor, "QueuePanel");
            UIFactory.AddLayoutElement(queuePanel.gameObject, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(queuePanel.gameObject, 10, new RectOffset(18, 18, 18, 18), false);

            Text queueTitle = UIFactory.CreateText(queuePanel.transform, theme.queueTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(queueTitle.gameObject, preferredHeight: 32f);

            Text queueHint = UIFactory.CreateText(queuePanel.transform, "Abra um caso por vez. A barra lateral mostra o restante da fila.", 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(queueHint.gameObject, preferredHeight: 58f);

            ScrollRect queueScroll = UIFactory.CreateScrollView(queuePanel.transform, theme.surfaceColor, theme.surfaceColor, out RectTransform queueContent);
            UIFactory.AddLayoutElement(queueScroll.gameObject, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(queueContent.gameObject, 10, new RectOffset(4, 4, 4, 4), false);
            CreateScrollHint(queuePanel.transform);

            for (int i = session.currentCaseIndex; i < cases.Count; i++)
            {
                CandidateCaseDefinition caseData = cases[i];
                Image card = UIFactory.CreatePanel(queueContent.transform, theme.surfaceColor, "CaseCard");
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

            Text impactTitle = UIFactory.CreateText(right.transform, theme.impactPanelTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(impactTitle.gameObject, preferredHeight: 30f);

            Text impactHint = UIFactory.CreateText(right.transform, "Cada decisão altera os três indicadores centrais.", 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(impactHint.gameObject, preferredHeight: 52f);

            ScrollRect logScroll = UIFactory.CreateScrollView(right.transform, theme.elevatedSurfaceColor, theme.elevatedSurfaceColor, out RectTransform logContent);
            UIFactory.AddLayoutElement(logScroll.gameObject, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(logContent.gameObject, 10, new RectOffset(14, 14, 14, 14), false);
            CreateScrollHint(right.transform);

            if (session.decisionHistory.Count == 0)
            {
                Image empty = UIFactory.CreatePanel(logContent.transform, theme.surfaceColor, "EmptyState");
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
                    CreateDecisionRecordCard(logContent.transform, session.decisionHistory[i]);
                }
            }
        }

        private void ShowCase(bool resetCaseState)
        {
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
            ClearContent();

            GameObject root = UIFactory.CreateUIObject("CaseRoot", contentRoot);
            UIFactory.Stretch(root.GetComponent<RectTransform>());
            UIFactory.AddHorizontalLayout(root, 18, new RectOffset(0, 0, 0, 0), true);

            Image profilePanel = UIFactory.CreatePanel(root.transform, theme.surfaceColor, "ProfilePanel");
            UIFactory.AddLayoutElement(profilePanel.gameObject, preferredWidth: 500f, flexibleHeight: 1f);

            Image algorithmPanel = UIFactory.CreatePanel(root.transform, theme.elevatedSurfaceColor, "AlgorithmPanel");
            UIFactory.AddLayoutElement(algorithmPanel.gameObject, preferredWidth: 420f, flexibleHeight: 1f);

            Image investigationPanel = UIFactory.CreatePanel(root.transform, theme.surfaceColor, "InvestigationPanel");
            UIFactory.AddLayoutElement(investigationPanel.gameObject, preferredWidth: 620f, flexibleHeight: 1f);

            ScrollRect profileScroll = UIFactory.CreateScrollView(profilePanel.transform, theme.surfaceColor, theme.surfaceColor, out RectTransform profileContent);
            UIFactory.Stretch(profileScroll.GetComponent<RectTransform>());
            UIFactory.AddVerticalLayout(profileContent.gameObject, 10, new RectOffset(18, 18, 18, 18), false);
            CreateScrollHint(profilePanel.transform);

            ScrollRect algorithmScroll = UIFactory.CreateScrollView(algorithmPanel.transform, theme.elevatedSurfaceColor, theme.elevatedSurfaceColor, out RectTransform algorithmContent);
            UIFactory.Stretch(algorithmScroll.GetComponent<RectTransform>());
            UIFactory.AddVerticalLayout(algorithmContent.gameObject, 10, new RectOffset(18, 18, 18, 18), false);
            CreateScrollHint(algorithmPanel.transform);

            ScrollRect investigationScroll = UIFactory.CreateScrollView(investigationPanel.transform, theme.surfaceColor, theme.surfaceColor, out RectTransform investigationContent);
            UIFactory.Stretch(investigationScroll.GetComponent<RectTransform>());
            UIFactory.AddVerticalLayout(investigationContent.gameObject, 12, new RectOffset(18, 18, 18, 18), false);
            CreateScrollHint(investigationPanel.transform);

            BuildProfilePanel(profileContent.transform, activeCase);
            BuildAlgorithmPanel(algorithmContent.transform, activeCase);
            BuildInvestigationPanel(investigationContent.transform, activeCase);
        }

        private void BuildProfilePanel(Transform parent, CandidateCaseDefinition caseData)
        {
            Text title = UIFactory.CreateText(parent, theme.profilePanelTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(title.gameObject, preferredHeight: 32f);

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
            Text title = UIFactory.CreateText(parent, theme.algorithmPanelTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(title.gameObject, preferredHeight: 32f);

            Image badge = UIFactory.CreatePanel(parent, theme.systemAccentColor, "ScoreBadge");
            UIFactory.AddLayoutElement(badge.gameObject, preferredHeight: 92f);
            UIFactory.AddVerticalLayout(badge.gameObject, 4, new RectOffset(16, 16, 14, 14), false);

            Text badgeKicker = UIFactory.CreateText(badge.transform, "NOTA ALGORÍTMICA", 14, theme.backgroundColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(badgeKicker.gameObject, preferredHeight: 18f);
            Text badgeScore = UIFactory.CreateText(badge.transform, caseData.algorithm.score.ToString("00") + "/100 · prioridade " + caseData.algorithm.recommendedPriority, 28, theme.backgroundColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(badgeScore.gameObject, preferredHeight: 34f);
            Text badgeFit = UIFactory.CreateText(badge.transform, caseData.algorithm.estimatedFit + " · risco " + caseData.algorithm.dropoutRisk, 14, theme.backgroundColor, TextAnchor.MiddleLeft);
            UIFactory.AddLayoutElement(badgeFit.gameObject, preferredHeight: 18f);

            analysisTimerText = UIFactory.CreateText(parent, string.Empty, 15, theme.textSecondaryColor, TextAnchor.MiddleLeft);
            UIFactory.AddLayoutElement(analysisTimerText.gameObject, preferredHeight: 20f);

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

            DecisionOptionData recommended = FindRecommendedDecision(caseData);
            if (recommended != null)
            {
                Button autoButton = UIFactory.CreateButton(parent, caseData.algorithm.suggestedDecisionLabel, theme.systemAccentColor, theme.textPrimaryColor, 18);
                UIFactory.AddLayoutElement(autoButton.gameObject, preferredHeight: 50f);
                autoButton.onClick.AddListener(() => CommitDecision(recommended));
            }

            Button unlockButton = UIFactory.CreateButton(parent, theme.unlockInvestigationLabel, theme.humanAccentColor, theme.backgroundColor, 18);
            UIFactory.AddLayoutElement(unlockButton.gameObject, preferredHeight: 46f);
            unlockButton.onClick.AddListener(() =>
            {
                investigationUnlocked = true;
                if (selectedLayerIndex < 0 && activeCase != null && activeCase.investigationLayers.Count > 0)
                {
                    selectedLayerIndex = 0;
                }
                ShowCase(false);
            });
        }

        private void BuildInvestigationPanel(Transform parent, CandidateCaseDefinition caseData)
        {
            Text title = UIFactory.CreateText(parent, theme.investigationPanelTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(title.gameObject, preferredHeight: 32f);

            if (!investigationUnlocked)
            {
                Image locked = UIFactory.CreatePanel(parent, theme.elevatedSurfaceColor, "LockedInvestigation");
                UIFactory.AddLayoutElement(locked.gameObject, flexibleHeight: 1f);
                UIFactory.AddVerticalLayout(locked.gameObject, 10, new RectOffset(20, 20, 20, 20), false);
                Text lockedTitle = UIFactory.CreateText(locked.transform, "Investigação ainda fechada", 22, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.AddLayoutElement(lockedTitle.gameObject, preferredHeight: 30f);
                Text lockedBody = UIFactory.CreateText(locked.transform, "A investigação existe, mas fica fora do fluxo mais confortável da interface.", 17, theme.textSecondaryColor, TextAnchor.UpperLeft);
                UIFactory.AddLayoutElement(lockedBody.gameObject, preferredHeight: 96f);
                if (caseData.investigationLayers.Count > 0)
                {
                    Text teaser = UIFactory.CreateText(locked.transform, "Primeiro indício: " + caseData.investigationLayers[0].shortHint, 16, theme.textPrimaryColor, TextAnchor.UpperLeft, FontStyle.Italic);
                    UIFactory.AddLayoutElement(teaser.gameObject, preferredHeight: 64f);
                }
                Button unlockButton = UIFactory.CreateButton(locked.transform, theme.unlockInvestigationLabel, theme.humanAccentColor, theme.backgroundColor, 18);
                UIFactory.AddLayoutElement(unlockButton.gameObject, preferredHeight: 46f);
                unlockButton.onClick.AddListener(() =>
                {
                    investigationUnlocked = true;
                    if (selectedLayerIndex < 0 && caseData.investigationLayers.Count > 0)
                    {
                        selectedLayerIndex = 0;
                    }
                    ShowCase(false);
                });
                return;
            }

            Text helper = UIFactory.CreateText(parent, "Estas camadas pedem leitura ativa e mudam a interpretação do caso.", 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(helper.gameObject, preferredHeight: 58f);

            GameObject tabsRow = UIFactory.CreateUIObject("TabsRow", parent);
            UIFactory.AddLayoutElement(tabsRow, preferredHeight: 42f);
            HorizontalLayoutGroup tabsLayout = UIFactory.AddHorizontalLayout(tabsRow, 8, new RectOffset(0, 0, 0, 0), false);
            tabsLayout.childAlignment = TextAnchor.MiddleLeft;

            for (int i = 0; i < caseData.investigationLayers.Count; i++)
            {
                int closureIndex = i;
                InvestigationLayerData layer = caseData.investigationLayers[i];
                bool selected = closureIndex == selectedLayerIndex;
                Color buttonColor = selected ? theme.humanAccentColor : theme.elevatedSurfaceColor;
                Color textColor = selected ? theme.backgroundColor : theme.textPrimaryColor;
                Button layerButton = UIFactory.CreateButton(tabsRow.transform, layer.buttonLabel, buttonColor, textColor, 14);
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

            Image contentPanel = UIFactory.CreatePanel(parent, theme.elevatedSurfaceColor, "LayerContent");
            UIFactory.AddLayoutElement(contentPanel.gameObject, preferredHeight: 290f);
            UIFactory.AddVerticalLayout(contentPanel.gameObject, 10, new RectOffset(16, 16, 16, 16), false);

            if (selectedLayerIndex >= 0 && selectedLayerIndex < caseData.investigationLayers.Count)
            {
                InvestigationLayerData layer = caseData.investigationLayers[selectedLayerIndex];
                openedLayers[selectedLayerIndex] = true;

                Text layerTitle = UIFactory.CreateText(contentPanel.transform, layer.buttonLabel + " · " + layer.revealTag, 20, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.AddLayoutElement(layerTitle.gameObject, preferredHeight: 28f);
                Text layerHint = UIFactory.CreateText(contentPanel.transform, layer.shortHint, 15, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Italic);
                UIFactory.AddLayoutElement(layerHint.gameObject, preferredHeight: 24f);
                Text layerBody = UIFactory.CreateText(contentPanel.transform, layer.content, 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
                UIFactory.AddLayoutElement(layerBody.gameObject, preferredHeight: 190f);
            }

            Text decisionsTitle = UIFactory.CreateText(parent, theme.alternativesPanelTitle, 20, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(decisionsTitle.gameObject, preferredHeight: 26f);

            ScrollRect decisionsScroll = UIFactory.CreateScrollView(parent, theme.surfaceColor, theme.surfaceColor, out RectTransform decisionsContent);
            UIFactory.AddLayoutElement(decisionsScroll.gameObject, preferredHeight: 360f);
            UIFactory.AddVerticalLayout(decisionsContent.gameObject, 10, new RectOffset(12, 12, 12, 12), false);
            CreateScrollHint(parent);

            for (int i = 0; i < caseData.decisionOptions.Count; i++)
            {
                DecisionOptionData option = caseData.decisionOptions[i];
                float optionCardHeight = option.justification != null && option.justification.Length > 150 ? 176f : 150f;
                Image decisionCard = UIFactory.CreatePanel(decisionsContent.transform, theme.elevatedSurfaceColor, "DecisionOption");
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
            SetHeader("Resultado imediato", "O resultado imediato aparece aqui. O impacto total continua acumulando.");
            ClearContent();

            GameObject root = UIFactory.CreateUIObject("ResultRoot", contentRoot);
            UIFactory.Stretch(root.GetComponent<RectTransform>());
            UIFactory.AddHorizontalLayout(root, 18, new RectOffset(0, 0, 0, 0), true);

            Image left = UIFactory.CreatePanel(root.transform, theme.surfaceColor, "ResultMain");
            UIFactory.AddLayoutElement(left.gameObject, preferredWidth: 980f, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(left.gameObject, 12, new RectOffset(22, 22, 22, 22), false);

            Image right = UIFactory.CreatePanel(root.transform, theme.surfaceColor, "ResultMetrics");
            UIFactory.AddLayoutElement(right.gameObject, preferredWidth: 520f, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(right.gameObject, 12, new RectOffset(22, 22, 22, 22), false);

            Text caseTitle = UIFactory.CreateText(left.transform, resolvedCase.candidateName + " · " + lastEvaluatedDecision.option.label, 28, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(caseTitle.gameObject, preferredHeight: 40f);

            Text feedback = UIFactory.CreateText(left.transform, lastEvaluatedDecision.option.immediateFeedback, 20, theme.textPrimaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(feedback.gameObject, preferredHeight: 100f);

            Image closing = UIFactory.CreatePanel(left.transform, theme.elevatedSurfaceColor, "Closing");
            UIFactory.AddLayoutElement(closing.gameObject, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(closing.gameObject, 10, new RectOffset(18, 18, 18, 18), false);
            Text closingTitle = UIFactory.CreateText(closing.transform, "Registro do caso", 20, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(closingTitle.gameObject, preferredHeight: 28f);
            Text closingBody = UIFactory.CreateText(closing.transform, resolvedCase.afterCaseMessage, 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(closingBody.gameObject, preferredHeight: 120f);
            Text analysisMeta = UIFactory.CreateText(closing.transform, "Tempo gasto: " + lastEvaluatedDecision.analysisSeconds.ToString("0.0") + "s · camadas abertas: " + lastEvaluatedDecision.layersRead, 16, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(analysisMeta.gameObject, preferredHeight: 24f);

            Button nextButton = UIFactory.CreateButton(left.transform, session.IsFinished(cases.Count) ? theme.sessionSummaryTitle : theme.nextCaseButtonLabel, theme.systemAccentColor, theme.textPrimaryColor, 18);
            UIFactory.AddLayoutElement(nextButton.gameObject, preferredHeight: 50f);
            nextButton.onClick.AddListener(() =>
            {
                if (session.IsFinished(cases.Count))
                {
                    ShowFinalSummary();
                }
                else
                {
                    ShowDashboard();
                }
            });

            Text metricsTitle = UIFactory.CreateText(right.transform, "Alteração nos indicadores", 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(metricsTitle.gameObject, preferredHeight: 32f);

            CreateDeltaCard(right.transform, "Eficiência operacional", lastEvaluatedDecision.appliedOperationalDelta, session.metrics.operationalEfficiency, theme.systemAccentColor);
            CreateDeltaCard(right.transform, "Confiança comunitária", lastEvaluatedDecision.appliedTrustDelta, session.metrics.communityTrust, theme.humanAccentColor);
            CreateDeltaCard(right.transform, "Sensibilidade do sistema", lastEvaluatedDecision.appliedSensitivityDelta, session.metrics.systemSensitivity, theme.positiveColor);

            Text rationale = UIFactory.CreateText(right.transform, lastEvaluatedDecision.option.justification, 16, theme.textSecondaryColor, TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(rationale.gameObject, preferredHeight: 140f);
        }

        private void ShowFinalSummary()
        {
            currentScreen = ScreenState.Final;
            finalEvaluation = DesalgoritmizacaoEvaluationEngine.EvaluateFinalState(session);
            EndingDefinition ending = FindEnding(finalEvaluation.endingId);

            SetHeader(theme.sessionSummaryTitle, "O ciclo termina. Suas decisões definem o rumo da plataforma.");
            ClearContent();

            GameObject root = UIFactory.CreateUIObject("FinalRoot", contentRoot);
            UIFactory.Stretch(root.GetComponent<RectTransform>());
            UIFactory.AddHorizontalLayout(root, 18, new RectOffset(0, 0, 0, 0), true);

            Image left = UIFactory.CreatePanel(root.transform, theme.surfaceColor, "FinalLeft");
            UIFactory.AddLayoutElement(left.gameObject, preferredWidth: 980f, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(left.gameObject, 14, new RectOffset(22, 22, 22, 22), false);

            Image right = UIFactory.CreatePanel(root.transform, theme.surfaceColor, "FinalRight");
            UIFactory.AddLayoutElement(right.gameObject, preferredWidth: 520f, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(right.gameObject, 12, new RectOffset(22, 22, 22, 22), false);

            Text endingTitle = UIFactory.CreateText(left.transform, ending != null ? ending.title : "Síntese indisponível", 30, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(endingTitle.gameObject, preferredHeight: 42f);

            if (ending != null)
            {
                Text endingTone = UIFactory.CreateText(left.transform, "Tonalidade do ciclo: " + ending.tone, 16, theme.humanAccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.AddLayoutElement(endingTone.gameObject, preferredHeight: 22f);
                Text endingBody = UIFactory.CreateText(left.transform, ending.description, 18, theme.textPrimaryColor, TextAnchor.UpperLeft);
                UIFactory.AddLayoutElement(endingBody.gameObject, preferredHeight: 120f);
            }

            CreateMetricsStrip(left.transform);

            Image synthesis = UIFactory.CreatePanel(left.transform, theme.elevatedSurfaceColor, "FinalSynthesis");
            UIFactory.AddLayoutElement(synthesis.gameObject, preferredHeight: 156f);
            UIFactory.AddVerticalLayout(synthesis.gameObject, 8, new RectOffset(18, 18, 18, 18), false);
            Text synthesisTitle = UIFactory.CreateText(synthesis.transform, "Leitura do comportamento da central", 20, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(synthesisTitle.gameObject, preferredHeight: 28f);
            Text synthesisBody = UIFactory.CreateText(synthesis.transform,
                "Concordância com o algoritmo: " + Mathf.RoundToInt(session.AlgorithmAgreementRatio * 100f) + "%\n" +
                "Camadas contextuais abertas: " + session.totalLayersRead + "\n" +
                "Tempo total investido: " + session.totalAnalysisSeconds.ToString("0.0") + "s",
                17,
                theme.textSecondaryColor,
                TextAnchor.UpperLeft);
            UIFactory.AddLayoutElement(synthesisBody.gameObject, preferredHeight: 92f);

            Button restartButton = UIFactory.CreateButton(left.transform, theme.restartButtonLabel, theme.systemAccentColor, theme.textPrimaryColor, 18);
            UIFactory.AddLayoutElement(restartButton.gameObject, preferredHeight: 50f);
            restartButton.onClick.AddListener(() =>
            {
                session.ResetFromTheme(theme);
                ShowMenu();
            });

            Text logTitle = UIFactory.CreateText(right.transform, theme.decisionLogTitle, 22, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(logTitle.gameObject, preferredHeight: 30f);

            ScrollRect logScroll = UIFactory.CreateScrollView(right.transform, theme.elevatedSurfaceColor, theme.elevatedSurfaceColor, out RectTransform logContent);
            UIFactory.AddLayoutElement(logScroll.gameObject, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(logContent.gameObject, 10, new RectOffset(12, 12, 12, 12), false);
            CreateScrollHint(right.transform);

            for (int i = 0; i < session.decisionHistory.Count; i++)
            {
                CreateDecisionRecordCard(logContent.transform, session.decisionHistory[i]);
            }
        }

        private void CreateScrollHint(Transform parent)
        {
            if (theme == null || string.IsNullOrWhiteSpace(theme.scrollIndicatorLabel))
            {
                return;
            }

            Text hint = UIFactory.CreateText(parent, "↓ " + theme.scrollIndicatorLabel, 13, theme.textSecondaryColor, TextAnchor.MiddleRight, FontStyle.Italic);
            UIFactory.AddLayoutElement(hint.gameObject, preferredHeight: 18f);
        }

        private void CreateMetricsStrip(Transform parent)
        {
            GameObject strip = UIFactory.CreateUIObject("MetricsStrip", parent);
            UIFactory.AddLayoutElement(strip, preferredHeight: 124f);
            UIFactory.AddHorizontalLayout(strip, 12, new RectOffset(0, 0, 0, 0), true);

            CreateMetricCard(strip.transform, "Eficiência operacional", session.metrics.operationalEfficiency, theme.systemAccentColor);
            CreateMetricCard(strip.transform, "Confiança comunitária", session.metrics.communityTrust, theme.humanAccentColor);
            CreateMetricCard(strip.transform, "Sensibilidade do sistema", session.metrics.systemSensitivity, theme.positiveColor);
        }

        private void CreateMetricCard(Transform parent, string label, int value, Color accentColor)
        {
            Image card = UIFactory.CreatePanel(parent, theme.elevatedSurfaceColor, "MetricCard");
            UIFactory.AddLayoutElement(card.gameObject, preferredHeight: 118f, flexibleHeight: 1f);
            UIFactory.AddVerticalLayout(card.gameObject, 8, new RectOffset(14, 14, 14, 14), false);

            Text labelText = UIFactory.CreateText(card.transform, label, 16, theme.textSecondaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(labelText.gameObject, preferredHeight: 20f);

            Text valueText = UIFactory.CreateText(card.transform, value.ToString("00") + "/100", 28, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(valueText.gameObject, preferredHeight: 30f);

            Image barBack = UIFactory.CreatePanel(card.transform, theme.surfaceColor, "BarBack");
            UIFactory.AddLayoutElement(barBack.gameObject, preferredHeight: 18f);
            RectTransform barBackRect = barBack.rectTransform;

            Image fill = UIFactory.CreatePanel(barBack.transform, accentColor, "BarFill");
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(value / 100f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        private void CreateDeltaCard(Transform parent, string label, int delta, int currentValue, Color accentColor)
        {
            Image card = UIFactory.CreatePanel(parent, theme.elevatedSurfaceColor, "DeltaCard");
            UIFactory.AddLayoutElement(card.gameObject, preferredHeight: 98f);
            UIFactory.AddVerticalLayout(card.gameObject, 6, new RectOffset(16, 16, 14, 14), false);

            Text labelText = UIFactory.CreateText(card.transform, label, 18, theme.textPrimaryColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(labelText.gameObject, preferredHeight: 24f);

            string deltaText = UIFactory.FormatDelta(delta) + " · total " + currentValue.ToString("00") + "/100";
            Color deltaColor = delta >= 0 ? accentColor : theme.negativeColor;
            Text valueText = UIFactory.CreateText(card.transform, deltaText, 20, deltaColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AddLayoutElement(valueText.gameObject, preferredHeight: 28f);
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
