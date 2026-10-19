using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Displays a single toast notification.
/// Intended to be used as a prefab.
/// </summary>
public class ToastNotification : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float moveUpDistance = 50f;

    private CanvasGroup canvasGroup;
    private RectTransform rt;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        rt = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Initialize the notification.
    /// </summary>
    public void Initialize(string message, float displayDuration, Color? backgroundColor = null)
    {
        if (messageText != null)
            messageText.text = message;
        else
            Debug.LogWarning("[ToastNotification] messageText is not assigned.");

        if (backgroundColor.HasValue && backgroundImage != null)
            backgroundImage.color = backgroundColor.Value;
        else if (backgroundImage == null)
            Debug.LogWarning("[ToastNotification] backgroundImage is not assigned.");

        // Fade in
        canvasGroup.alpha = 0f;
        // Reset the position to avoid inheriting the prefab's position.
        if (rt != null)
            rt.anchoredPosition = Vector2.zero;
        StartCoroutine(FadeInAndOut(displayDuration));
    }

    private IEnumerator FadeInAndOut(float displayDuration)
    {
        // Fade in (0.3 seconds)
        float fadeInDuration = 0.3f;
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;

        // Time to remain on screen
        yield return new WaitForSeconds(displayDuration - fadeInDuration);

        // Fade out and move upward
        elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0, moveUpDistance);

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;

            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            yield return null;
        }

        Destroy(gameObject);
    }
}
