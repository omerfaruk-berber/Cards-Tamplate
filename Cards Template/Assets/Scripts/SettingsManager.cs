using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionsManager : MonoBehaviour
{
    private TimeDisplay timeDisplay;

    private const string TimeDisplayModeKey = "TimeDisplayMode";

    [System.Obsolete]
    private void Awake()
    {
        // Sahnede TimeDisplay componentini otomatik bulur
        timeDisplay = FindObjectOfType<TimeDisplay>();

        if (timeDisplay == null)
        {
            Debug.LogWarning("[OptionsManager] TimeDisplay bulunamadı!");
        }

        LoadDisplayMode();
    }

    // Sistem saati / Oyun süresi arasında geçiş yapar
    public void SetDisplayMode(int index)
    {
        if (timeDisplay == null)
            return;

        bool showPlayTime = (index == 1);
        timeDisplay.TogglePlayTime(showPlayTime);

        // Kaydet
        PlayerPrefs.SetInt(TimeDisplayModeKey, index);
        PlayerPrefs.Save();
    }

    private void LoadDisplayMode() // Kaydedilen modu yükler
    {
        if (timeDisplay == null)
            return;

        int savedIndex = PlayerPrefs.GetInt(TimeDisplayModeKey, 0); 
        // Varsayılan: 0 → Sistem Saati

        bool showPlayTime = (savedIndex == 1);
        timeDisplay.TogglePlayTime(showPlayTime);
    }

    public void ResetSettings()
    {
        PlayerPrefs.DeleteKey(TimeDisplayModeKey);
        PlayerPrefs.Save();

        MultiObjectYSlider[] ySliders = FindObjectsByType<MultiObjectYSlider>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (MultiObjectYSlider ySlider in ySliders)
        {
            if (ySlider != null)
            {
                ySlider.ResetToDefault();
            }
        }

        if (timeDisplay == null)
            return;

        timeDisplay.TogglePlayTime(false);
        timeDisplay.SetPlayTimeFormat(TimeDisplay.PlayTimeFormat.Hour_Minute_Second);
    }

    // Oyun süresi formatını Saat:Dakika:Saniye olarak ayarlar
    public void SetFormatHourMinuteSecond()
    {
        if (timeDisplay != null)
        {
            timeDisplay.SetPlayTimeFormat(TimeDisplay.PlayTimeFormat.Hour_Minute_Second);
        }
    }


    // Oyun süresi formatını Gün:Saat:Dakika olarak ayarlar
    public void SetFormatDayHourMinute()
    {
        if (timeDisplay != null)
        {
            timeDisplay.SetPlayTimeFormat(TimeDisplay.PlayTimeFormat.Day_Hour_Minute);
        }
    }

    // Oyun süresi formatını Gün:Dakika:Saniye olarak ayarlar
    public void SetFormatDayMinuteSecond()
    {
        if (timeDisplay != null)
        {
            timeDisplay.SetPlayTimeFormat(TimeDisplay.PlayTimeFormat.Day_Minute_Second);
        }
    }


    // Toplam oyun süresini sıfırlar
    public void ResetPlayTime()
    {
        if (timeDisplay != null)
        {
            timeDisplay.ResetPlayTime();
        }
    }

}