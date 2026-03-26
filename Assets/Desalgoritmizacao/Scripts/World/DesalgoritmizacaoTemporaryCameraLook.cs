using UnityEngine;
using Desalgoritmizacao.Scene;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Desalgoritmizacao.World
{
    public class DesalgoritmizacaoTemporaryCameraLook : MonoBehaviour
    {
        [Min(1f)] public float totalYawRangeDegrees = 90f;
        [Min(0.01f)] public float dragSensitivity = 0.12f;
        [Min(0.1f)] public float returnSpeed = 120f;

        private Quaternion initialLocalRotation;
        private float currentYawOffset;

        private void Awake()
        {
            if (Object.FindFirstObjectByType<DesalgoritmizacaoSceneRig>() != null)
            {
                enabled = false;
                return;
            }

            initialLocalRotation = transform.localRotation;
        }

        private void Update()
        {
            if (IsLookHeld())
            {
                float halfRange = Mathf.Max(1f, totalYawRangeDegrees) * 0.5f;
                currentYawOffset += GetMouseDeltaX() * dragSensitivity;
                currentYawOffset = Mathf.Clamp(currentYawOffset, -halfRange, halfRange);
            }
            else
            {
                currentYawOffset = Mathf.MoveTowards(currentYawOffset, 0f, returnSpeed * Time.unscaledDeltaTime);
            }

            transform.localRotation = initialLocalRotation * Quaternion.Euler(0f, currentYawOffset, 0f);
        }

        private static bool IsLookHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.isPressed;
            }
#endif
            return Input.GetMouseButton(1);
        }

        private static float GetMouseDeltaX()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue().x;
            }
#endif
            return Input.GetAxisRaw("Mouse X") * 15f;
        }
    }
}
