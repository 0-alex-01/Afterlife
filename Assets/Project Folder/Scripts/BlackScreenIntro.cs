using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BlackScreenIntro : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Scene")]
    public string nextSceneName = "GameScene";

    [Header("Fade")]
    public Image blackScreen;
    public float fadeOutDuration = 2f;

    void Start()
    {
        if (blackScreen != null)
        {
            Color c = blackScreen.color;
            c.a = 1f;
            blackScreen.color = c;
        }

        StartCoroutine(IntroRoutine());
    }

    IEnumerator IntroRoutine()
    {
        audioSource.Play();

        yield return new WaitWhile(() => audioSource.isPlaying);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(nextSceneName);
        loadOperation.allowSceneActivation = false;

        yield return StartCoroutine(FadeOutBlack());

        loadOperation.allowSceneActivation = true;
    }

    IEnumerator FadeOutBlack()
    {
        float timer = 0f;

        while (timer < fadeOutDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeOutDuration);

            Color c = blackScreen.color;
            c.a = alpha;
            blackScreen.color = c;

            yield return null;
        }

        Color finalColor = blackScreen.color;
        finalColor.a = 0f;
        blackScreen.color = finalColor;
    }
}