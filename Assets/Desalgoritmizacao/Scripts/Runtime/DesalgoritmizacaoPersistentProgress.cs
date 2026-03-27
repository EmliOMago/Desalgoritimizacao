using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desalgoritmizacao.Runtime
{
    [Serializable]
    public class RankingEntryData
    {
        public int cycleNumber;
        public string playerName;
        public string summary;
        public string endingId;
        public int operational;
        public int communityTrust;
        public int systemSensitivity;
        public string recordedAt;
    }

    [Serializable]
    public class DeckReservationData
    {
        public int cycleNumber;
        public List<int> indices = new List<int>();
    }

    [Serializable]
    internal class PersistentProgressData
    {
        public string operatorName = "Operador";
        public int cycleCounter;
        public int shuffleGeneration;
        public int deckCursor;
        public List<int> shuffledIndices = new List<int>();
        public List<RankingEntryData> rankingEntries = new List<RankingEntryData>();
    }

    public static class DesalgoritmizacaoPersistentProgress
    {
        private const string PlayerPrefsKey = "Desalgoritmizacao.PersistentProgress";

        public static string LoadOperatorName(string fallbackName)
        {
            PersistentProgressData data = LoadData();
            if (string.IsNullOrWhiteSpace(data.operatorName))
            {
                data.operatorName = fallbackName;
                SaveData(data);
            }

            return string.IsNullOrWhiteSpace(data.operatorName) ? fallbackName : data.operatorName;
        }

        public static void SaveOperatorName(string operatorName, string fallbackName)
        {
            PersistentProgressData data = LoadData();
            data.operatorName = string.IsNullOrWhiteSpace(operatorName) ? fallbackName : operatorName.Trim();
            SaveData(data);
        }

        public static DeckReservationData ReserveCycleDeck(int poolCount, int requestedCount, int shuffleSeed)
        {
            DeckReservationData reservation = new DeckReservationData();
            if (poolCount <= 0 || requestedCount <= 0)
            {
                return reservation;
            }

            PersistentProgressData data = LoadData();
            requestedCount = Mathf.Clamp(requestedCount, 1, poolCount);

            if (data.shuffledIndices == null || data.shuffledIndices.Count != poolCount)
            {
                data.shuffledIndices = CreateShuffle(poolCount, shuffleSeed + data.shuffleGeneration);
                data.deckCursor = 0;
            }

            if (data.deckCursor + requestedCount > data.shuffledIndices.Count)
            {
                data.shuffleGeneration++;
                data.shuffledIndices = CreateShuffle(poolCount, shuffleSeed + data.shuffleGeneration);
                data.deckCursor = 0;
            }

            data.cycleCounter++;
            reservation.cycleNumber = data.cycleCounter;
            for (int i = 0; i < requestedCount; i++)
            {
                reservation.indices.Add(data.shuffledIndices[data.deckCursor + i]);
            }

            data.deckCursor += requestedCount;
            SaveData(data);
            return reservation;
        }

        public static List<RankingEntryData> LoadRankingEntries(int maxEntries)
        {
            PersistentProgressData data = LoadData();
            List<RankingEntryData> ranking = data.rankingEntries ?? new List<RankingEntryData>();
            ranking.Sort(CompareRanking);
            if (maxEntries > 0 && ranking.Count > maxEntries)
            {
                ranking = ranking.GetRange(0, maxEntries);
            }

            return ranking;
        }

        public static void SaveRankingEntry(RankingEntryData entry, int maxEntries)
        {
            if (entry == null)
            {
                return;
            }

            PersistentProgressData data = LoadData();
            if (data.rankingEntries == null)
            {
                data.rankingEntries = new List<RankingEntryData>();
            }

            data.rankingEntries.Add(entry);
            data.rankingEntries.Sort(CompareRanking);
            if (maxEntries > 0 && data.rankingEntries.Count > maxEntries)
            {
                data.rankingEntries.RemoveRange(maxEntries, data.rankingEntries.Count - maxEntries);
            }

            SaveData(data);
        }

        private static int CompareRanking(RankingEntryData a, RankingEntryData b)
        {
            int trust = b.communityTrust.CompareTo(a.communityTrust);
            if (trust != 0)
            {
                return trust;
            }

            int operational = b.operational.CompareTo(a.operational);
            if (operational != 0)
            {
                return operational;
            }

            int sensitivity = b.systemSensitivity.CompareTo(a.systemSensitivity);
            if (sensitivity != 0)
            {
                return sensitivity;
            }

            return b.cycleNumber.CompareTo(a.cycleNumber);
        }

        private static List<int> CreateShuffle(int count, int seed)
        {
            List<int> values = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                values.Add(i);
            }

            System.Random random = new System.Random(seed);
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }

            return values;
        }

        private static PersistentProgressData LoadData()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                return new PersistentProgressData();
            }

            string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PersistentProgressData();
            }

            try
            {
                PersistentProgressData data = JsonUtility.FromJson<PersistentProgressData>(json);
                return data ?? new PersistentProgressData();
            }
            catch
            {
                return new PersistentProgressData();
            }
        }

        private static void SaveData(PersistentProgressData data)
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }
    }
}
