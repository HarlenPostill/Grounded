using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class WindRibbon : MonoBehaviour
{
    [Header("Ribbon shape")]
    public float loopAmplitudeY = 1.2f;     // vertical loop height
    public float loopAmplitudeZ = 0.4f;     // depth wobble
    public float loopFrequencyY = 1.1f;     // how fast it curls vertically
    public float loopFrequencyZ = 0.7f;     // depth curl frequency
    public float lifetime = 4f;             // seconds before recycling

    [Header("Appearance")]
    public float startWidth = 0.18f;
    public float endWidth   = 0.04f;

    TrailRenderer _trail;
    float         _elapsed;
    Vector3       _origin;
    float         _phase;                   // random phase offset per ribbon

    void Awake()
    {
        _trail = GetComponent<TrailRenderer>();
        _trail.startWidth  = startWidth;
        _trail.endWidth    = endWidth;
        _trail.time        = lifetime * 0.6f;  // trail lingers 60% of lifetime
        _trail.minVertexDistance = 0.05f;
    }

    public void Launch(Vector3 origin, Material mat)
    {
        _origin  = origin;
        _phase   = Random.Range(0f, Mathf.PI * 2f);
        _elapsed = 0f;
        transform.position = origin;
        _trail.material    = mat;
        _trail.Clear();
        gameObject.SetActive(true);
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        // --- Base drift from global wind ---
        Vector3 wind = WindController.Instance != null
            ? WindController.Instance.CurrentWind
            : Vector3.right * 4f;

        // --- Ribbon loop offset (sine on Y and Z) ---
        float t  = _elapsed + _phase;
        float dy = Mathf.Sin(t * loopFrequencyY * Mathf.PI * 2f) * loopAmplitudeY;
        float dz = Mathf.Sin(t * loopFrequencyZ * Mathf.PI * 2f) * loopAmplitudeZ;

        // Advance position = drift + loop
        transform.position = _origin
                           + wind * _elapsed
                           + transform.up    * dy
                           + transform.forward * dz;

        // Recycle when lifetime exceeded or drifted far
        if (_elapsed >= lifetime)
            WindRibbonSpawner.Instance?.Recycle(this);
    }
}