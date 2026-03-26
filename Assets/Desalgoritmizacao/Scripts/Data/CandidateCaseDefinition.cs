using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desalgoritmizacao.Data
{
    [CreateAssetMenu(fileName = "CaseDefinition", menuName = "Desalgoritmizacao/Case Definition")]
    public class CandidateCaseDefinition : ScriptableObject
    {
        [Header("Identity")]
        public int order = 1;
        public string caseId = "CASO-001";
        public string cycleId = "Ciclo 1";
        public string opportunityTitle = "Programa Local";
        public string candidateName = "Candidata";
        public int candidateAge = 18;
        public string district = "Centro";
        public string interestArea = "Formação";
        public string applicationObjective = "Objetivo da candidatura";
        [TextArea(3, 8)] public string profileSummary = "Resumo inicial do perfil.";
        [TextArea(2, 6)] public string publicTraceSummary = "Como o sistema enxerga os rastros digitais visíveis.";

        [Header("Algorithmic Reading")]
        public AlgorithmReadingData algorithm = new AlgorithmReadingData();

        [Header("Investigation Layers")]
        public List<InvestigationLayerData> investigationLayers = new List<InvestigationLayerData>();

        [Header("Possible Decisions")]
        public List<DecisionOptionData> decisionOptions = new List<DecisionOptionData>();

        [Header("Case Closing")]
        [TextArea(2, 6)] public string afterCaseMessage = "Encerramento do caso.";
    }

    [Serializable]
    public class AlgorithmReadingData
    {
        [Range(0, 100)] public int score = 50;
        public string recommendedPriority = "Média";
        public string estimatedFit = "Compatibilidade parcial";
        public string dropoutRisk = "Moderado";
        public string documentReliability = "Conferível";
        public string engagementPattern = "Irregular";
        public List<string> keywords = new List<string>();
        [TextArea(2, 8)] public string automaticJustification = "Justificativa automática.";
        [TextArea(2, 6)] public string blindSpotHint = "Ponto cego potencial do sistema.";
        public string suggestedDecisionLabel = "Validar recomendação automática";
    }

    [Serializable]
    public class InvestigationLayerData
    {
        public string layerId = "camada";
        public string buttonLabel = "Abrir camada";
        public string shortHint = "Resumo curto do que pode ser encontrado.";
        public string revealTag = "contexto";
        [TextArea(4, 12)] public string content = "Conteúdo detalhado da camada.";
        [Min(0)] public int attentionCost = 1;
        public ImpactDelta readImpact = new ImpactDelta();
    }

    [Serializable]
    public class DecisionOptionData
    {
        public string optionId = "decisao";
        public string label = "Decidir";
        public bool visuallyPrimary = false;
        public bool matchesAlgorithmRecommendation = false;
        [TextArea(2, 8)] public string justification = "Por que essa decisão existe.";
        [TextArea(2, 8)] public string immediateFeedback = "Feedback imediato da decisão.";
        public ImpactDelta impact = new ImpactDelta();
    }

    [Serializable]
    public class ImpactDelta
    {
        public int operationalEfficiency;
        public int communityTrust;
        public int systemSensitivity;
    }
}
