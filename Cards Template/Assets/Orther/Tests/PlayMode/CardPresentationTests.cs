using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

// Existing scene components remain in Assembly-CSharp to preserve serialized UnityEvent bindings.
public class CardPresentationTests
{
    private static readonly Type ControllerType = Type.GetType("CardGameController, Assembly-CSharp", true);
    private static readonly Type DragType = Type.GetType("DragCard, Assembly-CSharp", true);
    private Component controller;
    private CardDataset testDataset;
    private Component Current => (Component)ControllerType.GetProperty("CurrentCard").GetValue(controller);
    private CardSession Session => (CardSession)ControllerType.GetProperty("Session").GetValue(controller);
    private static bool Ready(Component card) => card != null && (bool)DragType.GetProperty("IsReady").GetValue(card);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Time.timeScale = 1;
        foreach (string name in new[] { "AudioManager", "NotificationManager" })
            foreach (var item in Object.FindObjectsByType(Type.GetType(name + ", Assembly-CSharp"), FindObjectsSortMode.None))
                Object.Destroy(((Component)item).gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("MainScene");
        controller = (Component)Object.FindFirstObjectByType(ControllerType);
        Assert.That(controller, Is.Not.Null);
        FastAnimations();
        yield return WaitReady();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1;
        if (testDataset != null) Object.Destroy(testDataset);
        yield return null;
    }

    private void FastAnimations()
    {
        foreach (var item in Object.FindObjectsByType(DragType, FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            DragType.GetField("entranceAnimationDuration").SetValue(item, 0f);
            DragType.GetField("exitAnimationDuration").SetValue(item, 0.01f);
            DragType.GetField("returnAnimationDuration").SetValue(item, 0f);
        }
    }

    private IEnumerator WaitReady()
    {
        float deadline = Time.realtimeSinceStartup + 8;
        while (!Ready(Current) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Ready(Current), Is.True, "Current card did not become ready.");
    }

    private static void Swipe(Component card, bool yes)
    {
        var pointer = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.beginDragHandler);
        pointer.delta = new Vector2(yes ? 400 : -400, 0) * card.GetComponentInParent<Canvas>().scaleFactor;
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler);
    }

    private void Call(string method) => ControllerType.GetMethod(method).Invoke(controller, null);

    [UnityTest]
    public IEnumerator RestartInsideDisplayEventStillReportsPreviousCompletion()
    {
        yield return ReloadInsideDisplayEvent(false);
    }

    [UnityTest]
    public IEnumerator LoadInsideDisplayEventStillReportsPreviousCompletion()
    {
        yield return ReloadInsideDisplayEvent(true);
    }

    private IEnumerator ReloadInsideDisplayEvent(bool replace)
    {
        var originalDataset = (CardDataset)ControllerType.GetField("dataset").GetValue(controller);
        testDataset = ScriptableObject.CreateInstance<CardDataset>();
        testDataset.viewPrefab = originalDataset.viewPrefab;
        testDataset.cards.Add(new CardEntry { title = "Replacement" });
        var previous = Session;
        int completed = 0;
        bool reloaded = false;
        Action onComplete = () => completed++;
        Action<Component> onDisplay = card =>
        {
            if (reloaded || !previous.IsFinished) return;
            reloaded = true;
            if (replace)
                Assert.That((bool)ControllerType.GetMethod("LoadDataset").Invoke(controller, new object[] { testDataset }), Is.True);
            else Call("RestartGame");
        };
        var displayEvent = ControllerType.GetEvent("CurrentCardChanged");
        var handler = Delegate.CreateDelegate(displayEvent.EventHandlerType, onDisplay.Target, onDisplay.Method);
        displayEvent.AddEventHandler(controller, handler);
        ControllerType.GetEvent("GameCompleted").AddEventHandler(controller, onComplete);
        try
        {
            Swipe(Current, true);
            yield return WaitReady();
            Swipe(Current, true);
            yield return WaitReady();
            Assert.That(reloaded, Is.True);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(Session, Is.Not.SameAs(previous));
            Assert.That(Session.AnsweredCardCount, Is.Zero);
            Assert.That(Session.IsFinished, Is.False);
            yield return null;
            Assert.That(completed, Is.EqualTo(1));
        }
        finally
        {
            displayEvent.RemoveEventHandler(controller, handler);
            ControllerType.GetEvent("GameCompleted").RemoveEventHandler(controller, onComplete);
        }
    }

    [UnityTest]
    public IEnumerator InvalidViewAndCanvasLoadsPreserveSessionAndScreen()
    {
        var previous = Session;
        var previousCard = Current;
        testDataset = ScriptableObject.CreateInstance<CardDataset>();
        testDataset.cards.Add(new CardEntry { description = "Required description" });
        testDataset.cards.Add(new CardEntry { role = CardRole.Start });
        testDataset.cards.Add(new CardEntry { role = CardRole.Settings });
        var originalDataset = (CardDataset)ControllerType.GetField("dataset").GetValue(controller);
        var template = Object.Instantiate(originalDataset.viewPrefab);
        template.SetActive(false);
        testDataset.viewPrefab = template;
        var viewType = Type.GetType("CardView, Assembly-CSharp", true);
        var view = template.GetComponent(viewType);
        var drag = (Behaviour)template.GetComponent(DragType);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var spawnField = ControllerType.GetField("spawnPoint");
        var originalSpawn = spawnField.GetValue(controller);
        var outsideCanvas = new GameObject("Invalid spawn", typeof(RectTransform));
        try
        {
            drag.enabled = false;
            ExpectRejectedLoad();
            drag.enabled = true;
            foreach (string fieldName in new[] { "titleText", "descriptionText", "startContent", "startDescriptionText", "settingsContent", "settingsTitleText" })
            {
                var field = viewType.GetField(fieldName, flags);
                var original = field.GetValue(view);
                field.SetValue(view, null);
                ExpectRejectedLoad();
                field.SetValue(view, original);
            }
            spawnField.SetValue(controller, outsideCanvas.GetComponent<RectTransform>());
            ExpectRejectedLoad();
            Assert.That(Session, Is.SameAs(previous));
            Assert.That(Current, Is.SameAs(previousCard));
            Assert.That(Ready(Current), Is.True);
        }
        finally
        {
            spawnField.SetValue(controller, originalSpawn);
            Object.Destroy(template);
            Object.Destroy(outsideCanvas);
        }
        yield return null;
    }

    private void ExpectRejectedLoad()
    {
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("\\[Cards\\]"));
        Assert.That((bool)ControllerType.GetMethod("LoadDataset").Invoke(controller, new object[] { testDataset }), Is.False);
    }

    [UnityTest]
    public IEnumerator ReloadDuringExitCancelsPreviousAnimationAndCallbacks()
    {
        Swipe(Current, true);
        yield return WaitReady();
        DragType.GetField("exitAnimationDuration").SetValue(Current, 10f);
        Swipe(Current, true);
        Call("RestartGame");
        FastAnimations();
        yield return WaitReady();
        var restarted = Current;
        yield return new WaitForSecondsRealtime(0.15f);
        Assert.That(Current, Is.SameAs(restarted));
        Assert.That(Ready(Current), Is.True);
        Assert.That(Session.AnsweredCardCount, Is.Zero);
        Assert.That(Session.IsFinished, Is.False);
    }

    [UnityTest]
    public IEnumerator TotalPlayTimeAdvancesWhileRealClockIsShown()
    {
        var type = Type.GetType("TimeDisplay, Assembly-CSharp", true);
        var timer = Object.FindFirstObjectByType(type);
        int saved = PlayerPrefs.GetInt("TimeDisplayMode", 0);
        bool hadSaved = PlayerPrefs.HasKey("TimeDisplayMode");
        try
        {
            type.GetMethod("TogglePlayTime").Invoke(timer, new object[] { false });
            float before = (float)type.GetMethod("GetTotalPlayTime").Invoke(timer, null);
            yield return null;
            yield return null;
            float after = (float)type.GetMethod("GetTotalPlayTime").Invoke(timer, null);
            Assert.That(after, Is.GreaterThan(before));
        }
        finally
        {
            type.GetMethod("TogglePlayTime").Invoke(timer, new object[] { saved == 1 });
            if (hadSaved) PlayerPrefs.SetInt("TimeDisplayMode", saved); else PlayerPrefs.DeleteKey("TimeDisplayMode");
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator NotificationContainerReconnectsAfterSceneReload()
    {
        var type = Type.GetType("NotificationManager, Assembly-CSharp", true);
        var manager = Object.FindFirstObjectByType(type);
        yield return SceneManager.LoadSceneAsync("MainScene");
        type.GetMethod("ShowSuccess").Invoke(manager, new object[] { "Reload test", 0.1f });
        var container = (Transform)type.GetField("notificationContainer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
        Assert.That(container, Is.Not.Null);
        Assert.That(container.gameObject.scene, Is.EqualTo(SceneManager.GetActiveScene()));
        Assert.That(container.childCount, Is.GreaterThan(0));
        yield return null;
    }

    [UnityTest]
    public IEnumerator MusicSelectionPlaysRequestedIndexEvenWithRandomization()
    {
        var type = Type.GetType("AudioManager, Assembly-CSharp", true);
        var audio = Object.FindFirstObjectByType(type);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var field = type.GetField("backgroundMusicList", flags);
        var original = field.GetValue(audio);
        var list = (System.Collections.IList)Activator.CreateInstance(field.FieldType);
        var clipType = type.GetNestedType("BackgroundMusicClip");
        var first = AudioClip.Create("First", 44100, 1, 44100, false);
        var second = AudioClip.Create("Second", 44100, 1, 44100, false);
        foreach (var clip in new[] { first, second })
        {
            var entry = Activator.CreateInstance(clipType);
            clipType.GetField("clip").SetValue(entry, clip);
            list.Add(entry);
        }
        bool random = (bool)type.GetField("randomizeMusic", flags).GetValue(audio);
        try
        {
            field.SetValue(audio, list);
            type.GetMethod("SetRandomizeMusic").Invoke(audio, new object[] { true });
            type.GetMethod("PlayMusicByIndex").Invoke(audio, new object[] { 1 });
            var source = (AudioSource)type.GetField("musicAudioSource", flags).GetValue(audio);
            Assert.That(source.clip, Is.SameAs(second));
            Assert.That((bool)type.GetField("randomizeMusic", flags).GetValue(audio), Is.True);
        }
        finally
        {
            type.GetMethod("StopBackgroundMusic").Invoke(audio, null);
            type.GetMethod("SetRandomizeMusic").Invoke(audio, new object[] { random });
            field.SetValue(audio, original);
            Object.Destroy(first);
            Object.Destroy(second);
        }
        yield return null;
    }

    private void LoadTestDataset(bool start = true, bool end = true, bool settings = true)
    {
        testDataset.viewPrefab = ((CardDataset)ControllerType.GetField("dataset").GetValue(controller)).viewPrefab;
        if (start) testDataset.cards.Insert(0, new CardEntry { role = CardRole.Start, title = "Start" });
        if (end) testDataset.cards.Add(new CardEntry { role = CardRole.End, title = "End" });
        if (settings) testDataset.cards.Add(new CardEntry { role = CardRole.Settings, title = "Settings" });
        Assert.That((bool)ControllerType.GetMethod("LoadDataset").Invoke(controller, new object[] { testDataset }), Is.True);
    }

    [UnityTest]
    public IEnumerator StartRejectsLeftAndEndCardStaysVisible()
    {
        var start = Current;
        Swipe(start, false);
        yield return WaitReady();
        Assert.That(Current, Is.SameAs(start));
        Assert.That(Session.AnsweredCardCount, Is.Zero);
        Swipe(Current, true);
        yield return WaitReady();
        Swipe(Current, true);
        yield return WaitReady();
        var end = Current;
        Assert.That(Session.IsFinished, Is.True);
        Swipe(end, true);
        yield return WaitReady();
        Swipe(end, false);
        yield return WaitReady();
        Assert.That(Current, Is.SameAs(end));
        Assert.That(end.gameObject.activeInHierarchy, Is.True);
    }

    [UnityTest]
    public IEnumerator OverlayReturnsToSameCardAndRejectsDuplicateOpen()
    {
        Swipe(Current, true);
        yield return WaitReady();
        var main = Current;
        Call("SpawnOptionsCard");
        var options = Current;
        Assert.That(options, Is.Not.SameAs(main));
        Call("SpawnOptionsCard");
        Assert.That(Current, Is.SameAs(options));
        FastAnimations();
        yield return WaitReady();
        Swipe(options, false);
        yield return WaitReady();
        Assert.That(Current, Is.SameAs(main));
        Assert.That(Session.AnsweredCardCount, Is.Zero);
        Assert.That(main.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator PoolAndCallbacksRemainBoundedAcrossTwoHundredSwipes()
    {
        testDataset = ScriptableObject.CreateInstance<CardDataset>();
        testDataset.flowMode = CardFlowMode.Branching;
        var a = new CardEntry { title = "Loop A" };
        var b = new CardEntry { title = "Loop B" };
        a.yes.defaultTarget = a.no.defaultTarget = new CardTarget { type = CardTargetType.Card, cardId = b.id };
        b.yes.defaultTarget = b.no.defaultTarget = new CardTarget { type = CardTargetType.Card, cardId = a.id };
        testDataset.cards.AddRange(new[] { a, b });
        LoadTestDataset();
        FastAnimations();
        yield return WaitReady();
        Swipe(Current, true);
        yield return WaitReady();
        for (int i = 0; i < 200; i++)
        {
            var outgoing = Current;
            Swipe(outgoing, i % 2 == 0);
            // A repeated release from the old card must not advance the next card.
            ExecuteEvents.Execute(outgoing.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.endDragHandler);
            yield return WaitReady();
            Assert.That(Session.Current.id, Is.EqualTo(i % 2 == 0 ? b.id : a.id));
        }
        Assert.That(Object.FindObjectsByType(DragType, FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(2));
        Assert.That(Session.AnsweredCardCount, Is.EqualTo(2));
        Call("RestartGame");
        Assert.That(Session.AnsweredCardCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator UiContinuesWhenTimeScaleIsZero()
    {
        Time.timeScale = 0;
        Swipe(Current, false);
        yield return WaitReady();
        Swipe(Current, true);
        yield return WaitReady();
        Assert.That(Session.Current, Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator ExportContainsCardContentAndRestoresRenderTarget()
    {
        Swipe(Current, true);
        yield return WaitReady();
        var title = Current.GetComponentInChildren<TextMeshProUGUI>();
        title.text = "EXPORT CONTENT";
        var otherUi = new GameObject("Other UI", typeof(RectTransform), typeof(Image));
        otherUi.transform.SetParent(Current.GetComponentInParent<Canvas>().transform, false);
        ((RectTransform)otherUi.transform).sizeDelta = new Vector2(2000, 2000);
        otherUi.GetComponent<Image>().color = Color.magenta;
        otherUi.GetComponent<Image>().raycastTarget = false;
        var sentinel = new RenderTexture(8, 8, 0);
        sentinel.Create();
        var previous = RenderTexture.active;
        RenderTexture.active = sentinel;
        var exporter = Type.GetType("CardImageExporter, Assembly-CSharp", true);
        Texture2D texture = null;
        try
        {
            texture = (Texture2D)exporter.GetMethod("Capture").Invoke(null, new object[] { (RectTransform)Current.transform });
            byte[] png = texture.EncodeToPNG();
            string artifactDirectory = Environment.GetEnvironmentVariable("CARDS_TEST_ARTIFACTS");
            if (!string.IsNullOrEmpty(artifactDirectory))
            {
                System.IO.Directory.CreateDirectory(artifactDirectory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(artifactDirectory, "card-export.png"), png);
            }
            Assert.That(RenderTexture.active, Is.SameAs(sentinel));
            Assert.That(texture.width, Is.GreaterThan(100));
            bool opaque = false, textPixel = false, otherUiPixel = false;
            var pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                opaque |= pixel.a > 200;
                int x = i % texture.width, y = i / texture.width;
                bool titleArea = x > 70 && x < texture.width - 70 &&
                    y > texture.height - 230 && y < texture.height - 40;
                textPixel |= titleArea && pixel.r > 220 && pixel.g > 220 && pixel.b > 220 && pixel.a > 200;
                otherUiPixel |= pixel.r > 240 && pixel.g < 15 && pixel.b > 240 && pixel.a > 240;
            }
            Assert.That(opaque, Is.True, "Export is blank.");
            Assert.That(textPixel, Is.True, "Text was not rendered.");
            Assert.That(otherUiPixel, Is.False, "Other UI leaked into the card export.");
            Assert.That(png.Length, Is.GreaterThan(1000));
            Assert.That(Ready(Current), Is.True, "Export changed gameplay state.");
        }
        finally
        {
            RenderTexture.active = previous;
            sentinel.Release();
            Object.Destroy(sentinel);
            if (texture != null) Object.Destroy(texture);
            Object.Destroy(otherUi);
        }
        yield return null;
        Assert.That(GameObject.Find("Card Export"), Is.Null);
    }

    [UnityTest]
    public IEnumerator InvalidLoadPreservesExistingSessionAndDisplay()
    {
        var originalCard = Current;
        var originalSession = Session;
        testDataset = ScriptableObject.CreateInstance<CardDataset>();
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Veri setinde en az bir normal kart"));
        bool loaded = (bool)ControllerType.GetMethod("LoadDataset").Invoke(controller, new object[] { testDataset });
        Assert.That(loaded, Is.False);
        Assert.That(Current, Is.SameAs(originalCard));
        Assert.That(Session, Is.SameAs(originalSession));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ReusedViewClearsDescriptionArtworkAndChoiceLabels()
    {
        testDataset = ScriptableObject.CreateInstance<CardDataset>();
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        testDataset.cards.Add(new CardEntry { title = "A", description = "Description A", artwork = sprite });
        testDataset.cards.Add(new CardEntry { title = "B", description = "Description B", artwork = sprite });
        testDataset.cards.Add(new CardEntry { title = "C", yes = new CardChoice { label = "Choice C" } });
        LoadTestDataset();
        FastAnimations();
        yield return WaitReady();
        Swipe(Current, true);
        yield return WaitReady();
        var firstView = Current;
        Assert.That(firstView.transform.Find("Description").gameObject.activeSelf, Is.True);
        Assert.That(firstView.transform.Find("Artwork").GetComponent<Image>().sprite, Is.SameAs(sprite));
        Swipe(Current, true);
        yield return WaitReady();
        Swipe(Current, true);
        yield return WaitReady();
        Assert.That(Current, Is.SameAs(firstView));
        Assert.That(Current.transform.Find("Description").gameObject.activeSelf, Is.False);
        Assert.That(Current.transform.Find("Artwork").GetComponent<Image>().sprite, Is.Null);
        var yesText = (TMP_Text)DragType.GetField("yesText").GetValue(Current);
        Assert.That(yesText.text, Is.EqualTo("Choice C"));
        Assert.That(yesText.alpha, Is.Zero);
        Object.Destroy(sprite);
    }

    [UnityTest]
    public IEnumerator DownloadButtonTracksMotionAndWritesPng()
    {
        Swipe(Current, true);
        yield return WaitReady();
        var downloadType = Type.GetType("CardDownloadButton, Assembly-CSharp", true);
        var download = (Component)Object.FindFirstObjectByType(downloadType);
        var button = download.GetComponent<Button>();
        Assert.That(button.interactable, Is.True);
        var pointer = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(Current.gameObject, pointer, ExecuteEvents.beginDragHandler);
        Assert.That(button.interactable, Is.False);
        ExecuteEvents.Execute(Current.gameObject, pointer, ExecuteEvents.endDragHandler);
        yield return WaitReady();
        Assert.That(button.interactable, Is.True);
        button.onClick.Invoke();
        string path = (string)downloadType.GetProperty("LastSavedPath").GetValue(download);
        try
        {
            Assert.That(path, Is.Not.Null);
            Assert.That(System.IO.File.Exists(path), Is.True);
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Assert.That(bytes[0], Is.EqualTo(137));
            Assert.That(bytes[1], Is.EqualTo(80));
            Assert.That(bytes.Length, Is.GreaterThan(1000));
        }
        finally
        {
            if (path != null && System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    [UnityTest]
    public IEnumerator ThreeSequentialCardsIgnoreEmptyTargetsAndFinishWithoutEndRecord()
    {
        testDataset = ScriptableObject.CreateInstance<CardDataset>();
        for (int i = 0; i < 3; i++)
        {
            var card = new CardEntry { title = "Sequential " + i };
            card.yes.defaultTarget.type = CardTargetType.Card;
            testDataset.cards.Add(card);
        }
        int completed = 0;
        Action onComplete = () => completed++;
        ControllerType.GetEvent("GameCompleted").AddEventHandler(controller, onComplete);
        LoadTestDataset(false, false, false);
        FastAnimations();
        yield return WaitReady();
        for (int i = 0; i < 2; i++) { Swipe(Current, true); yield return WaitReady(); }
        var last = Current;
        Swipe(last, true);
        Assert.That(Current, Is.Null);
        Assert.That(Session.IsFinished, Is.True);
        Assert.That(completed, Is.EqualTo(1));
        float deadline = Time.realtimeSinceStartup + 4;
        while (last.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(last.gameObject.activeSelf, Is.False);
        Assert.That(completed, Is.EqualTo(1));
        Call("RestartGame");
        yield return WaitReady();
        Assert.That(Session.AnsweredCardCount, Is.Zero);
        Assert.That(Session.Current.id, Is.EqualTo(testDataset.cards[0].id));
        ControllerType.GetEvent("GameCompleted").RemoveEventHandler(controller, onComplete);
    }

    [UnityTest]
    public IEnumerator ThresholdReturnAndSecondPointerPreserveGestureBehavior()
    {
        Swipe(Current, true);
        yield return WaitReady();
        var card = Current;
        var origin = ((RectTransform)card.transform).anchoredPosition;
        var pointer = new PointerEventData(EventSystem.current) { pointerId = 11 };
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.beginDragHandler);
        pointer.delta = new Vector2(75, 0) * card.GetComponentInParent<Canvas>().scaleFactor;
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.dragHandler);
        var moved = ((RectTransform)card.transform).anchoredPosition;
        Assert.That(moved.x, Is.GreaterThan(origin.x));
        Assert.That(((TMP_Text)DragType.GetField("yesText").GetValue(card)).alpha, Is.InRange(0.45f, 0.55f));
        var other = new PointerEventData(EventSystem.current) { pointerId = 12, delta = Vector2.right * 5000 };
        ExecuteEvents.Execute(card.gameObject, other, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(card.gameObject, other, ExecuteEvents.endDragHandler);
        Assert.That(((RectTransform)card.transform).anchoredPosition, Is.EqualTo(moved));
        Assert.That(Session.AnsweredCardCount, Is.Zero);
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler);
        yield return WaitReady();
        Assert.That(Current, Is.SameAs(card));
        Assert.That(((RectTransform)card.transform).anchoredPosition, Is.EqualTo(origin));
        Assert.That(((TMP_Text)DragType.GetField("yesText").GetValue(card)).alpha, Is.Zero);
    }

    [UnityTest]
    public IEnumerator DatasetCheatUsesCommonOverlayAndPreservesProgress()
    {
        testDataset = ScriptableObject.CreateInstance<CardDataset>();
        testDataset.cards.Add(new CardEntry { title = "Normal" });
        testDataset.cards.Add(new CardEntry { role = CardRole.Cheat, cheatId = 73, title = "Dataset cheat" });
        LoadTestDataset();
        FastAnimations();
        yield return WaitReady();
        Swipe(Current, true);
        yield return WaitReady();
        var normal = Current;
        ControllerType.GetMethod("SpawnCheatCard").Invoke(controller, new object[] { 73 });
        FastAnimations();
        yield return WaitReady();
        var viewType = Type.GetType("CardView, Assembly-CSharp", true);
        var entry = (CardEntry)viewType.GetProperty("Entry").GetValue(Current.GetComponent(viewType));
        Assert.That(entry.role, Is.EqualTo(CardRole.Cheat));
        Assert.That(entry.title, Is.EqualTo("Dataset cheat"));
        Assert.That(Current.transform.Find("Settings Content"), Is.Not.Null);
        Assert.That(Current.transform.Find("Start Content"), Is.Not.Null);
        Swipe(Current, true);
        yield return WaitReady();
        Assert.That(Current, Is.SameAs(normal));
        Assert.That(Session.AnsweredCardCount, Is.Zero);
        Assert.That(Object.FindObjectsByType(DragType, FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator SettingsButtonsStillCallTheirOriginalHandlers()
    {
        int mode = PlayerPrefs.GetInt("TimeDisplayMode", 0);
        bool hadMode = PlayerPrefs.HasKey("TimeDisplayMode");
        Call("SpawnOptionsCard");
        FastAnimations();
        yield return WaitReady();
        try
        {
            var buttons = Current.GetComponentsInChildren<Button>();
            buttons.First(button => button.name == "ButtonGameTime").onClick.Invoke();
            Assert.That(PlayerPrefs.GetInt("TimeDisplayMode"), Is.EqualTo(1));
            buttons.First(button => button.name == "ButtonRealTime").onClick.Invoke();
            Assert.That(PlayerPrefs.GetInt("TimeDisplayMode"), Is.Zero);
            buttons.First(button => button.name == "ButtonReset").onClick.Invoke();
            Assert.That(PlayerPrefs.GetInt("TimeDisplayMode", 0), Is.Zero);
        }
        finally
        {
            if (hadMode) PlayerPrefs.SetInt("TimeDisplayMode", mode); else PlayerPrefs.DeleteKey("TimeDisplayMode");
            PlayerPrefs.Save();
        }
    }
}
