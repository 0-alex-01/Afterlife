using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Echter Planar-Spiegel fuer URP (Unity 6).
// Reflektierende Seite = +Z (transform.forward). Rueckseite ist weiss (Shader).
// Funktioniert in Edit- und Play-Mode, unabhaengig von der Position.
[RequireComponent(typeof(Renderer))]
[ExecuteAlways]
public class PlanarMirror : MonoBehaviour
{
    [Tooltip("Welche Layer im Spiegel reflektiert werden")]
    public LayerMask reflectLayers = ~0;

    [Tooltip("Zusaetzliche Layer, die NUR im Spiegel sichtbar sind (z.B. der morphende Seelen-Zwilling), auch wenn sie von der Hauptkamera ausgeblendet werden")]
    public LayerMask extraReflectionLayers = 0;

    [Tooltip("Aufloesung des Spiegelbilds relativ zum Bildschirm (kleiner = schneller)")]
    [Range(0.1f, 1f)] public float resolutionMultiplier = 0.75f;

    [Tooltip("Kleiner Versatz der Spiegelebene gegen Flimmern an der Kante")]
    public float clipPlaneOffset = 0.05f;

    [Tooltip("Reflektierende Seite umdrehen (auch die weisse Rueckseite wird mitgedreht)")]
    public bool flipNormal = false;

    [Tooltip("Faerbung/Abdunklung des Spiegelbilds")]
    public Color tint = Color.white;

    [Tooltip("Reflektion auch im Editor (Scene View) anzeigen?")]
    public bool showInEditor = true;

    [Header("Debug")]
    [Tooltip("Loggt die gewaehlte Viewer-Kamera und ob SubmitRenderRequest unterstuetzt wird.")]
    public bool debugLog = false;
    [Tooltip("Im Play-Mode: speichert die Reflexionstextur als PNG (persistentDataPath).")]
    public KeyCode dumpKey = KeyCode.F8;
    [Tooltip("Zeigt die Reflexionstextur LIVE in der Bildschirmecke (nur Play-Mode). Bestes Test-Werkzeug: zeigt direkt, was die Spiegelkamera sieht - unabhaengig von Raumlicht und Spiegel-Oberflaeche.")]
    public bool debugOverlay = false;

    private Camera _lastLoggedViewer;
    private Camera reflectionCamera;
    private RenderTexture reflectionTexture;
    private int rtWidth, rtHeight;
    private MaterialPropertyBlock propBlock;

    private static readonly int ReflectionTexID = Shader.PropertyToID("_ReflectionTex");
    private static readonly int TintID = Shader.PropertyToID("_Tint");
    private static readonly int FlipFaceID = Shader.PropertyToID("_FlipFace");
    private static readonly int MirrorVPID = Shader.PropertyToID("_MirrorVP");
    private static readonly HashSet<Camera> reflectionCameras = new HashSet<Camera>();

    private void OnDisable()
    {
        // TEMP-DBG: Wer schaltet den Spiegel ab? SetActive/enabled=false ruft OnDisable
        // synchron auf -> der Stack-Trace zeigt den Verursacher. Parent-Kette zeigt,
        // ob die Komponente selbst oder ein Eltern-Objekt deaktiviert wurde.
        if (Application.isPlaying)
        {
            var chain = new System.Text.StringBuilder();
            for (var t = transform; t != null; t = t.parent)
                chain.Append($"{t.name}(activeSelf={t.gameObject.activeSelf}) <- ");
            Debug.Log($"[MirrorDBG] OnDisable t={Time.unscaledTime:F2} enabled={enabled} chain={chain}\nSTACK:\n{new System.Diagnostics.StackTrace(true)}", this);
        }
        Cleanup();
    }

    // TEMP-DBG: Gegenprobe, falls der Spiegel spaeter wieder aktiviert wird.
    private void OnEnable()
    {
        if (Application.isPlaying)
            Debug.Log($"[MirrorDBG] OnEnable t={Time.unscaledTime:F2}", this);
    }

    // Unity 6 / URP 17: Die alte UniversalRenderPipeline.RenderSingleCamera ist veraltet und
    // liefert im Play-Mode ohne Fehlermeldung kein Bild mehr (der Spiegel bleibt weiss).
    // Stattdessen rendern wir das Spiegelbild in LateUpdate ueber RenderPipeline.SubmitRenderRequest.
    // Das laeuft AUSSERHALB der SRP-Render-Schleife und vermeidet so den
    // "Recursive rendering is not supported"-Fehler.
    private float _dbgNextLog;
    private string _dbgViewerLog;
    private void LateUpdate()
    {
        if (Application.isPlaying && Time.unscaledTime >= _dbgNextLog)
        {
            _dbgNextLog = Time.unscaledTime + 1f;
            Debug.Log($"[MirrorDBG] LateUpdate fired t={Time.unscaledTime:F1} isActEn={isActiveAndEnabled} rt={(reflectionTexture!=null?"OK":"NULL")} camMain={(Camera.main?Camera.main.name:"NULL")}", this);
        }
        if (Application.isPlaying && Input.GetKeyDown(dumpKey)) DumpReflectionTexture();
        RenderReflection();
    }

    private void RenderReflection()
    {
        if (!isActiveAndEnabled) return;
        if (!Application.isPlaying && !showInEditor) return;

        Camera viewer = GetViewerCamera();
        if (viewer == null)
        {
            if (Application.isPlaying) Debug.LogWarning("[MirrorDBG] RenderReflection: viewer NULL -> skipped", this);
            return;
        }
        if (Application.isPlaying && _dbgViewerLog != (viewer ? viewer.name : "NULL"))
        {
            _dbgViewerLog = viewer ? viewer.name : "NULL";
            Debug.Log($"[MirrorDBG] RenderReflection viewer -> '{_dbgViewerLog}'", this);
        }

        if (viewer != _lastLoggedViewer)
        {
            _lastLoggedViewer = viewer;
            if (debugLog) Debug.Log($"[PlanarMirror] Viewer = '{viewer.name}' (enabled={viewer.enabled}, mask=0x{viewer.cullingMask:X})", this);
        }

        EnsureResources();
        UpdateReflectionCamera(viewer);

        // Spiegelbild in die RenderTexture rendern (umgekehrtes Culling fuer den Spiegel).
        GL.invertCulling = true;
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = reflectionTexture };
        bool supported = RenderPipeline.SupportsRenderRequest(reflectionCamera, request);
        if (supported)
        {
            RenderPipeline.SubmitRenderRequest(reflectionCamera, request);
        }
        else if (debugLog)
        {
            Debug.LogError("[PlanarMirror] SubmitRenderRequest wird NICHT unterstuetzt (aktive Pipeline ist evtl. nicht URP).", this);
        }
        GL.invertCulling = false;

        // Textur + Eigenschaften an den Spiegel binden.
        Renderer r = GetComponent<Renderer>();
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        r.GetPropertyBlock(propBlock);
        propBlock.SetTexture(ReflectionTexID, reflectionTexture);
        propBlock.SetColor(TintID, tint);
        propBlock.SetFloat(FlipFaceID, flipNormal ? 1f : 0f);
        // VP-Matrix der Spiegelkamera für das World-Space-Sampling im Shader.
        // renderIntoTexture=false: URP schreibt die RT bereits in Top-Down-Konvention —
        // mit true wäre das Spiegelbild vertikal gespiegelt (Bild stand auf dem Kopf).
        Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, false);
        propBlock.SetMatrix(MirrorVPID, gpuProj * reflectionCamera.worldToCameraMatrix);
        r.SetPropertyBlock(propBlock);
    }

    private Camera GetViewerCamera()
    {
        if (Application.isPlaying)
        {
            var main = Camera.main;
            if (main != null && main.enabled && main.gameObject.activeInHierarchy) return main;
            
            var all = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in all)
            {
                if (c.enabled && c.gameObject.activeInHierarchy && c != reflectionCamera)
                    return c;
            }
        }
#if UNITY_EDITOR
        var sv = UnityEditor.SceneView.lastActiveSceneView;
        if (sv != null) return sv.camera;
#endif
        return null;
    }

    private void EnsureResources()
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(Screen.width * resolutionMultiplier));
        int h = Mathf.Max(8, Mathf.RoundToInt(Screen.height * resolutionMultiplier));

        if (reflectionTexture == null || rtWidth != w || rtHeight != h)
        {
            if (reflectionTexture != null) reflectionTexture.Release();
            reflectionTexture = new RenderTexture(w, h, 24) { name = "PlanarMirror RT", antiAliasing = 1 };
            rtWidth = w;
            rtHeight = h;
        }

        if (reflectionCamera == null)
        {
            GameObject existing = null;
            foreach (Transform t in transform)
                if (t.name == "PlanarMirror Reflection Cam") { existing = t.gameObject; break; }

            if (existing == null)
            {
                existing = new GameObject("PlanarMirror Reflection Cam");
                existing.hideFlags = HideFlags.HideAndDontSave;
                existing.transform.SetParent(transform, false);
            }

            reflectionCamera = existing.GetComponent<Camera>();
            if (reflectionCamera == null) reflectionCamera = existing.AddComponent<Camera>();
            reflectionCamera.enabled = false; // wird manuell via RenderSingleCamera gerendert
            reflectionCameras.Add(reflectionCamera);
        }
        reflectionCamera.targetTexture = reflectionTexture;
    }

    private void UpdateReflectionCamera(Camera viewer)
    {
        // Always clear to black to avoid skybox/background leakage in the reflection.
        reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
        reflectionCamera.backgroundColor = Color.black;
        
        reflectionCamera.fieldOfView = viewer.fieldOfView;
        reflectionCamera.aspect = viewer.aspect;
        reflectionCamera.nearClipPlane = viewer.nearClipPlane;
        reflectionCamera.farClipPlane = viewer.farClipPlane;
        reflectionCamera.orthographic = viewer.orthographic;
        reflectionCamera.orthographicSize = viewer.orthographicSize;
        
        // Ensure the mirror sees what the viewer sees, plus the special SoulMirror layer.
        reflectionCamera.cullingMask = (reflectLayers & viewer.cullingMask) | extraReflectionLayers;

        Vector3 pos = transform.position;
        Vector3 normal = flipNormal ? -transform.forward : transform.forward;

        // Normale immer zur Betrachterseite ausrichten -> verhindert "durch die Wand schauen"
        if (Vector3.Dot(normal, viewer.transform.position - pos) < 0f) normal = -normal;

        float d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
        Vector4 worldPlane = new Vector4(normal.x, normal.y, normal.z, d);
        Matrix4x4 reflection = Matrix4x4.identity;
        CalculateReflectionMatrix(ref reflection, worldPlane);

        // Set the transform (position AND mirrored orientation) FIRST
        reflectionCamera.transform.position = reflection.MultiplyPoint(viewer.transform.position);
        Vector3 reflectedForward = reflection.MultiplyVector(viewer.transform.forward);
        Vector3 reflectedUp = reflection.MultiplyVector(viewer.transform.up);
        if (reflectedForward.sqrMagnitude > 1e-6f && reflectedUp.sqrMagnitude > 1e-6f)
        {
            reflectionCamera.transform.rotation = Quaternion.LookRotation(reflectedForward, reflectedUp);
        }

        reflectionCamera.worldToCameraMatrix = viewer.worldToCameraMatrix * reflection;
        
        // Calculate oblique projection matrix for clipping
        // We use a slightly more robust calculation for the side sign
        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, pos, normal, 1.0f);
        reflectionCamera.projectionMatrix = viewer.CalculateObliqueMatrix(clipPlane);
    }

    private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
    {
        // Add a small offset to the clip plane to avoid precision artifacts at the mirror edge
        Vector3 offsetPos = pos + normal * clipPlaneOffset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }

    // Speichert die aktuelle Reflexionstextur als PNG, um zu pruefen, ob ueberhaupt etwas
    // gerendert wird (To-Do #3). Schwarz = nichts gerendert; Raum sichtbar = Renderpfad ok.
    private void DumpReflectionTexture()
    {
        if (reflectionTexture == null) { Debug.LogWarning("[PlanarMirror] Keine Reflexionstextur vorhanden.", this); return; }
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = reflectionTexture;
        var tex = new Texture2D(reflectionTexture.width, reflectionTexture.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, reflectionTexture.width, reflectionTexture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        string path = System.IO.Path.Combine(Application.persistentDataPath, "PlanarMirror_RT.png");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log($"[PlanarMirror] RT gespeichert: {path} ({reflectionTexture.width}x{reflectionTexture.height})", this);
    }

    // Live-Vorschau der Reflexionstextur in der Bildschirmecke. Damit kannst du beim Fliegen
    // sofort sehen, was die Spiegelkamera rendert - ohne auf das Raumlicht oder die
    // Spiegel-Oberflaeche angewiesen zu sein.
    private void OnGUI()
    {
        if (!debugOverlay || !Application.isPlaying || reflectionTexture == null) return;
        const float w = 320f;
        float h = w * (reflectionTexture.height / Mathf.Max(1f, (float)reflectionTexture.width));
        var rect = new Rect(Screen.width - w - 12f, 12f, w, h);
        GUI.color = Color.white;
        GUI.DrawTexture(rect, reflectionTexture, ScaleMode.ScaleToFit, false);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x, rect.yMax + 2f, w, 20f),
            $"Mirror RT {reflectionTexture.width}x{reflectionTexture.height}  Viewer: {(_lastLoggedViewer ? _lastLoggedViewer.name : "?")}");
    }

    private void Cleanup()
    {
        if (reflectionCamera != null)
        {
            reflectionCameras.Remove(reflectionCamera);
            if (Application.isPlaying) Destroy(reflectionCamera.gameObject);
            else DestroyImmediate(reflectionCamera.gameObject);
            reflectionCamera = null;
        }
        if (reflectionTexture != null) { reflectionTexture.Release(); reflectionTexture = null; }
    }

    private static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
    {
        reflectionMat.m00 = 1F - 2F * plane[0] * plane[0];
        reflectionMat.m01 = -2F * plane[0] * plane[1];
        reflectionMat.m02 = -2F * plane[0] * plane[2];
        reflectionMat.m03 = -2F * plane[3] * plane[0];
        reflectionMat.m10 = -2F * plane[1] * plane[0];
        reflectionMat.m11 = 1F - 2F * plane[1] * plane[1];
        reflectionMat.m12 = -2F * plane[1] * plane[2];
        reflectionMat.m13 = -2F * plane[3] * plane[1];
        reflectionMat.m20 = -2F * plane[2] * plane[0];
        reflectionMat.m21 = -2F * plane[2] * plane[1];
        reflectionMat.m22 = 1F - 2F * plane[2] * plane[2];
        reflectionMat.m23 = -2F * plane[3] * plane[2];
        reflectionMat.m30 = 0F;
        reflectionMat.m31 = 0F;
        reflectionMat.m32 = 0F;
        reflectionMat.m33 = 1F;
    }
}