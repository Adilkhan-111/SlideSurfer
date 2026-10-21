using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Merges the many little parts a generated model is built from (cubes, cylinders...) into one mesh with one
/// sub-mesh per material, so the phone draws a handful of batches per object instead of dozens.
/// </summary>
public static class MeshCombine
{
    /// <summary>
    /// Combine every MeshRenderer under <paramref name="group"/> (except the ones under <paramref name="skip"/>)
    /// into a single "Combined" child of the group; the original parts are removed. The mesh is saved at <paramref name="assetPath"/>.
    /// </summary>
    public static void Combine(Transform group, string assetPath, ShadowCastingMode shadows, params Transform[] skip)
    {
        var byMaterial = new Dictionary<Material, List<CombineInstance>>();
        var order = new List<Material>();
        var parts = new List<MeshRenderer>();
        Matrix4x4 toGroup = group.worldToLocalMatrix;

        foreach (MeshRenderer mr in group.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!mr.enabled || IsUnder(mr.transform, skip)) continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Material mat = mr.sharedMaterial;
            if (!byMaterial.TryGetValue(mat, out List<CombineInstance> list))
            {
                byMaterial[mat] = list = new List<CombineInstance>();
                order.Add(mat);
            }
            for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = toGroup * mr.transform.localToWorldMatrix });
            parts.Add(mr);
        }
        if (parts.Count == 0) return;

        // one mesh per material first, then those become the sub-meshes of the final mesh
        var perMaterial = new CombineInstance[order.Count];
        for (int i = 0; i < order.Count; i++)
        {
            var m = new Mesh { indexFormat = IndexFormat.UInt32 };
            m.CombineMeshes(byMaterial[order[i]].ToArray(), true, true);
            perMaterial[i] = new CombineInstance { mesh = m, transform = Matrix4x4.identity };
        }
        var combined = new Mesh { indexFormat = IndexFormat.UInt32 };
        combined.CombineMeshes(perMaterial, false, false);
        combined.RecalculateBounds();
        foreach (var ci in perMaterial) Object.DestroyImmediate(ci.mesh);
        Mesh saved = Save(combined, assetPath);

        // remove the parts, then any objects left empty
        foreach (MeshRenderer mr in parts)
        {
            var mf = mr.GetComponent<MeshFilter>();
            Object.DestroyImmediate(mr);
            if (mf != null) Object.DestroyImmediate(mf);
        }
        RemoveEmpty(group);

        var go = new GameObject("Combined");
        go.transform.SetParent(group, false);
        go.AddComponent<MeshFilter>().sharedMesh = saved;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = order.ToArray();
        renderer.shadowCastingMode = shadows;
    }

    private static bool IsUnder(Transform t, Transform[] roots)
    {
        foreach (Transform r in roots)
            if (r != null && (t == r || t.IsChildOf(r))) return true;
        return false;
    }

    // delete children that ended up with nothing but a Transform and no children
    private static void RemoveEmpty(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            Transform c = t.GetChild(i);
            RemoveEmpty(c);
            if (c.childCount == 0 && c.GetComponents<Component>().Length == 1) Object.DestroyImmediate(c.gameObject);
        }
    }

    // write through the Mesh API into an existing asset, so the GPU copy is refreshed too
    private static Mesh Save(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        existing.Clear();
        existing.indexFormat = mesh.indexFormat;
        existing.vertices = mesh.vertices;
        existing.normals = mesh.normals;
        existing.uv = mesh.uv;
        existing.subMeshCount = mesh.subMeshCount;
        for (int s = 0; s < mesh.subMeshCount; s++) existing.SetTriangles(mesh.GetTriangles(s), s);
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }
}
