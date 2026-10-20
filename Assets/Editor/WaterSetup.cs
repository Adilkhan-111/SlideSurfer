using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds the flowing water to the game scene. Run: Tools > SlideSurfer > Add Water.
/// </summary>
public static class WaterSetup
{
    private const string ScenePath = "Assets/Scenes/MainGame.unity";
    private const string MaterialPath = "Assets/Water/Water.mat";
    private const string ShaderName = "SlideSurfer/FlowingWater";
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

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("WaterSetup: shader '" + ShaderName + "' not found, check the Console for shader errors");
            return;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Water" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }

        // reuse the water if it is already in the scene
        FlowingWater water = Object.FindObjectOfType<FlowingWater>();
        if (water == null)
        {
            var go = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer), typeof(FlowingWater));
            Undo.RegisterCreatedObjectUndo(go, "Add water");
            water = go.GetComponent<FlowingWater>();
        }

        var renderer = water.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        water.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        water.transform.localScale = Vector3.one;
        water.gameSettings = AssetDatabase.LoadAssetAtPath<GameSettings>(GameSettingsPath);
        water.Build();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = water.gameObject;
        Debug.Log("WaterSetup: water added to " + ScenePath);
    }
}
