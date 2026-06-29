using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

namespace Afterlife
{
    /// <summary>
    /// Drives the three "world states" of the same nature world:
    /// 0 = Harmonie (lush), 1 = Zerfall (decay/autumn), 2 = Chaos (broken/storm).
    /// Blends lighting, fog, ambient, vegetation tint (via shared materials),
    /// wind and URP post-processing volumes smoothly between states.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldStateController : MonoBehaviour
    {
        [System.Serializable]
        public class WorldStatePreset
        {
            public string name = "State";

            [Header("Sun")]
            public Color sunColor = Color.white;
            public float sunIntensity = 1.5f;
            public Vector3 sunRotation = new Vector3(48f, 35f, 0f);

            [Header("Ambient & Fog")]
            [Range(0f, 3f)] public float ambientIntensity = 1f;
            public Color fogColor = new Color(0.78f, 0.85f, 0.82f);
            public float fogStart = 60f;
            public float fogEnd = 380f;

            [Header("Vegetation tint (multiplied with base)")]
            public Color foliageColor = Color.white;
            public Color barkColor = Color.white;

            [Header("Wind")]
            public float windMain = 0.6f;
            public float windTurbulence = 0.4f;
        }

        [Header("Presets (index 0=Harmonie, 1=Zerfall, 2=Chaos)")]
        public WorldStatePreset[] presets = new WorldStatePreset[]
        {
            new WorldStatePreset {
                name = "Harmonie",
                sunColor = new Color(1.0f, 0.95f, 0.84f), sunIntensity = 1.5f, sunRotation = new Vector3(48f, 35f, 0f),
                ambientIntensity = 1.0f, fogColor = new Color(0.78f, 0.85f, 0.82f), fogStart = 60f, fogEnd = 380f,
                foliageColor = Color.white, barkColor = Color.white, windMain = 0.6f, windTurbulence = 0.4f
            },
            new WorldStatePreset {
                name = "Zerfall",
                sunColor = new Color(0.95f, 0.78f, 0.55f), sunIntensity = 1.1f, sunRotation = new Vector3(25f, 60f, 0f),
                ambientIntensity = 0.7f, fogColor = new Color(0.62f, 0.58f, 0.5f), fogStart = 35f, fogEnd = 240f,
                foliageColor = new Color(0.85f, 0.55f, 0.28f), barkColor = new Color(0.72f, 0.62f, 0.5f),
                windMain = 1.4f, windTurbulence = 1.2f
            },
            new WorldStatePreset {
                name = "Chaos",
                sunColor = new Color(0.5f, 0.52f, 0.6f), sunIntensity = 0.5f, sunRotation = new Vector3(12f, 200f, 0f),
                ambientIntensity = 0.35f, fogColor = new Color(0.32f, 0.33f, 0.35f), fogStart = 12f, fogEnd = 130f,
                foliageColor = new Color(0.32f, 0.3f, 0.26f), barkColor = new Color(0.35f, 0.33f, 0.3f),
                windMain = 3.0f, windTurbulence = 3.0f
            },
        };

        [Header("Scene references")]
        public Light sun;
        public WindZone wind;
        [Tooltip("Shared foliage materials tinted by foliageColor (oak branches/billboard, bushes, flowers).")]
        public Material[] foliageMaterials;
        [Tooltip("Shared trunk/bark materials tinted by barkColor.")]
        public Material[] barkMaterials;
        [Tooltip("One global Volume per state, index-aligned with presets.")]
        public Volume[] stateVolumes;
        [Tooltip("One atmosphere GameObject (particles) per state, index-aligned with presets.")]
        public GameObject[] atmospheres;
        [Tooltip("One fragment-group GameObject per state, index-aligned with presets. " +
                 "Only the active state's group is enabled; its FloatingFragment children replay their spawn animation.")]
        public GameObject[] stateFragments;

        [Header("Transition")]
        [Min(0f)] public float transitionDuration = 2.5f;
        public int startState = 0;

        [Header("Input (New Input System)")]
        [Tooltip("Cycles to the next state.")]
        public InputActionProperty cycleAction = new InputActionProperty(
            new InputAction("CycleWorldState", InputActionType.Button));

        public int CurrentState { get; private set; }
        public bool IsTransitioning { get; private set; }

        // cached original (authored) material base colors so tint multiplies correctly
        private readonly Dictionary<Material, Color> _foliageBase = new Dictionary<Material, Color>();
        private readonly Dictionary<Material, Color> _barkBase = new Dictionary<Material, Color>();
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private Coroutine _routine;

        void Awake()
        {
            CacheBaseColors();
        }

        void OnEnable()
        {
            if (cycleAction.action != null)
            {
                // add default bindings if none authored
                if (cycleAction.action.bindings.Count == 0)
                {
                    cycleAction.action.AddBinding("<Keyboard>/space");
                    cycleAction.action.AddBinding("<Keyboard>/n");
                    cycleAction.action.AddBinding("<Gamepad>/buttonNorth");
                    cycleAction.action.AddBinding("<XRController>{RightHand}/primaryButton");
                    cycleAction.action.AddBinding("<XRController>{LeftHand}/primaryButton");
                }
                cycleAction.action.performed += OnCyclePerformed;
                cycleAction.action.Enable();
            }
            // Apply the starting state immediately so the world looks correct from frame 1.
            ApplyStateImmediate(Mathf.Clamp(startState, 0, presets.Length - 1));
        }

        void OnDisable()
        {
            if (cycleAction.action != null)
            {
                cycleAction.action.performed -= OnCyclePerformed;
                cycleAction.action.Disable();
            }
            RestoreBaseColors();
        }

        void Update()
        {
            // Direct number keys for quick testing in the Editor / desktop.
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) GoToState(0);
                else if (kb.digit2Key.wasPressedThisFrame) GoToState(1);
                else if (kb.digit3Key.wasPressedThisFrame) GoToState(2);
            }
        }

        private void OnCyclePerformed(InputAction.CallbackContext ctx)
        {
            GoToState((CurrentState + 1) % presets.Length);
        }

        /// <summary>Smoothly transition to the given state.</summary>
        public void GoToState(int index)
        {
            index = Mathf.Clamp(index, 0, presets.Length - 1);
            if (!Application.isPlaying)
            {
                ApplyStateImmediate(index);
                return;
            }
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(TransitionRoutine(index));
        }

        private IEnumerator TransitionRoutine(int target)
        {
            IsTransitioning = true;

            // Capture current live values as the "from" state.
            var fromSunColor = sun ? sun.color : Color.white;
            var fromSunInt = sun ? sun.intensity : 1f;
            var fromSunRot = sun ? sun.transform.eulerAngles : Vector3.zero;
            var fromAmbient = RenderSettings.ambientIntensity;
            var fromFogColor = RenderSettings.fogColor;
            var fromFogStart = RenderSettings.fogStartDistance;
            var fromFogEnd = RenderSettings.fogEndDistance;
            var fromWindMain = wind ? wind.windMain : 0f;
            var fromWindTurb = wind ? wind.windTurbulence : 0f;

            var fromFoliage = new Dictionary<Material, Color>();
            foreach (var kv in _foliageBase) fromFoliage[kv.Key] = kv.Key.HasProperty(BaseColorID) ? kv.Key.GetColor(BaseColorID) : Color.white;
            var fromBark = new Dictionary<Material, Color>();
            foreach (var kv in _barkBase) fromBark[kv.Key] = kv.Key.HasProperty(BaseColorID) ? kv.Key.GetColor(BaseColorID) : Color.white;

            float[] fromWeights = CaptureVolumeWeights();

            // Switch atmosphere emission immediately (particles cross-fade on their own).
            SetAtmosphere(target);
            // Swap fragment groups at the start of the transition so the new
            // ones spawn-in while the world morphs.
            SetFragments(target);

            var p = presets[target];
            // Target sun rotation chosen as nearest-equivalent to avoid long spins.
            Vector3 toSunRot = p.sunRotation;

            float t = 0f;
            float dur = Mathf.Max(0.01f, transitionDuration);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

                if (sun)
                {
                    sun.color = Color.Lerp(fromSunColor, p.sunColor, k);
                    sun.intensity = Mathf.Lerp(fromSunInt, p.sunIntensity, k);
                    sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(fromSunRot), Quaternion.Euler(toSunRot), k);
                }
                RenderSettings.ambientIntensity = Mathf.Lerp(fromAmbient, p.ambientIntensity, k);
                RenderSettings.fogColor = Color.Lerp(fromFogColor, p.fogColor, k);
                RenderSettings.fogStartDistance = Mathf.Lerp(fromFogStart, p.fogStart, k);
                RenderSettings.fogEndDistance = Mathf.Lerp(fromFogEnd, p.fogEnd, k);
                if (wind)
                {
                    wind.windMain = Mathf.Lerp(fromWindMain, p.windMain, k);
                    wind.windTurbulence = Mathf.Lerp(fromWindTurb, p.windTurbulence, k);
                }

                foreach (var kv in _foliageBase)
                {
                    var target2 = kv.Value * p.foliageColor;
                    kv.Key.SetColor(BaseColorID, Color.Lerp(fromFoliage[kv.Key], target2, k));
                }
                foreach (var kv in _barkBase)
                {
                    var target2 = kv.Value * p.barkColor;
                    kv.Key.SetColor(BaseColorID, Color.Lerp(fromBark[kv.Key], target2, k));
                }

                ApplyVolumeWeights(fromWeights, target, k);
                yield return null;
            }

            ApplyStateImmediate(target);
            IsTransitioning = false;
            _routine = null;
        }

        /// <summary>Instantly snap the world to a state (also used in Edit mode for previews).</summary>
        public void ApplyStateImmediate(int index)
        {
            if (presets == null || presets.Length == 0) return;
            index = Mathf.Clamp(index, 0, presets.Length - 1);
            var p = presets[index];
            CurrentState = index;

            if (_foliageBase.Count == 0 && _barkBase.Count == 0) CacheBaseColors();

            if (sun)
            {
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(p.sunRotation);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = p.ambientIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
            if (wind)
            {
                wind.windMain = p.windMain;
                wind.windTurbulence = p.windTurbulence;
            }
            foreach (var kv in _foliageBase) kv.Key.SetColor(BaseColorID, kv.Value * p.foliageColor);
            foreach (var kv in _barkBase) kv.Key.SetColor(BaseColorID, kv.Value * p.barkColor);

            if (stateVolumes != null)
                for (int i = 0; i < stateVolumes.Length; i++)
                    if (stateVolumes[i]) stateVolumes[i].weight = (i == index) ? 1f : 0f;

            SetAtmosphere(index);
            SetFragments(index);
            DynamicGI.UpdateEnvironment();
        }

        private void CacheBaseColors()
        {
            _foliageBase.Clear();
            _barkBase.Clear();
            if (foliageMaterials != null)
                foreach (var m in foliageMaterials)
                    if (m && m.HasProperty(BaseColorID) && !_foliageBase.ContainsKey(m))
                        _foliageBase[m] = m.GetColor(BaseColorID);
            if (barkMaterials != null)
                foreach (var m in barkMaterials)
                    if (m && m.HasProperty(BaseColorID) && !_barkBase.ContainsKey(m))
                        _barkBase[m] = m.GetColor(BaseColorID);
        }

        private void RestoreBaseColors()
        {
            foreach (var kv in _foliageBase) if (kv.Key) kv.Key.SetColor(BaseColorID, kv.Value);
            foreach (var kv in _barkBase) if (kv.Key) kv.Key.SetColor(BaseColorID, kv.Value);
        }

        private float[] CaptureVolumeWeights()
        {
            if (stateVolumes == null) return new float[0];
            var w = new float[stateVolumes.Length];
            for (int i = 0; i < stateVolumes.Length; i++) w[i] = stateVolumes[i] ? stateVolumes[i].weight : 0f;
            return w;
        }

        private void ApplyVolumeWeights(float[] from, int target, float k)
        {
            if (stateVolumes == null) return;
            for (int i = 0; i < stateVolumes.Length; i++)
            {
                if (!stateVolumes[i]) continue;
                float to = (i == target) ? 1f : 0f;
                float f = (from != null && i < from.Length) ? from[i] : stateVolumes[i].weight;
                stateVolumes[i].weight = Mathf.Lerp(f, to, k);
            }
        }

        private void SetAtmosphere(int index)
        {
            if (atmospheres == null) return;
            for (int i = 0; i < atmospheres.Length; i++)
            {
                if (!atmospheres[i]) continue;
                atmospheres[i].SetActive(true); // keep active so lingering particles fade
                var ps = atmospheres[i].GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var em = ps.emission;
                    em.enabled = (i == index);
                    if (i == index && !ps.isPlaying) ps.Play();
                }
            }
        }

        private void SetFragments(int index)
        {
            if (stateFragments == null) return;
            for (int i = 0; i < stateFragments.Length; i++)
            {
                if (!stateFragments[i]) continue;
                // Toggling SetActive(true) re-fires OnEnable on FloatingFragment
                // children, replaying their spawn-in animation.
                bool shouldShow = (i == index);
                if (stateFragments[i].activeSelf != shouldShow)
                    stateFragments[i].SetActive(shouldShow);
            }
        }
    }
}
