using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class CardIncomingReference
{
    public int CardIndex { get; }
    public string PropertyPath { get; }
    public string Message { get; }
    public CardIncomingReference(int cardIndex, string propertyPath, string message)
    { CardIndex = cardIndex; PropertyPath = propertyPath; Message = message; }
}

public static class CardDatasetEditing
{
    public static List<CardIncomingReference> GetIncomingReferences(CardDataset dataset, string cardId)
    {
        var references = new List<CardIncomingReference>();
        if (dataset?.cards == null || string.IsNullOrEmpty(cardId)) return references;
        for (int i = 0; i < dataset.cards.Count; i++)
        {
            var card = dataset.cards[i];
            if (card == null) continue;
            Collect(card.yes, i, "yes", card.title, cardId, references);
            Collect(card.no, i, "no", card.title, cardId, references);
        }
        return references;
    }

    private static void Collect(CardChoice choice, int index, string side, string title, string cardId,
        List<CardIncomingReference> references)
    {
        if (choice == null) return;
        string path = "cards.Array.data[" + index + "]." + side;
        string label = title + " / " + (side == "yes" ? "Evet" : "Hayır");
        if (choice.defaultTarget?.type == CardTargetType.Card && choice.defaultTarget.cardId == cardId)
            references.Add(new CardIncomingReference(index, path + ".defaultTarget.cardId", label + " / Varsayılan hedef"));
        if (choice.rules == null) return;
        for (int r = 0; r < choice.rules.Count; r++)
        {
            var rule = choice.rules[r];
            if (rule == null) continue;
            string rulePath = path + ".rules.Array.data[" + r + "]";
            if (rule.target?.type == CardTargetType.Card && rule.target.cardId == cardId)
                references.Add(new CardIncomingReference(index, rulePath + ".target.cardId", label + " / Kural " + (r + 1) + " hedefi"));
            if (rule.conditions == null) continue;
            for (int c = 0; c < rule.conditions.Count; c++)
                if (rule.conditions[c]?.cardId == cardId)
                    references.Add(new CardIncomingReference(index, rulePath + ".conditions.Array.data[" + c + "].cardId",
                        label + " / Kural " + (r + 1) + " geçmiş koşulu"));
        }
    }

    public static int Add(CardDataset dataset)
    {
        Undo.RecordObject(dataset, "Kart ekle");
        dataset.cards.Add(new CardEntry());
        EditorUtility.SetDirty(dataset);
        return dataset.cards.Count - 1;
    }

    public static int Duplicate(CardDataset dataset, int index)
    {
        Undo.RecordObject(dataset, "Kart çoğalt");
        var copy = dataset.cards[index].Copy(true);
        copy.title += " (Kopya)";
        if (copy.role != CardRole.Normal && copy.role != CardRole.Cheat)
        {
            copy.role = CardRole.Normal;
            copy.yes = new CardChoice { label = "Evet" };
            copy.no = new CardChoice { label = "Hayır" };
        }
        if (copy.role == CardRole.Cheat)
        {
            int candidate = 0;
            while (dataset.cards.Exists(card => card != null && card.role == CardRole.Cheat && card.cheatId == candidate)) candidate++;
            copy.cheatId = candidate;
        }
        dataset.cards.Insert(index + 1, copy);
        EditorUtility.SetDirty(dataset);
        return index + 1;
    }

    public static void Delete(CardDataset dataset, int index)
    {
        Undo.RecordObject(dataset, "Kart sil");
        dataset.cards.RemoveAt(index);
        EditorUtility.SetDirty(dataset);
    }
}

