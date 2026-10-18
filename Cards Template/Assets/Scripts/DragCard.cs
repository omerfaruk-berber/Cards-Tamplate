using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public enum CardType { StartGame, AnaCard, Pause, EndGame, Cheat }

[RequireComponent(typeof(RectTransform))]
public class DragCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CardType cardType = CardType.AnaCard;
    public float swipeThreshold = 150f; // px
    public float swipeSpeed = 1000f;
    public float returnAnimationDuration = 0.5f; // Geri dönüş süresi (saniye)
    public float exitAnimationDuration = 0.6f; // Çıkış animasyonu süresi
    public float entranceAnimationDuration = 0.6f; // Giriş animasyonu süresi
    
    [Header("Metin Animasyonları")]
    public TextMeshProUGUI yesText; // Sağ taraf "EVET" metni
    public TextMeshProUGUI noText;  // Sol taraf "HAYIR" metni
    public RectTransform textContainer; // Metinlerin sabit kalacağı parent (boşsa Canvas kullanılır)
    public bool detachTextsFromCard = true; // Metinleri karttan ayırıp sabit tut
    
    public UnityEvent onYes;
    public UnityEvent onNo;
    public UnityEvent onReturn;

    RectTransform rt;
    Canvas canvas;
    Vector2 startAnchoredPos;
    CanvasGroup canvasGroup;
    Vector2 lastDragVelocity = Vector2.zero;
    bool isEntering = true;
    bool isDragging;
    bool isReturning;
    UIAnimator animator;
    Vector2 yesFixedAnchoredPos;
    Vector2 noFixedAnchoredPos;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void Start()
    {
        startAnchoredPos = rt.anchoredPosition;
        // Metinleri başlangıçta gizle
        if (yesText != null) yesText.alpha = 0;
        if (noText != null) noText.alpha = 0;
        // Metinleri karttan ayırıp sabit konuma al
        if (detachTextsFromCard)
        {
            DetachTextIfNeeded(yesText, ref yesFixedAnchoredPos);
            DetachTextIfNeeded(noText, ref noFixedAnchoredPos);
        }
        // Giriş animasyonunu başlat
        StartCoroutine(EntranceAnimation());
    }

    private IEnumerator EntranceAnimation()
    {
        // Başlangıç konumunu aşağıda set et
        Vector2 bottomPos = startAnchoredPos + new Vector2(0, -300f);
        rt.anchoredPosition = bottomPos;
        canvasGroup.alpha = 0f;

        float elapsed = 0f;

        while (elapsed < entranceAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / entranceAnimationDuration);
            // Ease-out quad: aşağıdan yukarıya doğal hareket
            float eased = EaseOutQuad(t);

            rt.anchoredPosition = Vector2.Lerp(bottomPos, startAnchoredPos, eased);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, eased);

            yield return null;
        }

        rt.anchoredPosition = startAnchoredPos;
        canvasGroup.alpha = 1f;
        isEntering = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isEntering || isReturning || canvas == null) return;

        isDragging = true;
        
        canvasGroup.blocksRaycasts = false;
        // Kartı tuttuğunda önce ses çalmayacağız; bırakma anında çalmak için hareket etmeyiz
        Debug.Log($"OnBeginDrag: {gameObject.name}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || canvas == null) return;
        
        Vector2 delta = eventData.delta / canvas.scaleFactor;
        rt.anchoredPosition += new Vector2(delta.x, delta.y);
        lastDragVelocity = delta / Time.deltaTime; // Hız hesapla
        float rot = Mathf.Clamp(rt.anchoredPosition.x / 20f, -25f, 25f);
        rt.localRotation = Quaternion.Euler(0, 0, -rot);
        
        // Kartın konumuna göre metin alpha'sını ayarla
        float dx = rt.anchoredPosition.x - startAnchoredPos.x;
        
        if (dx > 0) // Sağa kaydırıldı
        {
            float alpha = Mathf.Clamp01(dx / swipeThreshold);
            if (yesText != null)
            {
                yesText.alpha = alpha;
            }
            if (noText != null)
            {
                noText.alpha = 0;
            }
        }
        else if (dx < 0) // Sola kaydırıldı
        {
            float alpha = Mathf.Clamp01(-dx / swipeThreshold);
            if (noText != null)
            {
                noText.alpha = alpha;
            }
            if (yesText != null)
            {
                yesText.alpha = 0;
            }
        }
        else // Merkeze dönüyorsa
        {
            if (yesText != null)
            {
                yesText.alpha = 0;
            }
            if (noText != null)
            {
                noText.alpha = 0;
            }
        }
        
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
        canvasGroup.blocksRaycasts = true;
        float dx = rt.anchoredPosition.x - startAnchoredPos.x;
        Debug.Log($"OnEndDrag: {gameObject.name} dx={dx}");
        if (dx > swipeThreshold)
        {
            // Momentum ile sağa çık
            float exitDuration = Mathf.Max(0.2f, exitAnimationDuration / (1f + Mathf.Abs(lastDragVelocity.x) / 500f));
            // Kartı bıraktığında ses oynat (YES)
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Swipe");
            StartCoroutine(AnimateOffWithMomentum(new Vector2(2000f, rt.anchoredPosition.y), exitDuration, true));
            onYes?.Invoke();
        }
        else if (dx < -swipeThreshold)
        {
            // Momentum ile sola çık
            float exitDuration = Mathf.Max(0.2f, exitAnimationDuration / (1f + Mathf.Abs(lastDragVelocity.x) / 500f));
            // Kartı bıraktığında ses oynat (NO)
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Swipe");
            StartCoroutine(AnimateOffWithMomentum(new Vector2(-2000f, rt.anchoredPosition.y), exitDuration, false));
            onNo?.Invoke();
        }
        else
        {
            // Kart eşiği aşmadı: geri dön ve "bırakma" sesi çal
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("CardTap");
            StartCoroutine(ReturnToOrigin());
            onReturn?.Invoke();
        }
    }

    // Easing function: Ease-out (daha yumuşak sonuş)
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    // Easing function: Ease-out Quad (geri dönüş için)
    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    IEnumerator AnimateOffWithMomentum(Vector2 targetPos, float duration, bool isYes)
    {
        Vector2 from = rt.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease-out cubic: hızlı başla, yavaşla (momentum etkisi)
            float eased = EaseOutCubic(t);
            rt.anchoredPosition = Vector2.Lerp(from, targetPos, eased);

            // Alfa kaybolfalt (fade out)
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        Destroy(gameObject);
    }

    IEnumerator AnimateOff(Vector2 targetPos, bool isYes)
    {
        while (Vector2.Distance(rt.anchoredPosition, targetPos) > 10f)
        {
            rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, targetPos, swipeSpeed * Time.deltaTime);
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator ReturnToOrigin()
    {
        isReturning = true;
        float t = 0f;
        Vector2 from = rt.anchoredPosition;
        Quaternion fromRot = rt.localRotation;
        
        while (t < 2f)
        {
            t += Time.deltaTime / returnAnimationDuration;
            // Ease-out quad: yumuşak geri dönüş
            float eased = EaseOutQuad(Mathf.Clamp01(t));
            rt.anchoredPosition = Vector2.Lerp(from, startAnchoredPos, eased);
            rt.localRotation = Quaternion.Slerp(fromRot, Quaternion.identity, eased);
            
            // Metinleri fade out yap
            if (yesText != null)
            {
                yesText.alpha = Mathf.Lerp(yesText.alpha, 0, eased);
            }
            if (noText != null)
            {
                noText.alpha = Mathf.Lerp(noText.alpha, 0, eased);
            }
            
            yield return null;
        }
        rt.anchoredPosition = startAnchoredPos;
        rt.localRotation = Quaternion.identity;
        
        // Metinleri tamamen sıfırla
        if (yesText != null)
        {
            yesText.alpha = 0;
        }
        if (noText != null)
        {
            noText.alpha = 0;
        }

        isReturning = false;
    }

    void DetachTextIfNeeded(TextMeshProUGUI text, ref Vector2 cachedAnchoredPos)
    {
        if (text == null) return;
        if (canvas == null) return;
        if (!text.transform.IsChildOf(rt)) return;

        RectTransform textRt = text.rectTransform;
        Transform targetParent = textContainer != null ? textContainer : canvas.transform;
        RectTransform parentRt = targetParent as RectTransform;
        if (parentRt == null) return;

        Vector3 worldPos = textRt.position;
        Vector2 sizeDelta = textRt.sizeDelta;
        Vector2 pivot = textRt.pivot;

        textRt.SetParent(targetParent, true);

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt,
            RectTransformUtility.WorldToScreenPoint(cam, worldPos),
            cam,
            out localPoint
        );

        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.pivot = pivot;
        textRt.anchoredPosition = localPoint;
        textRt.sizeDelta = sizeDelta;

        cachedAnchoredPos = textRt.anchoredPosition;
    }
}
