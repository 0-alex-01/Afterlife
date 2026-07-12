using UnityEngine;
using UnityEngine.VFX;

public class PortalChaos : MonoBehaviour
{
    public VisualEffect vfx;
    public Transform player;

    public float maxDistance = 15f;

    void Update()
    {
        float distance = Vector3.Distance(player.position, transform.position);

        // 0 = weit weg, 1 = direkt davor
        float chaos = Mathf.InverseLerp(maxDistance, 0f, distance);

        vfx.SetFloat("Chaos", chaos);
    }
}