using UnityEngine;
using TMPro;

/// <summary>
/// Sistem saatini TextMeshProUGUI'ye gerçek zamanlı olarak gösterir.
/// Saat:Dakika:Saniye formatında güncellenir.
/// </summary>
public class TimeDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private bool show24Hour = true; // true = 24 saat, false = 12 saat (AM/PM)
    
    [Header("Oyun İçi Süre Ayarları")]
    [SerializeField] private bool showPlayTime = false; // true = Oyun süresi göster, false = Sistem saati göster
    [SerializeField] private PlayTimeFormat playTimeFormat = PlayTimeFormat.Hour_Minute_Second; // Oyun süresi formatı
    
    private float totalPlayTime = 0f; // Toplam oyun süresi (saniye cinsinden)
    private const string PLAYTIME_SAVE_KEY = "TotalPlayTime"; // PlayerPrefs kayıt anahtarı
    private const string TimeDisplayModeKey = "TimeDisplayMode";

    public enum PlayTimeFormat
    {
        Hour_Minute_Second,    // Saat:Dakika:Saniye
        Day_Hour_Minute,       // Gün:Saat:Dakika
        Day_Minute_Second      // Gün:Dakika:Saniye
    }

    private void Start()
    {
        if (timeText == null)
        {
            Debug.LogError("[TimeDisplay] TextMeshProUGUI atanmadı!");
            return;
        }
        
        // Kaydedilmiş oyun süresini yükle
        LoadPlayTime();
        LoadDisplayMode();
    }

    private void Update()
    {
        totalPlayTime += Time.deltaTime;
        if (timeText == null) return;

        if (showPlayTime)
        {
            // Oyun süresini güncelle
            UpdatePlayTimeDisplay();
        }
        else
        {
            // Sistem saatini göster
            System.DateTime now = System.DateTime.Now;
            
            if (show24Hour)
            {
                // 24 saat formatı (HH:mm:ss)
                timeText.text = now.ToString("HH:mm:ss");
            }
            else
            {
                // 12 saat formatı (hh:mm:ss tt - tt = AM/PM)
                timeText.text = now.ToString("hh:mm:ss tt");
            }

        }
    }

    private void LoadDisplayMode() // Kaydedilen modu yükler
    {
        int savedIndex = PlayerPrefs.GetInt(TimeDisplayModeKey, 0); 
        // Varsayılan: 0 → Sistem Saati

        bool showPlayTime = (savedIndex == 1);
        TogglePlayTime(showPlayTime);
    }

    private void OnApplicationQuit()
    {
        // Oyun kapanırken toplam süreyi kaydet
        SavePlayTime();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // Uygulama arka plana geçtiğinde kaydet
        if (pauseStatus)
        {
            SavePlayTime();
        }
    }


    // Oyun içi geçirilen süreyi formatlar ve gösterir
    private void UpdatePlayTimeDisplay()
    {
        int totalSeconds = Mathf.FloorToInt(totalPlayTime);
        
        switch (playTimeFormat)
        {
            case PlayTimeFormat.Hour_Minute_Second:
                // Saat:Dakika:Saniye formatı
                int hours = totalSeconds / 3600;
                int minutes = (totalSeconds % 3600) / 60;
                int seconds = totalSeconds % 60;
                timeText.text = string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
                break;
                
            case PlayTimeFormat.Day_Hour_Minute:
                // Gün:Saat:Dakika formatı
                int days = totalSeconds / 86400;
                int hoursInDay = (totalSeconds % 86400) / 3600;
                int minutesInDay = (totalSeconds % 3600) / 60;
                timeText.text = string.Format("{0:00}:{1:00}:{2:00}", days, hoursInDay, minutesInDay);
                break;
                
            case PlayTimeFormat.Day_Minute_Second:
                // Gün:Dakika:Saniye formatı
                int daysAlt = totalSeconds / 86400;
                int minutesInDayAlt = (totalSeconds % 86400) / 60;
                int secondsInDay = totalSeconds % 60;
                timeText.text = string.Format("{0:00}:{1:00}:{2:00}", daysAlt, minutesInDayAlt, secondsInDay);
                break;
        }
    }


    // Oyun süresini PlayerPrefs'e kaydeder
    private void SavePlayTime()
    {
        PlayerPrefs.SetInt(TimeDisplayModeKey, showPlayTime ? 1 : 0);
        PlayerPrefs.SetFloat(PLAYTIME_SAVE_KEY, totalPlayTime);
        PlayerPrefs.Save();
    }


    // Oyun süresini PlayerPrefs'ten yükler
    private void LoadPlayTime()
    {
        totalPlayTime = PlayerPrefs.GetFloat(PLAYTIME_SAVE_KEY, 0f);
    }

 
    // Saat formatını değiştirir (24 saat veya 12 saat)
    public void SetTimeFormat(bool use24Hour)
    {
        show24Hour = use24Hour;
    }


    // Tarih ve saat bilgisini tamamen gösterir
    public void ShowFullDateTime()
    {
        show24Hour = true;
        System.DateTime now = System.DateTime.Now;
        timeText.text = now.ToString("dd.MM.yyyy HH:mm:ss");
    }

 
    // Sadece saat:dakika gösterir
    public void ShowTimeOnly()
    {
        System.DateTime now = System.DateTime.Now;
        timeText.text = now.ToString("HH:mm");
    }

    
    // Sistem saati ile oyun süresi arasında geçiş yapar
    public void TogglePlayTime(bool showPlay)
    {
        if (showPlayTime == showPlay) return;
        showPlayTime = showPlay;
        PlayerPrefs.SetInt(TimeDisplayModeKey, showPlay ? 1 : 0);
        PlayerPrefs.Save();
    }

 
    // Oyun süresi formatını değiştirir
    public void SetPlayTimeFormat(PlayTimeFormat format)
    {
        playTimeFormat = format;
    }


    // Toplam oyun süresini sıfırlar
    public void ResetPlayTime()
    {
        totalPlayTime = 0f;
        SavePlayTime();
    }


    // Toplam oyun süresini saniye cinsinden döndürür
    public float GetTotalPlayTime()
    {
        return totalPlayTime;
    }
}
