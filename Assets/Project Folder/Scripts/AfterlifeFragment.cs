using UnityEngine;
using System.Collections;

public class AfterlifeFragment : MonoBehaviour
{
    public float detectRadius = 1.5f;
    public float dissolveTime = 0.8f;
    public ParticleSystem shatterEffect;
    public AudioClip shatterSFX;
    [Range(0f, 1f)] public float shatterVolume = 0.18f;
    
    private bool isShattered = false;
    private Transform player;

    private void Start()
    {
        if (Camera.main != null) player = Camera.main.transform;
    }

    private void Update()
    {
        if (isShattered || player == null) return;

        if (Vector3.Distance(transform.position, player.position) < detectRadius)
        {
            Shatter();
        }
    }

    public void Shatter()
    {
        if (isShattered) return;
        isShattered = true;
        StartCoroutine(ShatterRoutine());
    }

    private IEnumerator ShatterRoutine()
    {
        if (shatterEffect != null)
        {
            var ps = Instantiate(shatterEffect, transform.position, Quaternion.identity);
            Destroy(ps.gameObject, ps.main.duration + 1f);
        }

        if (shatterSFX != null)
        {
            AudioSource.PlayClipAtPoint(shatterSFX, transform.position, shatterVolume);
        }

        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < dissolveTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dissolveTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
