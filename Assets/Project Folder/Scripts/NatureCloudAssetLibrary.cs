using UnityEngine;

/// <summary>
/// Hält direkte Referenzen auf die Meshes aus TrueClouds, damit sie auch in einem
/// Player-Build über Resources verfügbar sind, ohne die Demo-Szenen zu laden.
/// </summary>
public sealed class NatureCloudAssetLibrary : ScriptableObject
{
    public Mesh[] cloudMeshes;
    public Material cloudMaterial;
    public Material decayMaterial;
    public GameObject autoHandPlayerPrefab;

    [Header("Nature room voice lines")]
    public AudioClip harmonyArrivalLine;
    public AudioClip harmonyCaveLine;
    public AudioClip chaosArrivalLine;
    public AudioClip chaosMovingPortalLine;
    public AudioClip chaosRetryLine;
    public AudioClip decayArrivalLine;
    public AudioClip decayFirstAnchorLine;
}
