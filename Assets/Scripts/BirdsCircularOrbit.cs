using UnityEngine;

public class BirdsCircularOrbit : MonoBehaviour
{
    [SerializeField] private Transform birdsParent;
    [SerializeField] private float orbitSpeed = 30f;
    [SerializeField] private bool faceMovementDirection = true;

    private float radius;
    private float heightOffset;
    private float angle;

    private void Start()
    {
        Vector3 offset = birdsParent.position - transform.position;

        radius = new Vector2(offset.x, offset.z).magnitude;
        heightOffset = offset.y;
        angle = Mathf.Atan2(offset.z, offset.x);
    }

    private void Update()
    {
        angle += orbitSpeed * Mathf.Deg2Rad * Time.deltaTime;

        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;

        birdsParent.position = transform.position + new Vector3(x, heightOffset, z);

        if (faceMovementDirection)
        {
            Vector3 direction = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));

            if (direction.sqrMagnitude > 0.001f)
                birdsParent.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}