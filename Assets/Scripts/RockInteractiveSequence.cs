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

    [Header("Look UI")]
    [SerializeField] private GameObject lookInteractionUI;
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

    [Header("Look Exit Settings")]
    [SerializeField] private float cameraReturnDuration = 0.4f;

    [Header("Jump Interaction")]
    [SerializeField] private Transform topRockPivot;
    [SerializeField] private GameObject jumpInteractionUI;
    [SerializeField] private TextMeshProUGUI jumpPromptText;
    [SerializeField] private int requiredJumpPresses = 3;
    [SerializeField] private float jumpHeight = 0.35f;
    [SerializeField] private float jumpDuration = 0.35f;
    [SerializeField] private float jumpSquashAmount = 0.12f;
    [SerializeField] private float jumpTiltAmount = 5f;
    [SerializeField] private double jumpResumeTime = -1;

    private bool lookInteractionActive;
    private bool isReturningCamera;
    private float lookTimer;
    private float yaw;
    private float pitch;
    private Quaternion originalCM04Rotation;

    private bool jumpInteractionActive;
    private bool jumpInProgress;
    private int jumpPressCount;
    private Vector3 jumpStartLocalPosition;
    private Quaternion jumpStartLocalRotation;
    private Vector3 jumpStartLocalScale;

    private void Start()
    {
        SetLookUI(false);
        SetJumpUI(false);
    }

    private void Update()
    {
        if (lookInteractionActive && !isReturningCamera)
        {
            HandleLookInput();
            CheckBirdLookProgress();
            UpdateLookUI();
        }

        if (jumpInteractionActive && !jumpInProgress && Input.GetKeyDown(KeyCode.Space))
            StartCoroutine(DoRockJump());
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

    public void StartJumpInteraction()
    {
        if (jumpInteractionActive || jumpInProgress)
            return;

        Debug.Log("JUMP INTERACTION STARTED");

        if (timeline != null)
            timeline.Pause();

        jumpInteractionActive = true;
        jumpInProgress = false;
        jumpPressCount = 0;

        if (topRockPivot != null)
        {
            jumpStartLocalPosition = topRockPivot.localPosition;
            jumpStartLocalRotation = topRockPivot.localRotation;
            jumpStartLocalScale = topRockPivot.localScale;
        }

        SetJumpUI(true);
        UpdateJumpPrompt();
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

    private IEnumerator DoRockJump()
    {
        if (topRockPivot == null)
            yield break;

        jumpInProgress = true;
        jumpPressCount++;

        UpdateJumpPrompt();

        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);

            float jumpArc = Mathf.Sin(t * Mathf.PI);
            float squashCurve = Mathf.Sin(t * Mathf.PI * 2f);
            float tiltCurve = Mathf.Sin(t * Mathf.PI);

            Vector3 position = jumpStartLocalPosition + Vector3.up * (jumpArc * jumpHeight);
            Quaternion rotation = jumpStartLocalRotation * Quaternion.Euler(0f, 0f, tiltCurve * jumpTiltAmount);

            float squashX = 1f + Mathf.Max(0f, -squashCurve) * jumpSquashAmount;
            float squashY = 1f + Mathf.Max(0f, squashCurve) * jumpSquashAmount;

            topRockPivot.localPosition = position;
            topRockPivot.localRotation = rotation;
            topRockPivot.localScale = new Vector3(
                jumpStartLocalScale.x * squashX,
                jumpStartLocalScale.y * squashY,
                jumpStartLocalScale.z * squashX
            );

            yield return null;
        }

        topRockPivot.localPosition = jumpStartLocalPosition;
        topRockPivot.localRotation = jumpStartLocalRotation;
        topRockPivot.localScale = jumpStartLocalScale;

        jumpInProgress = false;

        if (jumpPressCount >= requiredJumpPresses)
            EndJumpInteraction();
    }

    private void EndJumpInteraction()
    {
        Debug.Log("JUMP INTERACTION ENDED");

        jumpInteractionActive = false;
        jumpInProgress = false;

        if (topRockPivot != null)
        {
            topRockPivot.localPosition = jumpStartLocalPosition;
            topRockPivot.localRotation = jumpStartLocalRotation;
            topRockPivot.localScale = jumpStartLocalScale;
        }

        SetJumpUI(false);

        if (timeline != null)
        {
            if (jumpResumeTime >= 0)
                timeline.time = jumpResumeTime;

            timeline.Play();
        }
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
        if (lookInteractionUI != null)
            lookInteractionUI.SetActive(active);

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

    private void SetJumpUI(bool active)
    {
        if (jumpInteractionUI != null)
            jumpInteractionUI.SetActive(active);

        if (jumpPromptText != null)
            jumpPromptText.gameObject.SetActive(active);
    }

    private void UpdateJumpPrompt()
    {
        if (jumpPromptText == null)
            return;

        jumpPromptText.text = $"PRESS SPACE TO JUMP [{jumpPressCount}/{requiredJumpPresses}]";
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }
}