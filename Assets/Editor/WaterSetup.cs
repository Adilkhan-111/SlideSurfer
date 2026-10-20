using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds the flowing water and the board spray to the game scene. Run: Tools > SlideSurfer > Add Water.
/// Running it again resets the water material to the shader defaults.
/// </summary>
public static class WaterSetup
{
    private const string ScenePath = "Assets/Scenes/MainGame.unity";
    private const string WaterMaterialPath = "Assets/Water/Water.mat";
    private const string SprayMaterialPath = "Assets/Water/Spray.mat";
    private const string WaterShaderName = "SlideSurfer/FlowingWater";
    private const string SprayShaderName = "SlideSurfer/Spray";
    private const string GameSettingsPath = "Assets/GameSettings.asset";

    [MenuItem("Tools/SlideSurfer/Add Water")]
    public static void AddWater()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Water setup", "Stop Play mode first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Material waterMaterial = FreshMaterial(WaterMaterialPath, WaterShaderName, "Water");
        Material sprayMaterial = FreshMaterial(SprayMaterialPath, SprayShaderName, "Spray");
        if (waterMaterial == null || sprayMaterial == null) return;
        var gameSettings = AssetDatabase.LoadAssetAtPath<GameSettings>(GameSettingsPath);

        // reuse the water if it is already in the scene
        FlowingWater water = Object.FindAnyObjectByType<FlowingWater>();
        if (water == null)
        {
            var go = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer), typeof(FlowingWater));
            Undo.RegisterCreatedObjectUndo(go, "Add water");
            water = go.GetComponent<FlowingWater>();
        }

        var renderer = water.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = waterMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        water.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        water.transform.localScale = Vector3.one;
        water.gameSettings = gameSettings;
        water.Build();

        // spray from the board
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var spray = player.GetComponent<SurferSpray>();
            if (spray == null) spray = Undo.AddComponent<SurferSpray>(player);
            var so = new SerializedObject(spray);
            so.FindProperty("sprayMaterial").objectReferenceValue = sprayMaterial;
            so.FindProperty("gameSettings").objectReferenceValue = gameSettings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("WaterSetup: no Player found, spray not added");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = water.gameObject;
        Debug.Log("WaterSetup: water and spray added to " + ScenePath);
    }

    // creates the material, or resets an existing one to the shader's defaults (keeping its GUID)
    private static Material FreshMaterial(string path, string shaderName, string name)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError("WaterSetup: shader '" + shaderName + "' not found, check the Console for shader errors");
            return null;
        }

        var fresh = new Material(shader) { name = name };
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }
        EditorUtility.CopySerialized(fresh, existing);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(fresh);
        return existing;
    }
}
