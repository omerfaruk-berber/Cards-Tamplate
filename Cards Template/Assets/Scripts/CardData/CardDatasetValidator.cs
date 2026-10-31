using System;
using System.Collections.Generic;

public enum CardIssueSeverity { Error, Warning }

public sealed class CardValidationIssue
{
    public string CardId { get; }
    public CardAnswer Choice { get; }
    public string Message { get; }
    public CardIssueSeverity Severity { get; }
    public string Code { get; }
    public int CardIndex { get; }
    public string PropertyPath { get; }
    public CardValidationIssue(string cardId, CardAnswer choice, string message,
        CardIssueSeverity severity = CardIssueSeverity.Error, string code = "InvalidData",
        int cardIndex = -1, string propertyPath = "")
    {
        CardId = cardId; Choice = choice; Message = message;
        Severity = severity; Code = code; CardIndex = cardIndex; PropertyPath = propertyPath;
    }
}

public sealed class CardValidationResult
{
    public readonly List<string> Errors = new List<string>();
    public readonly List<string> Warnings = new List<string>();
    public readonly List<CardValidationIssue> Issues = new List<CardValidationIssue>();
    public bool IsValid => Errors.Count == 0;
    public void AddError(string message, CardEntry card = null, CardAnswer choice = CardAnswer.Unanswered,
        string code = "InvalidData", int cardIndex = -1, string propertyPath = "")
    {
        Errors.Add(message);
        Issues.Add(new CardValidationIssue(card?.id, choice, message, CardIssueSeverity.Error, code, cardIndex, propertyPath));
    }
    public void AddWarning(string message, CardEntry card = null, CardAnswer choice = CardAnswer.Unanswered,
        string code = "Warning", int cardIndex = -1, string propertyPath = "")
    {
        Warnings.Add(message);
        Issues.Add(new CardValidationIssue(card?.id, choice, message, CardIssueSeverity.Warning, code, cardIndex, propertyPath));
    }
}

public static class CardDatasetValidator
{
    public static CardValidationResult Validate(CardDataset dataset)
    {
        var result = new CardValidationResult();
        if (dataset == null || dataset.cards == null || dataset.cards.Count == 0)
        {
            result.AddError("Veri setinde en az bir normal kart bulunmalı.", code: "MissingNormal", propertyPath: "cards");
            return result;
        }
        if (!Enum.IsDefined(typeof(CardFlowMode), dataset.flowMode))
            result.AddError("Geçersiz akış modu.", code: "InvalidFlow", propertyPath: "flowMode");
        var allIds = new HashSet<string>(StringComparer.Ordinal);
        var uniqueRoles = new HashSet<CardRole>();
        var cheatIds = new HashSet<int>();
        var normals = new List<CardEntry>();
        var sourceIndices = new List<int>();
        var normalIds = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int sourceIndex = 0; sourceIndex < dataset.cards.Count; sourceIndex++)
        {
            var card = dataset.cards[sourceIndex];
            string path = CardPath(sourceIndex);
            if (card == null) { result.AddError("Kart listesinde boş kayıt var.", code: "NullCard", cardIndex: sourceIndex, propertyPath: path); continue; }
            if (string.IsNullOrWhiteSpace(card.id)) result.AddError(card.title + ": kart kimliği eksik.", card, code: "MissingId", cardIndex: sourceIndex, propertyPath: path + ".id");
            else if (!allIds.Add(card.id)) result.AddError(card.title + ": tekrarlanan kart kimliği.", card, code: "DuplicateId", cardIndex: sourceIndex, propertyPath: path + ".id");
            if (!Enum.IsDefined(typeof(CardRole), card.role))
                result.AddError(card.title + ": geçersiz kart türü.", card, code: "InvalidRole", cardIndex: sourceIndex, propertyPath: path + ".role");
            else if (card.role == CardRole.Normal)
            {
                if (!string.IsNullOrEmpty(card.id) && !normalIds.ContainsKey(card.id))
                    normalIds.Add(card.id, normals.Count);
                normals.Add(card);
                sourceIndices.Add(sourceIndex);
            }
            else if (card.role == CardRole.Cheat)
            {
                if (!cheatIds.Add(card.cheatId))
                    result.AddError(card.title + ": hile kartı numarası başka bir kartta kullanılıyor.", card, code: "DuplicateCheatId", cardIndex: sourceIndex, propertyPath: path + ".cheatId");
            }
            else if (!uniqueRoles.Add(card.role))
                result.AddError(card.title + ": bu türden en fazla bir kart olabilir (" + card.role + ").", card, code: "DuplicateRole", cardIndex: sourceIndex, propertyPath: path + ".role");
        }
        if (normals.Count == 0) result.AddError("Veri setinde en az bir normal kart bulunmalı.", code: "MissingNormal", propertyPath: "cards");

        // Branch targets on sequential and non-normal records are intentionally ignored.
        if (dataset.flowMode != CardFlowMode.Branching) return result;
        var edges = new List<HashSet<int>>();
        for (int i = 0; i < normals.Count; i++) edges.Add(new HashSet<int>());
        for (int i = 0; i < normals.Count; i++)
        {
            ValidateChoice(normals[i], CardAnswer.Yes, i, sourceIndices[i], normals, normalIds, edges[i], result);
            ValidateChoice(normals[i], CardAnswer.No, i, sourceIndices[i], normals, normalIds, edges[i], result);
        }
        if (result.IsValid) ValidateGraph(normals, sourceIndices, edges, result);
        return result;
    }

    private static string CardPath(int index) => "cards.Array.data[" + index + "]";

    private static void ValidateChoice(CardEntry card, CardAnswer side, int index, int sourceIndex, List<CardEntry> normals,
        Dictionary<string, int> ids, HashSet<int> edges, CardValidationResult result)
    {
        var choice = side == CardAnswer.Yes ? card.yes : card.no;
        string context = card.title + ", " + (side == CardAnswer.Yes ? "Evet" : "Hayır");
        string path = CardPath(sourceIndex) + (side == CardAnswer.Yes ? ".yes" : ".no");
        if (choice == null || choice.rules == null)
        {
            result.AddError(context + ": seçenek veya kural listesi eksik.", card, side, "MissingChoice", sourceIndex, path);
            return;
        }
        var defaultEdges = new HashSet<int>();
        ValidateTarget(choice.defaultTarget, card, side, index, sourceIndex, normals.Count, ids, defaultEdges, result, path + ".defaultTarget");
        var earlier = new List<Dictionary<string, CardAnswer>>();
        bool guaranteedMatch = false;
        for (int r = 0; r < choice.rules.Count; r++)
        {
            var rule = choice.rules[r];
            string rulePath = path + ".rules.Array.data[" + r + "]";
            if (rule == null || rule.conditions == null || rule.conditions.Count == 0)
            {
                result.AddError(context + ": her kuralda en az bir koşul olmalı.", card, side, "MissingConditions", sourceIndex, rulePath);
                continue;
            }
            bool possible = true;
            var predicates = new Dictionary<string, CardAnswer>(StringComparer.Ordinal);
            for (int c = 0; c < rule.conditions.Count; c++)
            {
                var condition = rule.conditions[c];
                string conditionPath = rulePath + ".conditions.Array.data[" + c + "]";
                if (condition == null || string.IsNullOrEmpty(condition.cardId) ||
                    !ids.ContainsKey(condition.cardId) || !Enum.IsDefined(typeof(CardAnswer), condition.answer))
                {
                    result.AddError(context + ": koşul için geçerli bir normal kart ve cevap seçilmeli.", card, side,
                        "InvalidCondition", sourceIndex, conditionPath);
                    possible = false;
                    continue;
                }
                if (predicates.TryGetValue(condition.cardId, out var previous) && previous != condition.answer)
                {
                    result.AddWarning(context + ": aynı kart için çelişkili koşullar var; kural çalışamaz.", card, side,
                        "ContradictoryConditions", sourceIndex, conditionPath);
                    possible = false;
                }
                predicates[condition.cardId] = condition.answer;
                if (condition.cardId == card.id && condition.answer != side)
                {
                    result.AddWarning(context + ": mevcut cevap koşullardan önce kaydedilir; bu koşul eşleşemez.", card, side,
                        "ImpossibleCurrentAnswer", sourceIndex, conditionPath);
                    possible = false;
                }
            }
            var ruleEdges = new HashSet<int>();
            ValidateTarget(rule.target, card, side, index, sourceIndex, normals.Count, ids, ruleEdges, result, rulePath + ".target");
            if (!possible) continue;
            if (card.id != null) predicates.Remove(card.id);
            if (earlier.Exists(previous => Subsumes(previous, predicates)))
                result.AddWarning(context + ": önceki bir kural bu kuralın bütün eşleşmelerini kapsıyor.", card, side,
                    "ShadowedRule", sourceIndex, rulePath);
            else
            {
                edges.UnionWith(ruleEdges);
                earlier.Add(predicates);
                if (predicates.Count == 0) guaranteedMatch = true;
            }
        }
        if (!guaranteedMatch) edges.UnionWith(defaultEdges);
    }

    private static bool Subsumes(Dictionary<string, CardAnswer> earlier, Dictionary<string, CardAnswer> later)
    {
        foreach (var condition in earlier)
            if (!later.TryGetValue(condition.Key, out var answer) || answer != condition.Value) return false;
        return true;
    }

    private static void ValidateTarget(CardTarget target, CardEntry card, CardAnswer side, int index, int sourceIndex, int count,
        Dictionary<string, int> ids, HashSet<int> edges, CardValidationResult result, string path)
    {
        string context = card.title + ", " + (side == CardAnswer.Yes ? "Evet" : "Hayır");
        if (target == null || !Enum.IsDefined(typeof(CardTargetType), target.type))
            result.AddError(context + ": geçiş hedefi eksik veya geçersiz.", card, side, "InvalidTarget", sourceIndex, path);
        else if (target.type == CardTargetType.NextInList && index + 1 < count) edges.Add(index + 1);
        else if (target.type == CardTargetType.Card)
        {
            if (string.IsNullOrEmpty(target.cardId) || !ids.TryGetValue(target.cardId, out int destination))
                result.AddError(context + ": hedef normal kart seçilmemiş veya silinmiş.", card, side, "MissingTarget", sourceIndex, path + ".cardId");
            else edges.Add(destination);
        }
    }

    private static void ValidateGraph(List<CardEntry> cards, List<int> sourceIndices, List<HashSet<int>> edges, CardValidationResult result)
    {
        var visited = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(0);
        while (pending.Count > 0)
        {
            int index = pending.Pop();
            if (!visited.Add(index)) continue;
            foreach (int next in edges[index]) pending.Push(next);
        }
        for (int i = 0; i < cards.Count; i++)
            if (!visited.Contains(i)) result.AddWarning("Ulaşılamayan kart (olası bağlantılara göre): " + cards[i].title,
                cards[i], code: "UnreachableCard", cardIndex: sourceIndices[i], propertyPath: CardPath(sourceIndices[i]));
        var incoming = new int[edges.Count];
        foreach (var destinations in edges) foreach (int destination in destinations) incoming[destination]++;
        var ready = new Queue<int>();
        for (int i = 0; i < incoming.Length; i++) if (incoming[i] == 0) ready.Enqueue(i);
        int removed = 0;
        while (ready.Count > 0)
        {
            int current = ready.Dequeue();
            removed++;
            foreach (int destination in edges[current]) if (--incoming[destination] == 0) ready.Enqueue(destination);
        }
        if (removed < edges.Count)
        {
            int index = Array.FindIndex(incoming, value => value > 0);
            result.AddWarning("Kart bağlantılarında olası döngü var. Tekrar ziyaretlerde en son cevap geçerlidir.",
                cards[index], code: "PossibleCycle", cardIndex: sourceIndices[index], propertyPath: CardPath(sourceIndices[index]));
        }
    }
}
