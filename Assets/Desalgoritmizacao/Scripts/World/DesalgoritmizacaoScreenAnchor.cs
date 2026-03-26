using UnityEngine;

namespace Desalgoritmizacao.World
{
    public class DesalgoritmizacaoScreenAnchor : MonoBehaviour
    {
        public Transform mainCanvasAnchor;
        public Transform menuCanvasAnchor;
        public Camera uiCamera;
        public Vector2 canvasReferenceResolution = new Vector2(1600f, 900f);
        public float canvasScale = 0.001f;
        public Vector3 mainCanvasLocalPosition = Vector3.zero;
        public Vector3 mainCanvasLocalEulerAngles = Vector3.zero;
        public Vector3 menuCanvasLocalPosition = Vector3.zero;
        public Vector3 menuCanvasLocalEulerAngles = Vector3.zero;
        public Sprite initialMenuImage;
        public Color initialMenuTint = Color.white;

        public Transform MainParent => mainCanvasAnchor != null ? mainCanvasAnchor : transform;
        public Transform MenuParent => menuCanvasAnchor != null ? menuCanvasAnchor : transform;

        public Camera ResolveCamera()
        {
            if (uiCamera != null)
            {
                return uiCamera;
            }

            if (Camera.main != null)
            {
                return Camera.main;
            }

            return Object.FindFirstObjectByType<Camera>();
        }

        public void ApplyTransform(RectTransform rectTransform, bool isMainCanvas)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.localScale = Vector3.one * Mathf.Max(0.0001f, canvasScale);
            rectTransform.sizeDelta = canvasReferenceResolution;
            rectTransform.localPosition = isMainCanvas ? mainCanvasLocalPosition : menuCanvasLocalPosition;
            rectTransform.localRotation = Quaternion.Euler(isMainCanvas ? mainCanvasLocalEulerAngles : menuCanvasLocalEulerAngles);
        }
    }
}
