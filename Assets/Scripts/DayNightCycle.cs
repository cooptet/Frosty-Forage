
using UnityEngine;
using UnityEngine.Events;

public class DayNightCycle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light sun;

    [Header("Time")]
    [SerializeField] private float dayDuration = 480f;

    [Range(0f, 1f)]
    [SerializeField] private float timeOfDay = 0f;

    [Header("Lighting")]
    [SerializeField] private AnimationCurve sunlightIntensity =
        AnimationCurve.EaseInOut(0f, 0.15f, 1f, 0f);

    [SerializeField] private Gradient sunlightColor;

    [SerializeField] private Gradient ambientColor;

    [Header("Game End")]
    public UnityEvent onNightReached;

    private bool hasEnded;

    private void Start()
    {
        // Start just after sunrise.
        timeOfDay = 0.02f;
        UpdateEnvironment();
    }

    private void Update()
    {
        if (hasEnded) return;

        timeOfDay += Time.deltaTime / dayDuration;
        timeOfDay = Mathf.Clamp01(timeOfDay);

        UpdateEnvironment();

        if (timeOfDay >= 1f)
        {
            hasEnded = true;
            onNightReached?.Invoke();
        }
    }

    private void UpdateEnvironment()
    {
        // Rotate the sun from sunrise to sunset.
        float sunAngle = Mathf.Lerp(0f, 180f, timeOfDay);
        sun.transform.rotation = Quaternion.Euler(
            sunAngle, 170f, 0f
        );

        // Update sunlight.
        sun.intensity = sunlightIntensity.Evaluate(timeOfDay);
        sun.color = sunlightColor.Evaluate(timeOfDay);

        // Update ambient lighting.
        RenderSettings.ambientLight =
            ambientColor.Evaluate(timeOfDay);
    }
}
