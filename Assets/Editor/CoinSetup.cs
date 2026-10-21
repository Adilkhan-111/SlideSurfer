using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Sets up the coins: the coin prefab (gold disc with a rim and a raised star), the coin icon,
/// the counter at the bottom of the screen and the spawner on SpawnManager.
/// Run: Tools > SlideSurfer > Setup Coins. Running it again rebuilds everything.
/// </summary>
public static class CoinSetup
{
    private const string ScenePath = "Assets/Scenes/MainGame.unity";
    private const string PrefabPath = "Assets/Resources/Prefabs/Coin.prefab";
    private const string OutDir = "Assets/Coins";
    private const string IconPath = OutDir + "/CoinIcon.png";
    private const string SparkleMaterialPath = "Assets/Water/Spray.mat";
    private const string GameSettingsPath = "Assets/GameSettings.asset";
    private const string CounterName = "CoinCounter";

    [MenuItem("Tools/SlideSurfer/Setup Coins")]
    public static void SetupAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Coins", "Stop Play mode first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureFolder(OutDir);
        GameObject prefab = BuildCoinPrefab();
        Sprite icon = BuildIcon();
        BuildCounter(icon);
        AddSpawner(prefab);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CoinSetup: coins ready");
    }

    // ---------------------------------------------------------------- coin

    private static GameObject BuildCoinPrefab()
    {
        Material gold = Lit("Gold", new Color(1f, 0.76f, 0.15f), 1f, 0.82f, new Color(0.4f, 0.25f, 0.02f));
        Material goldDark = Lit("GoldRim", new Color(0.85f, 0.52f, 0.06f), 1f, 0.7f, new Color(0.2f, 0.1f, 0f));
        Material goldLight = Lit("GoldFace", new Color(1f, 0.88f, 0.4f), 1f, 0.9f, new Color(0.45f, 0.32f, 0.05f));

        var root = new GameObject("Coin");
        var collider = root.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.55f;
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // the coin stands upright facing the player and spins around Y; the model sits under a child so the root never rotates
        var model = new GameObject("Model").transform;
        model.SetParent(root.transform, false);
        var disc = new GameObject("Disc").transform;
        disc.SetParent(model, false);
        disc.localRotation = Quaternion.Euler(90f, 0f, 0f);   // cylinder axis along Z: faces look at the camera

        const float d = 0.7f;
        Part(disc, PrimitiveType.Cylinder, goldDark, Vector3.zero, Vector3.zero, new Vector3(d, 0.045f, d));            // rim
        Part(disc, PrimitiveType.Cylinder, gold, Vector3.zero, Vector3.zero, new Vector3(d * 0.86f, 0.05f, d * 0.86f));   // face
        // a raised eight-point star on both faces
        foreach (float y in new[] { 0.05f, -0.05f })
        {
            Part(disc, PrimitiveType.Cube, goldLight, new Vector3(0f, y, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.24f, 0.02f, 0.24f));
            Part(disc, PrimitiveType.Cube, goldLight, new Vector3(0f, y, 0f), new Vector3(0f, 45f, 0f), new Vector3(0.24f, 0.02f, 0.24f));
        }

        // moves toward the player like an obstacle, stops when the player dies
        var mover = root.AddComponent<GoPipeGo>();
        var coin = root.AddComponent<Coin>();
        var soMover = new SerializedObject(mover);
        soMover.FindProperty("gameSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameSettings>(GameSettingsPath);
        soMover.FindProperty("isObstacle").boolValue = true;
        soMover.ApplyModifiedPropertiesWithoutUndo();
        var soCoin = new SerializedObject(coin);
        soCoin.FindProperty("model").objectReferenceValue = model;
        soCoin.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void Part(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = type.ToString();
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // tiny and many: shadows are not worth it on mobile
    }

    private static Material Lit(string name, Color color, float metallic, float smoothness, Color emission)
    {
        string path = OutDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        // a little glow so the coins read against the water even in shade
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------- UI

    // a round gold coin drawn into a texture: dark rim, bright face, inner ring and a shine
    private static Sprite BuildIcon()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var rim = new Color(0.8f, 0.48f, 0.05f);
        var face = new Color(1f, 0.78f, 0.18f);
        var ring = new Color(0.9f, 0.6f, 0.08f);
        var shine = new Color(1f, 0.97f, 0.75f);
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                Color col = r < 0.82f ? face : rim;
                if (r > 0.62f && r < 0.68f) col = ring;
                // shine streak in the upper left
                float s = Mathf.Abs((dx + dy) * 0.7071f + 0.35f);
                if (r < 0.6f && s < 0.08f) col = Color.Lerp(col, shine, 0.8f);
                // soft antialiased edge
                col.a = Mathf.Clamp01((1f - r) * c * 0.5f);
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        File.WriteAllBytes(IconPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(IconPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    private static void BuildCounter(Sprite icon)
    {
        GameObject canvas = GameObject.Find("Canvas");
        Transform old = canvas.transform.Find(CounterName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // bottom-left pill: [coin] 12
        var counter = new GameObject(CounterName, typeof(RectTransform), typeof(Image), typeof(CoinCounterUI));
        Undo.RegisterCreatedObjectUndo(counter, "Coin counter");
        var rt = (RectTransform)counter.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(40f, 70f);
        rt.sizeDelta = new Vector2(380f, 150f);
        var bg = counter.GetComponent<Image>();
        bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, 0.35f);
        bg.raycastTarget = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var irt = (RectTransform)iconGo.transform;
        irt.SetParent(rt, false);
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(18f, 0f);
        irt.sizeDelta = new Vector2(115f, 115f);
        var img = iconGo.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var textGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
        var trt = (RectTransform)textGo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(150f, 0f);
        trt.offsetMax = new Vector2(-20f, 0f);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        var score = canvas.transform.Find("Score");
        if (score != null) text.font = score.GetComponent<TMP_Text>().font;
        text.text = "0";
        text.fontSize = 96;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.9f, 0.45f);
        text.alignment = TextAlignmentOptions.Left;
        text.enableAutoSizing = false;
        text.raycastTarget = false;

        var ui = counter.GetComponent<CoinCounterUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("label").objectReferenceValue = text;
        so.FindProperty("bounceTarget").objectReferenceValue = rt;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- spawner

    private static void AddSpawner(GameObject prefab)
    {
        GameObject manager = GameObject.Find("SpawnManager");
        var spawner = manager.GetComponent<CoinSpawner>();
        if (spawner == null) spawner = Undo.AddComponent<CoinSpawner>(manager);
        var so = new SerializedObject(spawner);
        so.FindProperty("coinPrefab").objectReferenceValue = prefab;
        so.FindProperty("sparkleMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(SparkleMaterialPath);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
