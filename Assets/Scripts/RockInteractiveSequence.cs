using UnityEngine;
using UnityEngine.Playables;

public class RockInteractiveSequence : MonoBehaviour
{
    [SerializeField] private PlayableDirector timeline;
    [SerializeField] private Transform lookPivot;
    [SerializeField] private Transform birdsLookTarget;
    [SerializeField] private Camera mainCamera;

    [SerializeField] private float lookSensitivity = 90f;
    [SerializeField] private float requiredLookTime = 3f;
    [SerializeField] private float lookAngleThreshold = 12f;

    private bool lookInteractionActive;
    private float lookTimer;
    private float yaw;
    private float pitch;

    private void Update()
    {
        if (!lookInteractionActive)
            return;

        HandleLookInput();
        CheckBirdLookProgress();
    }

    public void StartLookInteraction()
    {
        lookInteractionActive = true;
        lookTimer = 0f;

        if (timeline != null)
            timeline.Pause();

        Vector3 currentEuler = lookPivot.localEulerAngles;
        yaw = currentEuler.y;
        pitch = NormalizeAngle(currentEuler.x);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void EndLookInteraction()
    {
        lookInteractionActive = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (timeline != null)
            timeline.Play();
    }

    private void HandleLookInput()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        lookPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void CheckBirdLookProgress()
    {
        Vector3 directionToBirds = (birdsLookTarget.position - mainCamera.transform.position).normalized;
        float angle = Vector3.Angle(mainCamera.transform.forward, directionToBirds);

        if (angle <= lookAngleThreshold)
        {
            lookTimer += Time.deltaTime;

            if (lookTimer >= requiredLookTime)
                EndLookInteraction();
        }
        else
        {
            lookTimer = 0f;
        }
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }
}