using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class CardDownloadButton : MonoBehaviour
{
    [SerializeField] private Button downloadButton;
    [SerializeField] private string downloadFileName = "Card_";
    [SerializeField] private CardGameController controller;
    private DragCard currentCard;
    private bool saving;
    public string LastSavedPath { get; private set; }

    private void Awake()
    {
        if (downloadButton == null) downloadButton = GetComponent<Button>();
        if (controller == null) controller = FindFirstObjectByType<CardGameController>();
    }

    private void OnEnable()
    {
        if (downloadButton != null) downloadButton.onClick.AddListener(OnDownloadButtonClicked);
        if (controller != null)
        {
            controller.CurrentCardChanged += HandleCurrentCardChanged;
            HandleCurrentCardChanged(controller.CurrentCard);
        }
        else RefreshButton();
    }

    private void OnDisable()
    {
        if (downloadButton != null) downloadButton.onClick.RemoveListener(OnDownloadButtonClicked);
        if (controller != null) controller.CurrentCardChanged -= HandleCurrentCardChanged;
        if (currentCard != null) currentCard.AvailabilityChanged -= RefreshButton;
        currentCard = null;
    }

    private void HandleCurrentCardChanged(DragCard card)
    {
        if (currentCard != null) currentCard.AvailabilityChanged -= RefreshButton;
        currentCard = card;
        if (currentCard != null) currentCard.AvailabilityChanged += RefreshButton;
        RefreshButton();
    }

    private void RefreshButton()
    {
        if (downloadButton != null) downloadButton.interactable = !saving && currentCard != null && currentCard.IsReady;
    }

    private void OnDownloadButtonClicked()
    {
        if (saving || currentCard == null || !currentCard.IsReady) return;
        saving = true;
        RefreshButton();
        Texture2D texture = null;
        try
        {
            texture = CardImageExporter.Capture((RectTransform)currentCard.transform);
            byte[] png = texture.EncodeToPNG();
            string filename = downloadFileName + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") +
                "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".png";
            string filePath = Path.Combine(Application.persistentDataPath, filename);
            File.WriteAllBytes(filePath, png);
            LastSavedPath = filePath;
            Debug.Log("[Cards] Kart kaydedildi: " + filePath);
            NotificationManager.Instance?.ShowSuccess("Kart kaydedildi.");
#if UNITY_ANDROID && !UNITY_EDITOR
            AddImageToGallery(filePath);
#endif
        }
        catch (Exception error)
        {
            Debug.LogError("[Cards] Kart kaydedilemedi: " + error.Message);
            NotificationManager.Instance?.ShowError("Kart kaydedilemedi.");
        }
        finally
        {
            if (texture != null) Destroy(texture);
            saving = false;
            RefreshButton();
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void AddImageToGallery(string filePath)
    {
        // Scan only a successfully written file. The local PNG remains available if scanning fails.
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var scanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
                scanner.CallStatic("scanFile", activity, new[] { filePath }, null, null);
        }
        catch (Exception error)
        {
            Debug.LogWarning("[Cards] Galeri taraması tamamlanamadı: " + error.Message);
        }
    }
#endif
}
