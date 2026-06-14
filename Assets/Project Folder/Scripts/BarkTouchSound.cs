using UnityEngine;
using Autohand;

public class BarkTouchSound : MonoBehaviour
{
    public AudioClip touchClip;
    public float volume = 0.5f;
    public float minDelay = 0.2f;
    public LayerMask targetLayer;

    private Hand hand;
    private AudioSource audioSource;
    private float lastTouchTime;

    private void Start()
    {
        hand = GetComponent<Hand>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D sound

        if (hand != null)
        {
            hand.OnHandCollisionStart += OnHandTouch;
        }
    }

    private void OnHandTouch(Hand h, GameObject other)
    {
        if (((1 << other.layer) & targetLayer) != 0)
        {
            if (Time.time - lastTouchTime > minDelay)
            {
                audioSource.PlayOneShot(touchClip, volume);
                lastTouchTime = Time.time;
            }
        }
    }

    private void OnDestroy()
    {
        if (hand != null)
        {
            hand.OnHandCollisionStart -= OnHandTouch;
        }
    }
}
