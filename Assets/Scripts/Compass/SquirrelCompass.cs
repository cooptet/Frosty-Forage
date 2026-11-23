
using UnityEngine;

public class SquirrelCompass : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SquirrelPlayer player;
    [SerializeField] private Transform arrow;

    [Header("Position")]
    [SerializeField] private float heightOffset = 0.05f;
    [SerializeField] private float forwardOffset = 0.3f;

    [Header("Distance Colours")]
    [SerializeField] private float closeDistance = 2f;
    
    [SerializeField] private float farDistance = 10f;
    [SerializeField] private Color closeColor = Color.red;

    [SerializeField] private Color midColor = new Color(0.8f, 0.8f, 0.2f);
    [SerializeField] private Color farColor = new Color(0.03f, 0.08f, 0.4f);

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.6f;
    [SerializeField] private float extraVisibleTime = 1f;

    private Renderer arrowRenderer;

    private Vector3 lockedDirection;
    private float lockedDistance;

    private float visibleTimer;
    private float currentAlpha;

    private bool hintWasActive;

    private void Awake()
    {
        if (arrow == null)
            return;

        arrowRenderer = arrow.GetComponentInChildren<Renderer>();
        arrow.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (player == null || arrow == null || arrowRenderer == null)
            return;

        bool hasHint = player.TryGetHint(out Vector3 offset);

        // Capture the direction and distance only once per sniff.
        if (hasHint && !hintWasActive)
        {
            lockedDirection = offset.normalized;
            lockedDistance = offset.magnitude;
            visibleTimer = extraVisibleTime;
        }

        hintWasActive = hasHint;

        if (!hasHint)
            visibleTimer -= Time.deltaTime;

        bool shouldBeVisible = hasHint || visibleTimer > 0f;

        // Smoothly fade in and out.
        float targetAlpha = shouldBeVisible ? 1f : 0f;
        float fadeDuration = shouldBeVisible
            ? fadeInDuration
            : fadeOutDuration;

        currentAlpha = Mathf.MoveTowards(
            currentAlpha,
            targetAlpha,
            Time.deltaTime / Mathf.Max(0.01f, fadeDuration)
        );

        if (currentAlpha <= 0f)
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        arrow.gameObject.SetActive(true);

        // Keep the arrow close to and slightly above the squirrel.
        arrow.position = player.transform.position
    + Vector3.up * heightOffset;

        // Keep pointing in the direction captured during the sniff.
        if (lockedDirection.sqrMagnitude > 0.001f)
        {
            arrow.rotation = Quaternion.LookRotation(
                lockedDirection,
                Vector3.up
                );
            }

        float proximity = Mathf.InverseLerp(
        farDistance,
        closeDistance,
        lockedDistance
    );

    Color color;

    if (proximity < 0.5f)
    {
        // Blend from blue to yellow.
        color = Color.Lerp(farColor, midColor, proximity * 2f);
    }
    else
    {
        // Blend from yellow to red.
        color = Color.Lerp(midColor, closeColor, (proximity - 0.5f) * 2f);
    }

    color.a = currentAlpha;
    arrowRenderer.material.color = color;
            
    }
}
