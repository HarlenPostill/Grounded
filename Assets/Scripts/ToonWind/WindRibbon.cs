using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class WindRibbon : MonoBehaviour
{
  [Header("Ribbon shape")]
  public float loopAmplitudeY = 1.2f;
  public float loopAmplitudeZ = 0.4f;
  public float loopFrequencyY = 1.1f;
  public float loopFrequencyZ = 0.7f;
  public float lifetime = 4f;

  [Header("Disappear")]
  public float shrinkDuration = 0.6f;

  [Header("Appearance")]
  public float startWidth = 0.18f;
  public float endWidth = 0.04f;

  TrailRenderer _trail;
  float _elapsed;
  float _trailTime;
  Vector3 _origin;
  float _phase;

  enum State { Moving, Shrinking }
  State _state;

  void Awake()
  {
    _trail = GetComponent<TrailRenderer>();
    _trail.startWidth = startWidth;
    _trail.endWidth = endWidth;
    _trail.minVertexDistance = 0.05f;
  }

  public void Launch(Vector3 origin, Material mat)
  {
    _origin = origin;
    _phase = Random.Range(0f, Mathf.PI * 2f);
    _elapsed = 0f;
    _state = State.Moving;

    _trailTime = lifetime * 0.6f;
    _trail.time = _trailTime;
    _trail.material = mat;
    _trail.Clear();

    transform.position = origin;
    gameObject.SetActive(true);
  }

  void Update()
  {
    _elapsed += Time.deltaTime;

    if (_state == State.Moving)
    {
      MoveRibbon();

      if (_elapsed >= lifetime)
      {
        _state = State.Shrinking;
        _elapsed = 0f;
      }
    }
    else
    {
      float t = Mathf.Clamp01(_elapsed / shrinkDuration);
      _trail.time = Mathf.Lerp(_trailTime, 0f, t);

      if (t >= 1f)
        WindRibbonSpawner.Instance?.Recycle(this);
    }
  }

  void MoveRibbon()
  {
    Vector3 wind = WindController.Instance != null
        ? WindController.Instance.CurrentWind
        : Vector3.right * 4f;

    float t = _elapsed + _phase;
    float dy = Mathf.Sin(t * loopFrequencyY * Mathf.PI * 2f) * loopAmplitudeY;
    float dz = Mathf.Sin(t * loopFrequencyZ * Mathf.PI * 2f) * loopAmplitudeZ;

    transform.position = _origin
                       + wind * _elapsed
                       + transform.up * dy
                       + transform.forward * dz;
  }
}