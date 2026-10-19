using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Button for downloading a card.
/// Automatically detects and downloads the current card on screen.
/// </summary>
public class CardDownloadButton : MonoBehaviour
{
    [SerializeField] private Button downloadButton;
    [SerializeField] private string downloadFileName = "Card_";

    private DragCard currentCard; // Current card on screen

    private void Start()
    {
        if (downloadButton == null)
        {
            downloadButton = GetComponent<Button>();
        }

        if (downloadButton != null)
        {
            downloadButton.onClick.AddListener(OnDownloadButtonClicked);
        }

        // Find the active card in the scene (it will appear during the entrance animation)
        UpdateCurrentCard();
    }

    private void Update()
    {
        // Update the current card every frame as cards change
        UpdateCurrentCard();
    }

    /// <summary>
    /// Automatically detect the active DragCard on screen
    /// </summary>
    private void UpdateCurrentCard()
    {
        DragCard[] allCards = FindObjectsByType<DragCard>(FindObjectsSortMode.None);
        
        if (allCards.Length > 0)
        {
            // Get the most recently spawned card (the last one is the active card)
            currentCard = allCards[allCards.Length - 1];
        }
        else
        {
            currentCard = null;
        }
    }

    /// <summary>
    /// Called when the download button is clicked
    /// </summary>
    private void OnDownloadButtonClicked()
    {
        if (currentCard == null)
        {
            Debug.LogError("[CardDownloadButton] No card found on screen!");
            if (NotificationManager.Instance != null)
                NotificationManager.Instance.ShowError("Card Not Found!");
            else
                Debug.LogWarning("[CardDownloadButton] NotificationManager is unavailable; could not show notification.");
            return;
        }

        // Get the Image component of the current card
        Image cardImage = currentCard.GetComponent<Image>();
        if (cardImage == null)
        {
            Debug.LogError("[CardDownloadButton] Card Image component not found!");
            if (NotificationManager.Instance != null)
                NotificationManager.Instance.ShowError("Not Found!");
            else
                Debug.LogWarning("[CardDownloadButton] NotificationManager is unavailable; could not show notification.");
            return;
        }

        // Download the card
        DownloadCard(cardImage);
    }

    /// <summary>
    /// Download the card and save it to a file
    /// </summary>
    private void DownloadCard(Image cardImage)
    {
        try
        {
            // Get the card's RectTransform
            RectTransform cardRect = cardImage.GetComponent<RectTransform>();
            if (cardRect == null)
            {
                if (NotificationManager.Instance != null)
                    NotificationManager.Instance.ShowError("Could not download the card!");
                else
                    Debug.LogWarning("[CardDownloadButton] NotificationManager is unavailable; could not show notification.");
                return;
            }

            // Capture a screenshot
            Texture2D cardTexture = CaptureRectTransformToTexture(cardRect);
            if (cardTexture == null)
            {
                if (NotificationManager.Instance != null)
                    NotificationManager.Instance.ShowError("Could not capture the screenshot!");
                else
                    Debug.LogWarning("[CardDownloadButton] NotificationManager is unavailable; could not show notification.");
                return;
            }

            // Save as a PNG
            byte[] pngData = cardTexture.EncodeToPNG();
            string fileName = downloadFileName + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);

            System.IO.File.WriteAllBytes(filePath, pngData);
            Destroy(cardTexture);

            Debug.Log($"[CardDownloadButton] Card downloaded: {filePath}");
            if (NotificationManager.Instance != null)
                NotificationManager.Instance.ShowSuccess("Card Saved.");
            else
                Debug.LogWarning("[CardDownloadButton] NotificationManager is unavailable; could not show success notification.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CardDownloadButton] Download error: {ex.Message}");
            if (NotificationManager.Instance != null)
                NotificationManager.Instance.ShowError("Download failed!");
            else
                Debug.LogWarning("[CardDownloadButton] NotificationManager is unavailable; could not show error notification.");
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        AddImageToGallery(filePath);
        #endif
    }

    private void AddImageToGallery(string filePath)
    {
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass mediaScanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
            {
                mediaScanner.CallStatic(
                    "scanFile",
                    activity,
                    new string[] { filePath },
                    null,
                    null
                );
            }

            Debug.Log("[Gallery] Image added to gallery.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Gallery] Error adding image to gallery: " + e.Message);
        }
    }

    /// <summary>
    /// Capture the RectTransform as a texture
    /// </summary>
    private Texture2D CaptureRectTransformToTexture(RectTransform rect)
    {
        // Get the RectTransform dimensions
        Vector2 size = rect.rect.size;
        int width = (int)size.x;
        int height = (int)size.y;

        if (width <= 0 || height <= 0)
            return null;

        // Create a RenderTexture
        RenderTexture renderTexture = new RenderTexture(width, height, 24);
        Graphics.SetRenderTarget(renderTexture);
        GL.Clear(true, true, Color.clear);

        // Render the Canvas
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.worldCamera != null)
        {
            canvas.worldCamera.targetTexture = renderTexture;
            canvas.worldCamera.Render();
            canvas.worldCamera.targetTexture = null;
        }

        // Convert the RenderTexture to a Texture2D
        RenderTexture.active = renderTexture;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();

        RenderTexture.active = null;
        renderTexture.Release();
        Destroy(renderTexture);

        return texture;
    }
}
