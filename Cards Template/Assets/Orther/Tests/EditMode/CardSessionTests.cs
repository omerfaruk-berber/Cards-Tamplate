using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CardSessionTests
{
    private CardDataset dataset;

    [SetUp]
    public void SetUp()
    {
        dataset = ScriptableObject.CreateInstance<CardDataset>();
        dataset.cards.AddRange(new[] { new CardEntry { title = "A" }, new CardEntry { title = "B" },
            new CardEntry { title = "C" }, new CardEntry { title = "D" }, new CardEntry { title = "E" } });
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(dataset);

    private CardTarget To(int index) => new CardTarget { type = CardTargetType.Card, cardId = dataset.cards[index].id };

    [Test]
    public void ReturnedCardsCannotChangeSessionIdentityHistoryOrBranches()
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards[0].yes.defaultTarget = To(1);
        dataset.cards.Add(new CardEntry { role = CardRole.Start, title = "Start" });
        dataset.cards.Add(new CardEntry { role = CardRole.End, title = "End" });
        dataset.cards.Add(new CardEntry { role = CardRole.Settings, title = "Settings" });
        dataset.cards.Add(new CardEntry { role = CardRole.Cheat, cheatId = 7, title = "Cheat" });
        var session = new CardSession(dataset);
        string id = session.Current.id;
        var exposed = session.Current;
        exposed.id = "changed";
        exposed.yes.defaultTarget.cardId = "missing";
        session.StartCard.title = session.EndCard.title = session.SettingsCard.title = "changed";
        session.GetCheatCard(7).title = "changed";
        var next = session.Choose(CardAnswer.Yes);
        next.id = "changed";
        Assert.That(session.Current.id, Is.EqualTo(dataset.cards[1].id));
        Assert.That(session.GetAnswer(id), Is.EqualTo(CardAnswer.Yes));
        Assert.That(session.StartCard.title, Is.EqualTo("Start"));
        Assert.That(session.EndCard.title, Is.EqualTo("End"));
        Assert.That(session.SettingsCard.title, Is.EqualTo("Settings"));
        Assert.That(session.GetCheatCard(7).title, Is.EqualTo("Cheat"));
    }

    [Test]
    public void ImpossibleCurrentAnswerDoesNotCreateFalseCycle()
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards[0].yes.rules.Add(When(0, CardAnswer.No, 0));
        var result = CardDatasetValidator.Validate(dataset);
        Assert.That(result.IsValid, Is.True);
        var issue = result.Issues.Single(item => item.Code == "ImpossibleCurrentAnswer");
        Assert.That(issue.Severity, Is.EqualTo(CardIssueSeverity.Warning));
        Assert.That(issue.CardIndex, Is.Zero);
        Assert.That(issue.PropertyPath, Is.EqualTo("cards.Array.data[0].yes.rules.Array.data[0].conditions.Array.data[0]"));
        Assert.That(result.Issues.Any(item => item.Code == "PossibleCycle"), Is.False);
    }

    [Test]
    public void ContradictionsAndShadowingAreWarningsAndDoNotAddImpossibleEdges()
    {
        dataset.flowMode = CardFlowMode.Branching;
        var contradiction = When(1, CardAnswer.Yes, 0);
        contradiction.conditions.Add(new CardCondition { cardId = dataset.cards[1].id, answer = CardAnswer.No });
        dataset.cards[0].yes.rules.Add(contradiction);
        dataset.cards[0].yes.rules.Add(When(1, CardAnswer.Yes, 2));
        var shadowed = When(1, CardAnswer.Yes, 0);
        shadowed.conditions.Add(new CardCondition { cardId = dataset.cards[2].id, answer = CardAnswer.No });
        dataset.cards[0].yes.rules.Add(shadowed);
        var result = CardDatasetValidator.Validate(dataset);
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Issues.Any(item => item.Code == "ContradictoryConditions"), Is.True);
        Assert.That(result.Issues.Any(item => item.Code == "ShadowedRule"), Is.True);
        Assert.That(result.Issues.Any(item => item.Code == "PossibleCycle"), Is.False);
    }

    [Test]
    public void IssueIndicesReferToDatasetIncludingSpecialCards()
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards.Insert(0, new CardEntry { role = CardRole.Start });
        dataset.cards[1].yes.defaultTarget = new CardTarget { type = CardTargetType.Card, cardId = "missing" };
        var issue = CardDatasetValidator.Validate(dataset).Issues.Single(item => item.Code == "MissingTarget");
        Assert.That(issue.CardIndex, Is.EqualTo(1));
        Assert.That(issue.PropertyPath, Is.EqualTo("cards.Array.data[1].yes.defaultTarget.cardId"));
        var serialized = new SerializedObject(dataset);
        Assert.That(serialized.FindProperty(issue.PropertyPath), Is.Not.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void InvalidIdentityReturnsLocatedErrorsWithoutThrowing(string id)
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards[0].id = id;
        dataset.cards[0].yes.rules.Add(When(1, CardAnswer.Yes, 2));
        var result = CardDatasetValidator.Validate(dataset);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Issues.Any(issue => issue.Code == "MissingId" &&
            issue.CardIndex == 0 && issue.PropertyPath == "cards.Array.data[0].id"), Is.True);
    }

    [Test]
    public void IncomingReferencesIncludeTargetsAndHistoryAndSurviveRoleChangeUndo()
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards[0].yes.defaultTarget = To(1);
        dataset.cards[0].no.rules.Add(When(1, CardAnswer.Yes, 1));
        string id = dataset.cards[1].id;
        var references = CardDatasetEditing.GetIncomingReferences(dataset, id);
        Assert.That(references.Count, Is.EqualTo(3));
        Assert.That(references.All(item => new SerializedObject(dataset).FindProperty(item.PropertyPath) != null), Is.True);
        Undo.RecordObject(dataset, "Change role");
        dataset.cards[1].role = CardRole.Settings;
        Undo.FlushUndoRecordObjects();
        Assert.That(CardDatasetValidator.Validate(dataset).IsValid, Is.False);
        Assert.That(dataset.cards[0].yes.defaultTarget.cardId, Is.EqualTo(id));
        Undo.PerformUndo();
        Assert.That(CardDatasetValidator.Validate(dataset).IsValid, Is.True);
        Assert.That(CardDatasetEditing.GetIncomingReferences(dataset, id).Count, Is.EqualTo(3));
    }

    [Test]
    public void InspectorSelectionFollowsIdentityAcrossReorderAndUndo()
    {
        var editor = Editor.CreateEditor(dataset);
        try
        {
            var type = editor.GetType();
            type.GetMethod("SelectIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(editor, new object[] { 1 });
            string id = dataset.cards[1].id;
            var serialized = new SerializedObject(dataset);
            serialized.FindProperty("cards").MoveArrayElement(1, 4);
            serialized.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects();
            type.GetMethod("HandleUndo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(editor, null);
            var field = type.GetField("selected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That((int)field.GetValue(editor), Is.EqualTo(4));
            Undo.PerformUndo();
            Assert.That(dataset.cards[(int)field.GetValue(editor)].id, Is.EqualTo(id));
            Undo.PerformRedo();
            Assert.That((int)field.GetValue(editor), Is.EqualTo(4));
            Assert.That(dataset.cards[4].id, Is.EqualTo(id));
        }
        finally { UnityEngine.Object.DestroyImmediate(editor); }
    }
    private CardTransitionRule When(int index, CardAnswer answer, int target) => new CardTransitionRule
    {
        conditions = { new CardCondition { cardId = dataset.cards[index].id, answer = answer } },
        target = To(target)
    };

    [TestCase(CardAnswer.Yes)]
    [TestCase(CardAnswer.No)]
    public void SequentialIgnoresBranchTargetsAndFinishesInListOrder(CardAnswer answer)
    {
        dataset.cards[0].yes.defaultTarget.type = CardTargetType.EndGame;
        dataset.cards[0].no.defaultTarget = To(4);
        var session = new CardSession(dataset);
        foreach (var card in dataset.cards)
        {
            Assert.That(session.Current.id, Is.EqualTo(card.id));
            session.Choose(answer);
        }
        Assert.That(session.IsFinished, Is.True);
        Assert.That(session.Choose(answer), Is.Null);
    }

    [TestCase(CardAnswer.Yes, 3)]
    [TestCase(CardAnswer.No, 4)]
    public void DecisionAffectsTransitionSeveralCardsLater(CardAnswer answer, int expected)
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards[2].yes.rules.Add(When(0, CardAnswer.Yes, 3));
        dataset.cards[2].yes.rules.Add(When(0, CardAnswer.No, 4));
        var session = new CardSession(dataset);
        session.Choose(answer);
        session.Choose(CardAnswer.No);
        session.Choose(CardAnswer.Yes);
        Assert.That(session.Current.id, Is.EqualTo(dataset.cards[expected].id));
    }

    [Test]
    public void ConditionsAreAndCombinedAndFirstMatchingRuleWins()
    {
        dataset.flowMode = CardFlowMode.Branching;
        var impossible = When(0, CardAnswer.Yes, 4);
        impossible.conditions.Add(new CardCondition { cardId = dataset.cards[1].id, answer = CardAnswer.No });
        dataset.cards[1].yes.rules.Add(impossible);
        dataset.cards[1].yes.rules.Add(When(0, CardAnswer.Yes, 3));
        dataset.cards[1].yes.rules.Add(When(0, CardAnswer.Yes, 4));
        var session = new CardSession(dataset);
        session.Choose(CardAnswer.Yes);
        session.Choose(CardAnswer.Yes);
        Assert.That(session.Current.id, Is.EqualTo(dataset.cards[3].id));
        Assert.That(session.GetAnswer(dataset.cards[1].id), Is.EqualTo(CardAnswer.Yes));
    }

    [Test]
    public void UnansweredIsDistinctFromNoAndUnmatchedRulesUseDefault()
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards[0].yes.rules.Add(When(4, CardAnswer.No, 2));
        dataset.cards[0].yes.rules.Add(When(4, CardAnswer.Unanswered, 3));
        var session = new CardSession(dataset);
        session.Choose(CardAnswer.Yes);
        Assert.That(session.Current.id, Is.EqualTo(dataset.cards[3].id));
        session.Choose(CardAnswer.No);
        Assert.That(session.Current.id, Is.EqualTo(dataset.cards[4].id));
    }

    [Test]
    public void RevisitsReplaceAnswerAndRestartClearsHistory()
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards[0].yes.defaultTarget = To(0);
        var session = new CardSession(dataset);
        session.Choose(CardAnswer.Yes);
        session.Choose(CardAnswer.No);
        Assert.That(session.GetAnswer(dataset.cards[0].id), Is.EqualTo(CardAnswer.No));
        Assert.That(session.AnsweredCardCount, Is.EqualTo(1));
        session.Restart();
        Assert.That(session.Current.id, Is.EqualTo(dataset.cards[0].id));
        Assert.That(session.GetAnswer(dataset.cards[0].id), Is.EqualTo(CardAnswer.Unanswered));
        Assert.That(session.AnsweredCardCount, Is.Zero);
    }

    [Test]
    public void SessionSnapshotsDataAndNeverWritesToAsset()
    {
        var session = new CardSession(dataset);
        string originalTitle = dataset.cards[0].title;
        dataset.cards[0].title = "Edited after load";
        dataset.cards.Clear();
        Assert.That(session.Current.title, Is.EqualTo(originalTitle));
        Assert.DoesNotThrow(() => session.Choose(CardAnswer.Yes));
        Assert.That(session.Current.title, Is.EqualTo("B"));
    }

    [Test]
    public void ExplicitEndAndInvalidAnswerAreHandled()
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards[0].no.defaultTarget.type = CardTargetType.EndGame;
        var session = new CardSession(dataset);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Choose(CardAnswer.Unanswered));
        session.Choose(CardAnswer.No);
        Assert.That(session.IsFinished, Is.True);
    }

    [Test]
    public void InvalidDataCannotCreateSession()
    {
        dataset.cards.Add(dataset.cards[0]);
        Assert.That(CardDatasetValidator.Validate(dataset).Errors.Any(e => e.Contains("tekrarlanan")), Is.True);
        Assert.Throws<ArgumentException>(() => new CardSession(dataset));
        dataset.cards.Clear();
        Assert.That(CardDatasetValidator.Validate(dataset).IsValid, Is.False);
    }

    [Test]
    public void DeletedTargetsAndConditionsAreErrorsButCyclesAreWarnings()
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards[0].yes.defaultTarget = To(0);
        dataset.cards[0].no.defaultTarget.type = CardTargetType.EndGame;
        var validation = CardDatasetValidator.Validate(dataset);
        Assert.That(validation.IsValid, Is.True);
        Assert.That(validation.Warnings.Any(w => w.Contains("döngü")), Is.True);
        Assert.That(validation.Warnings.Any(w => w.Contains("Ulaşılamayan")), Is.True);
        dataset.cards[0].yes.rules.Add(When(4, CardAnswer.Yes, 4));
        dataset.cards.RemoveAt(4);
        validation = CardDatasetValidator.Validate(dataset);
        Assert.That(validation.Errors.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void DuplicateAndReorderPreserveReferencesAndSupportUndo()
    {
        dataset.cards[0].yes.defaultTarget = To(1);
        string targetId = dataset.cards[1].id;
        int copyIndex = CardDatasetEditing.Duplicate(dataset, 0);
        var copy = dataset.cards[copyIndex];
        Assert.That(copy.id, Is.Not.EqualTo(dataset.cards[0].id));
        Assert.That(copy.yes.defaultTarget.cardId, Is.EqualTo(targetId));
        copy.yes.label = "Copy only";
        Assert.That(dataset.cards[0].yes.label, Is.EqualTo("Evet"));
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(dataset.cards.Count, Is.EqualTo(5));
        var serialized = new SerializedObject(dataset);
        serialized.FindProperty("cards").MoveArrayElement(1, 4);
        serialized.ApplyModifiedProperties();
        Assert.That(dataset.cards[0].yes.defaultTarget.cardId, Is.EqualTo(targetId));
        Assert.That(CardDatasetValidator.Validate(dataset).IsValid, Is.True);
    }

    [Test]
    public void DeleteDoesNotSilentlyRewriteIncomingReferences()
    {
        dataset.flowMode = CardFlowMode.Branching;
        dataset.cards[0].yes.defaultTarget = To(1);
        CardDatasetEditing.Delete(dataset, 1);
        Assert.That(CardDatasetValidator.Validate(dataset).IsValid, Is.False);
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(CardDatasetValidator.Validate(dataset).IsValid, Is.True);
    }

    [TestCase("Assets/Data/Cards/DefaultSequential.asset", CardFlowMode.Sequential, 1)]
    [TestCase("Assets/Data/Cards/ButterflyExample.asset", CardFlowMode.Branching, 5)]
    public void ShippedDatasetsImportAndValidate(string path, CardFlowMode mode, int count)
    {
        var asset = AssetDatabase.LoadAssetAtPath<CardDataset>(path);
        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.flowMode, Is.EqualTo(mode));
        Assert.That(asset.cards.Count(card => card.role == CardRole.Normal), Is.EqualTo(count));
        Assert.That(asset.viewPrefab, Is.Not.Null);
        Assert.That(asset.cards.Count(card => card.role == CardRole.Start), Is.EqualTo(1));
        Assert.That(asset.cards.Count(card => card.role == CardRole.End), Is.EqualTo(1));
        Assert.That(asset.cards.Count(card => card.role == CardRole.Settings), Is.EqualTo(1));
        var validation = CardDatasetValidator.Validate(asset);
        Assert.That(validation.Errors, Is.Empty);
        Assert.That(validation.Warnings, Is.Empty);
    }
}
