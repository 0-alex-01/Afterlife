using UnityEngine;

public class TestVertexColors : MonoBehaviour
{
    void Start()
    {
        var smr = GetComponent<SkinnedMeshRenderer>();

        if (smr == null)
        {
            Debug.Log("Kein SkinnedMeshRenderer gefunden");
            return;
        }

        Debug.Log("Vertex Colors: " + smr.sharedMesh.colors.Length);
    }
}
