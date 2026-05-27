using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Echter Planar-Spiegel für URP.
// Auf ein flaches Quad legen (mit dem PlanarMirror-Material). Erzeugt zur Laufzeit
// eine gespiegelte Kamera + RenderTexture und blendet das Spiegelbild ein.
[RequireComponent(typeof(Renderer))]
public class PlanarMirror : MonoBehaviour
{
    [Tooltip("Welche Layer im Spiegel reflektiert werden")]
    public LayerMask reflectLayers = ~0;

    [Tooltip("Auflösung des Spiegelbilds relativ zum Bildschirm (kleiner = schneller)")]
    [Range(0.1f, 1f)] public float resolutionMultiplier = 0.75f;

    [Tooltip("Kleiner Versatz der Spiegelebene gegen Flimmern an der Kante")]
    public float clipPlaneOffset = 0.05f;

    [Tooltip("Reflektierende Seite umdrehen, falls der Spiegel die falsche Seite zeigt")]
    public bool flipNormal = false;

    [Tooltip("Färbung/Abdunklung des Spiegelbilds (z.B. leicht grau für alten Spiegel)")]
    public Color tint = Color.white;

    private Camera reflectionCamera;
    private RenderTexture reflectionTexture;
    private Camera viewerCamera;
    private Material mirrorMaterial;
    private int rtWidth, rtHeight;

    private static readonly int ReflectionTexID = Shader.PropertyToID("_ReflectionTex");
    private static readonly int TintID = Shader.PropertyToID("_Tint");
    private static readonly HashSet<Camera> reflectionCameras = new HashSet<Camera>();

    private void OnEnable()
    {
        viewerCamera = Camera.main;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
        Cleanup();
    }

    private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        // Die Reflexions-Kamera selbst: nur Matrizen aktualisieren und Winding umdrehen
        if (cam == reflectionCamera)
        {
            if (viewerCamera != null)
            {
                UpdateReflectionCamera(viewerCamera);
                GL.invertCulling = true;
            }
            return;
        }

        // Reflexions-Kameras anderer Spiegel ignorieren
        if (reflectionCameras.Contains(cam))
            return;

        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView)
            return;

        viewerCamera = cam;
        EnsureResources(cam);
    }

    private void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam == reflectionCamera)
            GL.invertCulling = false;
    }

    private void EnsureResources(Camera viewer)
    {
        if (mirrorMaterial == null)
            mirrorMaterial = GetComponent<Renderer>().material;

        int w = Mathf.Max(8, Mathf.RoundToInt(Screen.width * resolutionMultiplier));
        int h = Mathf.Max(8, Mathf.RoundToInt(Screen.height * resolutionMultiplier));

        if (reflectionTexture == null || rtWidth != w || rtHeight != h)
        {
            if (reflectionTexture != null)
                reflectionTexture.Release();

            reflectionTexture = new RenderTexture(w, h, 24)
            {
                name = "PlanarMirror RT",
                antiAliasing = 1
            };
            rtWidth = w;
            rtHeight = h;
        }

        if (reflectionCamera == null)
        {
            // Als verstecktes Kind-Objekt (ohne DontSave!), damit es beim Stoppen
            // automatisch mit der Szene aufgeräumt wird.
            var go = new GameObject("PlanarMirror Reflection Cam");
            go.hideFlags = HideFlags.HideInHierarchy;
            go.transform.SetParent(transform, false);
            reflectionCamera = go.AddComponent<Camera>();
            reflectionCamera.enabled = true;            // wird automatisch von URP gerendert
            reflectionCamera.depth = viewer.depth - 10; // vor der Hauptkamera rendern
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
        reflectionCamera.cullingMask = reflectLayers & viewer.cullingMask;
        reflectionCamera.targetTexture = reflectionTexture;

        Vector3 pos = transform.position;
        Vector3 normal = flipNormal ? -transform.forward : transform.forward;

        // Spiegelebene in Weltkoordinaten
        float d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
        Vector4 worldPlane = new Vector4(normal.x, normal.y, normal.z, d);

        Matrix4x4 reflection = Matrix4x4.identity;
        CalculateReflectionMatrix(ref reflection, worldPlane);

        reflectionCamera.worldToCameraMatrix = viewer.worldToCameraMatrix * reflection;

        // Schräge Near-Clip-Ebene, damit nichts hinter dem Spiegel gespiegelt wird
        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, pos, normal, 1.0f);
        reflectionCamera.projectionMatrix = viewer.CalculateObliqueMatrix(clipPlane);

        reflectionCamera.transform.position = reflection.MultiplyPoint(viewer.transform.position);

        mirrorMaterial.SetTexture(ReflectionTexID, reflectionTexture);
        mirrorMaterial.SetColor(TintID, tint);
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

        if (reflectionTexture != null)
        {
            reflectionTexture.Release();
            reflectionTexture = null;
        }
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
