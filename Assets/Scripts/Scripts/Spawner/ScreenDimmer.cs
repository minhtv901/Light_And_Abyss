using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenDimmer : MonoBehaviour
{
    public Image dimImage;

    private Coroutine dimRoutine;

    private void Awake()
    {
        if (dimImage == null)
        {
            dimImage = GetComponent<Image>();
        }

        SetAlpha(0f);
    }

    public void PlayDim(float maxAlpha, float fadeInTime, float holdTime, float fadeOutTime)
    {
        if (dimRoutine != null)
        {
            StopCoroutine(dimRoutine);
        }

        dimRoutine = StartCoroutine(DimRoutine(maxAlpha, fadeInTime, holdTime, fadeOutTime));
    }

    private IEnumerator DimRoutine(float maxAlpha, float fadeInTime, float holdTime, float fadeOutTime)
    {
        yield return FadeTo(maxAlpha, fadeInTime);

        if (holdTime > 0f)
        {
            yield return new WaitForSeconds(holdTime);
        }

        yield return FadeTo(0f, fadeOutTime);

        dimRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (dimImage == null) yield break;

        float startAlpha = dimImage.color.a;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = duration <= 0f ? 1f : timer / duration;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            SetAlpha(alpha);

            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (dimImage == null) return;

        Color c = dimImage.color;
        c.a = alpha;
        dimImage.color = c;
    }
}