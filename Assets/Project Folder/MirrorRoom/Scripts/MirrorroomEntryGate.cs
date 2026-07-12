using System.Collections;
using UnityEngine;

namespace MirrorRoom
{
    /// <summary>
    /// Handles the entry gate mechanic for the Mirrorroom.
    /// The player must cross this gate to unlock/enable the mirror self-perception.
    /// Opens physical door wings and dissolves the energy membrane when crossed.
    /// </summary>
    public class MirrorroomEntryGate : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The mirror controller that is unlocked after crossing this gate.")]
        [SerializeField] private SoulMorphController mirrorController;

        [Tooltip("The membrane renderer that dissolves when crossed.")]
        [SerializeField] private Renderer membraneRenderer;

        [Tooltip("The main trigger collider for player detection.")]
        [SerializeField] private Collider triggerCollider;

        [Header("Door Wings (Physical Doors)")]
        [Tooltip("The left door wing transform.")]
        [SerializeField] private Transform leftDoor;

        [Tooltip("The right door wing transform.")]
        [SerializeField] private Transform rightDoor;

        [Header("FX & Audio")]
        [Tooltip("Ambient/idle particles floating in the doorway.")]
        [SerializeField] private ParticleSystem idleParticles;

        [Tooltip("Particle system for the burst impact when crossed.")]
        [SerializeField] private ParticleSystem burstParticles;

        [Tooltip("Audio source to play the deep chime sound.")]
        [SerializeField] private AudioSource chimeAudio;

        [Header("Tuning")]
        [Tooltip("Duration in seconds for the membrane to dissolve/fade out.")]
        [SerializeField] private float dissolveDuration = 2.0f;

        [Tooltip("Duration in seconds for the door wings to open.")]
        [SerializeField] private float openDuration = 2.5f;

        [Tooltip("Target local Y rotation angle for the doors when opened.")]
        [SerializeField] private float openAngle = 95f;

        [Tooltip("Pitch applied to the chime to make it deep and resonant.")]
        [SerializeField] private float chimePitch = 0.55f;

        private bool _triggered;
        private MaterialPropertyBlock _membraneBlock;
        private static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ID_EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _membraneBlock = new MaterialPropertyBlock();

            // Set up trigger collider if not assigned
            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider>();
            }
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void Start()
        {
            // Ensure the mirror controller is disabled initially (Spiegel-Bereich gesperrt)
            if (mirrorController != null)
            {
                mirrorController.enabled = false;
            }

            // Ensure idle particles are playing
            if (idleParticles != null && !idleParticles.isPlaying)
            {
                idleParticles.Play();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;

            if (IsPlayer(other))
            {
                TriggerGate();
            }
        }

        private void TriggerGate()
        {
            _triggered = true;

            // Disable trigger so it only fires once
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }

            // Play deep chime
            if (chimeAudio != null)
            {
                chimeAudio.pitch = chimePitch;
                chimeAudio.Play();
            }

            // Emit particle burst
            if (burstParticles != null)
            {
                burstParticles.Play();
            }

            // Dissolve the membrane
            if (membraneRenderer != null)
            {
                StartCoroutine(DissolveMembraneRoutine());
            }

            // Open door wings
            if (leftDoor != null || rightDoor != null)
            {
                StartCoroutine(OpenDoorsRoutine());
            }

            // Unlock/Enable the mirror self-perception
            if (mirrorController != null)
            {
                mirrorController.enabled = true;
            }
        }

        private IEnumerator DissolveMembraneRoutine()
        {
            float elapsed = 0f;

            // Capture initial colors
            Color startBaseColor = Color.black;
            Color startEmissionColor = Color.black;

            if (membraneRenderer.sharedMaterial != null)
            {
                if (membraneRenderer.sharedMaterial.HasProperty(ID_BaseColor))
                    startBaseColor = membraneRenderer.sharedMaterial.GetColor(ID_BaseColor);
                if (membraneRenderer.sharedMaterial.HasProperty(ID_EmissionColor))
                    startEmissionColor = membraneRenderer.sharedMaterial.GetColor(ID_EmissionColor);
            }

            while (elapsed < dissolveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dissolveDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                // Interpolate colors to 0 alpha/emission
                Color currentBase = Color.Lerp(startBaseColor, new Color(startBaseColor.r, startBaseColor.g, startBaseColor.b, 0f), ease);
                Color currentEmission = Color.Lerp(startEmissionColor, Color.clear, ease);

                membraneRenderer.GetPropertyBlock(_membraneBlock);
                _membraneBlock.SetColor(ID_BaseColor, currentBase);
                _membraneBlock.SetColor(ID_EmissionColor, currentEmission);
                membraneRenderer.SetPropertyBlock(_membraneBlock);

                yield return null;
            }

            // Turn off the membrane and idle particles completely
            membraneRenderer.gameObject.SetActive(false);

            if (idleParticles != null)
            {
                idleParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private IEnumerator OpenDoorsRoutine()
        {
            float elapsed = 0f;

            Quaternion leftStartRot = leftDoor != null ? leftDoor.localRotation : Quaternion.identity;
            Quaternion rightStartRot = rightDoor != null ? rightDoor.localRotation : Quaternion.identity;

            Quaternion leftTargetRot = leftStartRot * Quaternion.Euler(0f, -openAngle, 0f);
            Quaternion rightTargetRot = rightStartRot * Quaternion.Euler(0f, openAngle, 0f);

            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / openDuration);
                // Ease out cubic
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                if (leftDoor != null)
                {
                    leftDoor.localRotation = Quaternion.Slerp(leftStartRot, leftTargetRot, ease);
                }
                if (rightDoor != null)
                {
                    rightDoor.localRotation = Quaternion.Slerp(rightStartRot, rightTargetRot, ease);
                }

                yield return null;
            }

            if (leftDoor != null) leftDoor.localRotation = leftTargetRot;
            if (rightDoor != null) rightDoor.localRotation = rightTargetRot;
        }

        /// <summary>
        /// Robust detection of the player.
        /// </summary>
        private static bool IsPlayer(Collider other)
        {
            if (other == null) return false;

            Transform root = other.transform.root;
            if (root != null && root.name.Contains("AutoHandPlayer"))
            {
                return true;
            }

            // Walk up parents looking for component named "AutoHandPlayer"
            Transform t = other.transform;
            while (t != null)
            {
                var comps = t.GetComponents<MonoBehaviour>();
                for (int i = 0; i < comps.Length; i++)
                {
                    var c = comps[i];
                    if (c != null && c.GetType().Name == "AutoHandPlayer")
                    {
                        return true;
                    }
                }
                t = t.parent;
            }

            // Fallback to attached rigidbody root
            var rb = other.attachedRigidbody;
            if (rb != null && rb.transform.root != null && rb.transform.root.name.Contains("AutoHandPlayer"))
            {
                return true;
            }

            return false;
        }
    }
}
