using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardFlowMode { Sequential, Branching }
public enum CardAnswer { Unanswered, No, Yes }
public enum CardTargetType { NextInList, Card, EndGame }
public enum CardRole { Normal, Start, End, Settings, Cheat }

[Serializable]
public class CardTarget
{
    public CardTargetType type;
    public string cardId;
    public CardTarget Copy() => new CardTarget { type = type, cardId = cardId };
}

[Serializable]
public class CardCondition
{
    public string cardId;
    public CardAnswer answer = CardAnswer.Yes;
    public CardCondition Copy() => new CardCondition { cardId = cardId, answer = answer };
}

[Serializable]
public class CardTransitionRule
{
    public List<CardCondition> conditions = new List<CardCondition>();
    public CardTarget target = new CardTarget();

    public CardTransitionRule Copy()
    {
        var copy = new CardTransitionRule { target = target?.Copy() };
        copy.conditions = conditions?.ConvertAll(condition => condition?.Copy());
        return copy;
    }
}

[Serializable]
public class CardChoice
{
    public string label;
    public CardTarget defaultTarget = new CardTarget();
    public List<CardTransitionRule> rules = new List<CardTransitionRule>();

    public CardChoice Copy()
    {
        var copy = new CardChoice { label = label, defaultTarget = defaultTarget?.Copy() };
        copy.rules = rules?.ConvertAll(rule => rule?.Copy());
        return copy;
    }
}

[Serializable]
public class CardEntry
{
    [HideInInspector] public string id = Guid.NewGuid().ToString("N");
    public CardRole role;
    public int cheatId;
    public string title = "Yeni kart";
    [TextArea(3, 8)] public string description;
    public Sprite artwork;
    public CardChoice yes = new CardChoice { label = "Evet" };
    public CardChoice no = new CardChoice { label = "Hayır" };

    public CardEntry Copy(bool newIdentity = false) => new CardEntry
    {
        id = newIdentity ? Guid.NewGuid().ToString("N") : id,
        role = role,
        cheatId = cheatId,
        title = title,
        description = description,
        artwork = artwork,
        yes = yes?.Copy(),
        no = no?.Copy()
    };
}

public class CardDataset : ScriptableObject
{
    public GameObject viewPrefab;
    [HideInInspector] public int schemaVersion;
    public string displayName = "Yeni veri seti";
    public CardFlowMode flowMode;
    public List<CardEntry> cards = new List<CardEntry>();

    private void OnValidate()
    {
        if (cards == null) return;
        // Only missing IDs are generated. Duplicate IDs must be reported, never silently rewritten.
        foreach (var card in cards)
            if (card != null && string.IsNullOrWhiteSpace(card.id))
                card.id = Guid.NewGuid().ToString("N");
    }
}
