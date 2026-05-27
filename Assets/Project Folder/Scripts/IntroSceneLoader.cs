using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroSceneLoader : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource introAudio;

    [Header("Fade Image - für Fade-In am Anfang und Fade-Out am Ende")]
    public Image fadeImage;

    [Header("Flatline Flicker (optional) - flackert beim Herzstillstand")]
    public FlatlineFlicker flatlineFlicker;

    [Header("Next Scene")]
    public string nextSceneName = "Environment_Free";

    [Header("Transition")]
    public float fadeInDuration = 1.5f;
    public float fadeDuration = 2f;

    [Header("Herzschlag-Verlangsamung")]
    public bool slowDownHeartbeat = true;
    public float startPitch = 1f;            // Tempo am Anfang (1 = normal)
    public float endPitch = 0.5f;            // Tempo am Ende (0.5 = halb so schnell, tiefer)
    public float slowdownCurve = 1.5f;       // >1 = bleibt länger schnell, fällt gegen Ende stärker ab
    public float audioLengthFallback = 42f;  // Clip-Länge in Sek., falls nicht auslesbar

    private void Start()
    {
        StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        // Schwarz starten und sanft einblenden
        if (fadeImage != null)
        {
            SetFadeAlpha(1f);
            yield return StartCoroutine(Fade(1f, 0f, fadeInDuration));
        }

        if (introAudio != null)
        {
            introAudio.pitch = slowDownHeartbeat ? startPitch : 1f;
            introAudio.Play();

            float length = introAudio.clip != null ? introAudio.clip.length : audioLengthFallback;

            // warten, bis der Herzschlag vorbei ist - dabei das Tempo langsam herunterregeln
            while (introAudio.isPlaying)
            {
                if (slowDownHeartbeat && length > 0f)
                {
                    float progress = Mathf.Clamp01(introAudio.time / length);
                    float eased = Mathf.Pow(progress, Mathf.Max(0.01f, slowdownCurve));
                    introAudio.pitch = Mathf.Lerp(startPitch, endPitch, eased);
                }
                yield return null;
            }
        }

        // Moment des Todes: kurzer Bildschirm-Flicker
        if (flatlineFlicker != null)
            yield return StartCoroutine(flatlineFlicker.FlickerRoutine());

        // auf Schwarz ausblenden und nächste Szene laden
        if (fadeImage != null)
            yield return StartCoroutine(Fade(0f, 1f, fadeDuration));

        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeImage == null)
            yield break;

        if (duration <= 0f)
        {
            SetFadeAlpha(to);
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            SetFadeAlpha(Mathf.Lerp(from, to, timer / duration));
            yield return null;
        }

        SetFadeAlpha(to);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
            return;

        Color c = fadeImage.color;
        c.a = alpha;
        fadeImage.color = c;
    }
}
