using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(DragCard))]
public class CardView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image artworkImage;
    [SerializeField] private GameObject startContent;
    [SerializeField] private TextMeshProUGUI startDescriptionText;
    [SerializeField] private GameObject settingsContent;
    [SerializeField] private TextMeshProUGUI settingsTitleText;
    private DragCard drag;
    public DragCard Drag => drag != null ? drag : (drag = GetComponent<DragCard>());
    public CardEntry Entry { get; private set; }
    public bool Supports(CardRole role) => role != CardRole.Settings || settingsContent != null;

    public IReadOnlyList<string> ValidateConfiguration(CardDataset dataset)
    {
        var errors = new List<string>();
        if (Drag == null || !Drag.enabled) errors.Add("Ortak görünümde etkin DragCard bulunmalı.");
        if (titleText == null || !titleText.transform.IsChildOf(transform))
            errors.Add("Ortak görünümün başlık alanı kendi prefabına bağlanmalı.");
        if (Drag != null && (Drag.yesText == null || Drag.noText == null))
            errors.Add("Ortak görünümün Evet/Hayır metinleri atanmalı.");
        if (dataset?.cards == null) return errors;
        foreach (var entry in dataset.cards)
        {
            if (entry == null) continue;
            if (entry.role == CardRole.Start &&
                (startContent == null || startDescriptionText == null))
                errors.Add("Başlangıç kartının içerik ve açıklama alanları atanmalı.");
            if (entry.role == CardRole.Settings &&
                (settingsContent == null || settingsTitleText == null))
                errors.Add("Ayarlar kartının kontrol bölümü ve başlık alanı atanmalı.");
            if (entry.role != CardRole.Start && entry.role != CardRole.Settings &&
                !string.IsNullOrWhiteSpace(entry.description) && descriptionText == null)
                errors.Add("Açıklaması olan kartlar için açıklama alanı atanmalı.");
            if (entry.role != CardRole.Settings && entry.artwork != null && artworkImage == null)
                errors.Add("Görselli kartlar için görsel alanı atanmalı.");
        }
        return new List<string>(new HashSet<string>(errors));
    }

    public void Bind(CardEntry entry)
    {
        Entry = entry;
        bool settings = entry.role == CardRole.Settings;
        bool start = entry.role == CardRole.Start;
        if (startContent != null) startContent.SetActive(start);
        if (settingsContent != null) settingsContent.SetActive(settings);
        if (titleText != null)
        {
            titleText.text = entry.title ?? "";
            titleText.gameObject.SetActive(!settings);
        }
        if (settingsTitleText != null) settingsTitleText.text = settings ? entry.title ?? "" : "";
        if (startDescriptionText != null) startDescriptionText.text = start ? entry.description ?? "" : "";
        if (descriptionText != null)
        {
            descriptionText.text = entry.description ?? "";
            descriptionText.gameObject.SetActive(!settings && !start && !string.IsNullOrWhiteSpace(entry.description));
        }
        if (artworkImage != null)
        {
            artworkImage.sprite = entry.artwork;
            artworkImage.gameObject.SetActive(!settings && entry.artwork != null);
        }
        bool showChoices = entry.role == CardRole.Normal || entry.role == CardRole.Cheat;
        if (Drag.yesText != null)
        {
            Drag.yesText.text = entry.yes?.label ?? "Evet";
            Drag.yesText.gameObject.SetActive(showChoices);
        }
        if (Drag.noText != null)
        {
            Drag.noText.text = entry.no?.label ?? "Hayır";
            Drag.noText.gameObject.SetActive(showChoices);
        }
        Drag.AllowYes = entry.role != CardRole.End;
        Drag.AllowNo = entry.role != CardRole.Start && entry.role != CardRole.End;
    }

    public void Clear()
    {
        Entry = null;
        if (titleText != null) titleText.text = "";
        if (descriptionText != null) { descriptionText.text = ""; descriptionText.gameObject.SetActive(false); }
        if (artworkImage != null) { artworkImage.sprite = null; artworkImage.gameObject.SetActive(false); }
        if (startDescriptionText != null) startDescriptionText.text = "";
        if (settingsTitleText != null) settingsTitleText.text = "";
        if (startContent != null) startContent.SetActive(false);
        if (settingsContent != null) settingsContent.SetActive(false);
    }
}
