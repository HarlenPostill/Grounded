using UnityEngine;

public class GullFly : MonoBehaviour
{
    [Header("Wing Bones")]
    public Transform leftWingBone;
    public Transform leftElbowBone;
    public Transform leftTipBone;
    public Transform rightWingBone;
    public Transform rightElbowBone;
    public Transform rightTipBone;

    [Header("Flap Animation")]
    [Tooltip("Flaps per second")]
    public float flapSpeed = 2f;

    [Tooltip("Up/down rotation range of the main wing bone (degrees)")]
    public float wingFlapRange = 35f;

    [Tooltip("Elbow folds in on downstroke (degrees)")]
    public float elbowFoldAngle = 20f;

    [Tooltip("Tip trails slightly behind the flap (degrees)")]
    public float tipLagAngle = 10f;

    [Tooltip("Soar: gentle drift (true) vs actively flapping (false)")]
    public bool soaring = false;

    [Tooltip("Wing drift range while soaring (degrees)")]
    public float soarDriftAngle = 4f;

    [Tooltip("Speed of the gentle soar drift")]
    public float soarDriftSpeed = 0.4f;

    [Tooltip("How quickly the wing transitions between flap and soar pose")]
    public float transitionSpeed = 3f;

    [Header("Trail Renderers")]
    public TrailRenderer leftTipTrail;
    public TrailRenderer rightTipTrail;

    [Tooltip("Trail lifetime in seconds")]
    public float trailTime = 0.4f;

    [Tooltip("Trail width at origin")]
    public float trailWidth = 0.04f;

    // Internal state
    private float _flapPhase = 0f;
    private float _blendWeight = 1f; // 1 = full flap, 0 = soar

    // Resting local rotations captured on Start
    private Quaternion _leftWingRest, _leftElbowRest, _leftTipRest;
    private Quaternion _rightWingRest, _rightElbowRest, _rightTipRest;

    void Start()
    {
        CacheRestPoses();
        SetupTrails();
    }

    void Update()
    {
        _flapPhase += Time.deltaTime * flapSpeed * Mathf.PI * 2f;

        float targetBlend = soaring ? 0f : 1f;
        _blendWeight = Mathf.MoveTowards(_blendWeight, targetBlend, Time.deltaTime * transitionSpeed);

        AnimateWings();
    }

    // -------------------------------------------------------------------------
    void CacheRestPoses()
    {
        if (leftWingBone)  _leftWingRest  = leftWingBone.localRotation;
        if (leftElbowBone) _leftElbowRest = leftElbowBone.localRotation;
        if (leftTipBone)   _leftTipRest   = leftTipBone.localRotation;

        if (rightWingBone)  _rightWingRest  = rightWingBone.localRotation;
        if (rightElbowBone) _rightElbowRest = rightElbowBone.localRotation;
        if (rightTipBone)   _rightTipRest   = rightTipBone.localRotation;
    }

    void SetupTrails()
    {
        foreach (var trail in new[] { leftTipTrail, rightTipTrail })
        {
            if (trail == null) continue;
            trail.time = trailTime;
            trail.startWidth = trailWidth;
            trail.endWidth = 0f;

            // Soft white gradient that fades to transparent
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]  { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]  { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            trail.colorGradient = gradient;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }
    }

    void AnimateWings()
    {
        // sin wave: +1 = top of stroke, -1 = bottom
        float wave = Mathf.Sin(_flapPhase);
        // Tip lags behind by ~90 degrees
        float tipWave = Mathf.Sin(_flapPhase - Mathf.PI * 0.5f);
        // Slow drift wave used while soaring
        float soarWave = Mathf.Sin(Time.time * soarDriftSpeed * Mathf.PI * 2f);

        // Wing root: rotates around Z (up-down flap)
        float wingAngle  = wave * wingFlapRange * _blendWeight
                         + soarWave * soarDriftAngle * (1f - _blendWeight);
        // Elbow: folds slightly on downstroke (wave < 0)
        float elbowAngle = Mathf.Max(0f, -wave) * elbowFoldAngle * _blendWeight;
        // Tip: subtle lag
        float tipAngle   = tipWave * tipLagAngle * _blendWeight
                         + soarWave * (soarDriftAngle * 0.5f) * (1f - _blendWeight);

        if (leftWingBone)
            leftWingBone.localRotation  = _leftWingRest  * Quaternion.Euler(0f, 0f,  wingAngle);
        if (leftElbowBone)
            leftElbowBone.localRotation = _leftElbowRest * Quaternion.Euler(0f, 0f, -elbowAngle);
        if (leftTipBone)
            leftTipBone.localRotation   = _leftTipRest   * Quaternion.Euler(0f, 0f,  tipAngle);

        // Right side uses same local rotation direction — the mirrored bone axes handle symmetry
        if (rightWingBone)
            rightWingBone.localRotation  = _rightWingRest  * Quaternion.Euler(0f, 0f,  wingAngle);
        if (rightElbowBone)
            rightElbowBone.localRotation = _rightElbowRest * Quaternion.Euler(0f, 0f, -elbowAngle);
        if (rightTipBone)
            rightTipBone.localRotation   = _rightTipRest   * Quaternion.Euler(0f, 0f,  tipAngle);
    }
}
