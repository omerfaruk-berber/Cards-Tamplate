using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public class CardMigrationTests
{
    private string folder;
    private string Normal => folder + "/Normal.prefab";
    private string Start => folder + "/Start.prefab";
    private string Settings => folder + "/Settings.prefab";
    private string Common => folder + "/Common.prefab";
    private static Type ViewType => Type.GetType("CardView, Assembly-CSharp", true);

    [SetUp]
    public void SetUp()
    {
        folder = "Assets/CardMigrationTest_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        Assert.That(AssetDatabase.CopyAsset(CardDatasetCreation.CommonViewPath, Normal), Is.True);
        var root = PrefabUtility.LoadPrefabContents(Normal);
        try
        {
            foreach (var name in new[] { "Start Content", "Settings Content" })
            {
                var child = root.transform.Find(name);
                PrefabUtility.SaveAsPrefabAsset(child.gameObject, name == "Start Content" ? Start : Settings);
                Object.DestroyImmediate(child.gameObject);
            }
            Object.DestroyImmediate(root.GetComponent(ViewType));
            PrefabUtility.SaveAsPrefabAsset(root, Normal);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TearDown]
    public void TearDown()
    {
        AssetDatabase.DeleteAsset(folder);
    }

    private bool Migrate(CardDataset[] datasets, string settingsPath = null)
    {
        var type = Type.GetType("CardSystemMigration, Assembly-CSharp-Editor", true);
        try
        {
            return (bool)type.GetMethod("Migrate").Invoke(null,
                new object[] { Common, Normal, Start, settingsPath ?? Settings, datasets });
        }
        catch (TargetInvocationException error)
        {
            throw error.InnerException ?? error;
        }
    }

    [Test]
    public void LegacyPrefabWithoutViewMigratesWithoutDeletingSourcesAndSecondRunIsNoOp()
    {
        var dataset = ScriptableObject.CreateInstance<CardDataset>();
        dataset.cards.Add(new CardEntry { title = "Preserve me", description = "Content" });
        string id = dataset.cards[0].id;
        dataset.cards[0].yes.label = "Keep choice";
        dataset.cards[0].yes.defaultTarget = new CardTarget { type = CardTargetType.Card, cardId = id };
        string path = folder + "/Dataset.asset";
        AssetDatabase.CreateAsset(dataset, path);
        string originalName = dataset.name;
        string[] sources = new[] { Normal, Start, Settings }.Select(File.ReadAllText).ToArray();
        string scenePath = EditorSceneManager.GetActiveScene().path;
        Assert.That(Migrate(new[] { dataset }), Is.True);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Common);
        Assert.That(prefab.GetComponent(ViewType), Is.Not.Null);
        Assert.That(dataset.schemaVersion, Is.EqualTo(CardDatasetCreation.CurrentVersion));
        var entry = dataset.cards.Single(card => card.role == CardRole.Normal);
        Assert.That(entry.id, Is.EqualTo(id));
        Assert.That(entry.title, Is.EqualTo("Preserve me"));
        Assert.That(entry.yes.label, Is.EqualTo("Keep choice"));
        Assert.That(entry.description, Is.EqualTo("Content"));
        Assert.That(entry.yes.defaultTarget.cardId, Is.EqualTo(id));
        Assert.That(dataset.name, Is.EqualTo(originalName));
        string commonBefore = File.ReadAllText(Common);
        string datasetBefore = File.ReadAllText(path);
        Assert.That(Migrate(new[] { dataset }), Is.False);
        Assert.That(File.ReadAllText(Common), Is.EqualTo(commonBefore));
        Assert.That(File.ReadAllText(path), Is.EqualTo(datasetBefore));
        CollectionAssert.AreEqual(sources, new[] { Normal, Start, Settings }.Select(File.ReadAllText).ToArray());
        Assert.That(EditorSceneManager.GetActiveScene().path, Is.EqualTo(scenePath));
    }

    [Test]
    public void MissingSourceFailsPreflightWithoutChangingAssets()
    {
        var dataset = ScriptableObject.CreateInstance<CardDataset>();
        dataset.cards.Add(new CardEntry { title = "Original" });
        string path = folder + "/Dataset.asset";
        AssetDatabase.CreateAsset(dataset, path);
        string before = File.ReadAllText(path);
        string normalBefore = File.ReadAllText(Normal);
        Assert.Throws<InvalidOperationException>(() => Migrate(new[] { dataset }, folder + "/Missing.prefab"));
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Common), Is.Null);
        Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        Assert.That(File.ReadAllText(Normal), Is.EqualTo(normalBefore));
        Assert.That(dataset.cards.Count, Is.EqualTo(1));
        Assert.That(dataset.schemaVersion, Is.Zero);
    }
}
