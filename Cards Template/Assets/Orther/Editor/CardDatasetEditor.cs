using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(CardDataset))]
public class CardDatasetEditor : Editor
{
    private ReorderableList list;
    private string search = "";
    private int selected;
    [SerializeField] private string selectedId;
    private int roleFilter;
    private int issueFilter;
    private CardValidationResult validation;
    private CardDataset Dataset => (CardDataset)target;

    private void OnEnable()
    {
        list = new ReorderableList(serializedObject, serializedObject.FindProperty("cards"), true, true, true, true);
        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Kartlar (sürükleyerek sıralayın)");
        list.drawElementCallback = (rect, index, active, focused) =>
        {
            var card = list.serializedProperty.GetArrayElementAtIndex(index);
            var issue = validation?.Issues.Find(item => item.CardIndex == index && item.Severity == CardIssueSeverity.Error) ??
                validation?.Issues.Find(item => item.CardIndex == index);
            if (issue != null)
                GUI.Label(new Rect(rect.xMax - 20, rect.y, 20, rect.height),
                    EditorGUIUtility.IconContent(issue.Severity == CardIssueSeverity.Error ? "console.erroricon.sml" : "console.warnicon.sml"));
            rect.width -= 22;
            var role = card.FindPropertyRelative("role");
            int roleIndex = role.enumValueIndex;
            string roleName = roleIndex >= 0 && roleIndex < role.enumDisplayNames.Length ? role.enumDisplayNames[roleIndex] : "Geçersiz tür";
            EditorGUI.LabelField(rect, (index + 1) + ". [" + roleName + "] " + card.FindPropertyRelative("title").stringValue);
        };
        list.onSelectCallback = value => SelectIndex(value.index);
        list.onReorderCallback = value => SelectIndex(value.index);
        list.onAddCallback = value =>
        {
            serializedObject.ApplyModifiedProperties();
            selected = CardDatasetEditing.Add(Dataset);
            serializedObject.Update();
            list.index = selected;
            SelectIndex(selected);
        };
        list.onRemoveCallback = value =>
        {
            if (value.index < 0) return;
            serializedObject.ApplyModifiedProperties();
            CardDatasetEditing.Delete(Dataset, value.index);
            selected = Mathf.Clamp(value.index - 1, 0, Dataset.cards.Count - 1);
            serializedObject.Update();
            list.index = selected;
            SelectIndex(selected);
        };
        ResolveSelection();
        Undo.undoRedoPerformed += HandleUndo;
    }

    private void OnDisable() => Undo.undoRedoPerformed -= HandleUndo;

    private void HandleUndo()
    {
        serializedObject.Update();
        ResolveSelection();
        Repaint();
    }

    private void SelectIndex(int index)
    {
        var cards = serializedObject.FindProperty("cards");
        selected = cards.arraySize == 0 ? -1 : Mathf.Clamp(index, 0, cards.arraySize - 1);
        list.index = selected;
        selectedId = selected < 0 ? null : cards.GetArrayElementAtIndex(selected).FindPropertyRelative("id").stringValue;
    }

    private void ResolveSelection()
    {
        var cards = serializedObject.FindProperty("cards");
        int index = -1;
        for (int i = 0; i < cards.arraySize; i++)
            if (!string.IsNullOrEmpty(selectedId) && cards.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == selectedId)
            { index = i; break; }
        SelectIndex(index >= 0 ? index : selected);
    }

    private void RefreshValidation()
    {
        validation = CardDatasetValidator.Validate(Dataset);
        var template = Dataset.viewPrefab != null ? Dataset.viewPrefab.GetComponent<CardView>() : null;
        if (template == null)
            validation.AddError("Ortak görünümde CardView bulunmalı.", code: "MissingView", propertyPath: "viewPrefab");
        else
            foreach (string error in template.ValidateConfiguration(Dataset))
                validation.AddError(error, code: "InvalidView", propertyPath: "viewPrefab");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ResolveSelection();
        RefreshValidation();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Veri seti adı"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("viewPrefab"), new GUIContent("Ortak görünüm"));
        if (Dataset.viewPrefab == null)
            EditorGUILayout.HelpBox("Ortak Card View prefabını bu veri setine bir kez atayın.", MessageType.Error);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("flowMode"), new GUIContent("Akış modu"));
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        RefreshValidation();
        EditorGUILayout.HelpBox("Bütün kartlar bu listeden ve tek ortak görünümden üretilir. Sequential yalnız Normal kartların sırasını kullanır; dallanma alanlarını okumaz.", MessageType.Info);
        search = EditorGUILayout.TextField("Kart ara", search);
        roleFilter = EditorGUILayout.Popup("Tür filtresi", roleFilter, new[] { "Tümü", "Normal", "Start", "End", "Settings", "Cheat" });
        issueFilter = EditorGUILayout.Popup("Doğrulama filtresi", issueFilter, new[] { "Tümü", "Hatalar", "Uyarılar" });
        if (string.IsNullOrWhiteSpace(search) && roleFilter == 0 && issueFilter == 0) list.DoLayoutList();
        else
        {
            for (int i = 0; i < Dataset.cards.Count; i++)
            {
                var entry = Dataset.cards[i];
                int index = i;
                bool matchesIssue = issueFilter == 0 || validation.Issues.Exists(issue =>
                    issue.CardIndex == index && issue.Severity == (issueFilter == 1 ? CardIssueSeverity.Error : CardIssueSeverity.Warning));
                if ((entry?.title ?? "(Boş kayıt)").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (roleFilter == 0 || (entry != null && (int)entry.role == roleFilter - 1)) && matchesIssue &&
                    GUILayout.Button((i + 1) + ". " + (entry?.title ?? "(Boş kayıt)")))
                    SelectIndex(i);
            }
            if (GUILayout.Button("Yeni kart ekle"))
            {
                serializedObject.ApplyModifiedProperties();
                selected = CardDatasetEditing.Add(Dataset);
                serializedObject.Update();
                list.index = selected;
                SelectIndex(selected);
            }
        }
        var cards = serializedObject.FindProperty("cards");
        selected = Mathf.Clamp(selected, 0, cards.arraySize - 1);
        if (cards.arraySize > 0)
        {
            var card = cards.GetArrayElementAtIndex(selected);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Seçili kart", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Sabit kimlik", card.FindPropertyRelative("id").stringValue);
            if (GUILayout.Button("Bu kartı çoğalt"))
            {
                serializedObject.ApplyModifiedProperties();
                selected = CardDatasetEditing.Duplicate(Dataset, selected);
                serializedObject.Update();
                list.index = selected;
                SelectIndex(selected);
                card = cards.GetArrayElementAtIndex(selected);
            }
            EditorGUILayout.PropertyField(card.FindPropertyRelative("title"), new GUIContent("Başlık"));
            EditorGUILayout.PropertyField(card.FindPropertyRelative("role"), new GUIContent("Kart türü"));
            var role = (CardRole)card.FindPropertyRelative("role").enumValueIndex;
            if (role == CardRole.Cheat)
                EditorGUILayout.PropertyField(card.FindPropertyRelative("cheatId"), new GUIContent("Hile numarası"));
            string cardId = card.FindPropertyRelative("id").stringValue;
            DrawIncoming(cardId);
            ShowIssues(cardId, CardAnswer.Unanswered);
            if (role != CardRole.Settings)
            {
                EditorGUILayout.PropertyField(card.FindPropertyRelative("description"), new GUIContent("Açıklama"));
                EditorGUILayout.PropertyField(card.FindPropertyRelative("artwork"), new GUIContent("Görsel (isteğe bağlı)"));
            }
            if (role == CardRole.Normal || role == CardRole.Cheat)
            {
                bool transitions = role == CardRole.Normal &&
                    serializedObject.FindProperty("flowMode").intValue == (int)CardFlowMode.Branching;
                DrawChoice(card.FindPropertyRelative("yes"), "Sağa / Evet", cardId, CardAnswer.Yes, transitions);
                DrawChoice(card.FindPropertyRelative("no"), "Sola / Hayır", cardId, CardAnswer.No, transitions);
            }
        }
        serializedObject.ApplyModifiedProperties();
        RefreshValidation();
        foreach (var issue in validation.Issues)
        {
            EditorGUILayout.HelpBox(issue.Message, issue.Severity == CardIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
            if (issue.CardIndex >= 0 && GUILayout.Button("İlgili kart / kurala git"))
                Navigate(issue.CardIndex, issue.PropertyPath);
        }
        if (validation.IsValid && validation.Warnings.Count == 0)
            EditorGUILayout.HelpBox("Veri seti geçerli.", MessageType.Info);
    }

    private void DrawIncoming(string cardId)
    {
        var references = CardDatasetEditing.GetIncomingReferences(Dataset, cardId);
        EditorGUILayout.LabelField("Gelen bağlantılar: " + references.Count, EditorStyles.boldLabel);
        if (references.Count == 0) return;
        EditorGUILayout.HelpBox("Silme veya Normal türünden çıkarma bu bağlantıları bozabilir. Hedefler otomatik değiştirilmez; Undo desteklenir.", MessageType.Warning);
        foreach (var reference in references)
            if (GUILayout.Button(reference.Message)) Navigate(reference.CardIndex, reference.PropertyPath);
    }

    private void Navigate(int index, string propertyPath)
    {
        SelectIndex(index);
        search = "";
        roleFilter = issueFilter = 0;
        string parent = propertyPath;
        while (!string.IsNullOrEmpty(parent))
        {
            var property = serializedObject.FindProperty(parent);
            if (property != null) property.isExpanded = true;
            int dot = parent.LastIndexOf('.');
            parent = dot < 0 ? "" : parent.Substring(0, dot);
        }
        Repaint();
        GUIUtility.ExitGUI();
    }

    private void ShowIssues(string cardId, CardAnswer side)
    {
        foreach (var issue in validation.Issues)
            if (issue.CardIndex == selected && issue.Choice == side)
                EditorGUILayout.HelpBox(issue.Message, issue.Severity == CardIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
    }

    private void DrawChoice(SerializedProperty choice, string heading, string cardId, CardAnswer side, bool transitions)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(choice.FindPropertyRelative("label"), new GUIContent("Seçenek metni"));
        if (transitions)
        {
            DrawTarget(choice.FindPropertyRelative("defaultTarget"), "Varsayılan geçiş");
            var rules = choice.FindPropertyRelative("rules");
            EditorGUILayout.LabelField("Koşullu geçişler (ilk eşleşen uygulanır)");
            for (int i = 0; i < rules.arraySize; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded, "Kural " + (i + 1), true);
                if (GUILayout.Button("↑", GUILayout.Width(26)) && i > 0) rules.MoveArrayElement(i, i - 1);
                if (GUILayout.Button("↓", GUILayout.Width(26)) && i + 1 < rules.arraySize) rules.MoveArrayElement(i, i + 1);
                bool remove = GUILayout.Button("Sil", GUILayout.Width(36));
                EditorGUILayout.EndHorizontal();
                if (remove)
                {
                    rules.DeleteArrayElementAtIndex(i--);
                    EditorGUILayout.EndVertical();
                    continue;
                }
                if (rule.isExpanded)
                {
                    var conditions = rule.FindPropertyRelative("conditions");
                    EditorGUILayout.LabelField("Aşağıdaki koşulların tümü sağlanmalı:");
                    for (int j = 0; j < conditions.arraySize; j++)
                    {
                        var condition = conditions.GetArrayElementAtIndex(j);
                        DrawCardPicker(condition.FindPropertyRelative("cardId"), "Geçmiş kart");
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(condition.FindPropertyRelative("answer"), new GUIContent("Cevap"));
                        if (GUILayout.Button("Sil", GUILayout.Width(36))) conditions.DeleteArrayElementAtIndex(j--);
                        EditorGUILayout.EndHorizontal();
                    }
                    if (GUILayout.Button("Koşul ekle"))
                    {
                        int index = conditions.arraySize++;
                        var condition = conditions.GetArrayElementAtIndex(index);
                        condition.FindPropertyRelative("cardId").stringValue = "";
                        condition.FindPropertyRelative("answer").enumValueIndex = (int)CardAnswer.Yes;
                    }
                    DrawTarget(rule.FindPropertyRelative("target"), "Eşleşince");
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("Koşullu geçiş ekle"))
            {
                int index = rules.arraySize++;
                var rule = rules.GetArrayElementAtIndex(index);
                rule.isExpanded = true;
                rule.FindPropertyRelative("conditions").ClearArray();
                var target = rule.FindPropertyRelative("target");
                target.FindPropertyRelative("type").enumValueIndex = 0;
                target.FindPropertyRelative("cardId").stringValue = "";
            }
        }
        if (transitions) ShowIssues(cardId, side);
        EditorGUILayout.EndVertical();
    }

    private void DrawTarget(SerializedProperty target, string label)
    {
        EditorGUILayout.PropertyField(target.FindPropertyRelative("type"), new GUIContent(label));
        if (target.FindPropertyRelative("type").enumValueIndex == (int)CardTargetType.Card)
            DrawCardPicker(target.FindPropertyRelative("cardId"), "Hedef kart");
    }

    private void DrawCardPicker(SerializedProperty id, string label)
    {
        var labels = new List<string> { "(Kart seçin)" };
        var ids = new List<string> { "" };
        for (int i = 0; i < Dataset.cards.Count; i++)
        {
            if (Dataset.cards[i] == null || Dataset.cards[i].role != CardRole.Normal) continue;
            labels.Add((i + 1) + ". " + Dataset.cards[i].title);
            ids.Add(Dataset.cards[i].id);
        }
        int current = ids.IndexOf(id.stringValue);
        if (current < 0)
        {
            current = ids.Count;
            ids.Add(id.stringValue);
            labels.Add("(Silinmiş/geçersiz kart: " + id.stringValue + ")");
        }
        int next = EditorGUILayout.Popup(label, current, labels.ToArray());
        if (next != current) id.stringValue = ids[next];
    }
}
