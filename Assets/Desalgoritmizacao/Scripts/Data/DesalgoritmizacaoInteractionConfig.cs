using UnityEngine;

namespace Desalgoritmizacao.Data
{
    [CreateAssetMenu(menuName = "Desalgoritmizacao/Config/Interaction", fileName = "InteractionConfig")]
    public class DesalgoritmizacaoInteractionConfig : ScriptableObject
    {
        [Header("Input")]
        public string runtimeInputJsonResourcePath = "Desalgoritmizacao/Config/InputSystem_Actions_Runtime";
        public string editorSourceRelativePath = "InputSystem_Actions.inputactions";
        public string playerActionMap = "Player";
        public string moveActionName = "Move";
        public string lookActionName = "Look";
        public string stationLookModifierActionName = "StationLook";
        public string printerUpActionName = "PrinterUp";
        public string printerDownActionName = "PrinterDown";
        public string printerLeftActionName = "PrinterLeft";
        public string printerRightActionName = "PrinterRight";

        [Header("Movement")]
        [Min(0.1f)] public float moveSpeed = 2.9f;
        [Min(0.1f)] public float lookSensitivity = 0.10f;
        [Min(0.1f)] public float stationLookSensitivity = 0.08f;
        [Range(-89f, 0f)] public float minimumPitch = -70f;
        [Range(0f, 89f)] public float maximumPitch = 72f;
        [Min(0.1f)] public float stationReleaseDistance = 1.35f;

        [Header("Atendimento")]
        [Min(1f)] public float atendimentoMinDelaySeconds = 25f;
        [Min(1f)] public float atendimentoMaxDelaySeconds = 55f;
        [Min(0.1f)] public float feedbackBannerSeconds = 2f;

        [Header("References")]
        public DesalgoritmizacaoPrinterQuickTimeConfig printerQuickTimeConfig;
    }
}
