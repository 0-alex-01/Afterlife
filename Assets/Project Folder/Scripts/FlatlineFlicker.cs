using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FlatlineFlicker : MonoBehaviour
{
    public AudioSource audioSource;
    public Image blackOverlay;
    public float flatlineTime = 38f;

    private bool triggered;

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

        for (int i = 0; i < 8; i++)
        {
            SetAlpha(Random.Range(0.15f, 0.55f));
            yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));

            SetAlpha(0f);
            yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
        }
    }

    void SetAlpha(float a)
    {
        Color c = blackOverlay.color;
        c.a = a;
        blackOverlay.color = c;
    }
}