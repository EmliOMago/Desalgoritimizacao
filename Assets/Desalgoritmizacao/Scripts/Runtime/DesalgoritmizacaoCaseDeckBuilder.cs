using System;
using System.Collections.Generic;
using Desalgoritmizacao.Data;
using UnityEngine;

namespace Desalgoritmizacao.Runtime
{
    public static class DesalgoritmizacaoCaseDeckBuilder
    {
        private static readonly string[] FirstNames =
        {
            "Adriana", "Afonso", "Aline", "Amanda", "Anderson", "Andreia", "Bárbara", "Bianca", "Bruno", "Caio", "Camila", "Carolina",
            "Cecília", "Cristiano", "Daiane", "Danilo", "Débora", "Eduarda", "Eliane", "Fabiana", "Felipe", "Gabriel", "Giovana", "Gustavo",
            "Helena", "Igor", "Isabela", "Jéssica", "Joana", "Jonas", "Juliana", "Karen", "Leonardo", "Letícia", "Lívia", "Marcela",
            "Marcos", "Mateus", "Natália", "Otávio", "Patrícia", "Priscila", "Renata", "Ricardo", "Rodrigo", "Sabrina", "Talita", "Vanessa"
        };

        private static readonly string[] LastNames =
        {
            "Almeida", "Barbosa", "Cardoso", "Costa", "Dias", "Faria", "Ferreira", "Gomes", "Lopes", "Martins", "Mendes", "Moraes",
            "Nascimento", "Oliveira", "Pacheco", "Pereira", "Ramos", "Rezende", "Rocha", "Santos", "Silva", "Souza", "Teixeira", "Vieira"
        };

        private static readonly string[] Districts =
        {
            "Jardim Horizonte", "Vila Aurora", "Parque das Flores", "Santa Clara", "Residencial União", "Boa Esperança", "Vila Progresso",
            "Jardim das Palmeiras", "Cidade Nova", "Nova Aliança", "Parque do Sol", "Vila Esperança"
        };

        private static readonly string[] InterestAreas =
        {
            "tecnologia cidadã", "economia solidária", "comunicação comunitária", "cultura digital", "gestão de pequenos negócios",
            "logística local", "cuidado comunitário", "produção cultural", "design social", "serviços urbanos"
        };

        private static readonly string[] OpportunityTitles =
        {
            "Laboratório de Mídias do Território", "Bolsa de Qualificação em Serviços Locais", "Programa de Formação em Economia Comunitária",
            "Residência de Comunicação de Bairro", "Ciclo de Capacitação em Tecnologia de Proximidade", "Programa de Apoio a Redes de Cuidado",
            "Trilha de Aprendizagem para Produção Cultural", "Incubadora de Soluções Urbanas"
        };

        private static readonly string[] Objectives =
        {
            "buscar renda estável sem abandonar o cuidado familiar", "transformar experiência informal em oportunidade reconhecida",
            "acessar formação curta para fortalecer uma prática já existente", "conseguir entrada em uma rede local de trabalho e apoio",
            "aprender ferramentas que ampliem autonomia no território", "evitar abandonar um percurso iniciado de forma autônoma"
        };

        private static readonly string[] ProfileAdditions =
        {
            "Também organiza tarefas domésticas em turnos irregulares e depende de deslocamentos longos por transporte público.",
            "Participa de iniciativas do bairro, mas quase nunca consegue documentar formalmente essas participações.",
            "Tem experiência prática acumulada fora de plataformas reconhecidas pela leitura automática.",
            "Interrompeu estudos anteriormente por pressão financeira, mas manteve atuação constante em redes locais.",
            "Divide equipamentos com familiares e prioriza demandas urgentes antes de qualquer atividade formativa."
        };

        private static readonly string[] TraceAdditions =
        {
            "Os registros digitais aparecem fragmentados entre perfis antigos, contas emprestadas e períodos de baixa conectividade.",
            "A plataforma lê lacunas de atualização como descontinuidade, sem distinguir falta de acesso de falta de interesse.",
            "Boa parte do histórico útil existe em mensagens privadas, grupos locais e registros que o sistema não indexa.",
            "Os poucos rastros públicos concentram momentos de urgência e não representam a regularidade da atuação cotidiana."
        };

        private static readonly string[] LayerAdditions =
        {
            "Esse detalhe altera a leitura do caso porque mostra capacidade real fora do padrão de documentação esperado.",
            "A camada contextual revela que a inconsistência digital não coincide com ausência de compromisso.",
            "Ao considerar o território, a interpretação automática perde parte de sua aparente neutralidade.",
            "A informação adiciona uma dimensão de cuidado e acesso desigual que o algoritmo não consegue representar sozinho."
        };

        private static readonly string[] FeedbackAdditions =
        {
            "A decisão repercute rapidamente entre pessoas que já conviviam com filtros invisíveis de plataforma.",
            "No registro institucional, o caso passa a ser referência sobre como contexto pode alterar a leitura do sistema.",
            "O efeito imediato reorganiza a percepção de legitimidade da central no território.",
            "A escolha redefine o quanto a triagem parece aberta a trajetórias menos padronizadas."
        };

        public static List<CandidateCaseDefinition> BuildExpandedPool(IReadOnlyList<CandidateCaseDefinition> templates, int desiredCount, int seed)
        {
            List<CandidateCaseDefinition> result = new List<CandidateCaseDefinition>();
            if (templates == null || templates.Count == 0)
            {
                return result;
            }

            System.Random random = new System.Random(seed);
            int target = Mathf.Max(templates.Count, desiredCount);
            for (int i = 0; i < target; i++)
            {
                CandidateCaseDefinition template = templates[i % templates.Count];
                CandidateCaseDefinition clone = UnityEngine.Object.Instantiate(template);
                clone.name = "RuntimeCase_" + (i + 1).ToString("000");
                ApplyVariant(clone, template, i, random);
                result.Add(clone);
            }

            return result;
        }

        private static void ApplyVariant(CandidateCaseDefinition clone, CandidateCaseDefinition template, int sequenceIndex, System.Random random)
        {
            clone.order = sequenceIndex + 1;
            clone.caseId = "CASO-" + (sequenceIndex + 1).ToString("000");
            clone.cycleId = "Ciclo variável";
            clone.candidateName = ComposeName(sequenceIndex, random);
            clone.candidateAge = 18 + (sequenceIndex % 19);
            clone.district = Pick(Districts, sequenceIndex + random.Next(0, Districts.Length));
            clone.interestArea = Pick(InterestAreas, sequenceIndex + random.Next(0, InterestAreas.Length));
            clone.opportunityTitle = Pick(OpportunityTitles, sequenceIndex + random.Next(0, OpportunityTitles.Length));
            clone.applicationObjective = UppercaseFirst(Pick(Objectives, sequenceIndex + random.Next(0, Objectives.Length)));

            clone.profileSummary = template.profileSummary.TrimEnd() + " " + Pick(ProfileAdditions, sequenceIndex + random.Next(0, ProfileAdditions.Length));
            clone.publicTraceSummary = template.publicTraceSummary.TrimEnd() + " " + Pick(TraceAdditions, sequenceIndex + random.Next(0, TraceAdditions.Length));
            clone.afterCaseMessage = template.afterCaseMessage.TrimEnd() + " " + Pick(FeedbackAdditions, sequenceIndex + random.Next(0, FeedbackAdditions.Length));

            int scoreDelta = random.Next(-10, 11);
            clone.algorithm.score = Mathf.Clamp(template.algorithm.score + scoreDelta, 18, 92);
            clone.algorithm.recommendedPriority = ResolvePriority(clone.algorithm.score);
            clone.algorithm.estimatedFit = clone.algorithm.score >= 70 ? "Compatibilidade promissora" : (clone.algorithm.score >= 45 ? "Compatibilidade condicionada" : "Compatibilidade incerta");
            clone.algorithm.dropoutRisk = clone.algorithm.score >= 70 ? "Baixo" : (clone.algorithm.score >= 45 ? "Moderado" : "Alto");
            clone.algorithm.blindSpotHint = template.algorithm.blindSpotHint.TrimEnd() + " " + Pick(LayerAdditions, sequenceIndex + random.Next(0, LayerAdditions.Length));

            if (clone.algorithm.keywords == null)
            {
                clone.algorithm.keywords = new List<string>();
            }
            if (!clone.algorithm.keywords.Contains(clone.district))
            {
                clone.algorithm.keywords.Add(clone.district.ToLowerInvariant());
            }
            if (!clone.algorithm.keywords.Contains(clone.interestArea))
            {
                clone.algorithm.keywords.Add(clone.interestArea);
            }

            if (clone.investigationLayers != null)
            {
                for (int i = 0; i < clone.investigationLayers.Count; i++)
                {
                    if (clone.investigationLayers[i] == null)
                    {
                        continue;
                    }

                    clone.investigationLayers[i].content = clone.investigationLayers[i].content.TrimEnd() + " " + Pick(LayerAdditions, sequenceIndex + i + random.Next(0, LayerAdditions.Length));
                }
            }

            if (clone.decisionOptions != null)
            {
                for (int i = 0; i < clone.decisionOptions.Count; i++)
                {
                    DecisionOptionData option = clone.decisionOptions[i];
                    if (option == null)
                    {
                        continue;
                    }

                    option.immediateFeedback = option.immediateFeedback.TrimEnd() + " " + Pick(FeedbackAdditions, sequenceIndex + i + random.Next(0, FeedbackAdditions.Length));
                    if (!option.matchesAlgorithmRecommendation && (option.impact.communityTrust > 0 || option.impact.systemSensitivity > 0))
                    {
                        option.impact.operationalEfficiency = Mathf.Max(option.impact.operationalEfficiency, 1);
                    }
                }
            }
        }

        private static string ComposeName(int sequenceIndex, System.Random random)
        {
            string first = Pick(FirstNames, sequenceIndex + random.Next(0, FirstNames.Length));
            string lastA = Pick(LastNames, sequenceIndex + random.Next(0, LastNames.Length));
            string lastB = Pick(LastNames, sequenceIndex * 3 + random.Next(0, LastNames.Length));
            if (lastA == lastB)
            {
                lastB = Pick(LastNames, sequenceIndex + 5);
            }

            return first + " " + lastA + " " + lastB;
        }

        private static string Pick(string[] values, int index)
        {
            if (values == null || values.Length == 0)
            {
                return string.Empty;
            }

            index = Mathf.Abs(index) % values.Length;
            return values[index];
        }

        private static string ResolvePriority(int score)
        {
            if (score >= 75) return "Alta";
            if (score >= 50) return "Média";
            return "Baixa";
        }

        private static string UppercaseFirst(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
