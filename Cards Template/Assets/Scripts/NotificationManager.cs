using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Manages the notification system using the singleton pattern.
/// Spawns and manages toast notification prefabs.
/// </summary>
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    [SerializeField] private GameObject toastNotificationPrefab;
    [SerializeField] private Transform notificationContainer; // Notification position within the Canvas
    [SerializeField] private int maxNotificationsOnScreen = 3;
    [SerializeField] private float verticalSpacing = 10f;
    [Header("Color Set")]
    [Tooltip("Background color for success notifications (HTML hex, e.g. #2AA24A)")]
    [SerializeField] private string successColorHex = "#287a3e";

    private List<GameObject> activeNotifications = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // If no container is assigned, find a Canvas and create a notification container
        if (notificationContainer == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform existing = canvas.transform.Find("NotificationContainer");
                if (existing != null)
                {
                    notificationContainer = existing;
                }
                else
                {
                    GameObject go = new GameObject("NotificationContainer", typeof(RectTransform));
                    go.transform.SetParent(canvas.transform, false);
                    RectTransform r = go.GetComponent<RectTransform>();
                    r.anchorMin = new Vector2(0.5f, 1f);
                    r.anchorMax = new Vector2(0.5f, 1f);
                    r.pivot = new Vector2(0.5f, 1f);
                    r.anchoredPosition = new Vector2(0f, -20f);

                    UnityEngine.UI.VerticalLayoutGroup v = go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                    v.spacing = verticalSpacing;
                    v.childAlignment = TextAnchor.UpperCenter;
                    v.childControlHeight = true;
                    v.childControlWidth = false;

                    UnityEngine.UI.ContentSizeFitter csf = go.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                    csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

                    notificationContainer = go.transform;
                }
            }
        }
    }

    /// <summary>
    /// Shows a notification.
    /// </summary>
    public void ShowNotification(string message, float duration = 3f, Color? backgroundColor = null)
    {
        if (toastNotificationPrefab == null)
        {
            Debug.LogError("[NotificationManager] Toast notification prefab is not assigned!");
            return;
        }

        if (notificationContainer == null)
        {
            Debug.LogError("[NotificationManager] Notification container is not assigned!");
            return;
        }

        // Spawn the prefab while preserving its local transform
        GameObject notificationObj = Instantiate(toastNotificationPrefab, notificationContainer, false);
        // Show the newest notification at the top
        notificationObj.transform.SetAsFirstSibling();

        Debug.Log($"[NotificationManager] Showing notification: {message}");
        ToastNotification notification = notificationObj.GetComponent<ToastNotification>();

        if (notification != null)
        {
            notification.Initialize(message, duration, backgroundColor);
        }

        activeNotifications.Add(notificationObj);

        // Remove the oldest notification when the screen limit is exceeded
        if (activeNotifications.Count > Mathf.Max(0, maxNotificationsOnScreen))
        {
            GameObject oldestNotification = activeNotifications[0];
            activeNotifications.RemoveAt(0);
            if (oldestNotification != null)
                Destroy(oldestNotification);
        }

        // Remove the notification from the active list when it expires
        StartCoroutine(RemoveNotificationAfterDuration(notificationObj, duration));
    }

    /// <summary>
    /// Shows a success notification (green).
    /// </summary>
    public void ShowSuccess(string message, float duration = 3f)
    {
        Color parsed;
        if (ColorUtility.TryParseHtmlString(successColorHex, out parsed))
        {
            parsed.a = 0.9f;
            ShowNotification(message, duration, parsed);
        }
        else
        {
            ShowNotification(message, duration, new Color(0.2f, 0.8f, 0.2f, 0.9f)); // Fallback color
        }
    }

    /// <summary>
    /// Shows an error notification (red).
    /// </summary>
    public void ShowError(string message, float duration = 3f)
    {
        ShowNotification(message, duration, new Color(0.9f, 0.2f, 0.2f, 0.9f)); // Red
    }

    /// <summary>
    /// Shows an informational notification (blue).
    /// </summary>
    public void ShowInfo(string message, float duration = 3f)
    {
        ShowNotification(message, duration, new Color(0.2f, 0.6f, 0.9f, 0.9f)); // Blue
    }

    /// <summary>
    /// Shows a warning notification (orange).
    /// </summary>
    public void ShowWarning(string message, float duration = 3f)
    {
        ShowNotification(message, duration, new Color(1f, 0.6f, 0.2f, 0.9f)); // Orange
    }

    private System.Collections.IEnumerator RemoveNotificationAfterDuration(GameObject notification, float duration)
    {
        yield return new WaitForSeconds(duration + 0.5f);

        if (notification != null)
            Destroy(notification);

        activeNotifications.Remove(notification);
    }
}
