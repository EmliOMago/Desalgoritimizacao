using Desalgoritmizacao.Data;
using UnityEngine;

namespace Desalgoritmizacao.Runtime
{
    public struct EvaluatedDecision
    {
        public DecisionOptionData option;
        public int appliedOperationalDelta;
        public int appliedTrustDelta;
        public int appliedSensitivityDelta;
        public float analysisSeconds;
        public int layersRead;
        public bool matchedAlgorithm;
    }

    public enum CycleOutcome
    {
        Ongoing,
        Victory,
        Defeat,
        Exhausted
    }

    public struct FinalEvaluation
    {
        public string endingId;
        public int operational;
        public int trust;
        public int sensitivity;
        public float agreementRatio;
        public int totalLayersRead;
        public CycleOutcome outcome;
        public string outcomeLabel;
    }

    public static class DesalgoritmizacaoEvaluationEngine
    {
        public static EvaluatedDecision ApplyDecision(
            DesalgoritmizacaoSessionState state,
            CandidateCaseDefinition currentCase,
            DecisionOptionData option,
            float analysisSeconds,
            int layersRead,
            AppThemeConfig theme)
        {
            EvaluatedDecision evaluated = new EvaluatedDecision
            {
                option = option,
                analysisSeconds = analysisSeconds,
                layersRead = layersRead,
                matchedAlgorithm = option != null && option.matchesAlgorithmRecommendation
            };

            int operationalDelta = option != null ? option.impact.operationalEfficiency : 0;
            int trustDelta = option != null ? option.impact.communityTrust : 0;
            int sensitivityDelta = option != null ? option.impact.systemSensitivity : 0;

            if (theme != null)
            {
                if (analysisSeconds > theme.recommendedAnalysisSeconds)
                {
                    float overtime = analysisSeconds - theme.recommendedAnalysisSeconds;
                    int penalty = Mathf.Clamp(Mathf.CeilToInt(overtime / Mathf.Max(5f, theme.slowPenaltyIntervalSeconds)), 0, 3);
                    operationalDelta -= penalty;
                }
                else if (analysisSeconds <= theme.recommendedAnalysisSeconds * 0.6f && layersRead == 0)
                {
                    operationalDelta += 1;
                }
            }

            if (layersRead >= 2 && option != null && !option.matchesAlgorithmRecommendation)
            {
                sensitivityDelta += 1;
            }

            state.metrics.operationalEfficiency += operationalDelta;
            state.metrics.communityTrust += trustDelta;
            state.metrics.systemSensitivity += sensitivityDelta;
            state.metrics.ClampAll();

            state.totalAnalysisSeconds += Mathf.Max(0f, analysisSeconds);
            state.totalLayersRead += Mathf.Max(0, layersRead);

            if (option != null && option.matchesAlgorithmRecommendation)
            {
                state.algorithmAgreementCount++;
            }

            DecisionRecord record = new DecisionRecord
            {
                caseId = currentCase != null ? currentCase.caseId : "CASO",
                candidateName = currentCase != null ? currentCase.candidateName : "Candidatura",
                decisionLabel = option != null ? option.label : "Sem decisão",
                immediateFeedback = option != null ? option.immediateFeedback : string.Empty,
                operationalDelta = operationalDelta,
                trustDelta = trustDelta,
                sensitivityDelta = sensitivityDelta,
                analysisSeconds = analysisSeconds,
                layersRead = layersRead,
                matchedAlgorithm = option != null && option.matchesAlgorithmRecommendation
            };

            state.decisionHistory.Add(record);
            state.currentCaseIndex++;

            evaluated.appliedOperationalDelta = operationalDelta;
            evaluated.appliedTrustDelta = trustDelta;
            evaluated.appliedSensitivityDelta = sensitivityDelta;
            return evaluated;
        }

        public static FinalEvaluation EvaluateFinal(DesalgoritmizacaoSessionState state, AppThemeConfig theme)
        {
            return EvaluateFinalState(state);
        }

        public static FinalEvaluation EvaluateFinalState(DesalgoritmizacaoSessionState state)
        {
            FinalEvaluation evaluation = new FinalEvaluation
            {
                operational = state.metrics.operationalEfficiency,
                trust = state.metrics.communityTrust,
                sensitivity = state.metrics.systemSensitivity,
                agreementRatio = state.AlgorithmAgreementRatio,
                totalLayersRead = state.totalLayersRead,
                outcome = CycleOutcome.Exhausted,
                outcomeLabel = "Ciclo concluído"
            };

            if (IsVictoryState(state))
            {
                evaluation.endingId = "collective_victory";
                evaluation.outcome = CycleOutcome.Victory;
                evaluation.outcomeLabel = "Vitória";
                return evaluation;
            }

            if (IsDefeatState(state))
            {
                evaluation.endingId = "indicator_collapse";
                evaluation.outcome = CycleOutcome.Defeat;
                evaluation.outcomeLabel = "Derrota";
                return evaluation;
            }

            if (evaluation.operational <= 28)
            {
                evaluation.endingId = "operational_collapse";
                return evaluation;
            }

            if (evaluation.sensitivity >= 78 && evaluation.trust >= 70 && evaluation.operational < 58)
            {
                evaluation.endingId = "radical_humanization";
                return evaluation;
            }

            if (evaluation.operational >= 70 && evaluation.trust <= 42 && evaluation.sensitivity <= 40 && evaluation.agreementRatio >= 0.6f)
            {
                evaluation.endingId = "total_automation";
                return evaluation;
            }

            if (evaluation.agreementRatio >= 0.8f && evaluation.sensitivity <= 38)
            {
                evaluation.endingId = "passive_conformity";
                return evaluation;
            }

            evaluation.endingId = "sensitive_efficiency";
            return evaluation;
        }

        public static bool IsVictoryState(DesalgoritmizacaoSessionState state)
        {
            return state != null &&
                   state.metrics.operationalEfficiency >= 100 &&
                   state.metrics.communityTrust >= 100 &&
                   state.metrics.systemSensitivity >= 100;
        }

        public static bool IsDefeatState(DesalgoritmizacaoSessionState state)
        {
            return state != null &&
                   (state.metrics.operationalEfficiency <= 0 ||
                    state.metrics.communityTrust <= 0 ||
                    state.metrics.systemSensitivity <= 0);
        }
    }
}
