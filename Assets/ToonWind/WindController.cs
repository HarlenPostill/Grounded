using UnityEngine;

public class WindController : MonoBehaviour
{
    public static WindController Instance { get; private set; }

    [Header("Wind")]
    public Vector3 windDirection = Vector3.right;
    [Range(0f, 20f)] public float windStrength = 5f;

    [Header("Gust")]
    [Range(0f, 1f)] public float gustFrequency = 0.3f;
    [Range(0f, 1f)] public float gustAmplitude = 0.5f;

    public Vector3 CurrentWind => windDirection.normalized
                                   * windStrength
                                   * (1f + Mathf.Sin(Time.time * gustFrequency * Mathf.PI * 2f) * gustAmplitude);

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }
}