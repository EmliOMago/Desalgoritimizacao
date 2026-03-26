using UnityEngine;
using UnityEngine.UI;

namespace Desalgoritmizacao.UI
{
    public static class UIFactory
    {
        private static Font cachedFont;

        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null)
                {
                    try
                    {
                        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                    catch
                    {
                        cachedFont = null;
                    }

                    if (cachedFont == null)
                    {
                        try
                        {
                            cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                        }
                        catch
                        {
                            cachedFont = null;
                        }
                    }
                }

                return cachedFont;
            }
        }

        public static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static RectTransform SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public static Image CreatePanel(Transform parent, Color color, string name = "Panel")
        {
            GameObject go = CreateUIObject(name, parent);
            Image image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(Transform parent, string content, int fontSize, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            GameObject go = CreateUIObject("Text", parent);
            Text text = go.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            return text;
        }

        public static Button CreateButton(Transform parent, string label, Color fillColor, Color textColor, int fontSize = 16)
        {
            GameObject go = CreateUIObject("Button", parent);
            Image image = go.AddComponent<Image>();
            image.color = fillColor;

            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = fillColor;
            colors.highlightedColor = fillColor * 1.08f;
            colors.pressedColor = fillColor * 0.92f;
            colors.selectedColor = fillColor;
            colors.disabledColor = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * 0.4f);
            button.colors = colors;
            button.targetGraphic = image;

            Text text = CreateText(go.transform, label, fontSize, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform);
            return button;
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject go, int spacing, RectOffset padding, bool forceExpandHeight = false)
        {
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = forceExpandHeight;
            layout.childForceExpandWidth = true;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject go, int spacing, RectOffset padding, bool forceExpandWidth = false)
        {
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = forceExpandWidth;
            return layout;
        }

        public static LayoutElement AddLayoutElement(GameObject go, float minHeight = -1f, float preferredHeight = -1f, float flexibleHeight = -1f, float preferredWidth = -1f)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = go.AddComponent<LayoutElement>();
            }

            if (minHeight >= 0f) element.minHeight = minHeight;
            if (preferredHeight >= 0f) element.preferredHeight = preferredHeight;
            if (flexibleHeight >= 0f) element.flexibleHeight = flexibleHeight;
            if (preferredWidth >= 0f) element.preferredWidth = preferredWidth;
            return element;
        }

        public static GameObject CreateSpacer(Transform parent, float height)
        {
            GameObject go = CreateUIObject("Spacer", parent);
            AddLayoutElement(go, preferredHeight: height, minHeight: height);
            return go;
        }

        public static ScrollRect CreateScrollView(Transform parent, Color viewportColor, Color contentColor, out RectTransform contentRect)
        {
            GameObject root = CreateUIObject("ScrollView", parent);
            Image rootImage = root.AddComponent<Image>();
            rootImage.color = viewportColor;

            ScrollRect scrollRect = root.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            GameObject viewport = CreateUIObject("Viewport", root.transform);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = viewportColor;
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            GameObject content = CreateUIObject("Content", viewport.transform);
            contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);
            Image contentImage = content.AddComponent<Image>();
            contentImage.color = contentColor;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            return scrollRect;
        }

        public static GameObject CreateDivider(Transform parent, Color color, float height = 1f)
        {
            Image image = CreatePanel(parent, color, "Divider");
            AddLayoutElement(image.gameObject, preferredHeight: height, minHeight: height);
            return image.gameObject;
        }

        public static string FormatDelta(int delta)
        {
            if (delta > 0) return "+" + delta;
            return delta.ToString();
        }
    }
}
