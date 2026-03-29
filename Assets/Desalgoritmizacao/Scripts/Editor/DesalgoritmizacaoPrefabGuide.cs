#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Desalgoritmizacao.Editor
{
    [InitializeOnLoad]
    public static class DesalgoritmizacaoPrefabGuide
    {
        private const string MenuPath = "Desalgoritmizacao/Guias de Prefab/Mostrar guias de layout";
        private const string PreferenceKey = "Desalgoritmizacao.PrefabGuide.Enabled";
        private static GUIStyle labelStyle;
        private static GUIStyle legendStyle;
        private static readonly Color FillDefault = new Color(0.45f, 0.55f, 0.68f, 0.08f);
        private static readonly Color FillPanel = new Color(0.25f, 0.6f, 1f, 0.10f);
        private static readonly Color FillScroll = new Color(1f, 0.6f, 0.1f, 0.10f);
        private static readonly Color FillTitle = new Color(0.7f, 0.35f, 1f, 0.12f);
        private static readonly Color FillMetric = new Color(0.15f, 0.85f, 0.55f, 0.10f);
        private static readonly Color OutlineSelected = new Color(1f, 1f, 1f, 0.95f);

        static DesalgoritmizacaoPrefabGuide()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.delayCall += RefreshMenuCheck;
        }


        private static bool EnsureStyles()
        {
            if (labelStyle == null)
            {
                GUIStyle miniBold = EditorStyles.miniBoldLabel;
                if (miniBold == null)
                {
                    return false;
                }

                labelStyle = new GUIStyle(miniBold)
                {
                    alignment = TextAnchor.UpperLeft,
                    richText = true,
                    padding = new RectOffset(5, 5, 3, 3)
                };
                labelStyle.normal.textColor = Color.white;
            }

            if (legendStyle == null)
            {
                GUIStyle helpBox = EditorStyles.helpBox;
                if (helpBox == null)
                {
                    return false;
                }

                legendStyle = new GUIStyle(helpBox)
                {
                    richText = true,
                    fontSize = 11,
                    padding = new RectOffset(8, 8, 8, 8)
                };
            }

            return true;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PreferenceKey, true);
            set
            {
                EditorPrefs.SetBool(PreferenceKey, value);
                RefreshMenuCheck();
                SceneView.RepaintAll();
            }
        }

        [MenuItem(MenuPath)]
        private static void ToggleGuides()
        {
            Enabled = !Enabled;
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleGuidesValidate()
        {
            RefreshMenuCheck();
            return true;
        }

        private static void RefreshMenuCheck()
        {
            Menu.SetChecked(MenuPath, Enabled);
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!Enabled || !EnsureStyles())
            {
                return;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || prefabStage.prefabContentsRoot == null)
            {
                return;
            }

            string assetPath = prefabStage.assetPath;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.Contains("Assets/Desalgoritmizacao/Resources/Desalgoritmizacao/Prefabs"))
            {
                return;
            }

            RectTransform[] rects = prefabStage.prefabContentsRoot.GetComponentsInChildren<RectTransform>(true);
            if (rects == null || rects.Length == 0)
            {
                return;
            }

            Handles.BeginGUI();
            try
            {
                DrawLegend(assetPath, rects.Length);
                GameObject selected = Selection.activeGameObject;
                for (int i = 0; i < rects.Length; i++)
                {
                    RectTransform rectTransform = rects[i];
                    if (rectTransform == null)
                    {
                        continue;
                    }

                    if (!TryGetGuiRect(rectTransform, out Rect guiRect))
                    {
                        continue;
                    }

                    bool isSelected = selected != null && (selected == rectTransform.gameObject || selected.transform.IsChildOf(rectTransform));
                    Color fill = ResolveFill(rectTransform.name);
                    Color outline = isSelected ? OutlineSelected : new Color(fill.r, fill.g, fill.b, 0.85f);
                    DrawRect(guiRect, fill, outline);

                    if (guiRect.width >= 80f && guiRect.height >= 18f)
                    {
                        string label = BuildLabel(rectTransform);
                        GUI.Label(new Rect(guiRect.x + 2f, guiRect.y + 2f, guiRect.width - 4f, 22f), label, labelStyle);
                    }
                }
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private static void DrawLegend(string assetPath, int rectCount)
        {
            string fileName = System.IO.Path.GetFileName(assetPath);
            string text = "<b>Guia visual do prefab</b>\n" +
                          fileName + " · " + rectCount + " áreas\n" +
                          "Roxo = títulos | Azul = painéis | Laranja = rolagem | Verde = métricas\n" +
                          "Menu: Desalgoritmizacao > Guias de Prefab > Mostrar guias de layout";
            GUI.Label(new Rect(12f, 12f, 360f, 80f), text, legendStyle);
        }

        private static bool TryGetGuiRect(RectTransform rectTransform, out Rect guiRect)
        {
            guiRect = default;
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < 4; i++)
            {
                Vector2 guiPoint = HandleUtility.WorldToGUIPoint(corners[i]);
                if (float.IsNaN(guiPoint.x) || float.IsNaN(guiPoint.y) || float.IsInfinity(guiPoint.x) || float.IsInfinity(guiPoint.y))
                {
                    return false;
                }

                min = Vector2.Min(min, guiPoint);
                max = Vector2.Max(max, guiPoint);
            }

            guiRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return guiRect.width >= 4f && guiRect.height >= 4f;
        }

        private static void DrawRect(Rect rect, Color fill, Color outline)
        {
            EditorGUI.DrawRect(rect, fill);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, 1f), outline);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), outline);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, 1f, rect.height), outline);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), outline);
        }

        private static string BuildLabel(RectTransform rectTransform)
        {
            string role = ResolveRole(rectTransform.name);
            Rect rect = rectTransform.rect;
            string size = Mathf.RoundToInt(rect.width) + "×" + Mathf.RoundToInt(rect.height);
            return "<b>" + rectTransform.name + "</b>  <color=#B8C6D8FF>(" + role + ")</color>  <color=#8FA7BEFF>" + size + "</color>";
        }

        private static string ResolveRole(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return "área";
            string name = objectName.ToLowerInvariant();
            if (name.Contains("header")) return "cabeçalho";
            if (name.Contains("title")) return "título";
            if (name.Contains("subtitle") || name.Contains("hint")) return "apoio";
            if (name.Contains("scroll") || name.Contains("viewport") || name.Contains("content")) return "rolagem";
            if (name.Contains("panel")) return "painel";
            if (name.Contains("metric")) return "indicador";
            if (name.Contains("button")) return "botão";
            if (name.Contains("label")) return "rótulo";
            if (name.Contains("value")) return "valor";
            if (name.Contains("tabs")) return "abas";
            return "área";
        }

        private static Color ResolveFill(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return FillDefault;
            string name = objectName.ToLowerInvariant();
            if (name.Contains("title") || name.Contains("header")) return FillTitle;
            if (name.Contains("scroll") || name.Contains("viewport") || name.Equals("content")) return FillScroll;
            if (name.Contains("metric")) return FillMetric;
            if (name.Contains("panel")) return FillPanel;
            return FillDefault;
        }
    }
}
#endif
