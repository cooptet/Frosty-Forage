using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Camera))]
public sealed class SquirrelCamera : MonoBehaviour {
    [SerializeField] private Transform target;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private float focusHeight = 0.38f;
    [SerializeField] private float distance = 2.8f;
    [SerializeField] private float shoulderOffset = 0.25f;
    [SerializeField] private float sensitivity = 1f;

    private const float StartPitch = 18f;
    private Vector3 focus;
    private Vector3 focusVelocity;
    private float yaw;
    private float pitch;
    private float desiredYaw;
    private float desiredPitch;
    private float clearDistance;

    public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    public bool HasLookControl => isActiveAndEnabled && target != null
        && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;

    private void OnEnable() {
        if (target != null && Application.isFocused) SetCursor(true);
        SnapBehindTarget();
    }

    private void OnDisable() => SetCursor(false);

    private void OnApplicationFocus(bool focused) {
        if (!focused) SetCursor(false);
    }

    public void SnapBehindTarget() {
        if (target == null) return;

        desiredYaw = yaw = target.eulerAngles.y;
        desiredPitch = pitch = StartPitch;
        focus = Anchor();
        focusVelocity = Vector3.zero;
        clearDistance = float.PositiveInfinity;
        Physics.SyncTransforms();
        PlaceCamera(0f);
    }

    private void Update() {
        if (target == null || !Application.isFocused) return;

#if ENABLE_INPUT_SYSTEM
        Keyboard keys = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool escape = keys?.escapeKey.wasPressedThisFrame ?? false;
        bool click = mouse?.leftButton.wasPressedThisFrame ?? false;
        bool recenter = keys?.rKey.wasPressedThisFrame ?? false;
        Vector2 look = mouse == null ? Vector2.zero : mouse.delta.ReadValue() * 0.13f;
#else
        bool escape = Input.GetKeyDown(KeyCode.Escape);
        bool click = Input.GetMouseButtonDown(0);
        bool recenter = Input.GetKeyDown(KeyCode.R);
        Vector2 look = new Vector2(
            Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 2.5f;
#endif

        if (escape) {
            SetCursor(false);
            return;
        }

        if (!HasLookControl) {
            if (click) SetCursor(true);
            return;
        }

        if (recenter) {
            desiredYaw = target.eulerAngles.y;
            desiredPitch = StartPitch;
        }
        else {
            desiredYaw = Mathf.Repeat(desiredYaw + look.x * sensitivity, 360f);
            desiredPitch = Mathf.Clamp(desiredPitch - look.y * sensitivity, 8f, 58f);
        }

        float blend = 1f - Mathf.Exp(-24f * Time.deltaTime);
        yaw = Mathf.LerpAngle(yaw, desiredYaw, blend);
        pitch = Mathf.Lerp(pitch, desiredPitch, blend);
    }

    private void LateUpdate() {
        if (target == null) return;

        float dt = Time.deltaTime;
        if (dt > 0f)
            focus = Vector3.SmoothDamp(focus, Anchor(), ref focusVelocity, 0.085f);

        PlaceCamera(dt);
    }

    private void PlaceCamera(float dt) {
        Vector3 anchor = Anchor();
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 right = orbit * Vector3.right;
        Vector3 desired = focus - orbit * Vector3.forward * distance
                          + right * shoulderOffset;
        Vector3 arm = desired - anchor;
        float length = arm.magnitude;

        if (length < 0.001f) return;

        Vector3 direction = arm / length;
        float safe = length;

        if (Physics.SphereCast(anchor, 0.12f, direction, out RaycastHit hit,
                length, obstacleLayers, QueryTriggerInteraction.Ignore)) {
            safe = Mathf.Max(0.02f, hit.distance - 0.05f);
        }

        clearDistance = safe < clearDistance
            ? safe : Mathf.Lerp(clearDistance, safe, 1f - Mathf.Exp(-dt * 6f));

        Vector3 position = anchor + direction * clearDistance;
        float framing = Mathf.InverseLerp(0.15f, length, clearDistance);
        Vector3 lookAt = Vector3.Lerp(anchor, focus + right * shoulderOffset * 1.4f, framing);
        Vector3 view = lookAt - position;

        if (view.sqrMagnitude > 0.00001f)
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(view, Vector3.up));
    }

    private Vector3 Anchor() => target.position + Vector3.up * focusHeight;

    private static void SetCursor(bool locked) {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}