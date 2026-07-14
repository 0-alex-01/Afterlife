using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FlatlineFlicker : MonoBehaviour
{
    public AudioSource audioSource;
    public Image blackOverlay;
    public float flatlineTime = 38f;

    private bool triggered;

    private void OnEnable()
    {
        // The intro should remain visually stable in the build. Older versions
        // left a partially opaque frame behind when the scene initialized.
        SetAlpha(0f);
    }

    void Update()
    {
        if (triggered || audioSource == null)
            return;

        if (audioSource.time >= flatlineTime)
        {
            triggered = true;
            StartCoroutine(FlickerRoutine());
        }
    }

    public IEnumerator FlickerRoutine()
    {
        triggered = true;
        SetAlpha(0f);
        yield break;
    }

    void SetAlpha(float a)
    {
        if (blackOverlay == null)
            return;

        Color c = blackOverlay.color;
        c.a = a;
        blackOverlay.color = c;
    }
}
