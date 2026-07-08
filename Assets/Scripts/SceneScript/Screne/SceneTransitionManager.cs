using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 1f;

    private CanvasGroup fadeGroup;
    private bool isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateFadeCanvas();
    }

    private void Start()
    {
        StartCoroutine(FadeIn());
    }

    private void CreateFadeCanvas()
    {
        GameObject canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        fadeGroup = canvasObj.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 1f;
        fadeGroup.blocksRaycasts = true;

        GameObject imageObj = new GameObject("BlackFadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        Image image = imageObj.AddComponent<Image>();
        image.color = Color.black;

        RectTransform rect = imageObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public void LoadSceneWithFade(string sceneName)
    {
        if (isTransitioning) return;
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isTransitioning = true;

        yield return FadeOut();

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        while (!operation.isDone)
        {
            yield return null;
        }

        yield return null;
        yield return FadeIn();

        isTransitioning = false;
    }

    private IEnumerator FadeOut()
    {
        fadeGroup.blocksRaycasts = true;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Clamp01(timer / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = 1f;
    }

    private IEnumerator FadeIn()
    {
        fadeGroup.blocksRaycasts = true;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            fadeGroup.alpha = 1f - Mathf.Clamp01(timer / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
    }
}
