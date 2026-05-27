using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroSceneLoader : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource introAudio;

    [Header("Black Screen")]
    public Image blackScreen;

    [Header("Next Scene")]
    public string nextSceneName = "Level1";

    [Header("Transition")]
    public float fadeDuration = 2f;

    private void Start()
    {
        StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        SetBlackAlpha(1f);

        introAudio.Play();

        yield return new WaitWhile(() => introAudio.isPlaying);

        AsyncOperation load = SceneManager.LoadSceneAsync(nextSceneName);
        load.allowSceneActivation = false;

        yield return StartCoroutine(FadeToClear());

        load.allowSceneActivation = true;
    }

    private IEnumerator FadeToClear()
    {
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            SetBlackAlpha(alpha);
            yield return null;
        }

        SetBlackAlpha(0f);
    }

    private void SetBlackAlpha(float alpha)
    {
        if (blackScreen == null)
            return;

        Color c = blackScreen.color;
        c.a = alpha;
        blackScreen.color = c;
    }
}