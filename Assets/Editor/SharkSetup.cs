using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the shark prefab a real model and the SharkMover: a lofted body (grey back, white belly),
/// dorsal, pectoral and tail fins, eyes, gills and a toothy mouth. The old cube stays as an invisible,
/// shark-sized hit box. Run: Tools > SlideSurfer > Build Shark. Running it again rebuilds the model.
/// The model points its nose along +Z; SharkMover turns the whole thing toward the player.
/// </summary>
public static class SharkSetup
{
    private const string PrefabPath = "Assets/Resources/Prefabs/Shark.prefab";
    private const string OutDir = "Assets/Shark";
    private const string GameSettingsPath = "Assets/GameSettings.asset";

    // body: nose at +Length/2, tail stalk at -Length/2
    private const float Length = 3f;
    private static readonly Color Back = new Color(0.36f, 0.46f, 0.56f);
    private static readonly Color Belly = new Color(0.93f, 0.94f, 0.95f);

    // half width and half height of the body along its length (t: 0 = nose, 1 = tail stalk)
    private static readonly AnimationCurve HalfWidth = new AnimationCurve(
        new Keyframe(0f, 0.02f), new Keyframe(0.07f, 0.22f), new Keyframe(0.25f, 0.42f), new Keyframe(0.45f, 0.45f),
        new Keyframe(0.7f, 0.3f), new Keyframe(0.9f, 0.12f), new Keyframe(1f, 0.08f));
    private static readonly AnimationCurve HalfHeight = new AnimationCurve(
        new Keyframe(0f, 0.02f), new Keyframe(0.07f, 0.22f), new Keyframe(0.25f, 0.48f), new Keyframe(0.45f, 0.52f),
        new Keyframe(0.7f, 0.36f), new Keyframe(0.9f, 0.15f), new Keyframe(1f, 0.1f));

    private static Material skin, fin, eye, teeth, mouth, gill;

    [MenuItem("Tools/SlideSurfer/Build Shark")]
    public static void Build()
    {
        EnsureFolder(OutDir);
        CreateMaterials();
        Mesh body = SaveMesh(BodyMesh(), "SharkBody");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // the old cube becomes a shark-sized hit box
            Transform box = root.transform.Find("SharkObj");
            box.localPosition = Vector3.zero;
            box.localRotation = Quaternion.identity;
            box.localScale = Vector3.one;
            box.GetComponent<MeshRenderer>().enabled = false;
            var collider = box.GetComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.05f, -0.1f);
            collider.size = new Vector3(0.95f, 1.05f, Length + 0.3f);

            // driven by SharkMover now, not by physics or GoPipeGo
            var oldMover = root.GetComponent<GoPipeGo>();
            if (oldMover != null) Object.DestroyImmediate(oldMover);
            var rb = root.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            Transform oldModel = root.transform.Find("Model");
            if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);
            Transform model = BuildModel(root.transform, body, out Transform tail);

            var mover = root.GetComponent<SharkMover>();
            if (mover == null) mover = root.AddComponent<SharkMover>();
            var so = new SerializedObject(mover);
            so.FindProperty("gameSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameSettings>(GameSettingsPath);
            so.FindProperty("model").objectReferenceValue = model;
            so.FindProperty("tail").objectReferenceValue = tail;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("SharkSetup: shark built");
    }

    // ---------------------------------------------------------------- model

    private static Transform BuildModel(Transform root, Mesh body, out Transform tail)
    {
        var model = new GameObject("Model").transform;
        model.SetParent(root, false);

        MeshPart(model, "Body", body, skin);

        float topY = BodyTop(0.4f);
        // dorsal fin, swept back
        MeshPart(model, "DorsalFin", SaveMesh(Fin(new[]
        {
            new Vector2(0.45f, topY - 0.05f), new Vector2(-0.3f, BodyTop(0.6f) - 0.05f), new Vector2(-0.45f, topY + 0.8f)
        }, 0.06f, FinPlane.YZ), "SharkDorsalFin"), fin);

        // pectoral fins, out and down from the belly
        for (int side = -1; side <= 1; side += 2)
        {
            var pec = MeshPart(model, side < 0 ? "PectoralL" : "PectoralR", SaveMesh(Fin(new[]
            {
                new Vector2(0f, 0.65f), new Vector2(0f, 0.25f), new Vector2(0.8f, -0.05f)
            }, 0.05f, FinPlane.XZ), "SharkPectoral"), fin);
            pec.localPosition = new Vector3(side * 0.32f, -0.22f, 0f);
            pec.localRotation = Quaternion.Euler(0f, 0f, side * -28f);
            pec.localScale = new Vector3(side, 1f, 1f);
        }

        // tail on its own pivot so it can beat
        tail = new GameObject("Tail").transform;
        tail.SetParent(model, false);
        float stalkZ = -Length * 0.5f + 0.15f;
        tail.localPosition = new Vector3(0f, 0f, stalkZ);
        MeshPart(tail, "TailFin", SaveMesh(Fin(new[]
        {
            new Vector2(0.1f, 0.06f), new Vector2(-0.75f, 0.95f), new Vector2(-0.42f, 0.02f),
            new Vector2(-0.7f, -0.6f), new Vector2(0.1f, -0.06f)
        }, 0.06f, FinPlane.YZ), "SharkTailFin"), fin);

        // face: eyes with a glint, gills, open mouth with teeth
        float eyeZ = Length * 0.5f - 0.42f;
        for (int side = -1; side <= 1; side += 2)
        {
            float w = HalfWidth.Evaluate(Tof(eyeZ));
            Prim(model, PrimitiveType.Sphere, eye, new Vector3(side * w * 0.86f, 0.1f, eyeZ), Vector3.zero, Vector3.one * 0.11f);
            Prim(model, PrimitiveType.Sphere, teeth, new Vector3(side * w * 0.92f, 0.13f, eyeZ + 0.03f), Vector3.zero, Vector3.one * 0.035f);
            for (int g = 0; g < 3; g++)
            {
                float gz = eyeZ - 0.38f - g * 0.11f;
                float gw = HalfWidth.Evaluate(Tof(gz));
                Prim(model, PrimitiveType.Cube, gill, new Vector3(side * gw * 0.97f, -0.02f, gz), new Vector3(0f, 0f, 0f), new Vector3(0.02f, 0.26f, 0.025f));
            }
        }
        float mouthZ = Length * 0.5f - 0.32f;
        Prim(model, PrimitiveType.Sphere, mouth, new Vector3(0f, -0.2f, mouthZ), Vector3.zero, new Vector3(0.36f, 0.09f, 0.3f));
        for (int i = 0; i < 7; i++)
        {
            float a = Mathf.Lerp(-70f, 70f, i / 6f) * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(a) * 0.16f, -0.17f, mouthZ + Mathf.Cos(a) * 0.13f);
            Prim(model, PrimitiveType.Cube, teeth, pos, new Vector3(0f, a * Mathf.Rad2Deg, 45f), new Vector3(0.04f, 0.04f, 0.015f));
        }
        // dozens of parts -> one mesh for the body (the tail keeps its own, it moves)
        MeshCombine.Combine(model, OutDir + "/SharkModel.asset", UnityEngine.Rendering.ShadowCastingMode.On, tail);
        MeshCombine.Combine(tail, OutDir + "/SharkTail.asset", UnityEngine.Rendering.ShadowCastingMode.On);
        return model;
    }

    private static float Tof(float z) => Mathf.InverseLerp(Length * 0.5f, -Length * 0.5f, z);

    private static float BodyTop(float t) => HalfHeight.Evaluate(t) + BodyLift(t);

    // a slight hump: the back curves up more than the belly curves down
    private static float BodyLift(float t) => 0.06f * Mathf.Sin(t * Mathf.PI);

    private static Mesh BodyMesh()
    {
        const int rings = 32, around = 20;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        for (int r = 0; r <= rings; r++)
        {
            float t = (float)r / rings;
            float z = Mathf.Lerp(Length * 0.5f, -Length * 0.5f, t);
            float w = HalfWidth.Evaluate(t), h = HalfHeight.Evaluate(t), lift = BodyLift(t);
            for (int a = 0; a <= around; a++)
            {
                float u = (float)a / around;
                float ang = u * Mathf.PI * 2f;   // 0 = top of the back, pi = belly
                // flatten the belly a little
                float y = Mathf.Cos(ang) * h;
                if (y < 0f) y *= 0.85f;
                verts.Add(new Vector3(Mathf.Sin(ang) * w, y + lift, z));
                uvs.Add(new Vector2(u, t));
            }
        }
        int stride = around + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int a = 0; a < around; a++)
            {
                int i0 = r * stride + a, i1 = i0 + 1, i2 = i0 + stride, i3 = i2 + 1;
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
                tris.Add(i1); tris.Add(i3); tris.Add(i2);
            }
        }
        // close the nose and the tail stalk
        int nose = verts.Count;
        verts.Add(new Vector3(0f, BodyLift(0f), Length * 0.5f + 0.03f)); uvs.Add(new Vector2(0f, 0f));
        int stalk = verts.Count;
        verts.Add(new Vector3(0f, BodyLift(1f), -Length * 0.5f - 0.02f)); uvs.Add(new Vector2(0.5f, 1f));
        for (int a = 0; a < around; a++)
        {
            tris.Add(nose); tris.Add(a + 1); tris.Add(a);
            int b = rings * stride + a;
            tris.Add(stalk); tris.Add(b); tris.Add(b + 1);
        }

        var mesh = new Mesh { name = "SharkBody" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private enum FinPlane { YZ, XZ }

    // a flat fin of the given outline (a fan from the first point): two faces with their own vertices,
    // one per side, so the fin is solid from every angle with the normal back-face culling
    private static Mesh Fin(Vector2[] outline, float thickness, FinPlane plane)
    {
        int n = outline.Length;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        float half = thickness * 0.5f;
        Vector3 To3(Vector2 p, float off) => plane == FinPlane.YZ ? new Vector3(off, p.y, p.x) : new Vector3(p.y, off, p.x);
        foreach (Vector2 p in outline) verts.Add(To3(p, half));
        foreach (Vector2 p in outline) verts.Add(To3(p, -half));
        for (int i = 1; i < n - 1; i++)
        {
            tris.Add(0); tris.Add(i); tris.Add(i + 1);
            tris.Add(n); tris.Add(n + i + 1); tris.Add(n + i);
        }
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Transform MeshPart(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    private static void Prim(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ---------------------------------------------------------------- assets

    private static void CreateMaterials()
    {
        skin = Lit("SharkSkin", Color.white, 0.55f, SkinTexture());
        fin = Lit("SharkFin", Back * 0.95f, 0.5f, null);
        fin.SetFloat("_Cull", 2f);   // back faces culled: the fin mesh has both sides
        gill = Lit("SharkGill", Back * 0.45f, 0.3f, null);
        eye = Lit("SharkEye", new Color(0.03f, 0.03f, 0.04f), 0.95f, null);
        teeth = Lit("SharkTeeth", new Color(0.97f, 0.97f, 0.94f), 0.7f, null);
        mouth = Lit("SharkMouth", new Color(0.3f, 0.05f, 0.07f), 0.4f, null);
    }

    // across the texture: the way round the body (0 = back, 0.5 = belly), so the belly is white and the back grey
    private static Texture2D SkinTexture()
    {
        string path = OutDir + "/SharkSkin.png";
        const int w = 256, h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int x = 0; x < w; x++)
        {
            float u = (x + 0.5f) / w;
            float belly = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.17f, 0.27f, Mathf.Abs(u - 0.5f)));
            // a darker stripe right along the spine
            float spine = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.08f, Mathf.Min(u, 1f - u)));
            Color c = Color.Lerp(Back, Belly, belly) * (1f - 0.15f * spine);
            c.a = 1f;
            for (int y = 0; y < h; y++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material Lit(string name, Color color, float smoothness, Texture2D tex)
    {
        string path = OutDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = OutDir + "/" + name + ".asset";
        mesh.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        // copy the data through the Mesh API: CopySerialized left the old geometry on the GPU
        existing.Clear();
        existing.vertices = mesh.vertices;
        existing.normals = mesh.normals;
        existing.uv = mesh.uv;
        existing.triangles = mesh.triangles;
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
