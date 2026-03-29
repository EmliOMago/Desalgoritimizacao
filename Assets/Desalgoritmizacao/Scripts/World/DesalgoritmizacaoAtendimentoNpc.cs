using System;
using UnityEngine;

namespace Desalgoritmizacao.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesalgoritmizacaoAtendimentoNpc : MonoBehaviour
    {
        private const string StarterControllerResourcePath = "Desalgoritmizacao/NPCs/StarterAssetsThirdPerson";
        private const float DefaultMoveSpeed = 1.52f;
        private const float DefaultWalkBlendSpeed = 2.0f;
        private const float DefaultWalkMotionSpeed = 1.08f;
        private const float DefaultRotationSpeed = 10f;
        private const float DefaultGravity = -18f;
        private const float DefaultStopDistance = 0.10f;

        private enum NpcState
        {
            IndoParaAtendimento,
            AguardandoAtendimento,
            IndoParaSaida,
            Finalizado
        }

        private CharacterController characterController;
        private Transform atendimentoPoint;
        private Transform exitPoint;
        private Transform playerTransform;
        private Action<DesalgoritmizacaoAtendimentoNpc> reachedDeskCallback;
        private Action<DesalgoritmizacaoAtendimentoNpc> exitedCallback;
        private Animator visualAnimator;
        private Transform visualRoot;
        private Avatar visualAvatar;
        private NpcState currentState;
        private float verticalVelocity;
        private bool hasRaisedDeskCallback;
        private bool hasRaisedExitCallback;

        public void Initialize(
            GameObject visualPrefab,
            Transform atendimentoDestination,
            Transform exitDestination,
            Transform player,
            Action<DesalgoritmizacaoAtendimentoNpc> onReachedDesk,
            Action<DesalgoritmizacaoAtendimentoNpc> onExited)
        {
            characterController = GetComponent<CharacterController>();
            characterController.minMoveDistance = 0f;
            characterController.radius = 0.26f;
            characterController.height = 1.8f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.stepOffset = 0.25f;
            characterController.slopeLimit = 45f;
            characterController.skinWidth = 0.02f;

            atendimentoPoint = atendimentoDestination;
            exitPoint = exitDestination;
            playerTransform = player;
            reachedDeskCallback = onReachedDesk;
            exitedCallback = onExited;
            currentState = NpcState.IndoParaAtendimento;
            verticalVelocity = -2f;

            BuildVisual(visualPrefab);
        }

        public void BeginLeaving()
        {
            if (currentState != NpcState.AguardandoAtendimento)
            {
                return;
            }

            currentState = NpcState.IndoParaSaida;
        }

        public void DisposeImmediately()
        {
            currentState = NpcState.Finalizado;
            Destroy(gameObject);
        }

        private void Update()
        {
            switch (currentState)
            {
                case NpcState.IndoParaAtendimento:
                    MoveTowards(atendimentoPoint, DefaultMoveSpeed, DefaultStopDistance);
                    break;
                case NpcState.AguardandoAtendimento:
                    UpdateLookAtPlayer();
                    ApplyAnimatorMotion(0f);
                    KeepGrounded();
                    break;
                case NpcState.IndoParaSaida:
                    MoveTowards(exitPoint, DefaultMoveSpeed, DefaultStopDistance);
                    break;
                case NpcState.Finalizado:
                    ApplyAnimatorMotion(0f);
                    break;
            }
        }

        private void MoveTowards(Transform destination, float moveSpeed, float stopDistance)
        {
            if (destination == null)
            {
                ApplyAnimatorMotion(0f);
                return;
            }

            Vector3 destinationPosition = destination.position;
            Vector3 horizontalOffset = destinationPosition - transform.position;
            horizontalOffset.y = 0f;
            float distance = horizontalOffset.magnitude;

            if (distance <= stopDistance)
            {
                SnapToDestination(destinationPosition);
                if (currentState == NpcState.IndoParaAtendimento)
                {
                    currentState = NpcState.AguardandoAtendimento;
                    RaiseDeskCallback();
                }
                else if (currentState == NpcState.IndoParaSaida)
                {
                    currentState = NpcState.Finalizado;
                    RaiseExitCallback();
                    Destroy(gameObject);
                }

                ApplyAnimatorMotion(0f);
                return;
            }

            Vector3 direction = horizontalOffset / Mathf.Max(distance, 0.0001f);
            RotateTowards(direction, DefaultRotationSpeed);

            float frameDistance = Mathf.Min(distance, moveSpeed * Time.deltaTime);
            Vector3 motion = direction * frameDistance;
            ApplyGravity();
            motion.y = verticalVelocity * Time.deltaTime;
            characterController.Move(motion);
            ApplyAnimatorMotion(moveSpeed);
        }

        private void UpdateLookAtPlayer()
        {
            if (playerTransform == null)
            {
                return;
            }

            Vector3 direction = playerTransform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            RotateTowards(direction.normalized, DefaultRotationSpeed * 1.35f);
        }

        private void ApplyGravity()
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
                return;
            }

            verticalVelocity += DefaultGravity * Time.deltaTime;
        }

        private void KeepGrounded()
        {
            ApplyGravity();
            characterController.Move(new Vector3(0f, verticalVelocity * Time.deltaTime, 0f));
        }

        private void RotateTowards(Vector3 direction, float turnSpeed)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        private void SnapToDestination(Vector3 destinationPosition)
        {
            Vector3 snapOffset = destinationPosition - transform.position;
            snapOffset.y = 0f;
            if (snapOffset.sqrMagnitude > 0.000001f)
            {
                characterController.Move(snapOffset);
            }

            verticalVelocity = -2f;
            characterController.Move(new Vector3(0f, verticalVelocity * Time.deltaTime, 0f));
        }

        private void RaiseDeskCallback()
        {
            if (hasRaisedDeskCallback)
            {
                return;
            }

            hasRaisedDeskCallback = true;
            reachedDeskCallback?.Invoke(this);
        }

        private void RaiseExitCallback()
        {
            if (hasRaisedExitCallback)
            {
                return;
            }

            hasRaisedExitCallback = true;
            exitedCallback?.Invoke(this);
        }

        private void BuildVisual(GameObject visualPrefab)
        {
            if (visualPrefab == null)
            {
                return;
            }

            GameObject visualInstance = Instantiate(visualPrefab, transform);
            visualInstance.name = visualPrefab.name;
            visualRoot = visualInstance.transform;
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;

            AlignVisualToGround();
            ConfigureControllerHeight();
            SetupAnimatorController(visualInstance);
            TryAttachPushHelper();
        }

        private void AlignVisualToGround()
        {
            if (visualRoot == null)
            {
                return;
            }

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            float bottomOffset = combinedBounds.min.y - transform.position.y;
            visualRoot.localPosition -= new Vector3(0f, bottomOffset, 0f);
        }

        private void ConfigureControllerHeight()
        {
            if (characterController == null || visualRoot == null)
            {
                return;
            }

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            float height = Mathf.Clamp(combinedBounds.size.y, 1.45f, 2.25f);
            float radius = Mathf.Clamp(combinedBounds.extents.x, 0.22f, 0.38f);
            characterController.height = height;
            characterController.radius = radius;
            characterController.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private void SetupAnimatorController(GameObject visualInstance)
        {
            if (visualInstance == null)
            {
                return;
            }

            visualAnimator = ResolveBestAnimator(visualInstance);
            if (visualAnimator == null)
            {
                visualAnimator = visualInstance.AddComponent<Animator>();
            }

            visualAvatar = ResolveAvatar(visualInstance, visualAnimator);
            if (visualAvatar != null)
            {
                visualAnimator.avatar = visualAvatar;
            }

            RuntimeAnimatorController starterController = Resources.Load<RuntimeAnimatorController>(StarterControllerResourcePath);
            if (starterController != null)
            {
                visualAnimator.runtimeAnimatorController = starterController;
            }

            DesalgoritmizacaoNpcAnimationEvents eventReceiver = visualAnimator.GetComponent<DesalgoritmizacaoNpcAnimationEvents>();
            if (eventReceiver == null)
            {
                eventReceiver = visualAnimator.gameObject.AddComponent<DesalgoritmizacaoNpcAnimationEvents>();
            }

            DisableExtraAnimators(visualInstance, visualAnimator);
            visualAnimator.applyRootMotion = false;
            visualAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            visualAnimator.Rebind();
            visualAnimator.Update(0f);
            ApplyAnimatorMotion(0f);
        }

        private static Animator ResolveBestAnimator(GameObject visualInstance)
        {
            Animator[] animators = visualInstance.GetComponentsInChildren<Animator>(true);
            Animator fallbackAnimator = visualInstance.GetComponent<Animator>();
            if (fallbackAnimator == null && animators.Length > 0)
            {
                fallbackAnimator = animators[0];
            }

            for (int i = 0; i < animators.Length; i++)
            {
                Animator candidate = animators[i];
                if (candidate != null && candidate.avatar != null && candidate.avatar.isValid)
                {
                    return candidate;
                }
            }

            return fallbackAnimator;
        }

        private static Avatar ResolveAvatar(GameObject visualInstance, Animator preferredAnimator)
        {
            if (preferredAnimator != null && preferredAnimator.avatar != null && preferredAnimator.avatar.isValid)
            {
                return preferredAnimator.avatar;
            }

            Animator[] animators = visualInstance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator candidate = animators[i];
                if (candidate != null && candidate.avatar != null && candidate.avatar.isValid)
                {
                    return candidate.avatar;
                }
            }

            return null;
        }

        private static void DisableExtraAnimators(GameObject visualInstance, Animator selectedAnimator)
        {
            Animator[] animators = visualInstance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator candidate = animators[i];
                if (candidate != null && candidate != selectedAnimator)
                {
                    candidate.enabled = false;
                }
            }
        }

        private void TryAttachPushHelper()
        {
            if (GetComponent<BasicRigidBodyPush>() != null)
            {
                return;
            }

            BasicRigidBodyPush pushHelper = gameObject.AddComponent<BasicRigidBodyPush>();
            pushHelper.canPush = false;
            pushHelper.strength = 1.1f;
            pushHelper.pushLayers = 0;
        }

        private void ApplyAnimatorMotion(float worldSpeed)
        {
            if (visualAnimator == null)
            {
                return;
            }

            if (visualAvatar != null && visualAnimator.avatar != visualAvatar)
            {
                visualAnimator.avatar = visualAvatar;
            }

            bool isMoving = worldSpeed > 0.05f;
            float blendSpeed = isMoving ? DefaultWalkBlendSpeed : 0f;
            float motionSpeed = isMoving ? DefaultWalkMotionSpeed : 0f;
            visualAnimator.SetBool("Grounded", true);
            visualAnimator.SetBool("Jump", false);
            visualAnimator.SetBool("FreeFall", false);
            visualAnimator.SetFloat("Speed", blendSpeed);
            visualAnimator.SetFloat("MotionSpeed", motionSpeed);
        }
    }

    public sealed class DesalgoritmizacaoNpcAnimationEvents : MonoBehaviour
    {
        public void OnFootstep(AnimationEvent animationEvent)
        {
        }

        public void OnLand(AnimationEvent animationEvent)
        {
        }
    }
}
