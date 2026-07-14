using System.Collections.Generic;
using Autohand;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Eigenständige Gameloop der Verwesungswelt: Drei Erinnerungsanker müssen in
/// Reihenfolge stabilisiert werden. Erst danach materialisiert sich der Ausgang.
/// </summary>
public sealed class DecayWorldEscapeController : MonoBehaviour
{
    public event System.Action FirstAnchorStabilized;
    public event System.Action AllAnchorsStabilized;

    private readonly List<Transform> anchorCores = new List<Transform>();
    private readonly List<Transform> orbitRoots = new List<Transform>();
    private readonly List<Light> anchorLights = new List<Light>();

    private Transform playerRoot;
    private Transform portalRoot;
    private Collider portalTrigger;
    private Material decayMaterial;
    private int nextAnchorIndex;
    private Color initialFogColor;
    private bool initialized;

    public void Initialize(
        Transform roomPlayer,
        Vector3 startPosition,
        Transform authoredExit,
        Vector3 worldCenter,
        Vector3 anchorAreaCenter,
        float worldRadius,
        Transform generatedParent,
        NatureCloudAssetLibrary assets)
    {
        if (initialized)
            return;

        initialized = true;
        playerRoot = roomPlayer;
        decayMaterial = assets != null && assets.decayMaterial != null
            ? assets.decayMaterial
            : CreateFallbackMaterial();
        initialFogColor = RenderSettings.fogColor;
        Shader.SetGlobalFloat("_DecayRestoration", 0f);

        transform.SetParent(generatedParent, false);
        transform.position = Vector3.zero;

        DisableAuthoredExit();

        Vector3 finalPosition = authoredExit != null
            ? authoredExit.position
            : worldCenter + new Vector3(worldRadius * 0.42f, 0f, worldRadius * 0.18f);
        finalPosition = PlaceOnGround(finalPosition, startPosition.y);

        // All three interactive anchors live on the authored middle island.
        // Their triangle layout keeps the order readable without rebuilding
        // the old brown bridge or forcing the player onto a generated path.
        Vector3[] islandOffsets =
        {
            new Vector3(-4.8f, 0f, -2.8f),
            new Vector3(4.6f, 0f, -2.4f),
            new Vector3(0.2f, 0f, 4.8f)
        };
        for (int index = 0; index < islandOffsets.Length; index++)
        {
            Vector3 position = anchorAreaCenter + islandOffsets[index];
            position = PlaceOnGround(position, startPosition.y);
            CreateMemoryAnchor(index, position);
        }

        CreateDecayMonoliths(worldCenter, Mathf.Min(worldRadius * 0.58f, 48f), startPosition.y);
        CreateExitPortal(finalPosition + Vector3.up * 1.7f);
    }

    private void Update()
    {
        if (!initialized)
            return;

        float progress = nextAnchorIndex / 3f;
        for (int index = 0; index < orbitRoots.Count; index++)
        {
            Transform orbitRoot = orbitRoots[index];
            if (orbitRoot != null)
                orbitRoot.Rotate(Vector3.up, (22f + index * 7f) * Time.deltaTime, Space.Self);

            if (index < anchorCores.Count && anchorCores[index] != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 1.8f + index * 1.7f) * 0.075f;
                anchorCores[index].localScale = new Vector3(0.48f, 1.75f, 0.48f) * pulse;
            }

            if (index < anchorLights.Count && anchorLights[index] != null)
            {
                bool restored = index < nextAnchorIndex;
                anchorLights[index].intensity = restored
                    ? 1.35f + Mathf.Sin(Time.time * 2.4f) * 0.18f
                    : 0.55f + Mathf.Sin(Time.time * 1.5f + index) * 0.12f;
            }
        }

        if (portalRoot != null && portalRoot.gameObject.activeSelf)
        {
            portalRoot.Rotate(Vector3.forward, 24f * Time.deltaTime, Space.Self);
            float portalPulse = 1f + Mathf.Sin(Time.time * 2.1f) * 0.055f;
            portalRoot.localScale = Vector3.one * portalPulse;
        }

        RenderSettings.fogColor = Color.Lerp(
            initialFogColor,
            new Color(0.27f, 0.34f, 0.3f),
            progress * 0.55f);
    }

    internal bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        AutoHandPlayer autoHandPlayer = other.GetComponentInParent<AutoHandPlayer>();
        if (autoHandPlayer != null && autoHandPlayer.gameObject.scene == gameObject.scene)
            return true;

        if (playerRoot != null && other.transform.root == playerRoot.root)
            return true;

        Rigidbody attachedBody = other.attachedRigidbody;
        if (playerRoot != null &&
            attachedBody != null &&
            attachedBody.transform.root == playerRoot.root)
        {
            return true;
        }

        return other.CompareTag("Player") || other.transform.root.CompareTag("Player");
    }

    public bool ValidateRuntimeSetup(out string report)
    {
        var problems = new List<string>();
        if (!initialized)
            problems.Add("Anker-Controller nicht initialisiert");
        if (playerRoot == null)
            problems.Add("Spielerreferenz fehlt");
        if (anchorCores.Count != 3)
            problems.Add($"Ankerzahl ist {anchorCores.Count} statt 3");
        if (anchorLights.Count != 3)
            problems.Add($"Ankerlichter sind {anchorLights.Count} statt 3");

        for (int index = 0; index < anchorCores.Count; index++)
        {
            Transform core = anchorCores[index];
            Transform anchorRoot = core != null ? core.parent : null;
            SphereCollider trigger = anchorRoot != null
                ? anchorRoot.GetComponent<SphereCollider>()
                : null;
            DecayAnchorTrigger behaviour = anchorRoot != null
                ? anchorRoot.GetComponent<DecayAnchorTrigger>()
                : null;

            if (core == null || trigger == null || !trigger.isTrigger || behaviour == null)
                problems.Add($"Anker {index + 1} hat keinen gültigen Hand-Trigger");
        }

        if (portalRoot == null || portalTrigger == null || !portalTrigger.isTrigger)
            problems.Add("Ausgangsportal-Trigger fehlt");
        else if (portalRoot.gameObject.activeSelf)
            problems.Add("Ausgangsportal ist vor dem dritten Anker aktiv");

        report = problems.Count == 0
            ? "3 Anker, Reihenfolge und Ausgangsportal sind bereit"
            : string.Join("; ", problems);
        return problems.Count == 0;
    }

    internal void TryRestoreAnchor(int anchorIndex)
    {
        if (anchorIndex < nextAnchorIndex)
            return;

        if (anchorIndex != nextAnchorIndex)
        {
            if (anchorIndex < anchorLights.Count && anchorLights[anchorIndex] != null)
                StartCoroutine(FlashWrongAnchor(anchorLights[anchorIndex]));
            return;
        }

        nextAnchorIndex++;
        if (nextAnchorIndex == 1)
            FirstAnchorStabilized?.Invoke();

        float progress = nextAnchorIndex / 3f;
        Shader.SetGlobalFloat("_DecayRestoration", progress);

        if (anchorIndex < anchorLights.Count && anchorLights[anchorIndex] != null)
        {
            anchorLights[anchorIndex].color = new Color(0.2f, 1f, 0.78f);
            anchorLights[anchorIndex].range = 8f;
        }

        if (anchorIndex < anchorCores.Count && anchorCores[anchorIndex] != null)
            CreateRestorationBurst(anchorCores[anchorIndex].position);

        if (nextAnchorIndex < 3)
        {
            if (nextAnchorIndex < anchorLights.Count && anchorLights[nextAnchorIndex] != null)
            {
                anchorLights[nextAnchorIndex].color = new Color(1f, 0.23f, 0.035f);
                anchorLights[nextAnchorIndex].range = 6.2f;
            }

        }
        else
        {
            ActivateExitPortal();
            AllAnchorsStabilized?.Invoke();
        }
    }

    internal void EnterRestoredPortal()
    {
        if (nextAnchorIndex < 3)
            return;

        const string destination = "Tree_Scene";
        if (Application.CanStreamedLevelBeLoaded(destination))
            SceneManager.LoadScene(destination);
        else
            Debug.LogError($"[Verwesung] Zielszene '{destination}' fehlt in den Build Settings.", this);
    }

    private void DisableAuthoredExit()
    {
        foreach (CaveWisdomTransitionTrigger caveTrigger in
                 Object.FindObjectsByType<CaveWisdomTransitionTrigger>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (caveTrigger.gameObject.scene != gameObject.scene)
                continue;

            caveTrigger.enabled = false;
            foreach (Collider triggerCollider in caveTrigger.GetComponents<Collider>())
                triggerCollider.enabled = false;
        }
    }

    private void CreateMemoryAnchor(int index, Vector3 position)
    {
        var root = new GameObject($"Memory Anchor {index + 1}");
        root.transform.SetParent(transform, true);
        root.transform.position = position;

        GameObject baseStone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseStone.name = "Cracked Memory Base";
        baseStone.transform.SetParent(root.transform, false);
        baseStone.transform.localPosition = Vector3.up * 0.16f;
        baseStone.transform.localScale = new Vector3(1.45f, 0.16f, 1.45f);
        ApplyDecayMaterial(baseStone.GetComponent<MeshRenderer>());

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
        core.name = "Memory Core";
        core.transform.SetParent(root.transform, false);
        core.transform.localPosition = Vector3.up * 1.25f;
        core.transform.localRotation = Quaternion.Euler(12f, 45f, -8f);
        core.transform.localScale = new Vector3(0.48f, 1.75f, 0.48f);
        DestroyGeneratedCollider(core.GetComponent<Collider>());
        ApplyDecayMaterial(core.GetComponent<MeshRenderer>());
        anchorCores.Add(core.transform);

        var orbit = new GameObject("Orbiting Fragments").transform;
        orbit.SetParent(root.transform, false);
        orbit.localPosition = Vector3.up * 1.2f;
        orbitRoots.Add(orbit);
        for (int fragmentIndex = 0; fragmentIndex < 5; fragmentIndex++)
        {
            float angle = fragmentIndex * Mathf.PI * 2f / 5f;
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.name = $"Memory Fragment {fragmentIndex + 1}";
            fragment.transform.SetParent(orbit, false);
            fragment.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * 1.35f,
                Mathf.Sin(angle * 2f) * 0.32f,
                Mathf.Sin(angle) * 1.35f);
            fragment.transform.localRotation = Quaternion.Euler(
                fragmentIndex * 27f,
                fragmentIndex * 43f,
                fragmentIndex * 19f);
            fragment.transform.localScale = new Vector3(0.18f, 0.44f, 0.14f);
            DestroyGeneratedCollider(fragment.GetComponent<Collider>());
            ApplyDecayMaterial(fragment.GetComponent<MeshRenderer>());
        }

        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.center = Vector3.up * 1.2f;
        trigger.radius = 0.95f;
        DecayAnchorTrigger triggerBehaviour = root.AddComponent<DecayAnchorTrigger>();
        triggerBehaviour.Initialize(this, index);

        Light anchorLight = root.AddComponent<Light>();
        anchorLight.type = LightType.Point;
        anchorLight.color = index == 0
            ? new Color(1f, 0.23f, 0.035f)
            : new Color(0.35f, 0.075f, 0.035f);
        anchorLight.intensity = index == 0 ? 0.82f : 0.38f;
        anchorLight.range = 5.5f;
        anchorLight.shadows = LightShadows.None;
        anchorLights.Add(anchorLight);
    }

    private void CreateDecayMonoliths(Vector3 center, float radius, float fallbackY)
    {
        var monolithRoot = new GameObject("Field of Broken Time").transform;
        monolithRoot.SetParent(transform, false);

        const int count = 14;
        for (int index = 0; index < count; index++)
        {
            float angle = index * Mathf.PI * 2f / count + Mathf.Sin(index * 2.7f) * 0.14f;
            float ringRadius = radius * Mathf.Lerp(0.62f, 1f, Mathf.Repeat(index * 0.37f, 1f));
            Vector3 position = center + new Vector3(
                Mathf.Cos(angle) * ringRadius,
                0f,
                Mathf.Sin(angle) * ringRadius);
            position = PlaceOnGround(position, fallbackY);

            GameObject monolith = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monolith.name = $"Decaying Time Monolith {index + 1:00}";
            monolith.transform.SetParent(monolithRoot, true);
            monolith.transform.position = position + Vector3.up * Mathf.Lerp(1.5f, 3.4f, Mathf.Repeat(index * 0.61f, 1f));
            monolith.transform.rotation = Quaternion.Euler(
                Mathf.Sin(index) * 12f,
                index * 37f,
                Mathf.Cos(index * 1.4f) * 10f);
            monolith.transform.localScale = new Vector3(
                Mathf.Lerp(0.45f, 1.1f, Mathf.Repeat(index * 0.43f, 1f)),
                Mathf.Lerp(3f, 6.8f, Mathf.Repeat(index * 0.61f, 1f)),
                Mathf.Lerp(0.4f, 0.9f, Mathf.Repeat(index * 0.29f, 1f)));
            DestroyGeneratedCollider(monolith.GetComponent<Collider>());
            ApplyDecayMaterial(monolith.GetComponent<MeshRenderer>());
        }
    }

    private void CreateExitPortal(Vector3 position)
    {
        portalRoot = new GameObject("Remembered Exit Portal").transform;
        portalRoot.SetParent(transform, true);
        portalRoot.position = position;

        const int segmentCount = 18;
        for (int index = 0; index < segmentCount; index++)
        {
            float angle = index * Mathf.PI * 2f / segmentCount;
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = $"Restored Portal Segment {index + 1:00}";
            segment.transform.SetParent(portalRoot, false);
            segment.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * 2.05f,
                Mathf.Sin(angle) * 2.5f,
                0f);
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            segment.transform.localScale = new Vector3(0.32f, 0.72f, 0.28f);
            DestroyGeneratedCollider(segment.GetComponent<Collider>());
            ApplyDecayMaterial(segment.GetComponent<MeshRenderer>());
        }

        BoxCollider trigger = portalRoot.gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3.4f, 4.3f, 2.2f);
        portalTrigger = trigger;
        DecayExitTrigger exitTrigger = portalRoot.gameObject.AddComponent<DecayExitTrigger>();
        exitTrigger.Initialize(this);
        portalRoot.gameObject.SetActive(false);
    }

    private void ActivateExitPortal()
    {
        if (portalRoot == null)
            return;

        portalRoot.gameObject.SetActive(true);
        if (portalTrigger != null)
            portalTrigger.enabled = true;
        CreateRestorationBurst(portalRoot.position);
    }

    private void CreateRestorationBurst(Vector3 position)
    {
        var burstObject = new GameObject("Memory Restoration Burst");
        burstObject.transform.SetParent(transform, true);
        burstObject.transform.position = position + Vector3.up * 1.1f;
        ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = 48;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.12f, 1f, 0.72f),
            new Color(1f, 0.35f, 0.04f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 42) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;
        particles.Play();
    }

    private System.Collections.IEnumerator FlashWrongAnchor(Light targetLight)
    {
        Color originalColor = targetLight.color;
        targetLight.color = new Color(1f, 0.02f, 0.02f);
        targetLight.intensity = 2.2f;
        yield return new WaitForSeconds(0.45f);
        if (targetLight != null)
        {
            targetLight.color = originalColor;
            targetLight.intensity = 0.65f;
        }
    }

    private Vector3 PlaceOnGround(Vector3 position, float fallbackY)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            position + Vector3.up * 45f,
            Vector3.down,
            100f,
            ~0,
            QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.normal.y < 0.58f)
                continue;
            string objectName = hit.collider.name;
            if (objectName.IndexOf("tree", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("leaf", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            position.y = hit.point.y;
            return position;
        }

        position.y = fallbackY;
        return position;
    }

    private void ApplyDecayMaterial(Renderer targetRenderer)
    {
        if (targetRenderer == null || decayMaterial == null)
            return;
        targetRenderer.sharedMaterial = decayMaterial;
        targetRenderer.shadowCastingMode = ShadowCastingMode.On;
        targetRenderer.receiveShadows = true;
    }

    private static Material CreateFallbackMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var material = new Material(shader) { name = "Fallback Decay Material" };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(0.11f, 0.065f, 0.035f));
        return material;
    }

    private void OnDestroy()
    {
        Shader.SetGlobalFloat("_DecayRestoration", 0f);
    }

    private static void DestroyGeneratedCollider(Collider target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
#if UNITY_EDITOR
        else
            DestroyImmediate(target);
#endif
    }
}

internal sealed class DecayAnchorTrigger : MonoBehaviour
{
    private DecayWorldEscapeController owner;
    private int anchorIndex;

    internal void Initialize(DecayWorldEscapeController controller, int index)
    {
        owner = controller;
        anchorIndex = index;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null && owner.IsPlayer(other))
            owner.TryRestoreAnchor(anchorIndex);
    }

    private void OnTriggerStay(Collider other)
    {
        // Fallback für Hände, die beim Aktivieren/Domain-Reload bereits im
        // Trigger liegen und deshalb kein neues OnTriggerEnter erzeugen.
        if (owner != null && owner.IsPlayer(other))
            owner.TryRestoreAnchor(anchorIndex);
    }
}

internal sealed class DecayExitTrigger : MonoBehaviour
{
    private DecayWorldEscapeController owner;

    internal void Initialize(DecayWorldEscapeController controller)
    {
        owner = controller;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null && owner.IsPlayer(other))
            owner.EnterRestoredPortal();
    }

    private void OnTriggerStay(Collider other)
    {
        if (owner != null && owner.IsPlayer(other))
            owner.EnterRestoredPortal();
    }
}
