using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CardDatasetRoleTests
{
    private CardDataset dataset;
    [SetUp] public void SetUp() => dataset = ScriptableObject.CreateInstance<CardDataset>();
    [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(dataset);

    [TestCase(CardAnswer.Yes)]
    [TestCase(CardAnswer.No)]
    public void SequentialRunsThreeCardsWithEmptyBranchTargets(CardAnswer answer)
    {
        for (int i = 0; i < 3; i++)
        {
            var card = new CardEntry { title = "Card " + i };
            card.yes.defaultTarget.type = card.no.defaultTarget.type = CardTargetType.Card;
            dataset.cards.Add(card);
        }
        Assert.That(CardDatasetValidator.Validate(dataset).IsValid, Is.True);
        var session = new CardSession(dataset);
        foreach (var card in dataset.cards)
        {
            Assert.That(session.Current.id, Is.EqualTo(card.id));
            session.Choose(answer);
        }
        Assert.That(session.IsFinished, Is.True);
        dataset.flowMode = CardFlowMode.Branching;
        var validation = CardDatasetValidator.Validate(dataset);
        Assert.That(validation.IsValid, Is.False);
        Assert.That(validation.Issues.Any(issue => issue.CardId == dataset.cards[0].id && issue.Choice == CardAnswer.Yes), Is.True);
    }

    [Test]
    public void SequentialDoesNotReadNullUnusedBranchConfiguration()
    {
        var card = new CardEntry();
        card.yes = null;
        card.no.defaultTarget = null;
        card.no.rules = null;
        dataset.cards.Add(card);
        var session = new CardSession(dataset);
        Assert.DoesNotThrow(() => session.Choose(CardAnswer.Yes));
        session.Restart();
        Assert.DoesNotThrow(() => session.Choose(CardAnswer.No));
    }

    [Test]
    public void SpecialRecordsAreSnapshotsAndNeverEnterNormalSequence()
    {
        var a = new CardEntry { title = "A" };
        var b = new CardEntry { title = "B" };
        var start = new CardEntry { role = CardRole.Start, title = "Begin" };
        var end = new CardEntry { role = CardRole.End };
        var settings = new CardEntry { role = CardRole.Settings };
        var cheat = new CardEntry { role = CardRole.Cheat, cheatId = 7 };
        dataset.cards.AddRange(new[] { start, a, settings, end, cheat, b });
        var session = new CardSession(dataset);
        start.title = "Edited";
        Assert.That(session.StartCard.title, Is.EqualTo("Begin"));
        Assert.That(session.SettingsCard.id, Is.EqualTo(settings.id));
        Assert.That(session.EndCard.id, Is.EqualTo(end.id));
        Assert.That(session.GetCheatCard(7).id, Is.EqualTo(cheat.id));
        Assert.That(session.Current.id, Is.EqualTo(a.id));
        session.Choose(CardAnswer.Yes);
        Assert.That(session.Current.id, Is.EqualTo(b.id));
        session.Choose(CardAnswer.No);
        Assert.That(session.IsFinished, Is.True);
        Assert.That(session.GetAnswer(start.id), Is.EqualTo(CardAnswer.Unanswered));
    }

    [Test]
    public void DuplicateSpecialRolesAndCheatNumbersAreLocatedErrors()
    {
        dataset.cards.Add(new CardEntry());
        dataset.cards.Add(new CardEntry { role = CardRole.Start });
        var duplicate = new CardEntry { role = CardRole.Start };
        dataset.cards.Add(duplicate);
        dataset.cards.Add(new CardEntry { role = CardRole.Cheat, cheatId = 1 });
        dataset.cards.Add(new CardEntry { role = CardRole.Cheat, cheatId = 1 });
        var validation = CardDatasetValidator.Validate(dataset);
        Assert.That(validation.Errors.Count, Is.EqualTo(2));
        Assert.That(validation.Issues.Any(issue => issue.CardId == duplicate.id), Is.True);
    }

    [Test]
    public void BranchTargetsAndHistoryConditionsMustUseNormalCards()
    {
        var normal = new CardEntry();
        var settings = new CardEntry { role = CardRole.Settings };
        dataset.cards.AddRange(new[] { normal, settings });
        dataset.flowMode = CardFlowMode.Branching;
        normal.yes.defaultTarget = new CardTarget { type = CardTargetType.Card, cardId = settings.id };
        normal.no.rules.Add(new CardTransitionRule
        {
            conditions = { new CardCondition { cardId = settings.id } }
        });
        Assert.That(CardDatasetValidator.Validate(dataset).Errors.Count, Is.EqualTo(2));
    }

    [Test]
    public void CreationAndDuplicationKeepDatasetReadyToUse()
    {
        CardDatasetCreation.Initialize(dataset);
        Assert.That(dataset.viewPrefab, Is.Not.Null);
        Assert.That(dataset.cards.Count, Is.EqualTo(4));
        int start = dataset.cards.FindIndex(card => card.role == CardRole.Start);
        int copy = CardDatasetEditing.Duplicate(dataset, start);
        Assert.That(dataset.cards[copy].role, Is.EqualTo(CardRole.Normal));
        Assert.That(dataset.cards[copy].yes.defaultTarget.type, Is.EqualTo(CardTargetType.NextInList));
        Assert.That(CardDatasetValidator.Validate(dataset).IsValid, Is.True);
    }

    [Test]
    public void ControllerOnlySerializesDatasetAndSpawnPoint()
    {
        var root = new GameObject("Controller contract");
        try
        {
            var component = root.AddComponent(Type.GetType("CardGameController, Assembly-CSharp", true));
            var serialized = new SerializedObject(component);
            var iterator = serialized.GetIterator();
            var fields = new System.Collections.Generic.List<string>();
            bool enter = true;
            while (iterator.NextVisible(enter))
            {
                enter = false;
                if (iterator.name != "m_Script") fields.Add(iterator.name);
            }
            CollectionAssert.AreEquivalent(new[] { "dataset", "spawnPoint" }, fields);
            var drag = Type.GetType("DragCard, Assembly-CSharp", true);
            Assert.That(drag.GetField("cardType"), Is.Null);
            Assert.That(drag.GetProperty("DestroyAfterExit"), Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
