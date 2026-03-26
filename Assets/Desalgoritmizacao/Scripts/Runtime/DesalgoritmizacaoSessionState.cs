using System;
using System.Collections.Generic;
using Desalgoritmizacao.Data;
using UnityEngine;

namespace Desalgoritmizacao.Runtime
{
    [Serializable]
    public class DecisionRecord
    {
        public string caseId;
        public string candidateName;
        public string decisionLabel;
        public string immediateFeedback;
        public int operationalDelta;
        public int trustDelta;
        public int sensitivityDelta;
        public float analysisSeconds;
        public int layersRead;
        public bool matchedAlgorithm;
    }

    [Serializable]
    public class SessionMetrics
    {
        [Range(0, 100)] public int operationalEfficiency;
        [Range(0, 100)] public int communityTrust;
        [Range(0, 100)] public int systemSensitivity;

        public void ClampAll()
        {
            operationalEfficiency = Mathf.Clamp(operationalEfficiency, 0, 100);
            communityTrust = Mathf.Clamp(communityTrust, 0, 100);
            systemSensitivity = Mathf.Clamp(systemSensitivity, 0, 100);
        }
    }

    public class DesalgoritmizacaoSessionState
    {
        public SessionMetrics metrics = new SessionMetrics();
        public readonly List<DecisionRecord> decisionHistory = new List<DecisionRecord>();

        public int currentCaseIndex;
        public int totalLayersRead;
        public int algorithmAgreementCount;
        public float totalAnalysisSeconds;

        public CandidateCaseDefinition CurrentCase(IReadOnlyList<CandidateCaseDefinition> cases)
        {
            if (cases == null || currentCaseIndex < 0 || currentCaseIndex >= cases.Count)
            {
                return null;
            }

            return cases[currentCaseIndex];
        }

        public void ResetFromTheme(AppThemeConfig theme)
        {
            metrics.operationalEfficiency = theme != null ? theme.startingOperationalEfficiency : 55;
            metrics.communityTrust = theme != null ? theme.startingCommunityTrust : 50;
            metrics.systemSensitivity = theme != null ? theme.startingSystemSensitivity : 35;
            metrics.ClampAll();

            decisionHistory.Clear();
            currentCaseIndex = 0;
            totalLayersRead = 0;
            algorithmAgreementCount = 0;
            totalAnalysisSeconds = 0f;
        }

        public float AlgorithmAgreementRatio
        {
            get
            {
                if (decisionHistory.Count <= 0)
                {
                    return 0f;
                }

                return (float)algorithmAgreementCount / decisionHistory.Count;
            }
        }

        public bool IsFinished(int totalCases)
        {
            return currentCaseIndex >= totalCases;
        }
    }
}
