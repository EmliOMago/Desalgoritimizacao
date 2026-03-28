using UnityEngine;

namespace Desalgoritmizacao.Data
{
    [CreateAssetMenu(menuName = "Desalgoritmizacao/Config/Printer Quick Time", fileName = "PrinterQuickTimeConfig")]
    public class DesalgoritmizacaoPrinterQuickTimeConfig : ScriptableObject
    {
        [Header("Visuals")]
        public Texture2D upTexture;
        public Texture2D downTexture;
        public Texture2D leftTexture;
        public Texture2D rightTexture;

        [Header("Difficulty")]
        [Min(2)] public int minimumSequenceLength = 4;
        [Min(2)] public int maximumSequenceLength = 8;
        [Min(0.15f)] public float minimumInputWindowSeconds = 0.45f;
        [Min(0.15f)] public float maximumInputWindowSeconds = 0.95f;
        [Min(0.05f)] public float restartPauseSeconds = 0.65f;
        [Min(1)] public int previewIconCount = 4;

        [Header("Labels")]
        public string title = "GPImpre";
        public string subtitle = "Acione as setas na ordem certa para concluir a impressão.";
        public string successMessage = "Registro impresso. Retorne ao terminal.";
        public string failureMessage = "Falha na impressão. Reiniciando sequência.";

        public Texture2D ResolveTexture(PrinterDirection direction)
        {
            switch (direction)
            {
                case PrinterDirection.Up:
                    return upTexture;
                case PrinterDirection.Down:
                    return downTexture;
                case PrinterDirection.Left:
                    return leftTexture;
                case PrinterDirection.Right:
                    return rightTexture;
                default:
                    return null;
            }
        }
    }

    public enum PrinterDirection
    {
        Up,
        Down,
        Left,
        Right
    }
}
