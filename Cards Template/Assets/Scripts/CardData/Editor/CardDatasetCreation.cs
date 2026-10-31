using UnityEditor;
using UnityEngine;

public static class CardDatasetCreation
{
    public const string CommonViewPath = "Assets/Prefabs/Cards/Card View.prefab";
    public const int CurrentVersion = 2;

    [MenuItem("Assets/Create/Cards/Kart Veri Seti", false, 120)]
    public static void CreateAsset()
    {
        var dataset = ScriptableObject.CreateInstance<CardDataset>();
        Initialize(dataset);
        ProjectWindowUtil.CreateAsset(dataset, "CardDataset.asset");
    }

    public static void Initialize(CardDataset dataset)
    {
        dataset.viewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CommonViewPath);
        EnsureStarterRecords(dataset);
        dataset.schemaVersion = CurrentVersion;
    }

    public static void EnsureStarterRecords(CardDataset dataset)
    {
        if (!dataset.cards.Exists(card => card != null && card.role == CardRole.Start))
            dataset.cards.Insert(0, new CardEntry { role = CardRole.Start, title = "Cards", description = "Tap And Slide" });
        if (!dataset.cards.Exists(card => card != null && card.role == CardRole.Normal))
            dataset.cards.Add(new CardEntry());
        if (!dataset.cards.Exists(card => card != null && card.role == CardRole.End))
            dataset.cards.Add(new CardEntry { role = CardRole.End, title = "End" });
        if (!dataset.cards.Exists(card => card != null && card.role == CardRole.Settings))
            dataset.cards.Add(new CardEntry { role = CardRole.Settings, title = "Options" });
    }
}
