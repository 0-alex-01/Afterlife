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

    private Camera reflectionCamera;
    private RenderTexture reflectionTexture;
    private int rtWidth, rtHeight;
    private MaterialPropertyBlock propBlock;

    private static readonly int ReflectionTexID = Shader.PropertyToID("_ReflectionTex");
    private static readonly int TintID = Shader.PropertyToID("_Tint");
    private static readonly int FlipFaceID = Shader.PropertyToID("_FlipFace");
    private static readonly HashSet<Camera> reflectionCameras = new HashSet<Camera>();

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        Cleanup();
    }

    private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (!isActiveAndEnabled) return;
        if (!Application.isPlaying && !showInEditor) return;

        // Rekursion vermeiden: nicht fuer unsere eigenen Reflexions-Kameras rendern
        if (reflectionCameras.Contains(cam)) return;
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

        EnsureResources();
        UpdateReflectionCamera(cam);

        // Spiegelbild jetzt in die RenderTexture rendern (umgekehrtes Culling fuer Spiegel)
        // RenderSingleCamera ist als veraltet markiert, ist aber fuer rekursives
        // Spiegel-Rendering der einzig gangbare Weg (SubmitRenderRequest ist waehrend
        // des Renderns nicht erlaubt).
        GL.invertCulling = true;
#pragma warning disable 618
        UniversalRenderPipeline.RenderSingleCamera(ctx, reflectionCamera);
#pragma warning restore 618
        GL.invertCulling = false;

        // Textur + Eigenschaften an den Spiegel binden
        Renderer r = GetComponent<Renderer>();
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        r.GetPropertyBlock(propBlock);
        propBlock.SetTexture(ReflectionTexID, reflectionTexture);
        propBlock.SetColor(TintID, tint);
        propBlock.SetFloat(FlipFaceID, flipNormal ? 1f : 0f);
        r.SetPropertyBlock(propBlock);
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
        reflectionCamera.clearFlags = viewer.clearFlags;
        reflectionCamera.backgroundColor = viewer.backgroundColor;
        reflectionCamera.fieldOfView = viewer.fieldOfView;
        reflectionCamera.aspect = viewer.aspect;
        reflectionCamera.nearClipPlane = viewer.nearClipPlane;
        reflectionCamera.farClipPlane = viewer.farClipPlane;
        reflectionCamera.orthographic = viewer.orthographic;
        reflectionCamera.orthographicSize = viewer.orthographicSize;
        reflectionCamera.cullingMask = (reflectLayers & viewer.cullingMask) | extraReflectionLayers;

        Vector3 pos = transform.position;
        Vector3 normal = flipNormal ? -transform.forward : transform.forward;

        // Normale immer zur Betrachterseite ausrichten -> verhindert "durch die Wand schauen"
        if (Vector3.Dot(normal, viewer.transform.position - pos) < 0f) normal = -normal;

        float d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
        Vector4 worldPlane = new Vector4(normal.x, normal.y, normal.z, d);
        Matrix4x4 reflection = Matrix4x4.identity;
        CalculateReflectionMatrix(ref reflection, worldPlane);

        reflectionCamera.worldToCameraMatrix = viewer.worldToCameraMatrix * reflection;
        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, pos, normal, 1.0f);
        reflectionCamera.projectionMatrix = viewer.CalculateObliqueMatrix(clipPlane);
        reflectionCamera.transform.position = reflection.MultiplyPoint(viewer.transform.position);
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

    private void OnDrawGizmosSelected()
    {
        Vector3 normal = flipNormal ? -transform.forward : transform.forward;
        Gizmos.color = Color.yellow;
        Vector3 start = transform.position;
        Vector3 end = start + normal * 1.5f;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.1f);
    }

    private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
    {
        Vector3 offsetPos = pos + normal * clipPlaneOffset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
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