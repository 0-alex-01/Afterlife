using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Scherben-Dramaturgie des Seelensees: Mit jedem neuen Seelenzustand des
/// Spiegels (SoulMorphController: Bloom, Decay, Chaos, Licht) lösen sich
/// Scherben vom großen Spiegel, treiben über den See hinaus und kreisen dort
/// langsam — jede Scherbe leuchtet in der Farbe IHRES Zustands und erinnert an
/// ihn. Trifft der Spieler seine Wahl (PedestalChoiceController), strömen alle
/// Scherben in das gewählte Portal. Beim Ablösen tuscht jede Scherbe eine
/// Welle auf das Wasser. Alles läuft zur Laufzeit — nichts muss platziert werden.
/// </summary>
public class MirrorShatterShards : MonoBehaviour
{
    [Header("Referenzen")]
    [Tooltip("Der Spiegel-Morph-Controller (liefert den Seelenzustand).")]
    public SoulMorphController mirror;
    [Tooltip("Die Wahl-Steuerung (liefert Commit + gewähltes Thema).")]
    public PedestalChoiceController choice;
    [Tooltip("Der stehende Spiegel, von dem sich die Scherben lösen.")]
    public Transform mirrorPlane;
    [Tooltip("Die Seeoberfläche für Ablöse-Wellen. Leer = automatisch gesucht.")]
    public SoulLakeSurface lake;

    [Header("Scherben")]
    [Tooltip("Scherben pro neu erreichtem Seelenzustand.")]
    [Range(1, 4)] public int shardsPerState = 2;
    [Tooltip("Orbit-Radius um den Spiegel: min/max in Metern.")]
    public Vector2 orbitRadius = new Vector2(4.5f, 8.5f);
    [Tooltip("Orbit-Höhe über dem Wasser: min/max in Metern.")]
    public Vector2 orbitHeight = new Vector2(0.8f, 2.6f);
    [Tooltip("Winkelgeschwindigkeit des Orbits (Grad/Sekunde).")]
    public float orbitSpeed = 4f;
    [Tooltip("Sekunden für das Ablösen und Hinaustreiben.")]
    public float breakoutDuration = 4f;
    [Tooltip("Sekunden für den Sog ins Portal nach der Wahl.")]
    public float streamDuration = 3.5f;

    [Header("Zustandsfarben (Rim-Glühen der Scherben)")]
    public Color bloomColor = new Color(0.35f, 0.95f, 0.45f);
    public Color decayColor = new Color(0.85f, 0.55f, 0.25f);
    public Color chaosColor = new Color(0.95f, 0.25f, 0.55f);
    public Color lightColor = new Color(1.0f, 0.92f, 0.70f);

    private class Shard
    {
        public Transform tf;
        public float orbitAngle;     // aktueller Winkel um den Spiegel
        public float orbitDist;
        public float height;
        public float bobPhase;
        public Vector3 spin;         // langsame Eigenrotation
        public Vector3 breakStart;
        public float breakT;         // 0..1 Ablöse-Fortschritt
        public Vector3 streamStart;
        public float streamT;        // 0..1 Portal-Sog-Fortschritt
    }

    private readonly List<Shard> _shards = new List<Shard>();
    private readonly HashSet<SoulMorphController.MorphState> _seenStates
        = new HashSet<SoulMorphController.MorphState>();
    private Material _shardMaterial;   // Basis; pro Scherbe Instanz mit Zustandsfarbe
    private bool _streaming;
    private Transform _streamTarget;

    private void Start()
    {
        if (mirror == null) mirror = FindFirstObjectByType<SoulMorphController>();
        if (choice == null) choice = FindFirstObjectByType<PedestalChoiceController>();
        if (lake == null) lake = FindFirstObjectByType<SoulLakeSurface>();
        _seenStates.Add(SoulMorphController.MorphState.Clear); // Startzustand löst nichts aus
    }

    private void Update()
    {
        // Neuen Seelenzustand erkennen -> Scherben ablösen.
        if (mirror != null && !_streaming)
        {
            var s = mirror.CurrentState;
            if (!_seenStates.Contains(s))
            {
                _seenStates.Add(s);
                ReleaseShards(StateColor(s));
            }
        }

        // Wahl getroffen -> alle Scherben ins gewählte Portal ziehen.
        if (!_streaming && choice != null && choice.Committed)
            BeginStreaming();

        AnimateShards();
    }

    private Color StateColor(SoulMorphController.MorphState s)
    {
        switch (s)
        {
            case SoulMorphController.MorphState.Bloom: return bloomColor;
            case SoulMorphController.MorphState.Decay: return decayColor;
            case SoulMorphController.MorphState.Chaos: return chaosColor;
            case SoulMorphController.MorphState.Light: return lightColor;
            default: return Color.white;
        }
    }

    private void ReleaseShards(Color rim)
    {
        if (mirrorPlane == null) return;

        for (int i = 0; i < shardsPerState; i++)
        {
            var shard = new Shard();

            var go = new GameObject("MirrorShard");
            go.transform.SetParent(transform, false);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildShardMesh(Random.Range(0.25f, 0.55f));
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = BuildShardMaterial(rim);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Start: an einer zufälligen Kante des Spiegels.
            float edgeX = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.35f, 0.65f);
            float edgeY = Random.Range(-0.9f, 1.1f);
            shard.breakStart = mirrorPlane.TransformPoint(new Vector3(edgeX, edgeY, 0f));
            go.transform.position = shard.breakStart;
            go.transform.rotation = mirrorPlane.rotation * Quaternion.Euler(
                Random.Range(-12f, 12f), Random.Range(-12f, 12f), Random.Range(0f, 360f));

            // Ziel-Orbitplatz über dem See.
            shard.orbitAngle = Random.value * 360f;
            shard.orbitDist = Random.Range(orbitRadius.x, orbitRadius.y);
            shard.height = Random.Range(orbitHeight.x, orbitHeight.y);
            shard.bobPhase = Random.value * Mathf.PI * 2f;
            shard.spin = new Vector3(Random.Range(-8f, 8f), Random.Range(-14f, 14f), Random.Range(-8f, 8f));
            shard.breakT = 0f;
            shard.tf = go.transform;
            _shards.Add(shard);
        }

        // Das Zerbrechen berührt den See.
        if (lake != null && mirrorPlane != null)
        {
            var p = mirrorPlane.position;
            lake.AddRipple(new Vector3(p.x, lake.WaterY, p.z), 1.2f);
        }
    }

    private void BeginStreaming()
    {
        _streaming = true;

        // Das aktive (materialisierte) Wahl-Portal ist das Ziel.
        foreach (var portal in FindObjectsByType<ChoicePortal>(FindObjectsSortMode.None))
        {
            if (portal.gameObject.activeInHierarchy)
            {
                _streamTarget = portal.transform;
                break;
            }
        }

        foreach (var s in _shards)
        {
            if (s.tf == null) continue;
            s.streamStart = s.tf.position;
            s.streamT = 0f;
        }
    }

    private void AnimateShards()
    {
        Vector3 center = mirrorPlane != null ? mirrorPlane.position : transform.position;
        float waterY = lake != null ? lake.WaterY : 0f;

        for (int i = _shards.Count - 1; i >= 0; i--)
        {
            var s = _shards[i];
            if (s.tf == null) { _shards.RemoveAt(i); continue; }

            if (_streaming && _streamTarget != null)
            {
                // Sog ins Portal: beschleunigend, dabei schrumpfen.
                s.streamT += Time.deltaTime / Mathf.Max(0.1f, streamDuration);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s.streamT));
                var target = _streamTarget.position + Vector3.up * 1.6f;
                s.tf.position = Vector3.Lerp(s.streamStart, target, k);
                s.tf.localScale = Vector3.one * (1f - 0.9f * k);
                s.tf.Rotate(s.spin * (Time.deltaTime * (1f + 3f * k)));
                if (s.streamT >= 1f)
                {
                    Destroy(s.tf.gameObject);
                    _shards.RemoveAt(i);
                }
                continue;
            }

            // Orbitziel berechnen (langsame Kreisbahn + Schweben).
            s.orbitAngle += orbitSpeed * Time.deltaTime;
            float rad = s.orbitAngle * Mathf.Deg2Rad;
            var orbitPos = new Vector3(
                center.x + Mathf.Cos(rad) * s.orbitDist,
                waterY + s.height + Mathf.Sin(Time.time * 0.6f + s.bobPhase) * 0.15f,
                center.z + Mathf.Sin(rad) * s.orbitDist);

            if (s.breakT < 1f)
            {
                // Ablösen: von der Spiegelkante hinaus auf die Kreisbahn.
                s.breakT += Time.deltaTime / Mathf.Max(0.1f, breakoutDuration);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s.breakT));
                s.tf.position = Vector3.Lerp(s.breakStart, orbitPos, k);
            }
            else
            {
                s.tf.position = orbitPos;
            }

            s.tf.Rotate(s.spin * Time.deltaTime);
        }
    }

    /// <summary>Unregelmäßiges, flaches Scherben-Dreieckspaar (beidseitig sichtbar).</summary>
    private static Mesh BuildShardMesh(float size)
    {
        // Vier Punkte eines unregelmäßigen Splitters in der XY-Ebene.
        var p0 = new Vector3(0f, size * Random.Range(0.9f, 1.3f), 0f);
        var p1 = new Vector3(size * Random.Range(0.5f, 0.8f), size * Random.Range(-0.1f, 0.2f), 0f);
        var p2 = new Vector3(0f, -size * Random.Range(0.7f, 1.1f), 0f);
        var p3 = new Vector3(-size * Random.Range(0.4f, 0.7f), size * Random.Range(-0.2f, 0.1f), 0f);

        var mesh = new Mesh { name = "ShardMesh" };
        mesh.vertices = new[] { p0, p1, p2, p3 };
        // Vorder- und Rückseite, damit die Scherbe aus jedem Winkel sichtbar ist.
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Material BuildShardMaterial(Color rim)
    {
        if (_shardMaterial == null)
        {
            var baseMat = Resources.Load<Material>("MirrorShard");
            if (baseMat == null)
            {
                var shader = Shader.Find("Custom/MirrorGlass");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                baseMat = new Material(shader);
            }
            _shardMaterial = baseMat;
        }

        var mat = new Material(_shardMaterial);
        if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", rim);
        if (mat.HasProperty("_RimStrength")) mat.SetFloat("_RimStrength", 2.2f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.03f, 0.04f, 0.06f, 0.55f));
        return mat;
    }
}
