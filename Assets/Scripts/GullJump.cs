using System.Collections;
using UnityEngine;

public class GullJump : MonoBehaviour
{
  [Header("Hop Settings")]
  public float hopHeight = 0.3f;
  public float hopDuration = 0.25f;
  public float turnAngle = 20f;

  [Header("Timing")]
  public float minWaitTime = 1.0f;
  public float maxWaitTime = 3.5f;

  private Vector3 groundPosition;
  private Quaternion baseRotation;

  void Start()
  {
    groundPosition = transform.position;
    baseRotation = transform.rotation;
    StartCoroutine(HopSequence());
  }

  IEnumerator HopSequence()
  {
    while (true)
    {
      // Wait a random time before the next sequence
      float waitTime = Random.Range(minWaitTime, maxWaitTime);
      yield return new WaitForSeconds(waitTime);

      // Hop 1: in place
      yield return StartCoroutine(DoHop(0f));

      yield return new WaitForSeconds(0.1f);

      // Hop 2: turn left 20 degrees
      yield return StartCoroutine(DoHop(-turnAngle));

      yield return new WaitForSeconds(0.1f);

      // Hop 3: in place (facing left)
      yield return StartCoroutine(DoHop(0f));

      yield return new WaitForSeconds(0.1f);

      // Hop 4: turn right 20 degrees (back to original)
      yield return StartCoroutine(DoHop(turnAngle));
    }
  }

  IEnumerator DoHop(float yRotationDelta)
  {
    float elapsed = 0f;
    Quaternion startRotation = transform.rotation;
    Quaternion endRotation = startRotation * Quaternion.Euler(0f, yRotationDelta, 0f);

    while (elapsed < hopDuration)
    {
      elapsed += Time.deltaTime;
      float t = elapsed / hopDuration;

      // Arc: sin curve so it goes up then comes back down
      float height = Mathf.Sin(t * Mathf.PI) * hopHeight;
      transform.position = groundPosition + Vector3.up * height;

      // Rotate during the hop
      transform.rotation = Quaternion.Lerp(startRotation, endRotation, t);

      yield return null;
    }

    transform.position = groundPosition;
    transform.rotation = endRotation;

    // Update base so cumulative rotation is tracked
    // (turns cancel out over the full sequence, but we track per-hop)
  }
}
