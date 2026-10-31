using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class DragCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Min(1)] public float swipeThreshold = 150f;
    public float swipeSpeed = 1000f; // Retained for existing prefab compatibility.
    [Min(0)] public float returnAnimationDuration = 0.5f;
    [Min(0)] public float exitAnimationDuration = 0.6f;
    [Min(0)] public float entranceAnimationDuration = 0.6f;
    public TextMeshProUGUI yesText;
    public TextMeshProUGUI noText;
    public RectTransform textContainer;
    public bool detachTextsFromCard = true;
    public UnityEvent onYes = new UnityEvent();
    public UnityEvent onNo = new UnityEvent();
    public UnityEvent onReturn = new UnityEvent();

    public event Action<DragCard, bool> Swiped;
    public event Action<DragCard> ExitCompleted;
    public event Action AvailabilityChanged;
    public bool AllowYes { get; set; } = true;
    public bool AllowNo { get; set; } = true;
    public bool IsReady => isActiveAndEnabled && state == MotionState.Idle && interactionAllowed && visible;
    public bool IsExiting => state == MotionState.Exiting;

    private enum MotionState { Hidden, Entering, Idle, Dragging, Returning, Exiting }
    private MotionState state;
    private RectTransform rt;
    private Canvas canvas;
    private CanvasGroup group;
    private Vector2 origin;
    private float velocityX;
    private int pointerId = int.MinValue;
    private bool interactionAllowed = true;
    private bool visible = true;
    private GameObject labelHost;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        origin = rt.anchoredPosition;
    }

    private void OnEnable() => ResetForDisplay();

    public void ResetForDisplay()
    {
        StopAllCoroutines();
        interactionAllowed = visible = true;
        velocityX = 0;
        pointerId = int.MinValue;
        rt.anchoredPosition = origin;
        rt.localRotation = Quaternion.identity;
        SetLabels(0, 0);
        PrepareFixedLabels();
        if (labelHost != null) labelHost.SetActive(true);
        ChangeState(MotionState.Entering);
        StartCoroutine(Entrance());
    }

    public void SetInteraction(bool allowed)
    {
        if (interactionAllowed == allowed) return;
        interactionAllowed = allowed;
        UpdateRaycasts();
        AvailabilityChanged?.Invoke();
    }

    public void SetVisible(bool value)
    {
        visible = value;
        group.alpha = value ? 1 : 0;
        if (labelHost != null) labelHost.SetActive(value);
        UpdateRaycasts();
        AvailabilityChanged?.Invoke();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsReady || canvas == null) return;
        pointerId = eventData.pointerId;
        velocityX = 0;
        ChangeState(MotionState.Dragging);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (state != MotionState.Dragging || eventData.pointerId != pointerId) return;
        Vector2 delta = eventData.delta / Mathf.Max(0.001f, canvas.scaleFactor);
        rt.anchoredPosition += delta;
        velocityX = delta.x / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
        float dx = rt.anchoredPosition.x - origin.x;
        rt.localRotation = Quaternion.Euler(0, 0, -Mathf.Clamp(dx / 20f, -25, 25));
        SetLabels(Mathf.Clamp01(dx / Mathf.Max(1, swipeThreshold)),
            Mathf.Clamp01(-dx / Mathf.Max(1, swipeThreshold)));
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (state != MotionState.Dragging || eventData.pointerId != pointerId) return;
        float dx = rt.anchoredPosition.x - origin.x;
        bool yes = dx > 0;
        if (Mathf.Abs(dx) <= Mathf.Max(1, swipeThreshold) || !(yes ? AllowYes : AllowNo))
        {
            ChangeState(MotionState.Returning);
            AudioManager.Instance?.PlaySound("CardTap");
            StartCoroutine(Return());
            onReturn?.Invoke();
            return;
        }
        ChangeState(MotionState.Exiting);
        AudioManager.Instance?.PlaySound("Swipe");
        StartCoroutine(Exit(yes));
        Swiped?.Invoke(this, yes);
        if (yes) onYes?.Invoke(); else onNo?.Invoke();
    }

    private IEnumerator Entrance()
    {
        Vector2 from = origin + Vector2.down * 300;
        group.alpha = 0;
        float elapsed = 0;
        while (elapsed < entranceAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOut(Mathf.Clamp01(elapsed / entranceAnimationDuration));
            rt.anchoredPosition = Vector2.Lerp(from, origin, t);
            group.alpha = visible ? t : 0;
            yield return null;
        }
        rt.anchoredPosition = origin;
        group.alpha = visible ? 1 : 0;
        ChangeState(MotionState.Idle);
    }

    private IEnumerator Return()
    {
        Vector2 from = rt.anchoredPosition;
        Quaternion rotation = rt.localRotation;
        float yesAlpha = yesText != null ? yesText.alpha : 0;
        float noAlpha = noText != null ? noText.alpha : 0;
        float elapsed = 0;
        while (elapsed < returnAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOut(Mathf.Clamp01(elapsed / returnAnimationDuration));
            rt.anchoredPosition = Vector2.Lerp(from, origin, t);
            rt.localRotation = Quaternion.Slerp(rotation, Quaternion.identity, t);
            SetLabels(yesAlpha * (1 - t), noAlpha * (1 - t));
            yield return null;
        }
        rt.anchoredPosition = origin;
        rt.localRotation = Quaternion.identity;
        SetLabels(0, 0);
        ChangeState(MotionState.Idle);
    }

    private IEnumerator Exit(bool yes)
    {
        Vector2 from = rt.anchoredPosition;
        float width = canvas != null ? ((RectTransform)canvas.transform).rect.width : 2000;
        Vector2 target = new Vector2(origin.x + (yes ? 1 : -1) * Mathf.Max(2000, width * 2), from.y);
        float duration = Mathf.Max(0.01f, exitAnimationDuration / (1 + Mathf.Abs(velocityX) / 500));
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rt.anchoredPosition = Vector2.Lerp(from, target, 1 - Mathf.Pow(1 - t, 3));
            group.alpha = 1 - t;
            SetLabels(0, 0);
            yield return null;
        }
        ChangeState(MotionState.Hidden);
        ExitCompleted?.Invoke(this);
    }

    private void ChangeState(MotionState next)
    {
        state = next;
        UpdateRaycasts();
        AvailabilityChanged?.Invoke();
    }

    private void UpdateRaycasts()
    {
        group.interactable = visible && interactionAllowed && state == MotionState.Idle;
        group.blocksRaycasts = visible;
    }

    private void SetLabels(float yes, float no)
    {
        if (yesText != null) yesText.alpha = yes;
        if (noText != null) noText.alpha = no;
    }

    private void PrepareFixedLabels()
    {
        if (!detachTextsFromCard || canvas == null || labelHost != null) return;
        if ((yesText == null || !yesText.transform.IsChildOf(transform)) &&
            (noText == null || !noText.transform.IsChildOf(transform))) return;
        labelHost = new GameObject(name + " Choices", typeof(RectTransform), typeof(CanvasGroup));
        var hostRect = (RectTransform)labelHost.transform;
        hostRect.SetParent(textContainer != null ? textContainer : canvas.transform, false);
        hostRect.anchorMin = Vector2.zero;
        hostRect.anchorMax = Vector2.one;
        hostRect.offsetMin = hostRect.offsetMax = Vector2.zero;
        labelHost.GetComponent<CanvasGroup>().blocksRaycasts = false;
        if (yesText != null && yesText.transform.IsChildOf(transform)) yesText.transform.SetParent(hostRect, true);
        if (noText != null && noText.transform.IsChildOf(transform)) noText.transform.SetParent(hostRect, true);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        state = MotionState.Hidden;
        SetLabels(0, 0);
        if (labelHost != null) labelHost.SetActive(false);
        AvailabilityChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (labelHost != null) Destroy(labelHost);
    }

    private static float EaseOut(float t) => 1 - (1 - t) * (1 - t);
}
