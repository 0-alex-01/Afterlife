using System.Collections;
using UnityEngine;

/// <summary>
/// The "brain" of the MirrorRoom soul-twin reflection mechanic.
/// Drives a duplicate soul body ("twin") placed behind a semi-transparent glass plane at the
/// mirror-reflected pose of the player (with damped lag), and morphs it through five soul states
/// driven by proximity, dwell, and ghost-hand touch. Pure transform/distance-driven; no input code.
/// All references are optional and null-checked so the scene works before everything is wired.
/// </summary>
public class SoulMorphController : MonoBehaviour
{
    public enum MorphState { Clear = 0, Bloom = 1, Decay = 2, Chaos = 3, Light = 4 }

    [System.Serializable]
    public class StateConfig
    {
        [Tooltip("Soul body base color.")]
        public Color soulBase = Color.black;
        [Tooltip("Soul fresnel rim color (the signature glow).")]
        public Color soulRim = Color.white;
        [Tooltip("Overhead beam light color.")]
        public Color beamColor = Color.white;
        [Tooltip("Overhead beam light base intensity.")]
        public float beamIntensity = 2f;
        [Tooltip("Room fog tint for this state (near-black).")]
        public Color fogColor = Color.black;
        [Tooltip("Soul dissolve amount (Decay).")]
        [Range(0f, 1f)] public float dissolve = 0f;
        [Tooltip("Soul glitch amount (Chaos).")]
        [Range(0f, 1f)] public float glitch = 0f;
        [Tooltip("Soul bodiless / light-form amount (Light).")]
        [Range(0f, 1f)] public float bodiless = 0f;
    }

    [Header("Player References (real VR player)")]
    [Tooltip("The real VR player's head transform.")]
    [SerializeField] private Transform playerHead;
    [Tooltip("The real VR player's left ghost hand.")]
    [SerializeField] private Transform playerHandLeft;
    [Tooltip("The real VR player's right ghost hand.")]
    [SerializeField] private Transform playerHandRight;

    [Header("Mirror References")]
    [Tooltip("The mirror's transform. Reflective side = its -Z facing the player; local +Z points behind into the twin space.")]
    [SerializeField] private Transform mirrorPlane;
    [Tooltip("Renderer of the glass plane (for touch ripple).")]
    [SerializeField] private Renderer mirrorGlassRenderer;
    [Tooltip("Mirror half width in meters (for UV mapping of touch point).")]
    [SerializeField] private float mirrorHalfWidth = 0.6f;
    [Tooltip("Mirror half height in meters (for UV mapping of touch point).")]
    [SerializeField] private float mirrorHalfHeight = 1.2f;

    [Header("Soul Twin References")]
    [Tooltip("Root of the soul twin (positioned at the reflected head pose).")]
    [SerializeField] private Transform soulTwinRoot;
    [Tooltip("Soul twin body transform.")]
    [SerializeField] private Transform soulTwinBody;
    [Tooltip("Soul twin body renderer (driven via MaterialPropertyBlock).")]
    [SerializeField] private Renderer soulTwinBodyRenderer;
    [Tooltip("Soul twin left hand.")]
    [SerializeField] private Transform soulTwinHandLeft;
    [Tooltip("Soul twin right hand.")]
    [SerializeField] private Transform soulTwinHandRight;
    [Tooltip("Optional extra semi-transparent twin copies for close-range / Chaos doubling. May be empty.")]
    [SerializeField] private Transform[] splitOverlays;

    [Header("Light & FX References")]
    [Tooltip("The overhead spotlight beam.")]
    [SerializeField] private Light beamLight;
    [Tooltip("Per-state particle systems, length 5, index per MorphState.")]
    [SerializeField] private ParticleSystem[] stateParticles = new ParticleSystem[5];

    [Header("Audio References")]
    [Tooltip("Per-state ambient loops, length 5, index per MorphState.")]
    [SerializeField] private AudioSource[] stateBeds = new AudioSource[5];
    [Tooltip("Touch chime audio source.")]
    [SerializeField] private AudioSource touchChime;

    [Header("Reflection / Motion Tuning")]
    [Tooltip("Planar-mirror mode: an external real mirror does the reflecting, so the twin COPIES the player's pose on the player's side (lagged) instead of being placed behind the mirror.")]
    [SerializeField] private bool planarMirrorMode = false;
    [Tooltip("Vertical offset applied to the twin in planar-mirror mode to align the soul body with the player.")]
    [SerializeField] private float planarTwinVerticalOffset = -0.1f;
    [Tooltip("Critically-damped smooth time for the twin to lag behind the reflected pose (seconds).")]
    [SerializeField] private float reflectionSmoothTime = 0.18f;
    [Tooltip("Idle hover bob amplitude (meters).")]
    [SerializeField] private float hoverAmplitude = 0.12f;
    [Tooltip("Idle hover bob period (seconds).")]
    [SerializeField] private float hoverPeriod = 7.8f;
    [Tooltip("Idle yaw drift speed (degrees/second).")]
    [SerializeField] private float yawDriftSpeed = 12f;

    [Header("Choice Mechanic (Pedestals)")]
    [Tooltip("The real planar mirror component, so VOID can fade its reflection tint to black.")]
    [SerializeField] private PlanarMirror planarMirror;
    [Tooltip("How far the mirror leans toward a theme on hover preview (0..1, non-committal).")]
    [Range(0f, 1f)][SerializeField] private float previewWeight = 0.55f;
    [Tooltip("Seconds for the choice overlay to ease in/out (preview revert + commit).")]
    [SerializeField] private float choiceBlendTime = 2.8f;
    [Tooltip("Optional per-theme particle systems around the mirror, indexed by (int)ChoiceTheme (0=None unused,1=Void,2=Rebirth,3=Judgement).")]
    [SerializeField] private ParticleSystem[] choiceParticles = new ParticleSystem[4];
    [Tooltip("JUDGEMENT split colours (golden left / red right).")]
    [SerializeField] private Color judgementGold = new Color(1.0f, 0.72f, 0.28f, 1f);
    [SerializeField] private Color judgementRed = new Color(1.0f, 0.12f, 0.14f, 1f);
    [Tooltip("VOID mirror theme.")]
    [SerializeField]
    private StateConfig voidConfig = new StateConfig
    {
        soulBase = new Color(0.01f, 0.01f, 0.02f, 1f),
        soulRim = new Color(0.10f, 0.15f, 0.30f, 1f),
        beamColor = new Color(0.10f, 0.13f, 0.22f, 1f),
        beamIntensity = 0.35f,
        fogColor = new Color(0.008f, 0.010f, 0.018f, 1f),
        dissolve = 0.6f, glitch = 0f, bodiless = 1f
    };
    [Tooltip("REBIRTH mirror theme (green bloom).")]
    [SerializeField]
    private StateConfig rebirthConfig = new StateConfig
    {
        soulBase = new Color(0.06f, 0.20f, 0.10f, 1f),
        soulRim = new Color(0.30f, 0.85f, 0.42f, 1f),
        beamColor = new Color(0.45f, 0.95f, 0.55f, 1f),
        beamIntensity = 3.0f,
        fogColor = new Color(0.018f, 0.045f, 0.026f, 1f),
        dissolve = 0f, glitch = 0f, bodiless = 0f
    };
    [Tooltip("JUDGEMENT mirror theme (gold soul, split handled on the glass).")]
    [SerializeField]
    private StateConfig judgementConfig = new StateConfig
    {
        soulBase = new Color(0.15f, 0.12f, 0.06f, 1f),
        soulRim = new Color(0.91f, 0.71f, 0.35f, 1f),
        beamColor = new Color(1.0f, 0.95f, 0.85f, 1f),
        beamIntensity = 3.2f,
        fogColor = new Color(0.05f, 0.04f, 0.03f, 1f),
        dissolve = 0f, glitch = 0f, bodiless = 0f
    };

    [Header("Proximity Tuning")]
    [Tooltip("Distance (m) at or beyond which proximity intensity is 0.")]
    [SerializeField] private float proximityFar = 3f;
    [Tooltip("Distance (m) at or within which proximity intensity is 1.")]
    [SerializeField] private float proximityNear = 1f;
    [Tooltip("Proximity above this enables close-range split overlays.")]
    [SerializeField] private float splitEnableThreshold = 0.7f;
    [Tooltip("Max particle emission rate scaled by proximity.")]
    [SerializeField] private float maxParticleEmission = 30f;

    [Header("Morph Tuning")]
    [Tooltip("State-to-state morph blend duration (seconds).")]
    [SerializeField] private float morphDuration = 3f;
    [Tooltip("Per-state configs, length 5 (defaults from DESIGN.md palette).")]
    [SerializeField]
    private StateConfig[] stateConfigs = new StateConfig[5]
    {
        // Clear
        new StateConfig {
            soulBase = new Color(0.05f, 0.18f, 0.22f, 1f),
            soulRim = new Color(0.25f, 0.69f, 0.80f, 1f),
            beamColor = new Color(0.749f, 0.918f, 0.949f, 1f), // #BFEAF2
            beamIntensity = 2.2f,
            fogColor = new Color(0.027f, 0.047f, 0.059f, 1f),  // #070C0F
            dissolve = 0f, glitch = 0f, bodiless = 0f
        },
        // Bloom
        new StateConfig {
            soulBase = new Color(0.30f, 0.22f, 0.10f, 1f),
            soulRim = new Color(0.91f, 0.71f, 0.35f, 1f),      // #E8B65A
            beamColor = new Color(0.949f, 0.784f, 0.478f, 1f), // #F2C87A
            beamIntensity = 3.2f,
            fogColor = new Color(0.055f, 0.039f, 0.024f, 1f),  // #0E0A06
            dissolve = 0f, glitch = 0f, bodiless = 0f
        },
        // Decay
        new StateConfig {
            soulBase = new Color(0.14f, 0.16f, 0.18f, 1f),
            soulRim = new Color(0.49f, 0.55f, 0.60f, 1f),      // #7E8C99
            beamColor = new Color(0.616f, 0.659f, 0.698f, 1f), // #9DA8B2
            beamIntensity = 1.2f,
            fogColor = new Color(0.031f, 0.039f, 0.047f, 1f),  // #080A0C
            dissolve = 0.45f, glitch = 0f, bodiless = 0f
        },
        // Chaos
        new StateConfig {
            soulBase = new Color(0.22f, 0.06f, 0.16f, 1f),
            soulRim = new Color(0.78f, 0.29f, 0.62f, 1f),      // #C84B9E
            beamColor = new Color(0.824f, 0.310f, 0.690f, 1f), // #D24FB0
            beamIntensity = 2.0f,
            fogColor = new Color(0.047f, 0.024f, 0.039f, 1f),  // #0C060A
            dissolve = 0f, glitch = 0.7f, bodiless = 0f
        },
        // Light
        new StateConfig {
            soulBase = new Color(0.30f, 0.30f, 0.29f, 1f),
            soulRim = new Color(0.97f, 0.96f, 0.93f, 1f),      // #F7F4EC
            beamColor = new Color(1f, 1f, 1f, 1f),             // #FFFFFF
            beamIntensity = 3.6f,
            fogColor = new Color(0.047f, 0.047f, 0.055f, 1f),  // #0C0C0E
            dissolve = 0f, glitch = 0f, bodiless = 0.85f
        }
    };

    [Header("Dwell Tuning")]
    [Tooltip("Seconds of stillness + facing before auto-cycle begins.")]
    [SerializeField] private float dwellDelay = 3.5f;
    [Tooltip("Seconds between auto-advances while dwelling.")]
    [SerializeField] private float dwellInterval = 14f;
    [Tooltip("Head movement above this (m/frame-step) pauses dwell.")]
    [SerializeField] private float dwellMoveThreshold = 0.02f;
    [Tooltip("Dot(head.forward, toMirror) must exceed this to count as facing.")]
    [SerializeField] private float facingDot = 0.6f;

    [Header("Touch Tuning")]
    [Tooltip("Hand-to-mirror distance (m) that triggers the fallback touch detector.")]
    [SerializeField] private float touchDistance = 0.06f;
    [Tooltip("Debounce between touches (seconds).")]
    [SerializeField] private float touchDebounce = 0.6f;
    [Tooltip("Ripple animation duration (seconds).")]
    [SerializeField] private float rippleDuration = 1f;
    [Tooltip("Touch chime pitch randomization (+/-).")]
    [SerializeField] private float chimePitchVariance = 0.12f;
    [Tooltip("Pulse overshoot fraction on touch (e.g. 0.15 = +15%).")]
    [SerializeField] private float pulseOvershoot = 0.15f;
    [Tooltip("Pulse settle duration (seconds).")]
    [SerializeField] private float pulseSettleTime = 0.7f;

    // --- Runtime state ---
    private MorphState _currentState = MorphState.Clear;
    private MorphState _targetState = MorphState.Clear;
    private StateConfig _blended;          // current displayed (lerped) config
    private float _morphT = 1f;            // 0..1 progress of current morph
    private StateConfig _morphFrom;        // snapshot at morph start
    private float _proximity;              // eased 0..1

    private MaterialPropertyBlock _mpb;
    private Vector3 _twinVel, _twinHandLVel, _twinHandRVel;
    private Vector3 _lastHeadPos;
    private float _dwellTimer;
    private float _dwellCycleTimer;
    private bool _dwelling;
    private float _lastTouchTime = -10f;
    private bool _handTouchingLast;
    private float _pulseValue;             // additive pulse 0..overshoot
    private Coroutine _pulseRoutine;
    private Coroutine _rippleRoutine;

    // Choice mechanic runtime.
    private ChoiceTheme _choiceTheme = ChoiceTheme.None;
    private bool _choiceCommitted;
    private float _choiceWeight;            // eased 0..1 overlay strength
    private Color _planarBaseTint = Color.white;
    private MaterialPropertyBlock _glassBlock;

    // Cached shader property IDs.
    private static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ID_RimColor = Shader.PropertyToID("_RimColor");
    private static readonly int ID_RimStrength = Shader.PropertyToID("_RimStrength");
    private static readonly int ID_DissolveAmount = Shader.PropertyToID("_DissolveAmount");
    private static readonly int ID_GlitchAmount = Shader.PropertyToID("_GlitchAmount");
    private static readonly int ID_BodilessAmount = Shader.PropertyToID("_BodilessAmount");
    private static readonly int ID_EmissionStrength = Shader.PropertyToID("_EmissionStrength");
    private static readonly int ID_RippleColor = Shader.PropertyToID("_RippleColor");
    private static readonly int ID_RippleCenter = Shader.PropertyToID("_RippleCenter");
    private static readonly int ID_RippleProgress = Shader.PropertyToID("_RippleProgress");
    private static readonly int ID_SplitProgress = Shader.PropertyToID("_SplitProgress");
    private static readonly int ID_SplitColorA = Shader.PropertyToID("_SplitColorA");
    private static readonly int ID_SplitColorB = Shader.PropertyToID("_SplitColorB");

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _glassBlock = new MaterialPropertyBlock();
        EnsureConfigs();
        _blended = CloneConfig(stateConfigs[(int)_currentState]);
        _morphFrom = CloneConfig(_blended);
        if (playerHead != null)
        {
            _lastHeadPos = playerHead.position;
        }
        if (planarMirror != null)
        {
            _planarBaseTint = planarMirror.tint;
        }
    }

    private void Start()
    {
        // Start the active state's audio bed and particles.
        for (int i = 0; i < 5; i++)
        {
            if (stateBeds != null && i < stateBeds.Length && stateBeds[i] != null)
            {
                stateBeds[i].volume = (i == (int)_currentState) ? 1f : 0f;
                if (!stateBeds[i].isPlaying)
                {
                    stateBeds[i].Play();
                }
            }
            if (stateParticles != null && i < stateParticles.Length && stateParticles[i] != null)
            {
                if (i == (int)_currentState)
                {
                    if (!stateParticles[i].isPlaying) stateParticles[i].Play();
                }
                else
                {
                    stateParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }

    private void Update()
    {
        UpdateProximity();
        UpdateMorphBlend();
        ApplyBlendedToVisuals();
        UpdateChoiceBlend();
        ApplyChoiceOverlay();
        UpdateDwell();
        UpdateTouchFallback();
    }

    private void LateUpdate()
    {
        UpdateReflectionTwin();
    }

    // ---------------------------------------------------------------- Reflection twin
    private void UpdateReflectionTwin()
    {
        if (mirrorPlane == null) return;

        Vector3 planePos = mirrorPlane.position;
        Vector3 planeNormal = mirrorPlane.forward;

        // Planar-mirror mode: an external real mirror reflects the twin, so the twin copies the
        // player's pose on the player's side instead of being placed behind the mirror.
        if (planarMirrorMode)
        {
            UpdatePlanarTwin(planePos);
            UpdateSplitOverlays();
            return;
        }

        // Twin root follows reflected head pose with damped lag.
        if (soulTwinRoot != null && playerHead != null)
        {
            ReflectPose(playerHead, planePos, planeNormal, out Vector3 rPos, out Quaternion rRot);

            // Idle hover bob (slightly out of phase).
            float t = Time.time;
            float bob = Mathf.Sin((t / Mathf.Max(hoverPeriod, 0.01f)) * Mathf.PI * 2f + 1.3f) * hoverAmplitude;
            rPos += Vector3.up * bob;

            soulTwinRoot.position = Vector3.SmoothDamp(soulTwinRoot.position, rPos, ref _twinVel, reflectionSmoothTime);

            // Twin body faces the player.
            if (soulTwinBody != null)
            {
                Vector3 toPlayer = playerHead.position - soulTwinBody.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 1e-4f)
                {
                    Quaternion faceRot = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                    float yawDrift = Mathf.Sin(t * Mathf.Deg2Rad * yawDriftSpeed) * 6f;
                    faceRot *= Quaternion.Euler(0f, yawDrift, 0f);
                    soulTwinBody.rotation = Quaternion.Slerp(soulTwinBody.rotation, faceRot, Time.deltaTime / Mathf.Max(reflectionSmoothTime, 0.001f));
                }
            }
        }

        // Reflect hands (twin hands mirror the player's hands).
        if (soulTwinHandLeft != null && playerHandLeft != null)
        {
            ReflectPose(playerHandLeft, planePos, planeNormal, out Vector3 hPos, out Quaternion hRot);
            soulTwinHandLeft.position = Vector3.SmoothDamp(soulTwinHandLeft.position, hPos, ref _twinHandLVel, reflectionSmoothTime);
            soulTwinHandLeft.rotation = Quaternion.Slerp(soulTwinHandLeft.rotation, hRot, Time.deltaTime / Mathf.Max(reflectionSmoothTime, 0.001f));
        }
        if (soulTwinHandRight != null && playerHandRight != null)
        {
            ReflectPose(playerHandRight, planePos, planeNormal, out Vector3 hPos, out Quaternion hRot);
            soulTwinHandRight.position = Vector3.SmoothDamp(soulTwinHandRight.position, hPos, ref _twinHandRVel, reflectionSmoothTime);
            soulTwinHandRight.rotation = Quaternion.Slerp(soulTwinHandRight.rotation, hRot, Time.deltaTime / Mathf.Max(reflectionSmoothTime, 0.001f));
        }

        UpdateSplitOverlays();
    }

    private void UpdatePlanarTwin(Vector3 mirrorPos)
    {
        float t = Time.time;

        // Twin root copies the player's head position (player side) with damped lag; an external
        // planar mirror reflects it into the glass so it reads as the player's reflection.
        if (soulTwinRoot != null && playerHead != null)
        {
            Vector3 targetPos = playerHead.position + Vector3.up * planarTwinVerticalOffset;
            float bob = Mathf.Sin((t / Mathf.Max(hoverPeriod, 0.01f)) * Mathf.PI * 2f + 1.3f) * hoverAmplitude;
            targetPos += Vector3.up * bob;
            soulTwinRoot.position = Vector3.SmoothDamp(soulTwinRoot.position, targetPos, ref _twinVel, reflectionSmoothTime);

            // Twin body faces the mirror so its reflection faces back at the player.
            if (soulTwinBody != null)
            {
                Vector3 toMirror = mirrorPos - soulTwinBody.position;
                toMirror.y = 0f;
                if (toMirror.sqrMagnitude > 1e-4f)
                {
                    Quaternion faceRot = Quaternion.LookRotation(toMirror.normalized, Vector3.up);
                    float yawDrift = Mathf.Sin(t * Mathf.Deg2Rad * yawDriftSpeed) * 6f;
                    faceRot *= Quaternion.Euler(0f, yawDrift, 0f);
                    soulTwinBody.rotation = Quaternion.Slerp(soulTwinBody.rotation, faceRot, Time.deltaTime / Mathf.Max(reflectionSmoothTime, 0.001f));
                }
            }
        }

        // Hands copy the player's hands directly (lagged), only if assigned & active.
        if (soulTwinHandLeft != null && soulTwinHandLeft.gameObject.activeSelf && playerHandLeft != null)
        {
            soulTwinHandLeft.position = Vector3.SmoothDamp(soulTwinHandLeft.position, playerHandLeft.position, ref _twinHandLVel, reflectionSmoothTime);
            soulTwinHandLeft.rotation = Quaternion.Slerp(soulTwinHandLeft.rotation, playerHandLeft.rotation, Time.deltaTime / Mathf.Max(reflectionSmoothTime, 0.001f));
        }
        if (soulTwinHandRight != null && soulTwinHandRight.gameObject.activeSelf && playerHandRight != null)
        {
            soulTwinHandRight.position = Vector3.SmoothDamp(soulTwinHandRight.position, playerHandRight.position, ref _twinHandRVel, reflectionSmoothTime);
            soulTwinHandRight.rotation = Quaternion.Slerp(soulTwinHandRight.rotation, playerHandRight.rotation, Time.deltaTime / Mathf.Max(reflectionSmoothTime, 0.001f));
        }
    }

    private static void ReflectPose(Transform src, Vector3 planePos, Vector3 planeNormal, out Vector3 pos, out Quaternion rot)
    {
        planeNormal = planeNormal.sqrMagnitude > 1e-6f ? planeNormal.normalized : Vector3.forward;
        Vector3 toPoint = src.position - planePos;
        float d = Vector3.Dot(toPoint, planeNormal);
        pos = src.position - 2f * d * planeNormal;

        // Reflect the rotation across the plane.
        Vector3 fwd = Vector3.Reflect(src.forward, planeNormal);
        Vector3 up = Vector3.Reflect(src.up, planeNormal);
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
        if (up.sqrMagnitude < 1e-6f) up = Vector3.up;
        rot = Quaternion.LookRotation(fwd, up);
    }

    private void UpdateSplitOverlays()
    {
        if (splitOverlays == null || splitOverlays.Length == 0) return;

        bool chaos = _blended != null && _blended.glitch > 0.1f;
        bool enable = _proximity >= splitEnableThreshold || chaos;

        for (int i = 0; i < splitOverlays.Length; i++)
        {
            Transform ov = splitOverlays[i];
            if (ov == null) continue;
            if (ov.gameObject.activeSelf != enable)
            {
                ov.gameObject.SetActive(enable);
            }
            if (enable && soulTwinBody != null)
            {
                // Drift apart a few cm with per-copy micro-lag, slightly offset.
                float phase = Time.time * (chaos ? 6f : 1.2f) + i * 2.1f;
                float offset = (chaos ? 0.06f : 0.03f) * (i + 1);
                Vector3 drift = new Vector3(Mathf.Sin(phase) * offset, Mathf.Cos(phase * 0.7f) * offset * 0.5f, 0f);
                ov.position = Vector3.Lerp(ov.position, soulTwinBody.position + drift, Time.deltaTime * 6f);
                ov.rotation = soulTwinBody.rotation;
            }
        }
    }

    // ---------------------------------------------------------------- Proximity
    private void UpdateProximity()
    {
        if (playerHead == null || mirrorPlane == null) return;
        float dist = Vector3.Distance(playerHead.position, mirrorPlane.position);
        // far -> 0, near -> 1
        float raw = Mathf.InverseLerp(proximityFar, proximityNear, dist);
        float target = Mathf.SmoothStep(0f, 1f, raw);
        _proximity = Mathf.Lerp(_proximity, target, Time.deltaTime / Mathf.Max(reflectionSmoothTime, 0.001f));
    }

    // ---------------------------------------------------------------- Morph blend
    private void UpdateMorphBlend()
    {
        if (_morphT < 1f)
        {
            _morphT += Time.deltaTime / Mathf.Max(morphDuration, 0.001f);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_morphT));
            StateConfig to = stateConfigs[(int)_targetState];
            LerpConfigInto(_blended, _morphFrom, to, k);

            // Crossfade audio beds.
            CrossfadeBeds(k);
        }
        _currentState = _morphT >= 1f ? _targetState : _currentState;
    }

    private void CrossfadeBeds(float k)
    {
        if (stateBeds == null) return;
        for (int i = 0; i < stateBeds.Length && i < 5; i++)
        {
            if (stateBeds[i] == null) continue;
            float targetVol;
            if (i == (int)_targetState) targetVol = k;
            else if (i == (int)_currentState) targetVol = 1f - k;
            else targetVol = 0f;
            // proximity gently fills the bed
            targetVol *= Mathf.Lerp(0.6f, 1f, _proximity);
            stateBeds[i].volume = targetVol;
            if (targetVol > 0.001f && !stateBeds[i].isPlaying) stateBeds[i].Play();
        }
    }

    // ---------------------------------------------------------------- Apply visuals
    private void ApplyBlendedToVisuals()
    {
        if (_blended == null) return;

        // Soul material via MPB.
        if (soulTwinBodyRenderer != null && _mpb != null)
        {
            soulTwinBodyRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ID_BaseColor, _blended.soulBase);
            _mpb.SetColor(ID_RimColor, _blended.soulRim);
            float rimStrength = Mathf.Lerp(1.6f, 3.8f, _proximity) + _pulseValue * 3f;
            _mpb.SetFloat(ID_RimStrength, rimStrength);
            _mpb.SetFloat(ID_DissolveAmount, _blended.dissolve);
            _mpb.SetFloat(ID_GlitchAmount, _blended.glitch);
            _mpb.SetFloat(ID_BodilessAmount, _blended.bodiless);
            _mpb.SetFloat(ID_EmissionStrength, 1f + _pulseValue * 2f);
            soulTwinBodyRenderer.SetPropertyBlock(_mpb);
        }

        // Beam light.
        if (beamLight != null)
        {
            beamLight.color = _blended.beamColor;
            float intensity = _blended.beamIntensity * Mathf.Lerp(0.7f, 1.2f, _proximity);
            intensity *= (1f + _pulseValue);

            // Chaos flicker: localized fast noise on beam intensity only.
            if (_blended.glitch > 0.1f)
            {
                float flick = (Mathf.PerlinNoise(Time.time * 25f, 0.5f) - 0.5f) * 2f; // -1..1
                float amp = 0.2f * Mathf.Lerp(0.4f, 1f, _proximity); // capped +/-20%
                intensity *= 1f + flick * amp;
            }
            beamLight.intensity = Mathf.Max(0f, intensity);
        }

        // Fog + ambient.
        RenderSettings.fogColor = _blended.fogColor;
        RenderSettings.ambientLight = _blended.fogColor * 1.5f;

        // Particle emission of active state scales with proximity.
        if (stateParticles != null)
        {
            int active = (int)_targetState;
            for (int i = 0; i < stateParticles.Length && i < 5; i++)
            {
                if (stateParticles[i] == null) continue;
                var emission = stateParticles[i].emission;
                if (i == active)
                {
                    if (!stateParticles[i].isPlaying) stateParticles[i].Play();
                    emission.rateOverTime = Mathf.Lerp(2f, maxParticleEmission, _proximity);
                }
                else
                {
                    emission.rateOverTime = 0f;
                }
            }
        }
    }

    // ---------------------------------------------------------------- Choice mechanic
    private void UpdateChoiceBlend()
    {
        float target = _choiceCommitted
            ? 1f
            : (_choiceTheme != ChoiceTheme.None ? previewWeight : 0f);
        _choiceWeight = Mathf.MoveTowards(_choiceWeight, target, Time.deltaTime / Mathf.Max(choiceBlendTime, 0.01f));
    }

    private StateConfig GetThemeConfig(ChoiceTheme theme)
    {
        switch (theme)
        {
            case ChoiceTheme.Void: return voidConfig;
            case ChoiceTheme.Rebirth: return rebirthConfig;
            case ChoiceTheme.Judgement: return judgementConfig;
            default: return null;
        }
    }

    private void ApplyChoiceOverlay()
    {
        // Glass split is driven every frame so it eases out to 0 when not Judgement.
        float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_choiceWeight));
        StateConfig theme = GetThemeConfig(_choiceTheme);

        // Glass vertical split (Judgement only; others keep split at 0).
        if (mirrorGlassRenderer != null)
        {
            if (_glassBlock == null) _glassBlock = new MaterialPropertyBlock();
            mirrorGlassRenderer.GetPropertyBlock(_glassBlock);
            float split = (_choiceTheme == ChoiceTheme.Judgement) ? w : 0f;
            _glassBlock.SetFloat(ID_SplitProgress, split);
            _glassBlock.SetColor(ID_SplitColorA, judgementGold);
            _glassBlock.SetColor(ID_SplitColorB, judgementRed);
            mirrorGlassRenderer.SetPropertyBlock(_glassBlock);
        }

        // Planar mirror tint -> black for VOID, restore otherwise.
        if (planarMirror != null)
        {
            Color tgt = (_choiceTheme == ChoiceTheme.Void) ? Color.black : _planarBaseTint;
            planarMirror.tint = Color.Lerp(_planarBaseTint, tgt, w);
        }

        if (theme == null || w <= 0.001f)
        {
            // Drive choice particles off when no active theme.
            DriveChoiceParticles(0f);
            return;
        }

        // Soul body overlay (lean colours + VOID dissolve/bodiless).
        if (soulTwinBodyRenderer != null && _mpb != null)
        {
            soulTwinBodyRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ID_BaseColor, Color.Lerp(_blended.soulBase, theme.soulBase, w));
            _mpb.SetColor(ID_RimColor, Color.Lerp(_blended.soulRim, theme.soulRim, w));
            _mpb.SetFloat(ID_DissolveAmount, Mathf.Lerp(_blended.dissolve, theme.dissolve, w));
            _mpb.SetFloat(ID_BodilessAmount, Mathf.Lerp(_blended.bodiless, theme.bodiless, w));
            soulTwinBodyRenderer.SetPropertyBlock(_mpb);
        }

        // Beam overlay.
        if (beamLight != null)
        {
            beamLight.color = Color.Lerp(beamLight.color, theme.beamColor, w);
            beamLight.intensity = Mathf.Lerp(beamLight.intensity, theme.beamIntensity, w);
        }

        // Fog overlay.
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, theme.fogColor, w);

        DriveChoiceParticles(w);
    }

    private void DriveChoiceParticles(float w)
    {
        if (choiceParticles == null) return;
        for (int i = 0; i < choiceParticles.Length; i++)
        {
            var ps = choiceParticles[i];
            if (ps == null) continue;
            bool active = (i == (int)_choiceTheme) && w > 0.05f && _choiceTheme != ChoiceTheme.Void;
            var em = ps.emission;
            if (active)
            {
                if (!ps.isPlaying) ps.Play();
                em.rateOverTime = Mathf.Lerp(2f, maxParticleEmission, w);
            }
            else
            {
                em.rateOverTime = 0f;
            }
        }
    }

    /// <summary>Non-committal hover preview: the mirror leans toward the theme; reversible.</summary>
    public void SetChoicePreview(ChoiceTheme theme)
    {
        if (_choiceCommitted) return;
        _choiceTheme = theme;
    }

    /// <summary>Withdraw preview: revert toward the neutral reflection.</summary>
    public void ClearChoicePreview()
    {
        if (_choiceCommitted) return;
        _choiceTheme = ChoiceTheme.None;
    }

    /// <summary>Finalize the choice: fully transform the mirror and lock further changes.</summary>
    public void CommitChoice(ChoiceTheme theme)
    {
        _choiceTheme = theme;
        _choiceCommitted = true;
    }

    public bool ChoiceCommitted => _choiceCommitted;
    public ChoiceTheme ActiveChoice => _choiceTheme;

    // ---------------------------------------------------------------- Dwell
    private void UpdateDwell()
    {
        if (_choiceCommitted) return;
        if (playerHead == null || mirrorPlane == null) return;

        float headDelta = (playerHead.position - _lastHeadPos).magnitude;
        _lastHeadPos = playerHead.position;

        Vector3 toMirror = (mirrorPlane.position - playerHead.position).normalized;
        bool facing = Vector3.Dot(playerHead.forward, toMirror) > facingDot;
        bool still = headDelta < dwellMoveThreshold;

        if (facing && still)
        {
            _dwellTimer += Time.deltaTime;
            if (_dwellTimer >= dwellDelay)
            {
                _dwelling = true;
                _dwellCycleTimer += Time.deltaTime;
                if (_dwellCycleTimer >= dwellInterval)
                {
                    _dwellCycleTimer = 0f;
                    AdvanceState();
                }
            }
        }
        else
        {
            // Significant movement gently pauses (decay, not harsh reset).
            _dwellTimer = Mathf.Max(0f, _dwellTimer - Time.deltaTime * 2f);
            if (_dwellTimer <= 0f) _dwelling = false;
        }
    }

    // ---------------------------------------------------------------- Touch
    private void UpdateTouchFallback()
    {
        if (mirrorPlane == null) return;
        if (Time.time - _lastTouchTime < touchDebounce) { _handTouchingLast = false; return; }

        bool touchingNow = false;
        touchingNow |= TryHandTouch(playerHandLeft, playerHandLeft);
        if (!touchingNow) touchingNow |= TryHandTouch(playerHandRight, playerHandRight);

        _handTouchingLast = touchingNow;
    }

    private bool TryHandTouch(Transform hand, Transform handForCallback)
    {
        if (hand == null) return false;
        Vector3 planePos = mirrorPlane.position;
        Vector3 planeNormal = mirrorPlane.forward.normalized;
        Vector3 toHand = hand.position - planePos;
        float dist = Mathf.Abs(Vector3.Dot(toHand, planeNormal));
        if (dist > touchDistance) return false;

        // Within mirror bounds?
        Vector3 projected = hand.position - Vector3.Dot(toHand, planeNormal) * planeNormal;
        Vector3 local = projected - planePos;
        float u = Vector3.Dot(local, mirrorPlane.right);
        float v = Vector3.Dot(local, mirrorPlane.up);
        if (Mathf.Abs(u) > mirrorHalfWidth || Mathf.Abs(v) > mirrorHalfHeight) return false;

        if (!_handTouchingLast)
        {
            OnMirrorTouch(projected, handForCallback);
        }
        return true;
    }

    /// <summary>
    /// Called when a ghost hand contacts the mirror surface. Spawns a ripple, pulses beam + rim,
    /// plays a chime, and advances to the next state.
    /// </summary>
    public void OnMirrorTouch(Vector3 worldHitPoint, Transform hand)
    {
        if (Time.time - _lastTouchTime < touchDebounce) return;
        _lastTouchTime = Time.time;

        // Map hit point to mirror UV.
        Vector2 uv = WorldToMirrorUV(worldHitPoint);

        // Ripple.
        if (mirrorGlassRenderer != null)
        {
            if (_rippleRoutine != null) StopCoroutine(_rippleRoutine);
            _rippleRoutine = StartCoroutine(RippleRoutine(uv));
        }

        // Pulse beam + rim.
        if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(PulseRoutine());

        // Chime.
        if (touchChime != null)
        {
            touchChime.pitch = 1f + Random.Range(-chimePitchVariance, chimePitchVariance);
            touchChime.Play();
        }

        // Advance.
        AdvanceState();
    }

    private Vector2 WorldToMirrorUV(Vector3 worldPoint)
    {
        if (mirrorPlane == null) return new Vector2(0.5f, 0.5f);
        Vector3 planeNormal = mirrorPlane.forward.normalized;
        Vector3 toPoint = worldPoint - mirrorPlane.position;
        Vector3 projected = toPoint - Vector3.Dot(toPoint, planeNormal) * planeNormal;
        float u = Vector3.Dot(projected, mirrorPlane.right) / Mathf.Max(mirrorHalfWidth * 2f, 1e-4f) + 0.5f;
        float v = Vector3.Dot(projected, mirrorPlane.up) / Mathf.Max(mirrorHalfHeight * 2f, 1e-4f) + 0.5f;
        return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
    }

    private IEnumerator RippleRoutine(Vector2 uv)
    {
        var block = new MaterialPropertyBlock();
        mirrorGlassRenderer.GetPropertyBlock(block);
        block.SetColor(ID_RippleColor, _blended != null ? _blended.soulRim : Color.cyan);
        block.SetVector(ID_RippleCenter, new Vector4(uv.x, uv.y, 0f, 0f));
        mirrorGlassRenderer.SetPropertyBlock(block);

        float t = 0f;
        while (t < rippleDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / rippleDuration);
            mirrorGlassRenderer.GetPropertyBlock(block);
            block.SetFloat(ID_RippleProgress, p);
            mirrorGlassRenderer.SetPropertyBlock(block);
            yield return null;
        }
        mirrorGlassRenderer.GetPropertyBlock(block);
        block.SetFloat(ID_RippleProgress, 0f);
        mirrorGlassRenderer.SetPropertyBlock(block);
        _rippleRoutine = null;
    }

    private IEnumerator PulseRoutine()
    {
        // Overshoot then settle: ramp up quickly to peak (~30% of duration), ease back to 0.
        float t = 0f;
        float peakTime = pulseSettleTime * 0.3f;
        while (t < pulseSettleTime)
        {
            t += Time.deltaTime;
            if (t < peakTime)
            {
                _pulseValue = Mathf.Lerp(0f, pulseOvershoot, t / Mathf.Max(peakTime, 1e-4f));
            }
            else
            {
                float k = (t - peakTime) / Mathf.Max(pulseSettleTime - peakTime, 1e-4f);
                _pulseValue = Mathf.Lerp(pulseOvershoot, 0f, Mathf.SmoothStep(0f, 1f, k));
            }
            yield return null;
        }
        _pulseValue = 0f;
        _pulseRoutine = null;
    }

    // ---------------------------------------------------------------- Public API
    public void AdvanceState()
    {
        SetState((MorphState)(((int)_targetState + 1) % 5));
    }

    public void SetState(MorphState state)
    {
        if (state == _targetState && _morphT >= 1f) return;
        EnsureConfigs();
        _currentState = (_morphT >= 1f) ? _targetState : _currentState;
        _morphFrom = CloneConfig(_blended);
        _targetState = state;
        _morphT = 0f;
    }

    public MorphState CurrentState => _currentState;
    public MorphState TargetState => _targetState;
    public float Proximity => _proximity;

    // ---------------------------------------------------------------- Helpers
    private void EnsureConfigs()
    {
        if (stateConfigs == null || stateConfigs.Length < 5)
        {
            var fixedArr = new StateConfig[5];
            for (int i = 0; i < 5; i++)
            {
                fixedArr[i] = (stateConfigs != null && i < stateConfigs.Length && stateConfigs[i] != null)
                    ? stateConfigs[i] : new StateConfig();
            }
            stateConfigs = fixedArr;
        }
        for (int i = 0; i < 5; i++)
        {
            if (stateConfigs[i] == null) stateConfigs[i] = new StateConfig();
        }
    }

    private static StateConfig CloneConfig(StateConfig s)
    {
        if (s == null) return new StateConfig();
        return new StateConfig
        {
            soulBase = s.soulBase,
            soulRim = s.soulRim,
            beamColor = s.beamColor,
            beamIntensity = s.beamIntensity,
            fogColor = s.fogColor,
            dissolve = s.dissolve,
            glitch = s.glitch,
            bodiless = s.bodiless
        };
    }

    private static void LerpConfigInto(StateConfig dst, StateConfig a, StateConfig b, float k)
    {
        dst.soulBase = Color.Lerp(a.soulBase, b.soulBase, k);
        dst.soulRim = Color.Lerp(a.soulRim, b.soulRim, k);
        dst.beamColor = Color.Lerp(a.beamColor, b.beamColor, k);
        dst.beamIntensity = Mathf.Lerp(a.beamIntensity, b.beamIntensity, k);
        dst.fogColor = Color.Lerp(a.fogColor, b.fogColor, k);
        dst.dissolve = Mathf.Lerp(a.dissolve, b.dissolve, k);
        dst.glitch = Mathf.Lerp(a.glitch, b.glitch, k);
        dst.bodiless = Mathf.Lerp(a.bodiless, b.bodiless, k);
    }
}
