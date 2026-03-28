using System;

namespace Desalgoritmizacao.Runtime
{
    public static class DesalgoritmizacaoGameplayBridge
    {
        public static Func<string> ExternalStatusProvider;
        public static Func<bool> CanOpenOperatorWorkflowProvider;
        public static Func<string> OperatorWorkflowBlockReasonProvider;
        public static Action RegisterAndReturnToCentralRequested;

        public static string GetExternalStatus()
        {
            return ExternalStatusProvider != null ? ExternalStatusProvider.Invoke() : string.Empty;
        }

        public static bool CanOpenOperatorWorkflow(out string blockReason)
        {
            bool canOpen = CanOpenOperatorWorkflowProvider == null || CanOpenOperatorWorkflowProvider.Invoke();
            blockReason = canOpen || OperatorWorkflowBlockReasonProvider == null
                ? string.Empty
                : OperatorWorkflowBlockReasonProvider.Invoke();
            return canOpen;
        }

        public static void NotifyRegisterAndReturnToCentral()
        {
            RegisterAndReturnToCentralRequested?.Invoke();
        }
    }
}
