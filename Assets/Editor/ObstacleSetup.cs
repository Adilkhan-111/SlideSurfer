using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the obstacle prefabs real models instead of white boxes. The boxes stay as invisible hit boxes,
/// so the gameplay does not change. Three looks, built from simple shapes:
///   "Wet floor"      - yellow caution signs on a slick puddle   (single obstacle in the middle)
///   "Steel barrier"  - galvanised fence panel with a hazard plate (single obstacle on the sides, double obstacle)
///   "Road closed"    - striped road barricade with lamps and cones across the whole slide (great wall)
/// Run: Tools > SlideSurfer > Build Obstacle Models. Running it again rebuilds the models.
/// </summary>
public static class ObstacleSetup
{
    private const string PrefabDir = "Assets/Resources/Prefabs";
    private const string OutDir = "Assets/Obstacles";
    private const string VisualName = "Visual";

    private static Material yellow, black, puddle, metal, hazard, redWhite, white, orange, lamp;
    private static Mesh coneMesh;

    [MenuItem("Tools/SlideSurfer/Build Obstacle Models")]
    public static void BuildAll()
    {
        EnsureFolder(OutDir);
        EnsureFolder(OutDir + "/Materials");
        EnsureFolder(OutDir + "/Textures");
        CreateAssets();

        // single obstacle: wet floor in the middle, steel barrier on the sides
        EditPrefab("SingleWall", root =>
        {
            Transform box = HitBox(root, "SingleWall");
            Transform holder = Holder(box);
            Vector3 size = box.lossyScale;
            GameObject wet = BuildWetFloor(holder, size);
            GameObject steel = BuildSteelBarrier(holder, size);
            var variant = root.GetComponent<ObstacleVariant>();
            if (variant == null) variant = root.gameObject.AddComponent<ObstacleVariant>();
            var so = new SerializedObject(variant);
            so.FindProperty("centerVisual").objectReferenceValue = wet;
            so.FindProperty("sideVisual").objectReferenceValue = steel;
            so.ApplyModifiedPropertiesWithoutUndo();
            steel.SetActive(false);
        });

        // double obstacle: a steel barrier on each of its two boxes
        EditPrefab("DoubleWall", root =>
        {
            foreach (string name in new[] { "SingleW", "SingleWRotated" })
            {
                Transform box = HitBox(root, name);
                BuildSteelBarrier(Holder(box), box.lossyScale);
            }
        });

        // full-width wall: road closed barricade that has to be jumped
        EditPrefab("GreatWall", root =>
        {
            Transform box = HitBox(root, "SingleWall");
            BuildRoadClosed(Holder(box), box.lossyScale);
        });

        AssetDatabase.SaveAssets();
        Debug.Log("ObstacleSetup: obstacle models built");
    }

    // ---------------------------------------------------------------- prefab plumbing

    private static void EditPrefab(string name, System.Action<Transform> edit)
    {
        string path = PrefabDir + "/" + name + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // the old white box: keep its collider, hide its mesh, drop a previous model
    private static Transform HitBox(Transform root, string name)
    {
        // the box child can share the root's name (SingleWall/SingleWall), so look among the children first
        Transform box = FindDeep(root, name);
        if (box == null) box = root;
        var renderer = box.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.enabled = false;
        Transform old = box.Find(VisualName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        return box;
    }

    // a child that cancels the box's (and its parents') scale, so the model can be built in metres
    private static Transform Holder(Transform box)
    {
        var go = new GameObject(VisualName);
        go.transform.SetParent(box, false);
        Vector3 s = box.lossyScale;
        go.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
        return go.transform;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t)
        {
            if (c.name == name) return c;
            Transform found = FindDeep(c, name);
            if (found != null) return found;
        }
        return null;
    }

    // ---------------------------------------------------------------- models
    // Every model fills the hit box it replaces: x = width, y = height, z = depth, centred on the box.

    private static GameObject BuildWetFloor(Transform holder, Vector3 box)
    {
        var root = Group(holder, "WetFloor");
        float floor = -box.y * 0.5f + 0.22f;   // the box sinks a little into the slide

        // a glossy slick spreading under the signs
        Part(root, PrimitiveType.Cylinder, puddle, new Vector3(0f, floor + 0.01f, 0f), Vector3.zero, new Vector3(box.x * 1.15f, 0.01f, 1.7f));
        Part(root, PrimitiveType.Cylinder, puddle, new Vector3(0.7f, floor + 0.012f, 0.45f), Vector3.zero, new Vector3(1.1f, 0.01f, 0.8f));

        // three folding caution signs side by side, a little out of line like they were put down in a hurry
        float[] xs = { -box.x * 0.33f, 0f, box.x * 0.33f };
        float[] yaw = { 12f, -6f, 9f };
        for (int i = 0; i < xs.Length; i++)
        {
            var sign = Group(root, "Sign" + i).transform;
            sign.localPosition = new Vector3(xs[i], floor, 0f);
            sign.localRotation = Quaternion.Euler(0f, yaw[i], 0f);
            CautionSign(sign, 1.45f, 0.72f);
        }
        return root;
    }

    // an A-frame "wet floor" sign: two yellow boards leaning together, black border and a big exclamation mark
    private static void CautionSign(Transform parent, float height, float width)
    {
        const float lean = 13f;
        float halfDepth = Mathf.Sin(lean * Mathf.Deg2Rad) * height * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            var board = new GameObject(side < 0 ? "Front" : "Back").transform;
            board.SetParent(parent, false);
            board.localPosition = new Vector3(0f, height * 0.5f, side * halfDepth);
            // both boards tip their tops toward the middle; the back one is turned round so its face looks outward too
            board.localRotation = Quaternion.Euler(lean, side < 0 ? 0f : 180f, 0f);

            Part(board, PrimitiveType.Cube, yellow, Vector3.zero, Vector3.zero, new Vector3(width, height, 0.035f));
            // black frame on the outer face
            float fz = -0.02f;
            Part(board, PrimitiveType.Cube, black, new Vector3(0f, height * 0.5f - 0.04f, fz), Vector3.zero, new Vector3(width, 0.08f, 0.01f));
            Part(board, PrimitiveType.Cube, black, new Vector3(0f, -height * 0.5f + 0.04f, fz), Vector3.zero, new Vector3(width, 0.08f, 0.01f));
            Part(board, PrimitiveType.Cube, black, new Vector3(-width * 0.5f + 0.04f, 0f, fz), Vector3.zero, new Vector3(0.08f, height, 0.01f));
            Part(board, PrimitiveType.Cube, black, new Vector3(width * 0.5f - 0.04f, 0f, fz), Vector3.zero, new Vector3(0.08f, height, 0.01f));
            // warning triangle with "!"
            Part(board, PrimitiveType.Cube, black, new Vector3(0f, 0.12f, fz), Vector3.zero, new Vector3(0.1f, 0.42f, 0.01f));
            Part(board, PrimitiveType.Cube, black, new Vector3(0f, -0.2f, fz), Vector3.zero, new Vector3(0.1f, 0.1f, 0.01f));
            Part(board, PrimitiveType.Cube, black, new Vector3(0f, -0.38f, fz), Vector3.zero, new Vector3(width * 0.62f, 0.06f, 0.01f));
        }
        // hinge on top
        Part(parent, PrimitiveType.Cylinder, black, new Vector3(0f, height * Mathf.Cos(13f * Mathf.Deg2Rad), 0f), new Vector3(0f, 0f, 90f), new Vector3(0.05f, width * 0.5f, 0.05f));
    }

    private static GameObject BuildSteelBarrier(Transform holder, Vector3 box)
    {
        var root = Group(holder, "SteelBarrier");
        float bottom = -box.y * 0.5f + 0.1f;
        float top = bottom + 2.3f;
        float halfW = box.x * 0.5f - 0.08f;
        const float post = 0.05f;

        // frame: two posts and three rails
        foreach (float x in new[] { -halfW, halfW })
        {
            Part(root, PrimitiveType.Cylinder, metal, new Vector3(x, (bottom + top) * 0.5f, 0f), Vector3.zero, new Vector3(post * 2f, (top - bottom) * 0.5f, post * 2f));
            // flat foot so it stands on its own
            Part(root, PrimitiveType.Cube, metal, new Vector3(x, bottom + 0.02f, 0f), Vector3.zero, new Vector3(0.12f, 0.04f, box.z * 0.9f));
            Part(root, PrimitiveType.Sphere, metal, new Vector3(x, top, 0f), Vector3.zero, Vector3.one * 0.13f);
        }
        foreach (float y in new[] { bottom + 0.15f, (bottom + top) * 0.5f + 0.35f, top - 0.02f })
            Part(root, PrimitiveType.Cylinder, metal, new Vector3(0f, y, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.07f, halfW, 0.07f));

        // vertical bars
        int bars = Mathf.RoundToInt(halfW * 2f / 0.18f);
        for (int i = 1; i < bars; i++)
        {
            float x = Mathf.Lerp(-halfW, halfW, (float)i / bars);
            Part(root, PrimitiveType.Cylinder, metal, new Vector3(x, (bottom + top) * 0.5f, 0f), Vector3.zero, new Vector3(0.035f, (top - bottom) * 0.5f, 0.035f));
        }

        // yellow and black hazard plate across the middle
        float plateY = (bottom + top) * 0.5f - 0.15f;
        Part(root, PrimitiveType.Cube, hazard, new Vector3(0f, plateY, 0f), Vector3.zero, new Vector3(halfW * 2f - 0.1f, 0.5f, 0.06f));
        return root;
    }

    private static GameObject BuildRoadClosed(Transform holder, Vector3 box)
    {
        var root = Group(holder, "RoadClosed");
        float bottom = -box.y * 0.5f + 0.2f;
        float halfW = box.x * 0.5f;

        // A-frame legs every couple of metres
        int legs = Mathf.Max(2, Mathf.RoundToInt(box.x / 2.2f) + 1);
        float legTop = bottom + 1.9f;
        for (int i = 0; i < legs; i++)
        {
            float x = Mathf.Lerp(-halfW + 0.3f, halfW - 0.3f, (float)i / (legs - 1));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(root, PrimitiveType.Cube, white, new Vector3(x, (bottom + legTop) * 0.5f, side * 0.25f), new Vector3(side * 12f, 0f, 0f), new Vector3(0.1f, legTop - bottom, 0.1f));
            }
            // flashing amber lamp on every leg
            Part(root, PrimitiveType.Cylinder, black, new Vector3(x, legTop + 0.06f, 0f), Vector3.zero, new Vector3(0.12f, 0.06f, 0.12f));
            Part(root, PrimitiveType.Sphere, lamp, new Vector3(x, legTop + 0.2f, 0f), Vector3.zero, Vector3.one * 0.22f);
        }

        // three red and white striped boards across the whole slide
        foreach (float y in new[] { bottom + 0.55f, bottom + 1.1f, bottom + 1.65f })
            Part(root, PrimitiveType.Cube, redWhite, new Vector3(0f, y, -0.3f), Vector3.zero, new Vector3(box.x, 0.32f, 0.06f));

        // traffic cones in front
        float[] cones = { -halfW * 0.75f, -halfW * 0.25f, halfW * 0.25f, halfW * 0.75f };
        foreach (float x in cones)
            Cone(root, new Vector3(x, bottom, -0.75f));
        return root;
    }

    private static void Cone(GameObject parent, Vector3 pos)
    {
        var cone = new GameObject("Cone");
        cone.transform.SetParent(parent.transform, false);
        cone.transform.localPosition = pos;
        cone.AddComponent<MeshFilter>().sharedMesh = coneMesh;
        cone.AddComponent<MeshRenderer>().sharedMaterial = orange;
        Part(cone, PrimitiveType.Cube, black, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.5f, 0.04f, 0.5f));
        // white reflective band
        Part(cone, PrimitiveType.Cylinder, white, new Vector3(0f, 0.42f, 0f), Vector3.zero, new Vector3(0.21f, 0.05f, 0.21f));
    }

    private static GameObject Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject Group(GameObject parent, string name) => Group(parent.transform, name);

    private static GameObject Part(GameObject parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
        => Part(parent.transform, type, mat, pos, euler, scale);

    private static GameObject Part(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());   // the old box is the hit box
        go.name = type.ToString();
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go;
    }

    // ---------------------------------------------------------------- materials, textures, meshes

    private static void CreateAssets()
    {
        Texture2D hazardTex = StripeTexture("Hazard", new Color(1f, 0.8f, 0.05f), new Color(0.08f, 0.08f, 0.08f));
        Texture2D redWhiteTex = StripeTexture("RedWhite", new Color(0.85f, 0.1f, 0.08f), new Color(0.97f, 0.97f, 0.97f));

        yellow = Lit("CautionYellow", new Color(1f, 0.82f, 0.05f), 0f, 0.55f);
        black = Lit("Black", new Color(0.06f, 0.06f, 0.06f), 0f, 0.4f);
        puddle = Lit("Puddle", new Color(0.35f, 0.62f, 0.85f), 0.3f, 0.97f);
        metal = Lit("GalvanisedSteel", new Color(0.72f, 0.75f, 0.78f), 0.9f, 0.65f);
        white = Lit("White", new Color(0.95f, 0.95f, 0.95f), 0f, 0.5f);
        orange = Lit("ConeOrange", new Color(1f, 0.42f, 0.05f), 0f, 0.45f);
        hazard = Lit("HazardStripes", Color.white, 0.2f, 0.5f, hazardTex, new Vector2(4f, 1f));
        redWhite = Lit("RedWhiteStripes", Color.white, 0f, 0.55f, redWhiteTex, new Vector2(9f, 1f));
        lamp = Lit("AmberLamp", new Color(1f, 0.6f, 0.05f), 0f, 0.8f, null, Vector2.one, new Color(1f, 0.55f, 0.05f) * 3f);

        coneMesh = ConeMesh();
    }

    private static Material Lit(string name, Color color, float metallic, float smoothness,
        Texture2D tex = null, Vector2 tiling = default, Color emission = default)
    {
        string path = OutDir + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling);
        }
        if (emission != default)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // diagonal two-colour stripes, tileable
    private static Texture2D StripeTexture(string name, Color a, Color b)
    {
        string path = OutDir + "/Textures/" + name + ".png";
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, ((x + y) / (size / 2)) % 2 == 0 ? a : b);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // traffic cone: a tapered tube with a rounded-off top, base on y = 0, 0.75 m tall
    private static Mesh ConeMesh()
    {
        string path = OutDir + "/Cone.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = "Cone" };
            AssetDatabase.CreateAsset(mesh, path);
        }
        const int seg = 20;
        const float h = 0.75f, rBottom = 0.2f, rTop = 0.035f;
        var verts = new Vector3[(seg + 1) * 2 + 1];
        var tris = new int[seg * 9];
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            verts[i * 2] = dir * rBottom;
            verts[i * 2 + 1] = dir * rTop + Vector3.up * h;
        }
        int tip = verts.Length - 1;
        verts[tip] = Vector3.up * (h + 0.01f);
        int t = 0;
        for (int i = 0; i < seg; i++)
        {
            int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
            tris[t++] = b0; tris[t++] = t0; tris[t++] = b1;
            tris[t++] = b1; tris[t++] = t0; tris[t++] = t1;
            tris[t++] = t0; tris[t++] = tip; tris[t++] = t1;
        }
        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
