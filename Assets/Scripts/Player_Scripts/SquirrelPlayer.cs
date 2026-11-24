using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public sealed class SquirrelPlayer : MonoBehaviour {
    [SerializeField] private SquirrelCamera followCamera;
    [SerializeField] private Animator animator;
    [SerializeField] private float trotSpeed = 2.4f;
    [SerializeField] private float sprintSpeed = 3.8f;
    [SerializeField] private float digRadius = 0.9f;


    [SerializeField] private GameObject dugUpAcorn;
    [SerializeField] private float acornForwardOffset = 0.8f;

    [SerializeField] private LayerMask groundLayer;

    [Header("Sounds")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip footstepSound;
    [SerializeField] private AudioClip munchSound;
    [SerializeField] private AudioClip digSound;
    [SerializeField] private AudioClip sniffSound;

    private enum Activity { Free, Scan, Dig, Sleep }

    private const float ScanDuration = 1.6f;
    private const float DigDuration = 0.8f;
    private const float HintDuration = 4.5f;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Scanning = Animator.StringToHash("Scanning");
    private static readonly int Digging = Animator.StringToHash("Digging");
    private static readonly int Sleeping = Animator.StringToHash("Sleeping");

    private CharacterController controller;
    private Activity activity;
    private Vector3 horizontalVelocity;
    private float verticalSpeed;
    private float actionRemaining;
    private float hintExpires;
    private SquirrelAcornTarget hintedAcorn;

    public event System.Action<SquirrelAcornTarget> AcornCollected;

    public bool IsScanning => activity == Activity.Scan;
    public bool IsDigging => activity == Activity.Dig;
    public bool IsSleeping => activity == Activity.Sleep;
    public float DigRadius => digRadius;
    public Vector3 ViewForward => followCamera != null
        ? followCamera.PlanarForward : Vector3.forward;

    private void Awake() {
        controller = GetComponent<CharacterController>();
        if (animator != null) animator.applyRootMotion = false;
    }

    private void OnDisable() {
        hintedAcorn = null;
        horizontalVelocity = Vector3.zero;
        verticalSpeed = 0f;
        SetActivity(Activity.Free);
    }

    private void Update() {
        float dt = Time.deltaTime;
        if (dt <= 0f || !controller.enabled) return;

        bool wasForaging = IsScanning || IsDigging;
        Vector2 input = Vector2.zero;
        bool sprint = false;

        if (activity == Activity.Free &&
            (followCamera == null || followCamera.HasLookControl)) {
            ReadInput(out input, out sprint, out bool scan, out bool dig);

            if (controller.isGrounded) {
                if (scan) {
                    hintedAcorn = null;
                    SetActivity(Activity.Scan, ScanDuration);
                    if (audioSource != null && sniffSound != null)
                        audioSource.PlayOneShot(sniffSound);
                }
                else if (dig) {
                    SetActivity(Activity.Dig, DigDuration);
                    if (audioSource != null && digSound != null)
                        audioSource.PlayOneShot(digSound);
                }
            }
        }

        if (activity != Activity.Free) input = Vector2.zero;
        Move(input, sprint, dt);
        UpdateFootsteps();

        if (wasForaging) AdvanceAction(dt);
    }

    private void Move(Vector2 input, bool sprint, float dt) {
        Vector3 forward = ViewForward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = right * input.x + forward * input.y;
        float speed = sprint ? sprintSpeed : trotSpeed;
        float changeRate = input.sqrMagnitude > 0f ? 16f : 23f;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity, direction * speed, changeRate * dt);

        if (direction.sqrMagnitude > 0.001f) {
            Quaternion heading = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, heading, 720f * dt);
        }

        verticalSpeed = controller.isGrounded
            ? -2f : Mathf.Max(verticalSpeed - 22f * dt, -25f);

        controller.Move((horizontalVelocity + Vector3.up * verticalSpeed) * dt);

        Vector3 actualVelocity = controller.velocity;
        actualVelocity.y = 0f;

        if (animator != null)
            animator.SetFloat(Speed, actualVelocity.magnitude, 0.10f, dt);
    }

    private void SetActivity(Activity next, float duration = 0f) {
        activity = next;
        actionRemaining = duration;

        if (next != Activity.Free) horizontalVelocity = Vector3.zero;

        if (animator == null) return;
        animator.SetBool(Scanning, IsScanning);
        animator.SetBool(Digging, IsDigging);
        animator.SetBool(Sleeping, IsSleeping);
        if (next != Activity.Free) animator.SetFloat(Speed, 0f);
    }

    private void AdvanceAction(float dt) {
        actionRemaining -= dt;
        if (actionRemaining > 0f) return;

        Activity finished = activity;
        SetActivity(Activity.Free);
        if (!controller.isGrounded) return;

        if (finished == Activity.Scan) {
            hintedAcorn = FindNearest(float.PositiveInfinity);
            hintExpires = Time.time + HintDuration;
            return;
        }

        SquirrelAcornTarget acorn = FindNearest(digRadius);
        if (acorn == null || !acorn.TryCollect()) return;

        
        if (hintedAcorn == acorn) hintedAcorn = null;
        ShowDugUpAcorn();

        if (audioSource != null && munchSound != null)
        audioSource.PlayOneShot(munchSound);

        AcornCollected?.Invoke(acorn);
    }

    private void ShowDugUpAcorn()
    {
        if (dugUpAcorn == null) return;

        Vector3 spawnPosition =
            transform.position + transform.forward * acornForwardOffset;

        Vector3 rayOrigin = spawnPosition + Vector3.up * 5f;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            15f,
            groundLayer))
        {
            spawnPosition = hit.point;
        }
        else
        {
            Debug.LogWarning("Couldn't find ground for dug-up acorn.");
            return;
        }

        GameObject spawnedAcorn = Instantiate(
            dugUpAcorn,
            spawnPosition,
            Quaternion.identity
        );

        Destroy(spawnedAcorn, 1f);
    }

    private SquirrelAcornTarget FindNearest(float maxDistance) {
        SquirrelAcornTarget closest = null;
        float bestSquared = maxDistance * maxDistance;

        foreach (SquirrelAcornTarget acorn in
                 FindObjectsByType<SquirrelAcornTarget>(FindObjectsSortMode.None)) {
            if (!acorn.isActiveAndEnabled) continue;

            float squared = OffsetTo(acorn).sqrMagnitude;
            if (squared > bestSquared) continue;

            closest = acorn;
            bestSquared = squared;
        }

        return closest;
    }

    private Vector3 OffsetTo(SquirrelAcornTarget acorn) {
        Vector3 offset = acorn.transform.position - transform.position;
        offset.y = 0f;
        return offset;
    }

    public bool TryGetHint(out Vector3 offset) {
        offset = Vector3.zero;

        if (hintedAcorn == null || !hintedAcorn.isActiveAndEnabled ||
            Time.time >= hintExpires)
            return false;

        offset = OffsetTo(hintedAcorn);
        return true;
    }

    public void SetSleeping(bool value) {
        if (!value && !IsSleeping) return;

        hintedAcorn = null;
        SetActivity(value ? Activity.Sleep : Activity.Free);
    }

    private static void ReadInput(
        out Vector2 move, out bool sprint, out bool scan, out bool dig) {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) {
            move = Vector2.zero;
            sprint = scan = dig = false;
            return;
        }

        float x = (keyboard.dKey.isPressed ? 1f : 0f) -
                  (keyboard.aKey.isPressed ? 1f : 0f);
        float z = (keyboard.wKey.isPressed ? 1f : 0f) -
                  (keyboard.sKey.isPressed ? 1f : 0f);
        sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        scan = keyboard.qKey.wasPressedThisFrame;
        dig = keyboard.eKey.wasPressedThisFrame;
#else
        float x = (Input.GetKey(KeyCode.D) ? 1f : 0f) -
                  (Input.GetKey(KeyCode.A) ? 1f : 0f);
        float z = (Input.GetKey(KeyCode.W) ? 1f : 0f) -
                  (Input.GetKey(KeyCode.S) ? 1f : 0f);
        sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        scan = Input.GetKeyDown(KeyCode.Q);
        dig = Input.GetKeyDown(KeyCode.E);
#endif
        move = Vector2.ClampMagnitude(new Vector2(x, z), 1f);
    }

    private float footstepTimer;

    private void UpdateFootsteps()
    {
        if (!controller.isGrounded || horizontalVelocity.magnitude < 0.1f)
        {
            footstepTimer = 0f;
            return;
        }

        footstepTimer -= Time.deltaTime;

        if (footstepTimer <= 0f)
        {
            if (audioSource != null && footstepSound != null)
                audioSource.PlayOneShot(footstepSound);

                footstepTimer = horizontalVelocity.magnitude > trotSpeed + 0.2f
                    ? 0.35f
                    : 0.5f;
        }
    }
}

