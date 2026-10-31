using UnityEngine;

public enum BackgroundTheme
{
    [InspectorName("Özel (Mevcut Renkler)")] Custom = 0,
    [InspectorName("Orman Yeşili")] Forest = 1,
    [InspectorName("Klasik Kırmızı / Turkuaz")] Classic = 2,
    [InspectorName("Mor / Altın")] PurpleGold = 3,
    [InspectorName("Gece Mavisi")] MidnightBlue = 4
}

public enum BackgroundPattern
{
    [InspectorName("Spiral")] Spiral = 0,
    [InspectorName("Sıvı Mermer")] LiquidMarble = 1,
    [InspectorName("Sıvı Hücreler")] CellularFluid = 2,
    [InspectorName("İpeksi Akış")] SilkFlow = 3,
    [InspectorName("Duman")] Smoke = 4,
    [InspectorName("Lav Lambası")] LavaLamp = 5,
    [InspectorName("Aurora")] Aurora = 6
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class Background : MonoBehaviour
{
    [Header("Rendering")]
    [Tooltip("Assign the Background material. A private copy is used during play.")]
    [SerializeField] private Material backgroundMaterial;

    [Header("Patterns")]
    [SerializeField] private BackgroundPattern pattern = BackgroundPattern.Spiral;
    [Tooltip("A repeatable variation of the selected pattern. Zero preserves the original spiral.")]
    [Range(0, 9999)] [SerializeField] private int patternSeed;
    [Tooltip("Highlight strength for the Liquid Marble pattern.")]
    [Range(0f, 1f)] [SerializeField] private float marbleGloss = 0.4f;
    [Tooltip("Seconds to crossfade patterns. The latest selection is queued during a crossfade.")]
    [Min(0f)] [SerializeField] private float patternTransitionDuration = 2f;
    [SerializeField] private bool automaticPatternCycling;
    [Tooltip("Seconds to wait after a pattern transition finishes. Independent of color themes.")]
    [Min(0.1f)] [SerializeField] private float patternHoldDuration = 15f;

    [Header("Themes")]
    [SerializeField] private BackgroundTheme theme = BackgroundTheme.Custom;
    [Tooltip("Seconds to blend into a newly selected theme. Zero switches immediately.")]
    [Min(0f)] [SerializeField] private float themeTransitionDuration = 2f;
    [Tooltip("Cycle through Forest, Classic, Purple / Gold, and Midnight Blue.")]
    [SerializeField] private bool automaticThemeCycling;
    [Tooltip("Seconds to wait after a transition finishes. Manual selection restarts this wait.")]
    [Min(0.1f)] [SerializeField] private float themeHoldDuration = 12f;

    [Header("Custom Palette")]
    [SerializeField] private Color darkColor = new Color(0.025f, 0.075f, 0.05f, 1f);
    [SerializeField] private Color greenColor = new Color(0.055f, 0.3f, 0.18f, 1f);
    [SerializeField] private Color mintColor = new Color(0.32f, 0.65f, 0.43f, 1f);

    [Header("Flow")]
    [Min(0f)] [SerializeField] private float flowSpeed = 0.35f;
    [Range(0f, 3f)] [SerializeField] private float swirlStrength = 1f;
    [Range(0.25f, 5f)] [SerializeField] private float patternScale = 1f;
    [Range(0.25f, 3f)] [SerializeField] private float contrast = 1.25f;
    [Tooltip("Run the flow at 25% speed and freeze grain animation. Theme transitions continue.")]
    [SerializeField] private bool reducedMotion;

    [Header("Finish")]
    [Tooltip("Size in screen pixels. Zero disables pixelation.")]
    [Range(0f, 8f)] [SerializeField] private float pixelSize = 2f;
    [Range(0f, 1f)] [SerializeField] private float vignette = 0.3f;

    [Header("CRT (Background Only)")]
    [SerializeField] private bool enableCRT;
    [Range(0f, 1f)] [SerializeField] private float scanlineStrength = 0.12f;
    [Range(0f, 0.25f)] [SerializeField] private float grainStrength = 0.025f;

    private static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
    private static readonly int GreenColorId = Shader.PropertyToID("_GreenColor");
    private static readonly int MintColorId = Shader.PropertyToID("_MintColor");
    private static readonly int FlowTimeId = Shader.PropertyToID("_FlowTime");
    private static readonly int SwirlStrengthId = Shader.PropertyToID("_SwirlStrength");
    private static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");
    private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int ViewportHeightId = Shader.PropertyToID("_ViewportHeight");
    private static readonly int CRTEnabledId = Shader.PropertyToID("_CRTEnabled");
    private static readonly int ScanlineStrengthId = Shader.PropertyToID("_ScanlineStrength");
    private static readonly int GrainStrengthId = Shader.PropertyToID("_GrainStrength");
    private static readonly int GrainTimeId = Shader.PropertyToID("_GrainTime");
    private static readonly int PatternFromId = Shader.PropertyToID("_PatternFrom");
    private static readonly int PatternToId = Shader.PropertyToID("_PatternTo");
    private static readonly int PatternBlendId = Shader.PropertyToID("_PatternBlend");
    private static readonly int PatternSeedId = Shader.PropertyToID("_PatternSeed");
    private static readonly int MarbleGlossId = Shader.PropertyToID("_MarbleGloss");

    private Camera targetCamera;
    private GameObject backgroundObject;
    private Sprite backgroundSprite;
    private Material runtimeMaterial;
    private float flowTime;
    private float grainTime;
    private BackgroundTheme activeTheme;
    private Palette currentPalette;
    private Palette transitionStart;
    private Palette transitionTarget;
    private float transitionElapsed;
    private float holdElapsed;
    private bool isTransitioning;
    private bool wasCycling;
    private BackgroundPattern activePattern;
    private BackgroundPattern targetPattern;
    private BackgroundPattern observedPattern;
    private float patternTransitionElapsed;
    private float patternBlend;
    private float patternHoldElapsed;
    private bool isPatternTransitioning;
    private bool wasPatternCycling;

    private struct Palette
    {
        public Color Dark;
        public Color Middle;
        public Color Light;

        public Palette(Color dark, Color middle, Color light)
        {
            Dark = dark;
            Middle = middle;
            Light = light;
        }

        public bool Matches(Palette other)
        {
            return Dark.Equals(other.Dark) && Middle.Equals(other.Middle) && Light.Equals(other.Light);
        }

        public static Palette Lerp(Palette from, Palette to, float amount)
        {
            return new Palette(Color.Lerp(from.Dark, to.Dark, amount),
                Color.Lerp(from.Middle, to.Middle, amount), Color.Lerp(from.Light, to.Light, amount));
        }
    }

    private void OnEnable()
    {
        if (backgroundMaterial == null || backgroundMaterial.shader == null || !backgroundMaterial.shader.isSupported)
        {
            Debug.LogError("Background needs a supported Background material assigned in the Inspector.", this);
            enabled = false;
            return;
        }

        targetCamera = GetComponent<Camera>();
        runtimeMaterial = new Material(backgroundMaterial)
        {
            name = "Background (Runtime)",
            hideFlags = HideFlags.DontSave
        };

        // Use the whole built-in white texture so sprite UVs span exactly 0..1.
        Texture2D white = Texture2D.whiteTexture;
        backgroundSprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height),
            new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        backgroundSprite.name = "Background (Runtime)";
        backgroundSprite.hideFlags = HideFlags.DontSave;

        backgroundObject = new GameObject("Flowing Background")
        {
            hideFlags = HideFlags.DontSave,
            layer = gameObject.layer
        };
        backgroundObject.transform.SetParent(transform, false);

        SpriteRenderer spriteRenderer = backgroundObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = backgroundSprite;
        spriteRenderer.sharedMaterial = runtimeMaterial;
        spriteRenderer.sortingLayerID = SortingLayer.layers[0].id;
        spriteRenderer.sortingOrder = short.MinValue;

        // Apply the configured palette immediately on startup, including legacy custom colors.
        activeTheme = theme;
        currentPalette = transitionStart = transitionTarget = ResolvePalette(theme);
        transitionElapsed = holdElapsed = 0f;
        isTransitioning = false;
        wasCycling = automaticThemeCycling;
        activePattern = targetPattern = observedPattern = pattern;
        patternTransitionElapsed = patternBlend = patternHoldElapsed = 0f;
        isPatternTransitioning = false;
        wasPatternCycling = automaticPatternCycling;
        UpdateBackground();
    }

    private void LateUpdate()
    {
        // Keep the backdrop alive while gameplay is paused, without using shader _Time.
        float deltaTime = Time.unscaledDeltaTime;
        flowTime += deltaTime * flowSpeed * (reducedMotion ? 0.25f : 1f);
        if (!reducedMotion)
            grainTime += deltaTime;

        UpdateTheme(deltaTime);
        UpdatePattern(deltaTime);
        UpdateBackground();
    }

    /// <summary>
    /// Crossfade into a pattern independently of the color theme. During a crossfade,
    /// only the latest selection is retained and applied after the current fade ends.
    /// Manual selection restarts the pattern hold timer without disabling cycling.
    /// </summary>
    public void SetPattern(BackgroundPattern selectedPattern)
    {
        if ((int)selectedPattern < (int)BackgroundPattern.Spiral || (int)selectedPattern > (int)BackgroundPattern.Aurora)
            throw new System.ArgumentOutOfRangeException(nameof(selectedPattern));

        pattern = observedPattern = selectedPattern;
        patternHoldElapsed = 0f;
        if (runtimeMaterial != null && !isPatternTransitioning)
            BeginPatternTransition(selectedPattern);
    }

    private void UpdatePattern(float deltaTime)
    {
        if (pattern != observedPattern)
        {
            observedPattern = pattern;
            patternHoldElapsed = 0f;
            if (!isPatternTransitioning)
                BeginPatternTransition(pattern);
        }

        if (wasPatternCycling != automaticPatternCycling)
        {
            wasPatternCycling = automaticPatternCycling;
            patternHoldElapsed = 0f;
        }

        if (isPatternTransitioning)
        {
            patternTransitionElapsed += deltaTime;
            float progress = patternTransitionDuration > 0f
                ? Mathf.Clamp01(patternTransitionElapsed / patternTransitionDuration) : 1f;
            patternBlend = Mathf.SmoothStep(0f, 1f, progress);
            if (progress >= 1f)
            {
                activePattern = targetPattern;
                isPatternTransitioning = false;
                patternBlend = patternHoldElapsed = 0f;
                if (pattern != activePattern)
                    BeginPatternTransition(pattern);
            }

            return;
        }

        if (automaticPatternCycling)
        {
            patternHoldElapsed += deltaTime;
            if (patternHoldElapsed >= patternHoldDuration)
            {
                pattern = observedPattern = activePattern < BackgroundPattern.Aurora
                    ? activePattern + 1 : BackgroundPattern.Spiral;
                BeginPatternTransition(pattern);
            }
        }
    }

    private void BeginPatternTransition(BackgroundPattern selectedPattern)
    {
        targetPattern = selectedPattern;
        patternTransitionElapsed = patternBlend = patternHoldElapsed = 0f;
        isPatternTransitioning = activePattern != selectedPattern && patternTransitionDuration > 0f;
        if (!isPatternTransitioning)
            activePattern = selectedPattern;
    }

    /// <summary>
    /// Blend into a theme without stopping the flow or disabling automatic cycling.
    /// Calling this also restarts the automatic hold timer, even for the same theme.
    /// </summary>
    public void SetTheme(BackgroundTheme selectedTheme)
    {
        if ((int)selectedTheme < (int)BackgroundTheme.Custom || (int)selectedTheme > (int)BackgroundTheme.MidnightBlue)
            throw new System.ArgumentOutOfRangeException(nameof(selectedTheme));

        theme = selectedTheme;
        holdElapsed = 0f;
        if (runtimeMaterial != null)
        {
            Palette palette = ResolvePalette(theme);
            if (activeTheme != theme || !transitionTarget.Matches(palette))
                BeginTransition(theme, palette);
        }
    }

    private void UpdateTheme(float deltaTime)
    {
        // Inspect serialized fields here: OnValidate can run off the main thread.
        Palette requestedPalette = ResolvePalette(theme);
        if (activeTheme != theme || !transitionTarget.Matches(requestedPalette))
            BeginTransition(theme, requestedPalette);

        if (wasCycling != automaticThemeCycling)
        {
            wasCycling = automaticThemeCycling;
            holdElapsed = 0f;
        }

        if (isTransitioning)
        {
            transitionElapsed += deltaTime;
            float progress = themeTransitionDuration > 0f
                ? Mathf.Clamp01(transitionElapsed / themeTransitionDuration) : 1f;
            currentPalette = Palette.Lerp(transitionStart, transitionTarget, Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f)
            {
                isTransitioning = false;
                holdElapsed = 0f;
            }

            // Hold time starts on the frame after the transition completes.
            return;
        }

        if (automaticThemeCycling)
        {
            holdElapsed += deltaTime;
            if (holdElapsed >= themeHoldDuration)
            {
                BackgroundTheme nextTheme = activeTheme >= BackgroundTheme.Forest && activeTheme < BackgroundTheme.MidnightBlue
                    ? activeTheme + 1 : BackgroundTheme.Forest;
                BeginTransition(nextTheme, ResolvePalette(nextTheme));
            }
        }
    }

    private void BeginTransition(BackgroundTheme selectedTheme, Palette palette)
    {
        theme = activeTheme = selectedTheme;
        transitionStart = currentPalette;
        transitionTarget = palette;
        transitionElapsed = holdElapsed = 0f;
        isTransitioning = themeTransitionDuration > 0f && !currentPalette.Matches(palette);
        if (!isTransitioning)
            currentPalette = palette;
    }

    private Palette ResolvePalette(BackgroundTheme selectedTheme)
    {
        switch (selectedTheme)
        {
            case BackgroundTheme.Forest:
                return new Palette(new Color(0.025f, 0.075f, 0.05f, 1f),
                    new Color(0.055f, 0.3f, 0.18f, 1f), new Color(0.32f, 0.65f, 0.43f, 1f));
            case BackgroundTheme.Classic:
                return new Palette(new Color(0.025f, 0.055f, 0.065f, 1f),
                    new Color(0.72f, 0.16f, 0.12f, 1f), new Color(0.12f, 0.62f, 0.66f, 1f));
            case BackgroundTheme.PurpleGold:
                return new Palette(new Color(0.045f, 0.02f, 0.08f, 1f),
                    new Color(0.32f, 0.11f, 0.5f, 1f), new Color(0.9f, 0.61f, 0.17f, 1f));
            case BackgroundTheme.MidnightBlue:
                return new Palette(new Color(0.012f, 0.025f, 0.065f, 1f),
                    new Color(0.04f, 0.18f, 0.4f, 1f), new Color(0.15f, 0.52f, 0.72f, 1f));
            default:
                return new Palette(darkColor, greenColor, mintColor);
        }
    }

    private void UpdateBackground()
    {
        if (runtimeMaterial == null || backgroundObject == null)
            return;

        // Stay inside the far plane, behind world effects and the camera-space canvas.
        float depth = Mathf.Lerp(targetCamera.nearClipPlane, targetCamera.farClipPlane, 0.95f);
        float height = targetCamera.orthographic
            ? 2f * targetCamera.orthographicSize
            : 2f * depth * Mathf.Tan(0.5f * targetCamera.fieldOfView * Mathf.Deg2Rad);
        float aspect = Mathf.Max(0.001f, targetCamera.aspect);
        Vector3 spriteSize = backgroundSprite.bounds.size;

        Transform backdrop = backgroundObject.transform;
        backdrop.localPosition = new Vector3(0f, 0f, depth);
        backdrop.localScale = new Vector3(height * aspect / spriteSize.x, height / spriteSize.y, 1f);

        runtimeMaterial.SetColor(DarkColorId, currentPalette.Dark);
        runtimeMaterial.SetColor(GreenColorId, currentPalette.Middle);
        runtimeMaterial.SetColor(MintColorId, currentPalette.Light);
        runtimeMaterial.SetFloat(FlowTimeId, flowTime);
        runtimeMaterial.SetFloat(SwirlStrengthId, swirlStrength);
        runtimeMaterial.SetFloat(PatternScaleId, patternScale);
        runtimeMaterial.SetFloat(ContrastId, contrast);
        runtimeMaterial.SetFloat(PixelSizeId, pixelSize);
        runtimeMaterial.SetFloat(VignetteId, vignette);
        runtimeMaterial.SetFloat(AspectId, aspect);
        runtimeMaterial.SetFloat(ViewportHeightId, Mathf.Max(1, targetCamera.pixelHeight));
        runtimeMaterial.SetFloat(CRTEnabledId, enableCRT ? 1f : 0f);
        runtimeMaterial.SetFloat(ScanlineStrengthId, scanlineStrength);
        runtimeMaterial.SetFloat(GrainStrengthId, grainStrength);
        runtimeMaterial.SetFloat(GrainTimeId, grainTime);
        runtimeMaterial.SetFloat(PatternFromId, (float)activePattern);
        runtimeMaterial.SetFloat(PatternToId, (float)targetPattern);
        runtimeMaterial.SetFloat(PatternBlendId, patternBlend);
        runtimeMaterial.SetFloat(PatternSeedId, patternSeed);
        runtimeMaterial.SetFloat(MarbleGlossId, marbleGloss);
    }

    private void OnDisable()
    {
        if (backgroundObject != null)
        {
            // Destroy is deferred, so hide it immediately if re-enabled this frame.
            backgroundObject.SetActive(false);
            Destroy(backgroundObject);
        }

        if (backgroundSprite != null)
            Destroy(backgroundSprite);

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);

        backgroundObject = null;
        backgroundSprite = null;
        runtimeMaterial = null;
    }

    private void OnValidate()
    {
        patternSeed = Mathf.Clamp(patternSeed, 0, 9999);
        marbleGloss = Mathf.Clamp01(marbleGloss);
        patternTransitionDuration = Mathf.Max(0f, patternTransitionDuration);
        patternHoldDuration = Mathf.Max(0.1f, patternHoldDuration);
        themeTransitionDuration = Mathf.Max(0f, themeTransitionDuration);
        themeHoldDuration = Mathf.Max(0.1f, themeHoldDuration);
        flowSpeed = Mathf.Max(0f, flowSpeed);
        swirlStrength = Mathf.Clamp(swirlStrength, 0f, 3f);
        patternScale = Mathf.Clamp(patternScale, 0.25f, 5f);
        contrast = Mathf.Clamp(contrast, 0.25f, 3f);
        pixelSize = Mathf.Clamp(pixelSize, 0f, 8f);
        vignette = Mathf.Clamp01(vignette);
        scanlineStrength = Mathf.Clamp01(scanlineStrength);
        grainStrength = Mathf.Clamp(grainStrength, 0f, 0.25f);
    }
}
