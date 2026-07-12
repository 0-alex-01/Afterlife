using System.Collections;
using UnityEngine;

public class RealisticJusticeScale : MonoBehaviour
{
    [Header("Waagen-Teile")]
    [Tooltip("Der unbewegliche obere Teil der Waage.")]
    [SerializeField] private Transform scaleUpper;

    [Tooltip("Der komplette bewegliche Teil: Balken, Ketten und Schalen.")]
    [SerializeField] private Transform scaleLower;


    [Header("Bewegung")]
    [Tooltip("Maximaler Neigungswinkel des beweglichen Waagenteils.")]
    [Range(1f, 20f)]
    [SerializeField] private float maximumAngle = 7f;

    [Tooltip("Dauer von einer Seite zur anderen.")]
    [SerializeField] private float movementDuration = 4.5f;

    [Tooltip("Pause an den äußersten Positionen.")]
    [SerializeField] private float pauseAtExtremes = 0.6f;

    [Tooltip("Kleine zufällige Abweichung des Winkels.")]
    [Range(0f, 0.4f)]
    [SerializeField] private float angleVariation = 0.08f;

    [Tooltip("Kleine zufällige Abweichung der Bewegungsdauer.")]
    [Range(0f, 0.4f)]
    [SerializeField] private float durationVariation = 0.12f;

    [Tooltip("Lokale Rotationsachse des beweglichen Waagenteils.")]
    [SerializeField] private RotationAxis rotationAxis =
        RotationAxis.LocalZ;

    [Tooltip("Bewegungskurve für eine schwere, träge Bewegung.")]
    [SerializeField] private AnimationCurve movementCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.25f, 0.08f),
            new Keyframe(0.75f, 0.92f),
            new Keyframe(1f, 1f, 0f, 0f)
        );


    [Header("Audio")]
    [Tooltip("AudioSource für einzelne Knarz- und Metallgeräusche.")]
    [SerializeField] private AudioSource oneShotSource;

    [Tooltip("Optionaler Loop während der Bewegung.")]
    [SerializeField] private AudioSource movementLoopSource;

    [Tooltip("Sounds beim Erreichen einer Seite.")]
    [SerializeField] private AudioClip[] directionChangeClips;

    [Tooltip("Optionale Ketten- oder Metallgeräusche.")]
    [SerializeField] private AudioClip[] movementClips;

    [Range(0f, 1f)]
    [SerializeField] private float directionChangeVolume = 0.6f;

    [Range(0f, 1f)]
    [SerializeField] private float movementSoundVolume = 0.3f;

    [Range(0f, 1f)]
    [SerializeField] private float loopMaximumVolume = 0.2f;

    [SerializeField] private Vector2 pitchRange =
        new Vector2(0.93f, 1.05f);

    [Range(0f, 1f)]
    [SerializeField] private float movementSoundChance = 0.5f;


    [Header("Verhalten")]
    [SerializeField] private bool playOnStart = true;

    [SerializeField] private float startDelay = 0f;

    [Tooltip("Startet mit einem kleinen zufälligen Winkel.")]
    [SerializeField] private bool randomizeStart = true;


    private Quaternion upperInitialRotation;
    private Quaternion lowerInitialRotation;

    private float currentAngle;

    private Coroutine movementRoutine;
    private bool isPlaying;


    public enum RotationAxis
    {
        LocalX,
        LocalY,
        LocalZ
    }


    private void Awake()
    {
        if (scaleUpper != null)
            upperInitialRotation = scaleUpper.localRotation;

        if (scaleLower != null)
            lowerInitialRotation = scaleLower.localRotation;

        if (movementLoopSource != null)
        {
            movementLoopSource.loop = true;
            movementLoopSource.volume = 0f;
        }
    }


    private void Start()
    {
        if (playOnStart)
            StartScaleMovement();
    }


    public void StartScaleMovement()
    {
        if (isPlaying)
            return;

        if (scaleLower == null)
        {
            Debug.LogError(
                "RealisticJusticeScale: Scale Lower wurde nicht zugewiesen.",
                this
            );

            return;
        }

        movementRoutine = StartCoroutine(ScaleMovementRoutine());
    }


    public void StopScaleMovement()
    {
        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
            movementRoutine = null;
        }

        isPlaying = false;

        if (movementLoopSource != null)
        {
            movementLoopSource.Stop();
            movementLoopSource.volume = 0f;
        }
    }


    public void ResetScale()
    {
        StopScaleMovement();

        if (scaleUpper != null)
            scaleUpper.localRotation = upperInitialRotation;

        if (scaleLower != null)
            scaleLower.localRotation = lowerInitialRotation;

        currentAngle = 0f;
    }


    private IEnumerator ScaleMovementRoutine()
    {
        isPlaying = true;

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (movementLoopSource != null &&
            movementLoopSource.clip != null)
        {
            movementLoopSource.Play();
        }

        currentAngle = randomizeStart
            ? Random.Range(
                -maximumAngle * 0.25f,
                maximumAngle * 0.25f
            )
            : 0f;

        SetLowerRotation(currentAngle);

        bool moveToNegativeAngle = true;

        while (isPlaying)
        {
            float variedAngle =
                maximumAngle *
                Random.Range(
                    1f - angleVariation,
                    1f + angleVariation
                );

            float targetAngle = moveToNegativeAngle
                ? -variedAngle
                : variedAngle;

            float variedDuration =
                movementDuration *
                Random.Range(
                    1f - durationVariation,
                    1f + durationVariation
                );

            yield return MoveLowerPart(
                currentAngle,
                targetAngle,
                variedDuration
            );

            currentAngle = targetAngle;

            PlayRandomClip(
                directionChangeClips,
                directionChangeVolume
            );

            if (Random.value <= movementSoundChance)
            {
                PlayRandomClip(
                    movementClips,
                    movementSoundVolume
                );
            }

            if (pauseAtExtremes > 0f)
            {
                yield return new WaitForSeconds(
                    pauseAtExtremes *
                    Random.Range(0.85f, 1.15f)
                );
            }

            moveToNegativeAngle = !moveToNegativeAngle;
        }
    }


    private IEnumerator MoveLowerPart(
        float fromAngle,
        float toAngle,
        float duration
    )
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(elapsed / duration);

            float curvedTime =
                movementCurve.Evaluate(normalizedTime);

            currentAngle = Mathf.LerpUnclamped(
                fromAngle,
                toAngle,
                curvedTime
            );

            SetLowerRotation(currentAngle);

            if (movementLoopSource != null)
            {
                float movementIntensity =
                    Mathf.Sin(normalizedTime * Mathf.PI);

                movementLoopSource.volume =
                    movementIntensity * loopMaximumVolume;
            }

            yield return null;
        }

        currentAngle = toAngle;
        SetLowerRotation(toAngle);

        if (movementLoopSource != null)
            movementLoopSource.volume = 0f;
    }


    private void SetLowerRotation(float angle)
    {
        if (scaleLower == null)
            return;

        scaleLower.localRotation =
            lowerInitialRotation *
            Quaternion.AngleAxis(
                angle,
                GetAxis(rotationAxis)
            );
    }


    private Vector3 GetAxis(RotationAxis axis)
    {
        switch (axis)
        {
            case RotationAxis.LocalX:
                return Vector3.right;

            case RotationAxis.LocalY:
                return Vector3.up;

            default:
                return Vector3.forward;
        }
    }


    private void PlayRandomClip(
        AudioClip[] clips,
        float volume
    )
    {
        if (oneShotSource == null ||
            clips == null ||
            clips.Length == 0)
        {
            return;
        }

        AudioClip selectedClip =
            clips[Random.Range(0, clips.Length)];

        if (selectedClip == null)
            return;

        oneShotSource.pitch =
            Random.Range(pitchRange.x, pitchRange.y);

        oneShotSource.PlayOneShot(
            selectedClip,
            volume
        );
    }


    [ContextMenu("Start Scale Movement")]
    private void StartFromInspector()
    {
        if (Application.isPlaying)
            StartScaleMovement();
    }


    [ContextMenu("Stop Scale Movement")]
    private void StopFromInspector()
    {
        if (Application.isPlaying)
            StopScaleMovement();
    }


    [ContextMenu("Reset Scale")]
    private void ResetFromInspector()
    {
        ResetScale();
    }
}