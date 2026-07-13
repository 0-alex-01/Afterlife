using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroSceneLoader : MonoBehaviour
{
    [Header("Start Menu")]
    [Tooltip("Zeigt vor der EKG-Sequenz das Afterlife-Startmenue an.")]
    public bool showStartMenu = true;

    [Header("Audio")]
    public AudioSource introAudio;

    [Tooltip("Separate Quelle für den durchgehenden EKG-Flatline-Ton.")]
    public AudioSource flatlineAudio;

    [Tooltip("Voice-Line, die kurz nach dem Herzstillstand abgespielt wird.")]
    public AudioSource voiceAudio;

    [Header("Flatline-Timing")]
    [Tooltip("Clip-Zeit des Herzschlags, an der die Flatline beginnt. Vorher waren es etwa 42 Sekunden.")]
    public float flatlineTime = 32f;

    [Tooltip("Startposition im Flatline-Clip, damit direkt der Dauerton beginnt und nicht dessen lange Einleitung.")]
    public float flatlineAudioStartTime = 42f;

    [Tooltip("Zeit vom Beginn der Flatline bis zum Start der Voice-Line. Der Bildschirm-Flicker laeuft innerhalb dieser Zeit.")]
    public float voiceDelayAfterFlatline = 2.5f;

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

    private IEnumerator Start()
    {
        // Sicherstellen, dass Play-On-Awake oder ein vorheriger Testlauf nicht unter dem Menue weiterlaeuft.
        StopIntroAudio();

        if (showStartMenu)
        {
            yield return AfterlifeStartMenu.WaitForStart();
            ResetEcgTrace();
        }

        yield return IntroRoutine();
    }

    private void StopIntroAudio()
    {
        if (introAudio != null)
            introAudio.Stop();
        if (flatlineAudio != null)
            flatlineAudio.Stop();
        if (voiceAudio != null)
            voiceAudio.Stop();
    }

    private static void ResetEcgTrace()
    {
        ECGLineUI[] traces = FindObjectsByType<ECGLineUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ECGLineUI trace in traces)
        {
            bool wasEnabled = trace.enabled;
            trace.enabled = false;
            trace.enabled = wasEnabled;
        }
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

            // Der Originalclip läuft deutlich länger als der eigentliche Herzschlag. Wir beenden
            // ihn bewusst am gemeinsamen Flatline-Zeitpunkt, statt auf das stille Clip-Ende zu warten.
            float heartbeatEnd = flatlineTime > 0f
                ? Mathf.Min(flatlineTime, length)
                : length;

            // Warten bis zur Flatline und dabei das Tempo langsam herunterregeln.
            while (introAudio.isPlaying && introAudio.time < heartbeatEnd)
            {
                if (slowDownHeartbeat && heartbeatEnd > 0f)
                {
                    float progress = Mathf.Clamp01(introAudio.time / heartbeatEnd);
                    float eased = Mathf.Pow(progress, Mathf.Max(0.01f, slowdownCurve));
                    introAudio.pitch = Mathf.Lerp(startPitch, endPitch, eased);
                }
                yield return null;
            }

            introAudio.Stop();
            introAudio.pitch = 1f;
        }

        // Moment des Todes: Flatline-Ton und kurzer Bildschirm-Flicker.
        float flatlineStartedAt = Time.unscaledTime;
        if (flatlineAudio != null && flatlineAudio.clip != null)
        {
            flatlineAudio.time = Mathf.Clamp(
                flatlineAudioStartTime,
                0f,
                Mathf.Max(0f, flatlineAudio.clip.length - 0.05f));
            flatlineAudio.Play();
        }

        if (flatlineFlicker != null)
            yield return StartCoroutine(flatlineFlicker.FlickerRoutine());

        // Die Erzählerstimme beginnt erst, nachdem die gerade Linie lange genug wirken konnte.
        // Der Flicker zählt bereits zu dieser Wartezeit und verlängert sie nicht zusätzlich.
        float flatlineElapsed = Time.unscaledTime - flatlineStartedAt;
        float remainingVoiceDelay = voiceDelayAfterFlatline - flatlineElapsed;
        if (remainingVoiceDelay > 0f)
            yield return new WaitForSecondsRealtime(remainingVoiceDelay);

        if (voiceAudio != null && voiceAudio.clip != null)
        {
            voiceAudio.Play();
            while (voiceAudio.isPlaying)
                yield return null;
        }

        // auf Schwarz ausblenden und nächste Szene laden
        if (fadeImage != null)
            yield return StartCoroutine(Fade(0f, 1f, fadeDuration));

        if (flatlineAudio != null)
            flatlineAudio.Stop();

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
