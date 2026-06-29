using UnityEngine;

namespace Afterlife
{
    /// <summary>
    /// Drives an art "fragment": gentle bob + spin, a scale-in spawn animation,
    /// and (optionally) a looping destruction cycle that matches the world state:
    ///   - CrumbleToDust (Zerfall): the fragment slowly sinks & shrinks while a
    ///     stream of dust pours off it, then reforms.
    ///   - Shatter (Chaos): the fragment violently bursts into shards, then reforms.
    /// The animated mesh is a separate <see cref="visual"/> child so the effect
    /// particle system (a sibling) is never scaled with it.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloatingFragment : MonoBehaviour
    {
        public enum DestructionMode { None, CrumbleToDust, Shatter }

        [Header("Animated visual")]
        [Tooltip("The mesh transform to animate. If null, this transform is used.")]
        public Transform visual;

        [Header("Idle motion")]
        [Tooltip("Vertical bob amplitude in metres.")]
        public float bobAmplitude = 0.12f;
        [Tooltip("Vertical bob speed.")]
        public float bobSpeed = 0.8f;
        [Tooltip("Degrees per second of slow spin around Y.")]
        public float spinSpeed = 12f;

        [Header("Spawn animation")]
        [Tooltip("Seconds for the scale-in spawn animation.")]
        public float spawnDuration = 0.9f;

        [Header("Destruction cycle")]
        public DestructionMode destructionMode = DestructionMode.None;
        [Tooltip("Seconds the fragment floats before it destructs (only if mode != None).")]
        public float visibleDuration = 6f;
        [Tooltip("Seconds the crumble animation lasts.")]
        public float destructDuration = 1.5f;
        [Tooltip("Seconds the fragment stays gone before reforming.")]
        public float hiddenDuration = 1.6f;
        [Tooltip("Particle system played during destruction (dust for crumble, shards for shatter).")]
        public ParticleSystem effectParticles;
        [Tooltip("How many shard particles to burst on Shatter.")]
        public int shatterBurst = 90;

        private enum Phase { Spawning, Idle, Destructing, Hidden }
        private Phase _state;
        private float _stateT;

        private Vector3 _baseLocalPos;
        private Vector3 _targetScale;
        private float _phase;
        private bool _initialized;

        void Awake() { Init(); }

        private void Init()
        {
            if (_initialized) return;
            if (visual == null) visual = transform;
            _baseLocalPos = visual.localPosition;
            _targetScale = visual.localScale;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            _initialized = true;
        }

        void OnEnable()
        {
            Init();
            EnterSpawning();
        }

        private void EnterSpawning()
        {
            _state = Phase.Spawning;
            _stateT = 0f;
            visual.localScale = Vector3.zero;
            visual.localPosition = _baseLocalPos;
        }

        void Update()
        {
            _stateT += Time.deltaTime;

            switch (_state)
            {
                case Phase.Spawning:
                {
                    float k = Mathf.Clamp01(_stateT / Mathf.Max(0.01f, spawnDuration));
                    visual.localScale = _targetScale * EaseOutBack(k);
                    IdleMotion();
                    if (k >= 1f) { _state = Phase.Idle; _stateT = 0f; }
                    break;
                }
                case Phase.Idle:
                {
                    visual.localScale = _targetScale;
                    IdleMotion();
                    if (destructionMode != DestructionMode.None && _stateT >= visibleDuration)
                        EnterDestructing();
                    break;
                }
                case Phase.Destructing:
                {
                    if (destructionMode == DestructionMode.CrumbleToDust)
                    {
                        float k = Mathf.Clamp01(_stateT / Mathf.Max(0.01f, destructDuration));
                        // sink and dissolve away to dust
                        visual.localScale = _targetScale * (1f - k);
                        visual.localPosition = _baseLocalPos + new Vector3(0f, -0.45f * k, 0f);
                        visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
                        if (k >= 1f) EnterHidden();
                    }
                    else // Shatter — fast pop
                    {
                        float k = Mathf.Clamp01(_stateT / 0.2f);
                        visual.localScale = _targetScale * (1f - k);
                        visual.Rotate(Vector3.up, spinSpeed * 4f * Time.deltaTime, Space.World);
                        if (_stateT >= 0.2f) EnterHidden();
                    }
                    break;
                }
                case Phase.Hidden:
                {
                    visual.localScale = Vector3.zero;
                    if (_stateT >= hiddenDuration) EnterSpawning();
                    break;
                }
            }
        }

        private void IdleMotion()
        {
            _phase += Time.deltaTime * bobSpeed;
            float y = Mathf.Sin(_phase) * bobAmplitude;
            visual.localPosition = _baseLocalPos + new Vector3(0f, y, 0f);
            visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        private void EnterDestructing()
        {
            _state = Phase.Destructing;
            _stateT = 0f;
            if (effectParticles != null)
            {
                effectParticles.Clear();
                effectParticles.Play();
                if (destructionMode == DestructionMode.Shatter)
                    effectParticles.Emit(Mathf.Max(1, shatterBurst));
            }
        }

        private void EnterHidden()
        {
            _state = Phase.Hidden;
            _stateT = 0f;
            visual.localScale = Vector3.zero;
            if (effectParticles != null)
                effectParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm1 = x - 1f;
            return 1f + c3 * xm1 * xm1 * xm1 + c1 * xm1 * xm1;
        }
    }
}
