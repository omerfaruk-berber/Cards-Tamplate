using System;
using System.Collections.Generic;

/// <summary>Snapshot of one dataset. Special records never enter the normal-card sequence.</summary>
public sealed class CardSession
{
    private readonly List<CardEntry> cards = new List<CardEntry>();
    private readonly Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, CardAnswer> answers = new Dictionary<string, CardAnswer>(StringComparer.Ordinal);
    private readonly Dictionary<int, CardEntry> cheats = new Dictionary<int, CardEntry>();
    private readonly CardFlowMode mode;
    private readonly CardEntry startCard;
    private readonly CardEntry endCard;
    private readonly CardEntry settingsCard;
    private int currentIndex;

    private CardEntry CurrentEntry => currentIndex < cards.Count ? cards[currentIndex] : null;
    public CardEntry Current => CurrentEntry?.Copy();
    public CardEntry StartCard => startCard?.Copy();
    public CardEntry EndCard => endCard?.Copy();
    public CardEntry SettingsCard => settingsCard?.Copy();
    public bool IsFinished => currentIndex >= cards.Count;
    public int AnsweredCardCount => answers.Count;

    public CardSession(CardDataset dataset)
    {
        var validation = CardDatasetValidator.Validate(dataset);
        if (!validation.IsValid) throw new ArgumentException(string.Join("\n", validation.Errors), nameof(dataset));
        mode = dataset.flowMode;
        foreach (var source in dataset.cards)
        {
            var entry = source.Copy();
            switch (entry.role)
            {
                case CardRole.Start: startCard = entry; break;
                case CardRole.End: endCard = entry; break;
                case CardRole.Settings: settingsCard = entry; break;
                case CardRole.Cheat: cheats.Add(entry.cheatId, entry); break;
                default:
                    indices.Add(entry.id, cards.Count);
                    cards.Add(entry);
                    break;
            }
        }
    }

    public CardEntry GetCheatCard(int cheatId) => cheats.TryGetValue(cheatId, out var card) ? card.Copy() : null;
    public CardAnswer GetAnswer(string cardId) =>
        cardId != null && answers.TryGetValue(cardId, out var answer) ? answer : CardAnswer.Unanswered;

    public CardEntry Choose(CardAnswer answer)
    {
        if (answer != CardAnswer.Yes && answer != CardAnswer.No) throw new ArgumentOutOfRangeException(nameof(answer));
        if (IsFinished) return null;
        var card = CurrentEntry;
        answers[card.id] = answer;
        // Sequential datasets never read unused branching configuration, even if incomplete.
        if (mode == CardFlowMode.Sequential)
        {
            currentIndex++;
            return Current;
        }
        var choice = answer == CardAnswer.Yes ? card.yes : card.no;
        var target = choice.defaultTarget;
        foreach (var rule in choice.rules)
        {
            bool matches = true;
            foreach (var condition in rule.conditions)
                if (GetAnswer(condition.cardId) != condition.answer) { matches = false; break; }
            if (matches) { target = rule.target; break; }
        }
        if (target.type == CardTargetType.NextInList) currentIndex++;
        else if (target.type == CardTargetType.EndGame) currentIndex = cards.Count;
        else currentIndex = indices[target.cardId];
        return Current;
    }

    public void Restart()
    {
        answers.Clear();
        currentIndex = 0;
    }
}
