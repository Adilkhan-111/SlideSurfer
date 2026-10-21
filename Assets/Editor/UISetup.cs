using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the menus on the Canvas: the settings window (music / sound effects on-off and volume, graphics
/// quality, works as a pause menu during a run), the shop button in the bottom-right corner and the
/// "will be added soon" window it opens. Sprites (rounded card, circle, shopping bag) are drawn by code.
/// Run: Tools > SlideSurfer > Setup Menus. Running it again rebuilds them.
/// </summary>
public static class UISetup
{
    private const string ScenePath = "Assets/Scenes/MainGame.unity";
    private const string SpriteDir = "Assets/UI/Generated";

    private static readonly Color Accent = new Color(0.15f, 0.65f, 0.75f);
    private static readonly Color CardColor = new Color(0.97f, 0.98f, 0.99f);
    private static readonly Color Ink = new Color(0.1f, 0.25f, 0.33f);
    private static readonly Color Soft = new Color(0.86f, 0.9f, 0.92f);

    private static Sprite rounded, circle, bag;
    private static TMP_FontAsset font;

    [MenuItem("Tools/SlideSurfer/Setup Menus")]
    public static void SetupAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Menus", "Stop Play mode first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureFolder(SpriteDir);
        rounded = SaveSprite("RoundedRect", RoundedRect(128, 44), 48);
        circle = SaveSprite("Circle", Circle(256), 0);
        bag = SaveSprite("ShopBag", Bag(256), 0);

        GameObject canvas = GameObject.Find("Canvas");
        font = canvas.transform.Find("Score").GetComponent<TMP_Text>().font;
        if (canvas.GetComponent<UIInputGuard>() == null) Undo.AddComponent<UIInputGuard>(canvas);

        BuildSettings(canvas.transform.Find("SettingsMenu").gameObject);
        MessagePopup popup = BuildPopup(canvas.transform);
        BuildShopButton(canvas.transform, popup);
        BuildScoreUI(canvas.transform);

        // windows on top of everything else
        canvas.transform.Find("SettingsMenu").SetAsLastSibling();
        popup.transform.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("UISetup: menus built");
    }

    // ---------------------------------------------------------------- settings

    private static void BuildSettings(GameObject menu)
    {
        // the existing SettingsMenu becomes a full-screen dim behind the card
        var rt = (RectTransform)menu.transform;
        Stretch(rt);
        rt.localScale = Vector3.one;
        var dim = menu.GetComponent<Image>();
        dim.sprite = null;
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        for (int i = rt.childCount - 1; i >= 0; i--) Object.DestroyImmediate(rt.GetChild(i).gameObject);

        RectTransform card = Card(rt, "Card", new Vector2(880f, 1440f));
        TMP_Text title = Text(card, "Title", "SETTINGS", 84, FontStyles.Bold, Ink, new Vector2(0f, 620f), new Vector2(760f, 120f));

        Toggle musicToggle; Slider musicSlider;
        AudioRow(card, "Music", 440f, out musicToggle, out musicSlider);
        Toggle sfxToggle; Slider sfxSlider;
        AudioRow(card, "Sound effects", 170f, out sfxToggle, out sfxSlider);

        // steering sensitivity with its value on the right
        Text(card, "SensitivityLabel", "Sensitivity", 60, FontStyles.Bold, Ink, new Vector2(-150f, -40f), new Vector2(460f, 90f), TextAlignmentOptions.Left);
        TMP_Text sensitivityValue = Text(card, "SensitivityValue", "1.0x", 56, FontStyles.Bold, Accent, new Vector2(280f, -40f), new Vector2(200f, 90f), TextAlignmentOptions.Right);
        Slider sensitivity = VolumeSlider(card, "SensitivitySlider", new Vector2(0f, -160f));
        sensitivity.minValue = ControlSettings.Min;
        sensitivity.maxValue = ControlSettings.Max;
        sensitivity.value = 1f;

        // graphics quality: three segmented buttons
        Text(card, "GraphicsLabel", "Graphics", 60, FontStyles.Bold, Ink, new Vector2(-150f, -300f), new Vector2(460f, 90f), TextAlignmentOptions.Left);
        string[] names = { "Low", "Medium", "High" };
        var quality = new Button[3];
        for (int i = 0; i < 3; i++)
            quality[i] = ButtonWithText(card, "Quality" + names[i], names[i], new Vector2(-260f + i * 260f, -420f), new Vector2(240f, 110f), Soft, Ink, 48);

        Button close = ButtonWithText(card, "Close", "CLOSE", new Vector2(0f, -600f), new Vector2(600f, 140f), Accent, Color.white, 60);

        var panel = menu.GetComponent<SettingsPanel>();
        if (panel == null) panel = Undo.AddComponent<SettingsPanel>(menu);
        var so = new SerializedObject(panel);
        so.FindProperty("title").objectReferenceValue = title;
        so.FindProperty("musicToggle").objectReferenceValue = musicToggle;
        so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
        so.FindProperty("sfxToggle").objectReferenceValue = sfxToggle;
        so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
        so.FindProperty("sensitivitySlider").objectReferenceValue = sensitivity;
        so.FindProperty("sensitivityValue").objectReferenceValue = sensitivityValue;
        var qb = so.FindProperty("qualityButtons");
        qb.arraySize = 3;
        for (int i = 0; i < 3; i++) qb.GetArrayElementAtIndex(i).objectReferenceValue = quality[i];
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.ApplyModifiedPropertiesWithoutUndo();
        menu.SetActive(false);
    }

    // "Music      (o  )"  with a volume slider under it
    private static void AudioRow(RectTransform card, string label, float y, out Toggle toggle, out Slider slider)
    {
        Text(card, label + "Label", label, 60, FontStyles.Bold, Ink, new Vector2(-150f, y + 60f), new Vector2(460f, 90f), TextAlignmentOptions.Left);
        toggle = Switch(card, label + "Switch", new Vector2(280f, y + 60f));
        slider = VolumeSlider(card, label + "Volume", new Vector2(0f, y - 60f));
    }

    private static Toggle Switch(RectTransform parent, string name, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(ToggleSwitch));
        var rt = Place(go, parent, pos, new Vector2(170f, 90f));
        var track = go.GetComponent<Image>();
        track.sprite = rounded;
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 1.3f;

        var knobGo = new GameObject("Knob", typeof(RectTransform), typeof(Image));
        var knob = Place(knobGo, rt, Vector2.zero, new Vector2(72f, 72f));
        knobGo.GetComponent<Image>().sprite = circle;
        knobGo.GetComponent<Image>().raycastTarget = false;

        var toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = track;
        toggle.transition = Selectable.Transition.None;
        toggle.isOn = true;

        var so = new SerializedObject(go.GetComponent<ToggleSwitch>());
        so.FindProperty("track").objectReferenceValue = track;
        so.FindProperty("knob").objectReferenceValue = knob;
        so.FindProperty("knobTravel").floatValue = 40f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return toggle;
    }

    private static Slider VolumeSlider(RectTransform parent, string name, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        var rt = Place(go, parent, pos, new Vector2(740f, 70f));

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var bg = (RectTransform)bgGo.transform;
        bg.SetParent(rt, false);
        bg.anchorMin = new Vector2(0f, 0.3f); bg.anchorMax = new Vector2(1f, 0.7f);
        bg.offsetMin = bg.offsetMax = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = rounded; bgImg.type = Image.Type.Sliced; bgImg.pixelsPerUnitMultiplier = 3f; bgImg.color = Soft;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(rt, false);
        fillArea.anchorMin = new Vector2(0f, 0.3f); fillArea.anchorMax = new Vector2(1f, 0.7f);
        fillArea.offsetMin = new Vector2(10f, 0f); fillArea.offsetMax = new Vector2(-10f, 0f);
        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fill = (RectTransform)fillGo.transform;
        fill.SetParent(fillArea, false);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(-10f, 0f); fill.offsetMax = new Vector2(10f, 0f);
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.sprite = rounded; fillImg.type = Image.Type.Sliced; fillImg.pixelsPerUnitMultiplier = 3f; fillImg.color = Accent;

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(rt, false);
        handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(35f, 0f); handleArea.offsetMax = new Vector2(-35f, 0f);
        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        var handle = (RectTransform)handleGo.transform;
        handle.SetParent(handleArea, false);
        handle.anchorMin = new Vector2(0f, 0f); handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(70f, 0f);
        var handleImg = handleGo.GetComponent<Image>();
        handleImg.sprite = circle; handleImg.color = Color.white;
        var shadow = handleGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        shadow.effectDistance = new Vector2(0f, -4f);

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0.7f;
        var colors = slider.colors;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        slider.colors = colors;
        return slider;
    }

    // ---------------------------------------------------------------- popup and shop

    private static MessagePopup BuildPopup(Transform canvas)
    {
        Transform old = canvas.Find("MessagePopup");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject("MessagePopup", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MessagePopup));
        Undo.RegisterCreatedObjectUndo(root, "Popup");
        var rt = (RectTransform)root.transform;
        rt.SetParent(canvas, false);
        Stretch(rt);
        var dim = root.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        var dimButton = root.GetComponent<Button>();
        dimButton.transition = Selectable.Transition.None;

        RectTransform card = Card(rt, "Card", new Vector2(800f, 640f));
        // the shop bag sits on a badge that overlaps the top edge of the card
        var badge = Img(card, "Badge", circle, Accent, new Vector2(0f, 320f), new Vector2(220f, 220f));
        Img(badge, "Icon", bag, Color.white, Vector2.zero, new Vector2(130f, 130f));
        TMP_Text title = Text(card, "Title", "SHOP", 84, FontStyles.Bold, Ink, new Vector2(0f, 120f), new Vector2(700f, 110f));
        TMP_Text message = Text(card, "Message", "Will be added soon!", 56, FontStyles.Normal, new Color(0.3f, 0.4f, 0.45f), new Vector2(0f, 0f), new Vector2(700f, 140f));
        Button ok = ButtonWithText(card, "OK", "OK", new Vector2(0f, -200f), new Vector2(400f, 140f), Accent, Color.white, 60);

        var popup = root.GetComponent<MessagePopup>();
        var so = new SerializedObject(popup);
        so.FindProperty("card").objectReferenceValue = card;
        so.FindProperty("title").objectReferenceValue = title;
        so.FindProperty("message").objectReferenceValue = message;
        so.FindProperty("okButton").objectReferenceValue = ok;
        so.FindProperty("backgroundButton").objectReferenceValue = dimButton;
        so.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(false);
        return popup;
    }

    private static void BuildShopButton(Transform canvas, MessagePopup popup)
    {
        Transform old = canvas.Find("ShopButton");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("ShopButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ShopButton));
        Undo.RegisterCreatedObjectUndo(go, "Shop button");
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas, false);
        // bottom-right, level with the coin counter on the left
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 70f);
        rt.sizeDelta = new Vector2(150f, 150f);
        var img = go.GetComponent<Image>();
        img.sprite = circle;
        img.color = Accent;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
        shadow.effectDistance = new Vector2(0f, -6f);
        Img(rt, "Icon", bag, Color.white, new Vector2(0f, 4f), new Vector2(92f, 92f));

        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;

        var so = new SerializedObject(go.GetComponent<ShopButton>());
        so.FindProperty("popup").objectReferenceValue = popup;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- best score

    // "BEST 123" under the score, a "NEW RECORD!" flash, and the result on the defeat window
    private static void BuildScoreUI(Transform canvas)
    {
        foreach (string n in new[] { "BestScore", "NewRecordBanner" })
        {
            Transform old = canvas.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        var bestGo = new GameObject("BestScore", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(bestGo, "Best score");
        var brt = (RectTransform)bestGo.transform;
        brt.SetParent(canvas, false);
        brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0f, 1f);
        brt.anchoredPosition = new Vector2(44f, -245f);
        brt.sizeDelta = new Vector2(600f, 80f);
        var best = bestGo.GetComponent<TextMeshProUGUI>();
        best.font = font;
        best.text = "BEST 0";
        best.fontSize = 60;
        best.fontStyle = FontStyles.Bold;
        best.color = new Color(1f, 0.9f, 0.45f);
        best.alignment = TextAlignmentOptions.TopLeft;
        best.raycastTarget = false;

        var bannerGo = new GameObject("NewRecordBanner", typeof(RectTransform), typeof(Image), typeof(PopIn));
        Undo.RegisterCreatedObjectUndo(bannerGo, "Record banner");
        var nrt = (RectTransform)bannerGo.transform;
        nrt.SetParent(canvas, false);
        nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 1f);
        nrt.pivot = new Vector2(0.5f, 0.5f);
        nrt.anchoredPosition = new Vector2(0f, -470f);
        nrt.sizeDelta = new Vector2(820f, 170f);
        var bannerImg = bannerGo.GetComponent<Image>();
        bannerImg.sprite = rounded;
        bannerImg.type = Image.Type.Sliced;
        bannerImg.color = new Color(1f, 0.55f, 0.15f);
        bannerImg.raycastTarget = false;
        Text(nrt, "Label", "NEW RECORD!", 96, FontStyles.Bold, Color.white, Vector2.zero, nrt.sizeDelta);
        bannerGo.SetActive(false);

        // result on the defeat window, above the Replay button
        Transform defeat = canvas.Find("DefeatWindow");
        Transform oldResult = defeat.Find("Result");
        if (oldResult != null) Object.DestroyImmediate(oldResult.gameObject);
        TMP_Text result = Text((RectTransform)defeat, "Result", "SCORE 0\n<size=80%>BEST 0</size>", 84, FontStyles.Bold, Color.white,
            new Vector2(0f, 60f), new Vector2(640f, 260f));

        var ui = Object.FindAnyObjectByType<UIManager>();
        var so = new SerializedObject(ui);
        so.FindProperty("BestTXT").objectReferenceValue = best;
        so.FindProperty("NewRecordBanner").objectReferenceValue = bannerGo;
        so.FindProperty("ResultTXT").objectReferenceValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- building blocks

    private static RectTransform Card(RectTransform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = Place(go, parent, Vector2.zero, size);
        var img = go.GetComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = CardColor;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
        shadow.effectDistance = new Vector2(0f, -10f);
        return rt;
    }

    private static RectTransform Img(RectTransform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = Place(go, parent, pos, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return rt;
    }

    private static TMP_Text Text(RectTransform parent, string name, string text, float size, FontStyles style, Color color,
        Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Place(go, parent, pos, box);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private static Button ButtonWithText(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, Color bg, Color fg, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = Place(go, parent, pos, size);
        var img = go.GetComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = bg;
        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;
        var t = Text(rt, "Label", label, fontSize, FontStyles.Bold, fg, Vector2.zero, size);
        t.raycastTarget = false;
        return button;
    }

    private static RectTransform Place(GameObject go, RectTransform parent, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    // ---------------------------------------------------------------- sprites drawn by code

    private static Color[] RoundedRect(int size, float radius)
    {
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
        return px;
    }

    private static Color[] Circle(int size)
    {
        var px = new Color[size * size];
        float c = size * 0.5f, r = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) - r;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
        return px;
    }

    // a shopping bag: a slightly tapered body with rounded corners and a handle loop on top
    private static Color[] Bag(int size)
    {
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                // body: wider at the bottom
                float halfW = Mathf.Lerp(0.36f, 0.31f, Mathf.InverseLerp(0.1f, 0.66f, v));
                float bx = Mathf.Abs(u - 0.5f) - halfW, by = Mathf.Max(0.1f - v, v - 0.66f);
                const float r = 0.06f;
                float body = new Vector2(Mathf.Max(bx + r, 0f), Mathf.Max(by + r, 0f)).magnitude
                             + Mathf.Min(Mathf.Max(bx + r, by + r), 0f) - r;
                // handle: the upper half of a ring
                float ring = Mathf.Abs(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.66f)) - 0.16f) - 0.035f;
                if (v < 0.66f) ring = 1f;
                // two little holes where the handle meets the body
                float holes = Mathf.Min(Vector2.Distance(new Vector2(u, v), new Vector2(0.34f, 0.56f)), Vector2.Distance(new Vector2(u, v), new Vector2(0.66f, 0.56f))) - 0.03f;
                float d = Mathf.Max(Mathf.Min(body, ring), -holes);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d * size));
            }
        return px;
    }

    private static Sprite SaveSprite(string name, Color[] pixels, int border)
    {
        int size = (int)Mathf.Sqrt(pixels.Length);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        string path = SpriteDir + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
