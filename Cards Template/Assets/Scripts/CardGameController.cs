using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>All card roles are records in one dataset and use the same view factory.</summary>
public class CardGameController : MonoBehaviour
{
    public CardDataset dataset;
    public RectTransform spawnPoint;

    public DragCard CurrentCard { get; private set; }
    public CardSession Session { get; private set; }
    public event Action<DragCard> CurrentCardChanged;
    public event Action GameCompleted;

    private readonly List<CardView> pool = new List<CardView>(2);
    private CardView baseView;
    private CardView overlayView;
    private GameObject poolTemplate;
    private Transform inactiveStorage;
    private bool initialized;
    private bool completionReported;
    private bool loading;
    private bool destroying;
    private int generation;
    private RectTransform poolParent;

    private void Start()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBackgroundMusic();
        if (!initialized) LoadDataset(dataset);
    }

    public bool LoadDataset(CardDataset nextDataset)
    {
        if (loading || destroying) return false;
        var validation = CardDatasetValidator.Validate(nextDataset);
        if (!validation.IsValid)
        {
            Debug.LogError("[Cards] " + string.Join("\n", validation.Errors), this);
            return false;
        }
        var template = nextDataset.viewPrefab != null ? nextDataset.viewPrefab.GetComponent<CardView>() : null;
        if (spawnPoint == null || spawnPoint.GetComponentInParent<Canvas>() == null || template == null)
        {
            Debug.LogError("[Cards] Canvas altında Spawn Point ve CardView içeren ortak görünüm atanmalı.", this);
            return false;
        }
        var presentationErrors = template.ValidateConfiguration(nextDataset);
        if (presentationErrors.Count > 0)
        {
            Debug.LogError("[Cards] " + string.Join("\n", presentationErrors), this);
            return false;
        }
        CardSession nextSession;
        var prepared = new List<CardView>(2);
        Transform preparedStorage = null;
        bool replacePool = poolTemplate != nextDataset.viewPrefab || poolParent != spawnPoint ||
            pool.Count != 2 || pool.Exists(view => view == null);
        loading = true;
        try
        {
            nextSession = new CardSession(nextDataset);
            if (replacePool)
            {
                var storage = new GameObject("Inactive Card Storage", typeof(RectTransform));
                storage.SetActive(false);
                storage.transform.SetParent(spawnPoint, false);
                preparedStorage = storage.transform;
                for (int i = 0; i < 2; i++)
                    prepared.Add(CreateView(nextDataset.viewPrefab, preparedStorage, "Card View " + (i + 1)));
                prepared[0].Bind(nextSession.StartCard ?? nextSession.Current);
            }
        }
        catch (Exception error)
        {
            foreach (var view in prepared) DisposeView(view);
            if (preparedStorage != null) Destroy(preparedStorage.gameObject);
            loading = false;
            Debug.LogError("[Cards] Veri seti hazırlanamadı: " + error.Message, this);
            return false;
        }
        generation++;
        ClearDisplay();
        if (replacePool)
        {
            DisposeViews();
            if (inactiveStorage != null) Destroy(inactiveStorage.gameObject);
            inactiveStorage = preparedStorage;
            pool.AddRange(prepared);
            poolTemplate = nextDataset.viewPrefab;
            poolParent = spawnPoint;
        }
        dataset = nextDataset;
        Session = nextSession;
        completionReported = false;
        initialized = true;
        loading = false;
        foreach (string warning in validation.Warnings) Debug.LogWarning("[Cards] " + warning, this);
        ShowMain(Session.StartCard ?? Session.Current);
        return true;
    }

    public void RestartGame() => LoadDataset(dataset);

    private CardView CreateView(GameObject template, Transform storage, string viewName)
    {
        // Instantiate under an inactive parent so no role controls run before the record is bound.
        var instance = Instantiate(template, storage, false);
        instance.SetActive(false);
        instance.name = viewName;
        ((RectTransform)instance.transform).anchoredPosition = Vector2.zero;
        var view = instance.GetComponent<CardView>();
        view.Drag.Swiped += HandleSwipe;
        view.Drag.ExitCompleted += HandleExit;
        return view;
    }

    private CardView CreateView(string viewName) => CreateView(poolTemplate, inactiveStorage, viewName);

    private void ShowMain(CardEntry entry)
    {
        if (entry == null)
        {
            baseView = null;
            SetCurrent(null);
            return;
        }
        var view = pool.Find(candidate => !candidate.gameObject.activeSelf);
        if (view == null) throw new InvalidOperationException("Card pool is busy.");
        Activate(view, entry);
        baseView = view;
        RefreshInput();
        SetCurrent(view);
    }

    private void Activate(CardView view, CardEntry entry)
    {
        view.Clear();
        view.Bind(entry);
        view.transform.SetParent(spawnPoint, false);
        view.transform.SetAsLastSibling();
        view.gameObject.SetActive(true);
    }

    private void HandleSwipe(DragCard source, bool yes)
    {
        if (source != CurrentCard) return;
        var view = source.GetComponent<CardView>();
        if (view == overlayView)
        {
            SetCurrent(null);
            return;
        }
        if (view.Entry.role == CardRole.Start) ShowMain(Session.Current);
        else if (view.Entry.role == CardRole.Normal)
        {
            Session.Choose(yes ? CardAnswer.Yes : CardAnswer.No);
            bool reportCompletion = Session.IsFinished && !completionReported;
            if (reportCompletion) completionReported = true;
            ShowMain(Session.Current ?? Session.EndCard);
            // A display listener may already have loaded a new session. This completion
            // belongs to the session that accepted the answer, independently of that load.
            if (reportCompletion) GameCompleted?.Invoke();
        }
    }

    private void HandleExit(DragCard source)
    {
        int version = generation;
        var view = source.GetComponent<CardView>();
        view.gameObject.SetActive(false);
        view.Clear();
        if (view == overlayView)
        {
            if (baseView != null) baseView.Drag.SetVisible(true);
            SetCurrent(baseView);
        }
        if (version == generation) RefreshInput();
    }

    private bool HasExitingView()
    {
        foreach (var view in pool) if (view != null && view.Drag.IsExiting) return true;
        return overlayView != null && overlayView.Drag.IsExiting;
    }

    private void RefreshInput()
    {
        if (baseView != null)
            baseView.Drag.SetInteraction((overlayView == null || !overlayView.gameObject.activeSelf) && !HasExitingView());
    }

    private void SetCurrent(CardView view)
    {
        CurrentCard = view != null ? view.Drag : null;
        CurrentCardChanged?.Invoke(CurrentCard);
    }

    public void SpawnOptionsCard()
    {
        if (Session?.SettingsCard == null)
        {
            Debug.LogWarning("[Cards] Veri setinde Settings türünde kart bulunamadı.", this);
            return;
        }
        ShowOverlay(Session.SettingsCard);
    }

    public void SpawnCheatCard(int cheatId)
    {
        var entry = Session?.GetCheatCard(cheatId);
        if (entry == null)
        {
            Debug.LogWarning("[Cards] Veri setinde hile kartı bulunamadı: " + cheatId, this);
            return;
        }
        ShowOverlay(entry);
    }

    private void ShowOverlay(CardEntry entry)
    {
        if (baseView == null || !baseView.Drag.IsReady || HasExitingView() ||
            (overlayView != null && overlayView.gameObject.activeSelf)) return;
        if (overlayView == null) overlayView = CreateView("Overlay Card View");
        baseView.Drag.SetVisible(false);
        baseView.Drag.SetInteraction(false);
        Activate(overlayView, entry);
        SetCurrent(overlayView);
    }

    private void ClearDisplay()
    {
        CurrentCard = null;
        baseView = null;
        foreach (var view in pool) Deactivate(view);
        Deactivate(overlayView);
    }

    private static void Deactivate(CardView view)
    {
        if (view == null) return;
        view.gameObject.SetActive(false);
        view.Clear();
    }

    private void DisposeViews()
    {
        foreach (var view in pool) DisposeView(view);
        pool.Clear();
        DisposeView(overlayView);
        overlayView = null;
    }

    private void DisposeView(CardView view)
    {
        if (view == null) return;
        view.Drag.Swiped -= HandleSwipe;
        view.Drag.ExitCompleted -= HandleExit;
        Destroy(view.gameObject);
    }

    private void OnDestroy()
    {
        destroying = true;
        generation++;
        ClearDisplay();
        DisposeViews();
        if (inactiveStorage != null) Destroy(inactiveStorage.gameObject);
    }
}
