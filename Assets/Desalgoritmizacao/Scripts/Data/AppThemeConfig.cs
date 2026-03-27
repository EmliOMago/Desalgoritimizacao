using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desalgoritmizacao.Data
{
    [CreateAssetMenu(fileName = "AppThemeConfig", menuName = "Desalgoritmizacao/App Theme Config")]
    public class AppThemeConfig : ScriptableObject
    {
        [Header("Project Identity")]
        public string gameTitle = "Desalgoritmizacao";
        public string presentationLine = "Nem toda trajetória cabe no perfil que o sistema enxerga.";
        [TextArea(2, 6)] public string menuIntro = "Você opera uma central de triagem.";
        [TextArea(2, 4)] public string dashboardSummary = "Resumo da central.";

        [Header("Primary Labels")]
        public string startButtonLabel = "Iniciar operação";
        public string queueTitle = "Fila ativa";
        public string beginCaseButtonLabel = "Analisar próximo caso";
        public string profilePanelTitle = "Ficha visível";
        public string algorithmPanelTitle = "Leitura algorítmica";
        public string investigationPanelTitle = "Investigação contextual";
        public string alternativesPanelTitle = "Saídas humanas";
        public string impactPanelTitle = "Registro de impacto";
        public string nextCaseButtonLabel = "Prosseguir";
        public string restartButtonLabel = "Reiniciar ciclo";
        public string quitGameButtonLabel = "Sair do jogo";
        public string unlockInvestigationLabel = "Abrir investigação";
        public string returnToDashboardLabel = "Voltar à central";
        public string sessionSummaryTitle = "Síntese do ciclo";
        public string decisionLogTitle = "Histórico de decisões";
        public string scrollIndicatorLabel = "Role para ver mais";

        [Header("Simulation Defaults")]
        [Range(0, 100)] public int startingOperationalEfficiency = 58;
        [Range(0, 100)] public int startingCommunityTrust = 52;
        [Range(0, 100)] public int startingSystemSensitivity = 34;
        [Min(5f)] public float recommendedAnalysisSeconds = 45f;
        [Min(5f)] public float slowPenaltyIntervalSeconds = 25f;

        [Header("Cycle Rules")]
        [Min(6)] public int casesPerCycle = 20;
        [Min(40)] public int generatedCasePoolSize = 120;
        [Min(1)] public int rankingMaxEntries = 12;
        [Min(1)] public int shuffleSeed = 41031;
        public string defaultOperatorName = "Operador";
        [TextArea(1, 3)] public string victoryRuleLabel = "Vitória: os três indicadores precisam chegar a 100.";
        [TextArea(1, 3)] public string defeatRuleLabel = "Derrota: qualquer indicador chegando a 0 encerra o ciclo.";
        public string rankingTitle = "Registro de ciclos";
        public string rankingSubtitle = "Confiança comunitária define a ordem do painel.";

        [Header("Typography")]
        [Min(0.6f)] public float smallTextScale = 1.0f;
        [Min(0.6f)] public float bodyTextScale = 1.0f;
        [Min(0.6f)] public float headingTextScale = 1.0f;
        [Min(0.6f)] public float titleTextScale = 1.0f;
        [Min(10)] public int minimumReadableFontSize = 12;
        [Min(12)] public int scrollbarWidth = 16;

        [Header("Colors")]
        public Color backgroundColor = new Color(0.07f, 0.08f, 0.11f, 1f);
        public Color surfaceColor = new Color(0.11f, 0.13f, 0.18f, 1f);
        public Color elevatedSurfaceColor = new Color(0.15f, 0.18f, 0.24f, 1f);
        public Color systemAccentColor = new Color(0.25f, 0.67f, 0.98f, 1f);
        public Color humanAccentColor = new Color(0.95f, 0.76f, 0.33f, 1f);
        public Color positiveColor = new Color(0.29f, 0.78f, 0.50f, 1f);
        public Color negativeColor = new Color(0.88f, 0.35f, 0.35f, 1f);
        public Color warningColor = new Color(0.96f, 0.53f, 0.25f, 1f);
        public Color textPrimaryColor = new Color(0.95f, 0.96f, 0.98f, 1f);
        public Color textSecondaryColor = new Color(0.72f, 0.76f, 0.84f, 1f);
        public Color mutedOverlayColor = new Color(0f, 0f, 0f, 0.18f);

        [Header("Ending Catalog")]
        public List<EndingDefinition> endings = new List<EndingDefinition>();

        public int ScaleFont(int baseSize)
        {
            float scale = bodyTextScale;
            if (baseSize >= 28) scale = titleTextScale;
            else if (baseSize >= 20) scale = headingTextScale;
            else if (baseSize <= 14) scale = smallTextScale;

            return Mathf.Max(minimumReadableFontSize, Mathf.RoundToInt(baseSize * scale));
        }
    }

    [Serializable]
    public class EndingDefinition
    {
        public string endingId = "efficiency_sensitive";
        public string title = "Eficiência Sensível";
        [TextArea(3, 8)] public string description = "Descrição do final.";
        public string tone = "equilíbrio";
    }
}
