using UnityEngine;

/// <summary>
/// Place this component on any GameObject to create an exclusion zone that
/// wind trails will steer around. Supports Box and Sphere shapes.
/// Resize using the Transform scale (box) or the Radius field (sphere).
/// </summary>
public class WindTrailExclusionVolume : MonoBehaviour
{
    public enum VolumeShape { Box, Sphere }

    [Header("Shape")]
    public VolumeShape shape = VolumeShape.Box;

    [Tooltip("Only used when Shape = Sphere. Box shape uses Transform scale directly.")]
    public float radius = 1f;

    [Header("Deflection")]
    [Tooltip("How strongly trail points are pushed away from the surface.")]
    [Range(0.5f, 10f)]
    public float deflectionStrength = 3f;

    [Tooltip("Distance outside the volume at which deflection begins ramping up.")]
    [Range(0f, 2f)]
    public float influenceMargin = 0.3f;

    // -------------------------------------------------------------------------
    // Public API used by WindTrailSystem
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if worldPoint is inside the exclusion volume (plus margin).
    /// Also outputs the deflection vector to push the point clear.
    /// </summary>
    public bool GetDeflection(Vector3 worldPoint, out Vector3 deflection)
    {
        deflection = Vector3.zero;

        if (shape == VolumeShape.Sphere)
            return SphereDeflection(worldPoint, out deflection);
        else
            return BoxDeflection(worldPoint, out deflection);
    }

    // -------------------------------------------------------------------------
    // Sphere
    // -------------------------------------------------------------------------
    private bool SphereDeflection(Vector3 worldPoint, out Vector3 deflection)
    {
        deflection = Vector3.zero;
        float outerRadius = radius + influenceMargin;
        Vector3 toPoint = worldPoint - transform.position;
        float dist = toPoint.magnitude;

        if (dist >= outerRadius) return false;

        // Avoid divide-by-zero at dead centre
        Vector3 dir = dist > 0.001f ? toPoint / dist : Vector3.up;

        // Ramp: 0 at outerRadius, 1 at surface, >1 inside volume
        float t = 1f - Mathf.Clamp01((dist - radius) / influenceMargin);
        deflection = dir * deflectionStrength * (1f + t);
        return true;
    }

    // -------------------------------------------------------------------------
    // Box
    // -------------------------------------------------------------------------
    private bool BoxDeflection(Vector3 worldPoint, out Vector3 deflection)
    {
        deflection = Vector3.zero;

        // Transform point into local space of the box
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        Vector3 halfExt = Vector3.one * 0.5f; // local extents (scale baked into transform)
        Vector3 margin = new Vector3(
            influenceMargin / transform.lossyScale.x,
            influenceMargin / transform.lossyScale.y,
            influenceMargin / transform.lossyScale.z);
        Vector3 outerHalf = halfExt + margin;

        // Outside even the margin?
        if (Mathf.Abs(local.x) > outerHalf.x ||
            Mathf.Abs(local.y) > outerHalf.y ||
            Mathf.Abs(local.z) > outerHalf.z)
            return false;

        // Find the closest face and its penetration depth
        float px = outerHalf.x - Mathf.Abs(local.x);
        float py = outerHalf.y - Mathf.Abs(local.y);
        float pz = outerHalf.z - Mathf.Abs(local.z);

        Vector3 localDeflect;
        if (px < py && px < pz)
            localDeflect = new Vector3(Mathf.Sign(local.x) * px, 0, 0);
        else if (py < pz)
            localDeflect = new Vector3(0, Mathf.Sign(local.y) * py, 0);
        else
            localDeflect = new Vector3(0, 0, Mathf.Sign(local.z) * pz);

        // Ramp t: how far inside are we relative to margin
        float minPen = Mathf.Min(px, py, pz);
        float marginLocal = Mathf.Min(margin.x, margin.y, margin.z);
        float t = marginLocal > 0f ? 1f - Mathf.Clamp01(minPen / marginLocal) : 1f;

        // Back to world space
        deflection = transform.TransformDirection(localDeflect) * deflectionStrength * (1f + t);
        return true;
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawGizmo(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmo(true);
    }

    private void DrawGizmo(bool selected)
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Color col = selected ? new Color(0.2f, 0.8f, 1f, 0.5f) : new Color(0.2f, 0.8f, 1f, 0.15f);

        if (shape == VolumeShape.Box)
        {
            // Inner solid volume
            Gizmos.color = col;
            Gizmos.DrawCube(Vector3.zero, Vector3.one);

            // Outer influence margin (wireframe)
            Gizmos.color = new Color(col.r, col.g, col.b, col.a * 0.4f);
            Vector3 marginScale = new Vector3(
                1f + influenceMargin * 2f / transform.lossyScale.x,
                1f + influenceMargin * 2f / transform.lossyScale.y,
                1f + influenceMargin * 2f / transform.lossyScale.z);
            Gizmos.DrawWireCube(Vector3.zero, marginScale);
        }
        else
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = col;
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.color = new Color(col.r, col.g, col.b, col.a * 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius + influenceMargin);
        }
    }
#endif
}
