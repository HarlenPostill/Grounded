using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

public class RockInteractiveSequence : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector timeline;

    [Header("Look Interaction")]
    [SerializeField] private Transform cm04Camera;
    [SerializeField] private Transform birdsLookTarget;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject topRockVisibleModel;

    [Header("UI")]
    [SerializeField] private GameObject lookInteractionCanvas;
    [SerializeField] private RectTransform lookProgressFill;
    [SerializeField] private TextMeshProUGUI leftBracket;
    [SerializeField] private TextMeshProUGUI rightBracket;
    [SerializeField] private Color reticleNormalColor = Color.white;
    [SerializeField] private Color reticleActiveColor = Color.green;

    [Header("Look Settings")]
    [SerializeField] private float lookSensitivity = 85f;
    [SerializeField] private float requiredLookTime = 3f;
    [SerializeField] private float lookAngleThreshold = 20f;
    [SerializeField] private float minPitch = -55f;
    [SerializeField] private float maxPitch = 55f;

    [Header("Exit Settings")]
    [SerializeField] private float cameraReturnDuration = 0.4f;

    private bool lookInteractionActive;
    private bool isReturningCamera;
    private float lookTimer;
    private float yaw;
    private float pitch;
    private Quaternion originalCM04Rotation;

    private void Start()
    {
        SetLookUI(false);
    }

    private void Update()
    {
        if (!lookInteractionActive || isReturningCamera)
            return;

        HandleLookInput();
        CheckBirdLookProgress();
        UpdateLookUI();
    }

    public void StartLookInteraction()
    {
        if (lookInteractionActive || isReturningCamera)
            return;

        Debug.Log("LOOK INTERACTION STARTED");

        if (timeline != null)
            timeline.Pause();

        if (topRockVisibleModel != null)
            topRockVisibleModel.SetActive(false);

        originalCM04Rotation = cm04Camera.rotation;

        Vector3 euler = cm04Camera.eulerAngles;
        yaw = euler.y;
        pitch = NormalizeAngle(euler.x);

        lookTimer = 0f;
        lookInteractionActive = true;

        SetLookUI(true);
        SetReticleActive(false);
        UpdateLookUI();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandleLookInput()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * lookSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxisRaw("Mouse Y") * lookSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cm04Camera.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void CheckBirdLookProgress()
    {
        Vector3 directionToBirds = (birdsLookTarget.position - mainCamera.transform.position).normalized;
        float angle = Vector3.Angle(mainCamera.transform.forward, directionToBirds);
        bool isLookingAtBirds = angle <= lookAngleThreshold;

        if (isLookingAtBirds)
        {
            lookTimer += Time.deltaTime;

            if (lookTimer >= requiredLookTime)
                EndLookInteraction();
        }
        else
        {
            lookTimer = 0f;
        }

        SetReticleActive(isLookingAtBirds);
    }

    private void EndLookInteraction()
    {
        if (!lookInteractionActive)
            return;

        Debug.Log("LOOK INTERACTION ENDED");

        lookInteractionActive = false;
        isReturningCamera = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetLookUI(false);

        StartCoroutine(ReturnCameraThenResume());
    }

    private IEnumerator ReturnCameraThenResume()
    {
        Quaternion startRotation = cm04Camera.rotation;
        float elapsed = 0f;

        while (elapsed < cameraReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cameraReturnDuration);
            t = t * t * (3f - 2f * t);

            cm04Camera.rotation = Quaternion.Slerp(startRotation, originalCM04Rotation, t);

            yield return null;
        }

        cm04Camera.rotation = originalCM04Rotation;

        if (topRockVisibleModel != null)
            topRockVisibleModel.SetActive(true);

        isReturningCamera = false;

        if (timeline != null)
            timeline.Play();
    }

    private void UpdateLookUI()
    {
        if (lookProgressFill == null)
            return;

        float progress = Mathf.Clamp01(lookTimer / requiredLookTime);
        lookProgressFill.localScale = new Vector3(progress, 1f, 1f);
    }

    private void SetLookUI(bool active)
    {
        if (lookInteractionCanvas != null)
            lookInteractionCanvas.SetActive(active);

        if (lookProgressFill != null)
            lookProgressFill.localScale = new Vector3(0f, 1f, 1f);

        SetReticleActive(false);
    }

    private void SetReticleActive(bool active)
    {
        Color colour = active ? reticleActiveColor : reticleNormalColor;

        if (leftBracket != null)
            leftBracket.color = colour;

        if (rightBracket != null)
            rightBracket.color = colour;
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }
}