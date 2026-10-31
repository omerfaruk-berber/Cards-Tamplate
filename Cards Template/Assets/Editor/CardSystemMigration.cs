using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit migration. Sources and open scenes are never removed or saved.</summary>
public static class CardSystemMigration
{
    [MenuItem("Tools/Cards/Migrate to Dataset Cards")]
    public static void Run()
    {
        var datasets = AssetDatabase.FindAssets("t:CardDataset", new[] { "Assets" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardDataset>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        bool changed = Migrate(CardDatasetCreation.CommonViewPath,
            "Assets/Prefabs/Cards/Card 1.prefab", "Assets/Prefabs/Cards/Play Card.prefab",
            "Assets/Prefabs/Cards/Options Card.prefab", datasets);
        Debug.Log(changed ? "CARD_DATASET_MIGRATION: Succeeded" : "CARD_DATASET_MIGRATION: Already current");
    }

    // Explicit paths make the same preflight usable by tests and projects with another asset layout.
    public static bool Migrate(string commonPath, string normalPath, string startPath, string settingsPath,
        CardDataset[] datasets)
    {
        if (datasets == null) throw new ArgumentNullException(nameof(datasets));
        var upgrades = new List<KeyValuePair<CardDataset, CardDataset>>();
        GameObject root = null;
        CardDataset probe = ScriptableObject.CreateInstance<CardDataset>();
        try
        {
            CardDatasetCreation.EnsureStarterRecords(probe);
            probe.cards.Find(card => card.role == CardRole.Normal).description = "Validation";
            foreach (var dataset in datasets)
            {
                if (dataset == null) throw new ArgumentException("Boş veri seti.", nameof(datasets));
                if (dataset.schemaVersion >= CardDatasetCreation.CurrentVersion) continue;
                var copy = Object.Instantiate(dataset);
                copy.name = dataset.name;
                upgrades.Add(new KeyValuePair<CardDataset, CardDataset>(dataset, copy));
                if (copy.cards == null) throw new InvalidOperationException(dataset.name + ": kart listesi eksik.");
                CardDatasetCreation.EnsureStarterRecords(copy);
                var validation = CardDatasetValidator.Validate(copy);
                if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
                probe.cards.AddRange(copy.cards.Where(card => card.role == CardRole.Normal || card.role == CardRole.Cheat));
            }

            string sourcePath = AssetDatabase.LoadAssetAtPath<GameObject>(commonPath) != null ? commonPath : normalPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
                throw new InvalidOperationException("Ortak görünüm veya eski normal kart kaynağı bulunamadı: " + sourcePath);
            // All work below happens in an isolated prefab scene until every source validates.
            root = PrefabUtility.LoadPrefabContents(sourcePath);
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    throw new InvalidOperationException("Kaynak prefabda eksik script var: " + sourcePath);
            var view = root.GetComponent<CardView>();
            bool rebuild = sourcePath != commonPath || view == null || view.ValidateConfiguration(probe).Count > 0;
            if (rebuild)
            {
                BuildCommonView(root, startPath, settingsPath);
                view = root.GetComponent<CardView>();
                var errors = view.ValidateConfiguration(probe);
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            }
            if (!rebuild && upgrades.Count == 0) return false;

            // Preflight has completed. No scene is opened and legacy assets stay in place.
            if (rebuild)
            {
                PrefabUtility.SaveAsPrefabAsset(root, commonPath, out bool success);
                if (!success) throw new InvalidOperationException("Ortak görünüm kaydedilemedi: " + commonPath);
            }
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(commonPath);
            foreach (var upgrade in upgrades)
            {
                Undo.RecordObject(upgrade.Key, "Kart veri setini güncelle");
                upgrade.Value.viewPrefab = template;
                upgrade.Value.schemaVersion = CardDatasetCreation.CurrentVersion;
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(upgrade.Value), upgrade.Key);
                EditorUtility.SetDirty(upgrade.Key);
                AssetDatabase.SaveAssetIfDirty(upgrade.Key);
            }
            return true;
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
            foreach (var upgrade in upgrades) Object.DestroyImmediate(upgrade.Value);
            Object.DestroyImmediate(probe);
        }
    }

    private static void BuildCommonView(GameObject root, string startPath, string settingsPath)
    {
        root.name = "Card View";
        var view = root.GetComponent<CardView>() ?? root.AddComponent<CardView>();
        var drag = root.GetComponent<DragCard>();
        drag.enabled = true;
        var serialized = new SerializedObject(view);
        var existingStart = root.transform.Find("Start Content");
        var start = existingStart != null ? existingStart.gameObject : CopyContent(startPath, root.transform, "Start Content");
        var settings = root.transform.Find("Settings Content")?.gameObject ?? CopyContent(settingsPath, root.transform, "Settings Content");
        if (existingStart == null)
            foreach (var text in start.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.text == "Cards") Object.DestroyImmediate(text.gameObject);
        SetMissing(serialized, "startContent", start);
        SetMissing(serialized, "settingsContent", settings);

        var title = root.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(text =>
            text != drag.yesText && text != drag.noText && text.transform.parent == root.transform &&
            text.name != "Description");
        SetMissing(serialized, "titleText", title != null ? title : CreateText(root.transform, "Title", new Vector2(0, 170)));
        SetMissing(serialized, "descriptionText", FindOrCreateText(root.transform, "Description", Vector2.zero));
        var artwork = root.transform.Find("Artwork")?.GetComponent<Image>();
        if (artwork == null)
        {
            var image = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(root.transform, false);
            ((RectTransform)image.transform).sizeDelta = new Vector2(260, 160);
            ((RectTransform)image.transform).anchoredPosition = new Vector2(0, -130);
            artwork = image.GetComponent<Image>();
            artwork.raycastTarget = false;
            image.SetActive(false);
        }
        SetMissing(serialized, "artworkImage", artwork);
        var startDescription = start.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(text => text.text != "Cards");
        SetMissing(serialized, "startDescriptionText", startDescription != null ? startDescription :
            CreateText(start.transform, "Start Description", Vector2.zero));
        var settingsTitle = settings.GetComponentsInChildren<TextMeshProUGUI>(true)
            .FirstOrDefault(text => text.text == "Options" || text.name == "Title");
        if (settingsTitle == null) settingsTitle = CreateText(settings.transform, "Title", new Vector2(0, 170));
        SetMissing(serialized, "settingsTitleText", settingsTitle);
        if (drag.yesText == null) drag.yesText = FindOrCreateText(root.transform, "Yes", new Vector2(0, -220));
        if (drag.noText == null) drag.noText = FindOrCreateText(root.transform, "No", new Vector2(0, -220));
        drag.detachTextsFromCard = false;
        start.SetActive(false);
        settings.SetActive(false);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(false);
    }

    private static void SetMissing(SerializedObject serialized, string name, Object value)
    {
        var property = serialized.FindProperty(name);
        if (property.objectReferenceValue == null) property.objectReferenceValue = value;
    }

    private static TextMeshProUGUI FindOrCreateText(Transform parent, string name, Vector2 position) =>
        parent.Find(name)?.GetComponent<TextMeshProUGUI>() ?? CreateText(parent, name, position);

    private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 position)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        var rect = (RectTransform)item.transform;
        rect.sizeDelta = new Vector2(420, 120);
        rect.anchoredPosition = position;
        var text = item.GetComponent<TextMeshProUGUI>();
        text.fontSize = 32;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    private static GameObject CopyContent(string path, Transform parent, string name)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            throw new InvalidOperationException("Kart görünümü için kaynak bulunamadı: " + path);
        var source = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var copy = Object.Instantiate(source, parent, false);
            if (PrefabUtility.IsPartOfPrefabInstance(copy))
                PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            copy.name = name;
            var view = copy.GetComponent<CardView>();
            if (view != null) Object.DestroyImmediate(view);
            var drag = copy.GetComponent<DragCard>();
            if (drag != null) Object.DestroyImmediate(drag);
            var outline = copy.GetComponent<Outline>();
            if (outline != null) Object.DestroyImmediate(outline);
            var image = copy.GetComponent<Image>();
            if (image != null) Object.DestroyImmediate(image);
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            return copy;
        }
        finally { PrefabUtility.UnloadPrefabContents(source); }
    }
}
