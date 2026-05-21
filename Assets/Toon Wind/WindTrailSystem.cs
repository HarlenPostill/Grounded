using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simulates a toon-style wind trail ribbon in 3D space.
/// Points travel along a noise-driven wind field and steer around
/// any WindTrailExclusionVolume components in the scene.
///
/// Attach to an empty GameObject. Assign the WindTrailRibbon material.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WindTrailSystem : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Trail Shape")]
    [Tooltip("Number of points along the trail spline.")]
    [Range(8, 128)]
    public int pointCount = 48;

    [Tooltip("World-space length of the trail.")]
    public float trailLength = 6f;

    [Tooltip("Width at the head of the trail.")]
    public float headWidth = 0.18f;

    [Tooltip("Width at the tail (tip).")]
    public float tailWidth = 0.01f;

    [Tooltip("Curve controlling width taper along the trail (0=head, 1=tail).")]
    public AnimationCurve widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Wind Simulation")]
    [Tooltip("Primary direction all trails flow. Set this to match your scene's wind.")]
    public Vector3 windDirection = new Vector3(1f, 0f, 0f);

    [Tooltip("Base speed the trail head moves through the wind field.")]
    public float windSpeed = 2.5f;

    [Tooltip("How much noise turbulence is added on top of the base direction (0 = perfectly straight).")]
    [Range(0f, 1f)]
    public float turbulence = 0.3f;

    [Tooltip("Scale of the Perlin noise field driving turbulence.")]
    public float noiseScale = 0.35f;

    [Tooltip("How quickly the turbulence changes over time.")]
    public float noiseTimeScale = 0.4f;

    [Tooltip("Vertical drift bias — positive bends trail upward.")]
    public float liftBias = 0.15f;

    [Tooltip("How snappily trail points follow the head (higher = tighter trail).")]
    [Range(1f, 30f)]
    public float followStiffness = 8f;

    [Header("Gust")]
    public bool enableGusts = true;
    [Range(0f, 1f)]
    public float gustFrequency = 0.15f;
    public float gustStrength = 3f;
    public float gustDuration = 0.4f;

    [Header("Exclusion Volumes")]
    [Tooltip("Leave empty to auto-find all WindTrailExclusionVolumes in the scene at Start.")]
    public List<WindTrailExclusionVolume> exclusionVolumes = new List<WindTrailExclusionVolume>();

    [Tooltip("How many exclusion deflection iterations to run per frame per point.")]
    [Range(1, 4)]
    public int deflectionIterations = 2;

    [Header("Rendering")]
    public Material trailMaterial;

    [Tooltip("If true, the ribbon always faces the camera. If false it uses the trail's local up.")]
    public bool billboardToCamera = true;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private Vector3[] points;
    private Vector3[] velocities;
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private float noiseOffsetX;
    private float noiseOffsetZ;
    private float gustTimer;
    private float gustCooldown;
    private Vector3 gustVelocity;

    // Reused buffers
    private Vector3[] verts;
    private Vector2[] uvs;
    private int[] tris;
    private Color[] colors;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh { name = "WindTrailRibbon" };
        mesh.MarkDynamic();
        meshFilter.mesh = mesh;

        if (trailMaterial != null)
            meshRenderer.sharedMaterial = trailMaterial;

        // Random noise offset so multiple trails don't move identically
        noiseOffsetX = Random.Range(0f, 999f);
        noiseOffsetZ = Random.Range(0f, 999f);

        InitPoints();
        AllocateBuffers();
    }

    private void Start()
    {
        // Auto-collect exclusion volumes if none assigned
        if (exclusionVolumes.Count == 0)
        {
            exclusionVolumes.AddRange(
                FindObjectsByType<WindTrailExclusionVolume>(FindObjectsSortMode.None));
        }

        gustCooldown = 1f / Mathf.Max(gustFrequency, 0.001f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        HandleGust(dt);
        MoveHead(dt);
        PropagatePoints(dt);
        ApplyExclusionVolumes();
        BuildMesh();
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    private void InitPoints()
    {
        points = new Vector3[pointCount];
        velocities = new Vector3[pointCount];

        // Spread initial points along a gentle line so the trail
        // doesn't start as a collapsed point
        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            points[i] = transform.position + Vector3.right * (t * trailLength);
        }
    }

    private void AllocateBuffers()
    {
        int vertCount = pointCount * 2;
        verts = new Vector3[vertCount];
        uvs = new Vector2[vertCount];
        colors = new Color[vertCount];

        int quadCount = pointCount - 1;
        tris = new int[quadCount * 6];
    }

    // -------------------------------------------------------------------------
    // Simulation
    // -------------------------------------------------------------------------

    private void HandleGust(float dt)
    {
        if (!enableGusts) return;

        gustTimer += dt;
        if (gustTimer >= gustCooldown)
        {
            gustTimer = 0f;
            gustCooldown = (1f / Mathf.Max(gustFrequency, 0.001f))
                           * Random.Range(0.5f, 1.5f);

            // Random horizontal gust direction
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            gustVelocity = new Vector3(
                Mathf.Cos(angle), Random.Range(-0.2f, 0.4f), Mathf.Sin(angle))
                * gustStrength;

            // Decay gust over its duration using a coroutine-free approach —
            // we blend it out in MoveHead using Time.time
        }
    }

    private void MoveHead(float dt)
    {
        float t = Time.time;

        // Base flow: normalised wind direction set in the inspector
        Vector3 baseDir = windDirection.sqrMagnitude > 0.001f
            ? windDirection.normalized
            : Vector3.right;

        // Turbulence: small noise offsets perpendicular to the base direction
        // We only perturb along the two axes orthogonal to wind so the trail
        // never flies backwards against the wind direction.
        Vector3 perp1 = Vector3.Cross(baseDir, Vector3.up);
        if (perp1.sqrMagnitude < 0.001f)
            perp1 = Vector3.Cross(baseDir, Vector3.forward);
        perp1.Normalize();
        Vector3 perp2 = Vector3.Cross(baseDir, perp1).normalized;

        float n1 = Mathf.PerlinNoise(
            points[0].magnitude * noiseScale + noiseOffsetX,
            t * noiseTimeScale) * 2f - 1f;

        float n2 = Mathf.PerlinNoise(
            points[0].magnitude * noiseScale + noiseOffsetZ,
            t * noiseTimeScale + 17.3f) * 2f - 1f;

        Vector3 turbulenceOffset = (perp1 * n1 + perp2 * (n2 * 0.4f + liftBias)) * turbulence;

        Vector3 windDir = (baseDir + turbulenceOffset).normalized;

        // Gust contribution (decays over gustDuration)
        float gustBlend = 1f - Mathf.Clamp01(gustTimer / gustDuration);
        Vector3 totalVelocity = windDir * windSpeed + gustVelocity * gustBlend;

        velocities[0] = totalVelocity;
        points[0] += totalVelocity * dt;
    }

    private void PropagatePoints(float dt)
    {
        float segLength = trailLength / (pointCount - 1);

        for (int i = 1; i < pointCount; i++)
        {
            // Spring follow: each point chases the one ahead
            Vector3 target = points[i - 1];
            Vector3 diff = target - points[i];

            velocities[i] += diff * followStiffness * dt;
            velocities[i] *= 1f - Mathf.Clamp01(dt * 4f); // damping

            points[i] += velocities[i] * dt;

            // Hard constraint: enforce segment length to prevent stretching
            Vector3 toPoint = points[i] - points[i - 1];
            if (toPoint.magnitude > segLength * 1.5f)
                points[i] = points[i - 1] + toPoint.normalized * segLength * 1.5f;
        }
    }

    private void ApplyExclusionVolumes()
    {
        if (exclusionVolumes.Count == 0) return;

        for (int iter = 0; iter < deflectionIterations; iter++)
        {
            foreach (var vol in exclusionVolumes)
            {
                if (vol == null) continue;

                for (int i = 0; i < pointCount; i++)
                {
                    if (vol.GetDeflection(points[i], out Vector3 deflect))
                    {
                        points[i] += deflect * Time.deltaTime;

                        // Also redirect velocity so the point doesn't
                        // immediately re-enter the volume next frame
                        velocities[i] += deflect * 0.5f;
                    }
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Mesh building
    // -------------------------------------------------------------------------

    private void BuildMesh()
    {
        Camera cam = Camera.main;

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            float halfW = Mathf.Lerp(headWidth, tailWidth, widthCurve.Evaluate(t)) * 0.5f;

            // Tangent along the trail
            Vector3 tangent;
            if (i == 0)
                tangent = (points[1] - points[0]).normalized;
            else if (i == pointCount - 1)
                tangent = (points[i] - points[i - 1]).normalized;
            else
                tangent = (points[i + 1] - points[i - 1]).normalized;

            // Ribbon normal — billboard or fixed up
            Vector3 toCamera = billboardToCamera && cam != null
                ? (cam.transform.position - points[i]).normalized
                : Vector3.up;

            Vector3 right = Vector3.Cross(tangent, toCamera).normalized;

            // Fallback if tangent and toCamera are parallel
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(tangent, Vector3.forward).normalized;

            int v0 = i * 2;
            int v1 = i * 2 + 1;

            verts[v0] = points[i] - transform.position + right * halfW;
            verts[v1] = points[i] - transform.position - right * halfW;

            uvs[v0] = new Vector2(t, 0f);
            uvs[v1] = new Vector2(t, 1f);

            // Fade alpha at the tail for a dissolve effect
            float alpha = 1f - widthCurve.Evaluate(t) < 0.05f ? 0f : 1f;
            alpha = Mathf.Clamp01((1f - t) * 4f); // quick fade near tail
            colors[v0] = colors[v1] = new Color(1, 1, 1, alpha);
        }

        // Build triangle indices (only once if pointCount unchanged)
        int ti = 0;
        for (int i = 0; i < pointCount - 1; i++)
        {
            int a = i * 2, b = i * 2 + 1;
            int c = (i + 1) * 2, d = (i + 1) * 2 + 1;

            tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
            tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Teleport the trail head to a new world position.</summary>
    public void SetHeadPosition(Vector3 worldPos)
    {
        Vector3 delta = worldPos - points[0];
        for (int i = 0; i < pointCount; i++)
            points[i] += delta;
    }

    /// <summary>Instantly re-register exclusion volumes (call after scene changes).</summary>
    public void RefreshExclusionVolumes()
    {
        exclusionVolumes.Clear();
        exclusionVolumes.AddRange(
            FindObjectsByType<WindTrailExclusionVolume>(FindObjectsSortMode.None));
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (points == null || points.Length < 2) return;

        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
        for (int i = 0; i < points.Length - 1; i++)
            Gizmos.DrawLine(points[i], points[i + 1]);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(points[0], 0.06f);
    }
#endif
}