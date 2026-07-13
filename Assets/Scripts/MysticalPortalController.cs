using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MysticalPortalController : MonoBehaviour
{
    public event System.Action HuntActivated;
    public event System.Action PortalEscaped;

    public bool IsHuntActivated => sequenceActivated;

    [Header("Referenzen")]
    [Tooltip("Hier das komplette Complete XR Origin Objekt eintragen.")]
    [SerializeField] private Transform player;

    [Tooltip("Hier die Main Camera innerhalb des XR Origin eintragen.")]
    [SerializeField] private Transform playerCamera;

    [Tooltip("Das sichtbare Portalmodell. Falls leer, wird das gesamte Portalobjekt skaliert.")]
    [SerializeField] private Transform portalVisual;

    [Tooltip("Fluchtpunkte in der gewünschten Reihenfolge.")]
    [SerializeField] private Transform[] escapePoints;

    [Header("Portal-Ausrichtung")]
    [SerializeField] private bool alwaysFacePlayer = true;

    [Tooltip("Falls die Rückseite zum Player zeigt, meistens Y = 180 verwenden.")]
    [SerializeField] private Vector3 faceRotationOffset = Vector3.zero;

    [Header("Schweben")]
    [SerializeField] private bool useStartYAsFixedHeight = true;
    [SerializeField] private float fixedYHeight = 2f;
    [SerializeField] private float floatAmplitude = 0.25f;
    [SerializeField] private float floatSpeed = 1.2f;

    [Header("Erste Aktivierung durch Stepping-Stone-Trigger")]
    [Tooltip("Zeit zwischen dem Stepping-Stone-Trigger und der ersten Flucht.")]
    [SerializeField] private float firstEscapeDelay = 5f;

    [Header("Fluchtbewegung")]
    [SerializeField] private float escapeMoveSpeed = 3f;
    [SerializeField] private float arrivalDistance = 0.05f;

    [Header("Finale Portal-Sequenz")]
    [Tooltip("Kurze Pause, bevor das Portal den Player verschlingt.")]
    [SerializeField] private float beforeFinalDelay = 0.1f;

    [Tooltip("Wie schnell das Portal abrupt groß wird.")]
    [SerializeField] private float finalExpansionDuration = 0.12f;

    [SerializeField] private float finalExpansionMultiplier = 3f;

    [Tooltip("Wie schnell das Portal auf die XR-Kamera zufliegt.")]
    [SerializeField] private float finalMoveDuration = 0.7f;

    [Tooltip("Endabstand des Portalzentrums vor der Kamera.")]
    [SerializeField] private float finalEndDistance = 0.12f;

    [Tooltip("Das Portal folgt während des Finales der aktuellen Blickrichtung.")]
    [SerializeField] private bool followHeadDirectionDuringFinal = true;

    [Tooltip("Zusätzlicher Versatz relativ zur XR-Kamera.")]
    [SerializeField] private Vector3 finalCameraLocalOffset = Vector3.zero;

    [Header("Blackout / Verschlucken")]
    [Tooltip("Kann leer bleiben, wenn das Canvas automatisch erstellt werden soll.")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [SerializeField] private bool createFadeCanvasAutomatically = true;

    [Tooltip("Abstand des schwarzen Canvas vor der XR-Kamera.")]
    [SerializeField] private float fadeCanvasDistanceFromCamera = 0.3f;

    [Tooltip("Dauer der Schwarzblende.")]
    [SerializeField] private float blackoutFadeDuration = 0.15f;

    [Tooltip("Kurze schwarze Pause, bevor der Text erscheint.")]
    [SerializeField] private float afterBlackoutDelay = 0.5f;

    [Header("Weisheitstext")]
    [Tooltip("Kann leer bleiben, wenn der Text automatisch erstellt wird.")]
    [SerializeField] private TMP_Text wisdomText;

    [SerializeField] private bool createWisdomTextAutomatically = true;

    [TextArea(2, 6)]
    [SerializeField] private string wisdomMessage =
        "Not all chaos must be conquered.\n" +
        "Sometimes, you simply have to step away.";

    [Tooltip("Dauer des Einblendens.")]
    [SerializeField] private float textFadeInDuration = 0.8f;

    [Tooltip("Wie lange der Text vollständig sichtbar bleibt.")]
    [SerializeField] private float textVisibleDuration = 3.5f;

    [Tooltip("Dauer des Ausblendens.")]
    [SerializeField] private float textFadeOutDuration = 0.8f;

    [Tooltip("Kurze Pause nach dem Text vor dem Szenenwechsel.")]
    [SerializeField] private float afterTextDelay = 0.3f;

    [SerializeField] private float wisdomFontSize = 54f;
    [SerializeField] private Color wisdomTextColor = Color.white;

    [Header("Szenenwechsel")]
    [Tooltip("Exakter Name der nächsten Szene. Bei leerem Feld geht es zur Tree_Scene.")]
    [SerializeField] private string nextSceneName = "Tree_Scene";

    [Header("VR-Bewegung im Finale deaktivieren")]
    [Tooltip("Zum Beispiel Continuous Move Provider oder Teleportation Provider eintragen.")]
    [SerializeField] private Behaviour[] movementScriptsToDisable;

    [Header("Portaljagd - visuelles Feedback")]
    [SerializeField] private bool createHuntVfxAutomatically = true;
    [SerializeField] private Color huntCoreColor = new Color(0.55f, 0.12f, 1f, 1f);
    [SerializeField] private Color huntAccentColor = new Color(0.08f, 0.9f, 1f, 1f);
    [SerializeField] private float huntOrbitRadius = 1.2f;
    [SerializeField] private float huntOrbitSpeed = 58f;
    [SerializeField] private float huntPulseSpeed = 2.2f;

    private int nextEscapePointIndex;

    private bool sequenceActivated;
    private bool initialEscapeFinished;
    private bool isMoving;
    private bool isWaiting;
    private bool finalSequenceStarted;

    private Vector3 baseFloatPosition;
    private Vector3 portalBaseScale;

    private CanvasGroup wisdomTextCanvasGroup;
    private Transform huntVfxRoot;
    private Transform[] huntOrbiters;
    private ParticleSystem huntParticles;
    private Light huntLight;

    private Transform VisualTransform
    {
        get
        {
            return portalVisual != null ? portalVisual : transform;
        }
    }

    private void Start()
    {
        if (useStartYAsFixedHeight)
        {
            fixedYHeight = transform.position.y;
        }

        baseFloatPosition = LockY(transform.position);
        transform.position = baseFloatPosition;

        portalBaseScale = VisualTransform.localScale;

        if (createFadeCanvasAutomatically && fadeCanvasGroup == null)
        {
            CreateFadeCanvas();
        }

        if (createWisdomTextAutomatically && wisdomText == null)
        {
            CreateWisdomText();
        }

        PrepareWisdomText();

        SetFadeAlpha(0f);
        SetWisdomTextAlpha(0f);

        if (createHuntVfxAutomatically)
            CreateHuntVfx();
    }

    private void Update()
    {
        AnimateHuntVfx();

        if (finalSequenceStarted)
            return;

        if (!isMoving)
        {
            ApplyFloatingPosition();
        }

        FacePlayerHorizontally();
    }

    /// <summary>
    /// Wird vom SteppingStonePortalTrigger aufgerufen.
    /// Das Portal wartet und flieht anschließend zum ersten Escape Point.
    /// </summary>
    public void ActivateFromSteppingStoneTrigger()
    {
        if (sequenceActivated || finalSequenceStarted)
            return;

        sequenceActivated = true;
        HuntActivated?.Invoke();
        StartCoroutine(HuntAwakeningPulse());
        StartCoroutine(InitialEscapeRoutine());
    }

    /// <summary>
    /// Liefert alle Stationen der Portaljagd, damit die Szenengrenze sie sicher einschließt.
    /// </summary>
    public List<Vector3> GetHuntWaypointPositions()
    {
        var points = new List<Vector3> { transform.position };

        if (escapePoints == null)
            return points;

        foreach (Transform escapePoint in escapePoints)
        {
            if (escapePoint != null)
                points.Add(escapePoint.position);
        }

        return points;
    }

    private IEnumerator InitialEscapeRoutine()
    {
        isWaiting = true;

        if (firstEscapeDelay > 0f)
        {
            yield return new WaitForSeconds(firstEscapeDelay);
        }

        isWaiting = false;

        if (!HasValidEscapePoint(nextEscapePointIndex))
        {
            Debug.LogWarning(
                "MysticalPortalController: Kein gültiger erster Escape Point eingetragen."
            );

            yield break;
        }

        Transform firstPoint = escapePoints[nextEscapePointIndex];

        nextEscapePointIndex++;

        yield return StartCoroutine(EscapeToPoint(firstPoint));

        initialEscapeFinished = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Vor der Stepping-Stone-Aktivierung darf das Portal nicht fliehen.
        if (!sequenceActivated || !initialEscapeFinished)
            return;

        if (isMoving || isWaiting || finalSequenceStarted)
            return;

        if (!IsPlayer(other))
            return;

        // Es existiert noch ein weiterer Fluchtpunkt.
        if (HasValidEscapePoint(nextEscapePointIndex))
        {
            StartCoroutine(EscapeToNextPointRoutine());
        }
        else
        {
            // Der Player erreicht das Portal am letzten Fluchtpunkt.
            StartCoroutine(FinalPortalSequence());
        }
    }

    private IEnumerator EscapeToNextPointRoutine()
    {
        isWaiting = true;

        Transform targetPoint = escapePoints[nextEscapePointIndex];
        nextEscapePointIndex++;

        isWaiting = false;

        yield return StartCoroutine(EscapeToPoint(targetPoint));
    }

    private IEnumerator EscapeToPoint(Transform targetPoint)
    {
        if (targetPoint == null)
            yield break;

        isMoving = true;

        // Das Portal behält während jeder normalen Flucht seine Größe.
        VisualTransform.localScale = portalBaseScale;

        Vector3 targetBasePosition = LockY(targetPoint.position);

        while (
            Vector3.Distance(baseFloatPosition, targetBasePosition)
            > arrivalDistance
        )
        {
            baseFloatPosition = Vector3.MoveTowards(
                baseFloatPosition,
                targetBasePosition,
                escapeMoveSpeed * Time.deltaTime
            );

            ApplyFloatingPosition();
            FacePlayerHorizontally();

            yield return null;
        }

        baseFloatPosition = targetBasePosition;

        ApplyFloatingPosition();
        FacePlayerHorizontally();

        isMoving = false;
        PortalEscaped?.Invoke();
    }

    private IEnumerator FinalPortalSequence()
    {
        finalSequenceStarted = true;
        isMoving = false;
        isWaiting = false;

        DisablePlayerMovement();

        if (beforeFinalDelay > 0f)
        {
            yield return new WaitForSeconds(beforeFinalDelay);
        }

        Transform visual = VisualTransform;

        Vector3 finalStartPosition = transform.position;
        Vector3 lockedFinalTarget = GetFinalCameraTargetPosition();

        float timer = 0f;

        float safeMoveDuration = Mathf.Max(0.01f, finalMoveDuration);
        float safeExpansionDuration =
            Mathf.Max(0.01f, finalExpansionDuration);

        while (timer < safeMoveDuration)
        {
            timer += Time.deltaTime;

            float moveProgress =
                Mathf.Clamp01(timer / safeMoveDuration);

            float expansionProgress =
                Mathf.Clamp01(timer / safeExpansionDuration);

            // Schnelle, abrupte Bewegung.
            float fastMoveProgress =
                1f - Mathf.Pow(1f - moveProgress, 3f);

            float fastExpansionProgress =
                1f - Mathf.Pow(1f - expansionProgress, 3f);

            Vector3 currentTarget =
                followHeadDirectionDuringFinal
                    ? GetFinalCameraTargetPosition()
                    : lockedFinalTarget;

            transform.position = Vector3.Lerp(
                finalStartPosition,
                currentTarget,
                fastMoveProgress
            );

            visual.localScale = Vector3.Lerp(
                portalBaseScale,
                portalBaseScale * finalExpansionMultiplier,
                fastExpansionProgress
            );

            FaceCameraDirectly();

            yield return null;
        }

        transform.position =
            followHeadDirectionDuringFinal
                ? GetFinalCameraTargetPosition()
                : lockedFinalTarget;

        visual.localScale =
            portalBaseScale * finalExpansionMultiplier;

        FaceCameraDirectly();

        // Bildschirm wird schwarz.
        yield return StartCoroutine(Blackout());

        // Kurze Pause auf schwarzem Bildschirm.
        if (afterBlackoutDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                afterBlackoutDelay
            );
        }

        // Weisheitstext erscheint.
        yield return StartCoroutine(ShowWisdomTextSequence());

        if (afterTextDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                afterTextDelay
            );
        }

        string destinationScene = string.IsNullOrWhiteSpace(nextSceneName)
            ? "Tree_Scene"
            : nextSceneName;

        if (Application.CanStreamedLevelBeLoaded(destinationScene))
        {
            SceneManager.LoadScene(destinationScene);
        }
        else
        {
            Debug.LogError(
                $"MysticalPortalController: Zielszene '{destinationScene}' fehlt in den Build Settings.",
                this);
        }
    }

    private IEnumerator ShowWisdomTextSequence()
    {
        if (wisdomText == null)
        {
            if (createWisdomTextAutomatically)
            {
                CreateWisdomText();
                PrepareWisdomText();
            }
        }

        if (wisdomText == null || wisdomTextCanvasGroup == null)
        {
            Debug.LogWarning(
                "MysticalPortalController: Weisheitstext konnte nicht erstellt werden."
            );

            yield break;
        }

        wisdomText.text = wisdomMessage;
        wisdomText.fontSize = wisdomFontSize;
        wisdomText.color = wisdomTextColor;

        SetWisdomTextAlpha(0f);

        // Text einblenden.
        yield return StartCoroutine(
            FadeWisdomText(
                0f,
                1f,
                textFadeInDuration
            )
        );

        // Text sichtbar halten.
        if (textVisibleDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                textVisibleDuration
            );
        }

        // Text ausblenden.
        yield return StartCoroutine(
            FadeWisdomText(
                1f,
                0f,
                textFadeOutDuration
            )
        );
    }

    private IEnumerator FadeWisdomText(
        float startAlpha,
        float targetAlpha,
        float duration
    )
    {
        if (wisdomTextCanvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            SetWisdomTextAlpha(targetAlpha);
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(timer / duration);

            float alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                progress
            );

            SetWisdomTextAlpha(alpha);

            yield return null;
        }

        SetWisdomTextAlpha(targetAlpha);
    }

    private IEnumerator Blackout()
    {
        if (
            createFadeCanvasAutomatically
            && fadeCanvasGroup == null
        )
        {
            CreateFadeCanvas();
        }

        if (fadeCanvasGroup == null)
        {
            Debug.LogWarning(
                "MysticalPortalController: Kein Fade Canvas vorhanden."
            );

            yield break;
        }

        if (blackoutFadeDuration <= 0f)
        {
            SetFadeAlpha(1f);
            yield break;
        }

        float timer = 0f;

        while (timer < blackoutFadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(
                    timer / blackoutFadeDuration
                );

            SetFadeAlpha(progress);

            yield return null;
        }

        SetFadeAlpha(1f);
    }

    private void ApplyFloatingPosition()
    {
        float yOffset =
            Mathf.Sin(Time.time * floatSpeed)
            * floatAmplitude;

        transform.position =
            baseFloatPosition
            + Vector3.up * yOffset;
    }

    private Vector3 LockY(Vector3 position)
    {
        position.y = fixedYHeight;
        return position;
    }

    private void CreateHuntVfx()
    {
        if (huntVfxRoot != null)
            return;

        var rootObject = new GameObject("Portal Hunt VFX");
        huntVfxRoot = rootObject.transform;
        huntVfxRoot.SetParent(VisualTransform, false);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material orbMaterial = shader != null ? new Material(shader) : null;
        if (orbMaterial != null)
        {
            orbMaterial.name = "Runtime Portal Hunt Orbs";
            if (orbMaterial.HasProperty("_BaseColor"))
                orbMaterial.SetColor("_BaseColor", huntAccentColor * 1.8f);
            else if (orbMaterial.HasProperty("_Color"))
                orbMaterial.SetColor("_Color", huntAccentColor);
        }

        huntOrbiters = new Transform[3];
        for (int index = 0; index < huntOrbiters.Length; index++)
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = $"Hunt Wisp {index + 1}";
            orb.transform.SetParent(huntVfxRoot, false);
            orb.transform.localScale = Vector3.one * 0.075f;
            Destroy(orb.GetComponent<Collider>());

            MeshRenderer meshRenderer = orb.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = orbMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            TrailRenderer trail = orb.AddComponent<TrailRenderer>();
            trail.time = 0.42f;
            trail.startWidth = 0.055f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.08f;
            trail.sharedMaterial = orbMaterial;
            trail.startColor = huntAccentColor;
            trail.endColor = new Color(huntCoreColor.r, huntCoreColor.g, huntCoreColor.b, 0f);

            huntOrbiters[index] = orb.transform;
        }

        var particleObject = new GameObject("Portal Hunt Sparks");
        particleObject.transform.SetParent(huntVfxRoot, false);
        huntParticles = particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = huntParticles.main;
        main.loop = true;
        main.maxParticles = 90;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.34f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.085f);
        main.startColor = new ParticleSystem.MinMaxGradient(huntCoreColor, huntAccentColor);

        ParticleSystem.EmissionModule emission = huntParticles.emission;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = huntParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.3f, huntOrbitRadius * 0.72f);
        shape.radiusThickness = 0.18f;

        ParticleSystemRenderer particleRenderer =
            particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader != null)
        {
            var particleMaterial = new Material(particleShader)
            {
                name = "Runtime Portal Hunt Sparks"
            };
            if (particleMaterial.HasProperty("_BaseColor"))
                particleMaterial.SetColor("_BaseColor", huntCoreColor);
            particleRenderer.sharedMaterial = particleMaterial;
        }

        huntLight = rootObject.AddComponent<Light>();
        huntLight.type = LightType.Point;
        huntLight.color = huntCoreColor;
        huntLight.range = 4.5f;
        huntLight.intensity = 0.85f;
        huntLight.shadows = LightShadows.None;
    }

    private void AnimateHuntVfx()
    {
        if (huntVfxRoot == null)
            return;

        float movementMultiplier = isMoving ? 2.25f : sequenceActivated ? 1.35f : 0.7f;
        float pulse = 0.88f + Mathf.Sin(Time.time * huntPulseSpeed) * 0.12f;
        huntVfxRoot.localScale = Vector3.one * pulse;

        if (huntOrbiters != null)
        {
            for (int index = 0; index < huntOrbiters.Length; index++)
            {
                Transform orb = huntOrbiters[index];
                if (orb == null)
                    continue;

                float angle = (Time.time * huntOrbitSpeed * movementMultiplier) + index * 120f;
                float radians = angle * Mathf.Deg2Rad;
                float vertical = Mathf.Sin(radians * 1.7f) * 0.34f;
                orb.localPosition = new Vector3(
                    Mathf.Cos(radians) * huntOrbitRadius,
                    vertical,
                    Mathf.Sin(radians) * huntOrbitRadius);
            }
        }

        if (huntLight != null)
            huntLight.intensity = (sequenceActivated ? 1.2f : 0.72f) + pulse * 0.32f;

        if (huntParticles != null)
        {
            ParticleSystem.EmissionModule emission = huntParticles.emission;
            emission.rateOverTime = isMoving ? 34f : sequenceActivated ? 22f : 12f;
        }
    }

    private IEnumerator HuntAwakeningPulse()
    {
        if (huntParticles != null)
            huntParticles.Emit(28);

        float duration = 0.55f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (huntLight != null)
                huntLight.intensity = Mathf.Lerp(3.4f, 1.45f, elapsed / duration);
            yield return null;
        }
    }

    private bool HasValidEscapePoint(int index)
    {
        return escapePoints != null
            && index >= 0
            && index < escapePoints.Length
            && escapePoints[index] != null;
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        if (player == null)
            return false;

        if (other.transform == player)
            return true;

        if (other.transform.IsChildOf(player))
            return true;

        // Zusätzliche Erkennung über einen Rigidbody
        // innerhalb des XR Origins.
        if (other.attachedRigidbody != null)
        {
            Transform rigidbodyTransform =
                other.attachedRigidbody.transform;

            if (rigidbodyTransform == player)
                return true;

            if (rigidbodyTransform.IsChildOf(player))
                return true;
        }

        return false;
    }

    private void FacePlayerHorizontally()
    {
        if (!alwaysFacePlayer)
            return;

        Transform target =
            playerCamera != null
                ? playerCamera
                : player;

        if (target == null)
            return;

        Vector3 direction =
            target.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

        transform.rotation =
            targetRotation
            * Quaternion.Euler(faceRotationOffset);
    }

    private void FaceCameraDirectly()
    {
        Transform target =
            playerCamera != null
                ? playerCamera
                : player;

        if (target == null)
            return;

        Vector3 direction =
            target.position - transform.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

        transform.rotation =
            targetRotation
            * Quaternion.Euler(faceRotationOffset);
    }

    private Vector3 GetFinalCameraTargetPosition()
    {
        Transform reference =
            playerCamera != null
                ? playerCamera
                : player;

        if (reference == null)
            return transform.position;

        Vector3 targetPosition =
            reference.position
            + reference.forward * finalEndDistance;

        targetPosition +=
            reference.TransformVector(
                finalCameraLocalOffset
            );

        return targetPosition;
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = alpha;
        }
    }

    private void SetWisdomTextAlpha(float alpha)
    {
        if (wisdomTextCanvasGroup != null)
        {
            wisdomTextCanvasGroup.alpha = alpha;
        }
    }

    private void CreateFadeCanvas()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning(
                "MysticalPortalController: Ohne Player Camera kann kein Fade Canvas erstellt werden."
            );

            return;
        }

        GameObject canvasObject =
            new GameObject("VR_Blackout_Canvas");

        canvasObject.transform.SetParent(
            playerCamera,
            false
        );

        canvasObject.transform.localPosition =
            new Vector3(
                0f,
                0f,
                fadeCanvasDistanceFromCamera
            );

        canvasObject.transform.localRotation =
            Quaternion.identity;

        Canvas canvas =
            canvasObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 9999;
        canvas.overrideSorting = true;

        Camera cameraComponent =
            playerCamera.GetComponent<Camera>();

        if (cameraComponent != null)
        {
            canvas.worldCamera = cameraComponent;
        }

        RectTransform canvasRect =
            canvas.GetComponent<RectTransform>();

        canvasRect.sizeDelta =
            new Vector2(3000f, 3000f);

        canvasRect.localScale =
            Vector3.one * 0.001f;

        fadeCanvasGroup =
            canvasObject.AddComponent<CanvasGroup>();

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;

        GameObject imageObject =
            new GameObject("Black_Image");

        imageObject.transform.SetParent(
            canvasObject.transform,
            false
        );

        Image image =
            imageObject.AddComponent<Image>();

        image.color = Color.black;
        image.raycastTarget = false;

        RectTransform imageRect =
            image.GetComponent<RectTransform>();

        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
    }

    private void CreateWisdomText()
    {
        if (fadeCanvasGroup == null)
        {
            if (createFadeCanvasAutomatically)
            {
                CreateFadeCanvas();
            }
        }

        if (fadeCanvasGroup == null)
            return;

        GameObject textObject =
            new GameObject("Wisdom_Text");

        textObject.transform.SetParent(
            fadeCanvasGroup.transform,
            false
        );

        TextMeshProUGUI textComponent =
            textObject.AddComponent<TextMeshProUGUI>();

        textComponent.text = wisdomMessage;
        textComponent.fontSize = wisdomFontSize;
        textComponent.color = wisdomTextColor;
        textComponent.alignment =
            TextAlignmentOptions.Center;

        textComponent.enableWordWrapping = true;
        textComponent.raycastTarget = false;

        RectTransform textRect =
            textComponent.rectTransform;

        textRect.anchorMin =
            new Vector2(0.15f, 0.35f);

        textRect.anchorMax =
            new Vector2(0.85f, 0.65f);

        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        wisdomText = textComponent;

        wisdomTextCanvasGroup =
            textObject.AddComponent<CanvasGroup>();

        wisdomTextCanvasGroup.alpha = 0f;
        wisdomTextCanvasGroup.blocksRaycasts = false;
        wisdomTextCanvasGroup.interactable = false;
    }

    private void PrepareWisdomText()
    {
        if (wisdomText == null)
            return;

        wisdomText.text = wisdomMessage;
        wisdomText.fontSize = wisdomFontSize;
        wisdomText.color = wisdomTextColor;
        wisdomText.alignment =
            TextAlignmentOptions.Center;

        wisdomTextCanvasGroup =
            wisdomText.GetComponent<CanvasGroup>();

        if (wisdomTextCanvasGroup == null)
        {
            wisdomTextCanvasGroup =
                wisdomText.gameObject
                    .AddComponent<CanvasGroup>();
        }

        wisdomTextCanvasGroup.alpha = 0f;
        wisdomTextCanvasGroup.blocksRaycasts = false;
        wisdomTextCanvasGroup.interactable = false;
    }

    private void DisablePlayerMovement()
    {
        if (movementScriptsToDisable == null)
            return;

        foreach (
            Behaviour movementScript
            in movementScriptsToDisable
        )
        {
            if (movementScript != null)
            {
                movementScript.enabled = false;
            }
        }
    }
}
