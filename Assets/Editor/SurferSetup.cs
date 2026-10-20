using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the surfer visual (prefab, animation clips, animator controller) from the rigged Mixamo model
/// and the board, and puts it on the Player instead of the placeholder. Run it from
/// Tools > SlideSurfer > Setup Surfer Visual.
/// </summary>
public static class SurferSetup
{
    private const string SurferFbx = "Assets/Models/SurferRFRigged.fbx";
    // the old bone-less model: only used to work out which way the board points (the board was authored against it)
    private const string OldSurferFbx = "Assets/Models/Surfer.fbx";
    private const string BoardFbx = "Assets/Models/SurfBoard.fbx";
    private const string OutDir = "Assets/Surfer";
    private const string AnimDir = OutDir + "/Animations";
    private const string PrefabPath = OutDir + "/SurferVisual.prefab";
    private const string ControllerPath = AnimDir + "/SurferController.controller";
    private const string ScenePath = "Assets/Scenes/MainGame.unity";
    private const string VisualName = "SurferVisual";

    // Player capsule is 2 tall and centred on the Player transform, so the board bottom sits at -1
    private const float BoardBottomY = -1f;
    private const float SurferHeight = 1.8f;
    // The model faces the board nose; turn the surfer sideways for a surf stance (+90 = chest to the right)
    private const float StanceYaw = 90f;

    private const string Pivot = "Pivot";
    private const string Board = "Pivot/Board";
    private const string Surfer = "Pivot/Surfer";

    [MenuItem("Tools/SlideSurfer/Setup Surfer Visual")]
    public static void SetupAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Surfer setup", "Stop Play mode first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("SurferSetup: no GameObject with tag Player in " + ScenePath);
            return;
        }

        // drop a previous visual before its prefab asset is rebuilt
        Transform old = player.transform.Find(VisualName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        EnsureFolder(OutDir);
        EnsureFolder(AnimDir);

        GameObject prefab = BuildPrefab();
        if (prefab == null) return;
        ApplyToPlayer(player, prefab);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("SurferSetup: done. Surfer visual is on the Player, assets are in " + OutDir);
    }

    // ---------------------------------------------------------------- prefab

    private static GameObject BuildPrefab()
    {
        var surferAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SurferFbx);
        var boardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BoardFbx);
        if (surferAsset == null || boardAsset == null)
        {
            Debug.LogError("SurferSetup: missing " + SurferFbx + " or " + BoardFbx);
            return null;
        }

        var root = new GameObject(VisualName);
        var animator = root.AddComponent<Animator>();
        animator.applyRootMotion = false;

        // everything is measured with the pivot at the origin, then the pivot is moved into place
        Transform pivot = NewChild(root.transform, "Pivot");
        Transform board = NewChild(pivot, "Board");
        Transform surfer = NewChild(pivot, "Surfer");
        var boardModel = (GameObject)PrefabUtility.InstantiatePrefab(boardAsset, board);
        var surferModel = (GameObject)PrefabUtility.InstantiatePrefab(surferAsset, surfer);

        // the FBX brings its own Animator without a controller, keep it out of the way
        var innerAnimator = surferModel.GetComponent<Animator>();
        if (innerAnimator != null) innerAnimator.enabled = false;

        // ---- board: the old static surfer faces along the board nose, use it to point the board along +Z
        float boardYaw = DetectBoardYaw();
        Quaternion boardBase = boardModel.transform.localRotation;
        boardModel.transform.localRotation = Quaternion.Euler(0f, boardYaw, 0f) * boardBase;

        // one scale for the board, taken from the old model, so the board keeps its proportion to the surfer
        float boardScale = OldSurferScale();
        boardModel.transform.localScale = Vector3.one * boardScale;

        Bounds bb = BoundsOf(WorldVerts(boardModel));
        boardModel.transform.localPosition += new Vector3(-bb.center.x, -bb.min.y, -bb.center.z);
        float boardTop = bb.size.y;

        // ---- surfer: use the skeleton to see where he faces, then turn him sideways (+X)
        Rig rig = Rig.Find(surferModel.transform);
        if (rig == null)
        {
            UnityEngine.Object.DestroyImmediate(root);
            return null;
        }

        Vector3 facing = rig.Facing();
        facing.y = 0f;
        if (facing.magnitude < 1e-4f)
        {
            Debug.LogWarning("SurferSetup: could not tell which way the surfer faces, assuming +Z");
            facing = Vector3.forward;
        }
        float turn = Vector3.SignedAngle(facing, Vector3.right, Vector3.up);
        surferModel.transform.rotation = Quaternion.Euler(0f, turn, 0f) * surferModel.transform.rotation;

        List<Vector3> sv = WorldVerts(surferModel);
        float height = BoundsOf(sv).size.y;
        surferModel.transform.localScale = Vector3.one * (SurferHeight / height);

        // feet centred on the board, soles on its top
        List<Vector3> sv2 = WorldVerts(surferModel);
        Bounds sb = BoundsOf(sv2);
        Vector2 feet = CentroidXZ(sv2, sb.min.y, sb.min.y + 0.06f * sb.size.y);
        surferModel.transform.localPosition += new Vector3(-feet.x, boardTop - sb.min.y, -feet.y);

        pivot.localPosition = new Vector3(0f, BoardBottomY, 0f);

        // the skeleton is now in its final place, so the poses can be solved against it
        rig.Capture(Vector3.right);
        AnimatorController controller = BuildAnimations(root.transform, rig);
        animator.runtimeAnimatorController = controller;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static float DetectBoardYaw()
    {
        var oldAsset = AssetDatabase.LoadAssetAtPath<GameObject>(OldSurferFbx);
        if (oldAsset == null) return 0f;
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(oldAsset);
        try
        {
            List<Vector3> sv = WorldVerts(tmp);
            Bounds sb = BoundsOf(sv);
            float h = sb.size.y;
            Vector2 foot = CentroidXZ(sv, sb.min.y, sb.min.y + 0.06f * h);
            Vector2 shin = CentroidXZ(sv, sb.min.y + 0.15f * h, sb.min.y + 0.28f * h);
            Vector2 facing = foot - shin;
            if (facing.magnitude <= 0.005f * h) return 0f;
            float angle = Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg;
            return -Mathf.Round(angle / 90f) * 90f;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tmp);
        }
    }

    private static float OldSurferScale()
    {
        var oldAsset = AssetDatabase.LoadAssetAtPath<GameObject>(OldSurferFbx);
        if (oldAsset == null) return 1f;
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(oldAsset);
        try { return SurferHeight / BoundsOf(WorldVerts(tmp)).size.y; }
        finally { UnityEngine.Object.DestroyImmediate(tmp); }
    }

    private static void ApplyToPlayer(GameObject player, GameObject prefab)
    {
        // the red cylinder placeholder: keep the capsule collider and rigidbody, drop only the visuals
        var renderer = player.GetComponent<MeshRenderer>();
        if (renderer != null) Undo.DestroyObjectImmediate(renderer);
        var filter = player.GetComponent<MeshFilter>();
        if (filter != null) Undo.DestroyObjectImmediate(filter);

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        Undo.RegisterCreatedObjectUndo(visual, "Add surfer visual");

        var visualAnimator = visual.GetComponent<Animator>();
        if (visualAnimator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("SurferSetup: the prefab lost its controller reference, assigning it on the scene instance");
            visualAnimator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        }

        var driver = player.GetComponent<PlayerAnimator>();
        if (driver == null) driver = Undo.AddComponent<PlayerAnimator>(player);
        var so = new SerializedObject(driver);
        so.FindProperty("animator").objectReferenceValue = visual.GetComponent<Animator>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- animations

    private static AnimatorController BuildAnimations(Transform visualRoot, Rig rig)
    {
        AnimationClip rideLeft = MakeRide("Ride_Left", visualRoot, rig, -1f, 18f, -10f);
        AnimationClip rideCenter = MakeRide("Ride", visualRoot, rig, 0f, 0f, 0f);
        AnimationClip rideRight = MakeRide("Ride_Right", visualRoot, rig, 1f, -18f, 10f);
        AnimationClip[] tricks =
        {
            MakeShoveIt(visualRoot, rig),
            MakeKickflip(visualRoot, rig),
            MakeSpin360(visualRoot, rig),
        };
        AssetDatabase.DeleteAsset(AnimDir + "/Jump.anim");   // single-trick clip from the earlier version

        AnimatorController controller = ResetController();
        controller.AddParameter("Lean", AnimatorControllerParameterType.Float);
        controller.AddParameter("Airborne", AnimatorControllerParameterType.Bool);
        controller.AddParameter("JumpSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Trick", AnimatorControllerParameterType.Int);
        AnimatorControllerParameter[] ps = controller.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].name == "JumpSpeed") ps[i].defaultFloat = 1f;
        controller.parameters = ps;

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState ride = controller.CreateBlendTreeInController("Ride", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Lean";
        tree.useAutomaticThresholds = false;
        tree.AddChild(rideLeft, -1f);
        tree.AddChild(rideCenter, 0f);
        tree.AddChild(rideRight, 1f);

        // one state per trick, PlayerAnimator picks the "Trick" index when the jump starts
        for (int i = 0; i < tricks.Length; i++)
        {
            AnimatorState trickState = sm.AddState(tricks[i].name);
            trickState.motion = tricks[i];
            trickState.speedParameterActive = true;
            trickState.speedParameter = "JumpSpeed";

            AnimatorStateTransition toTrick = ride.AddTransition(trickState);
            toTrick.hasExitTime = false;
            toTrick.duration = 0.05f;
            toTrick.AddCondition(AnimatorConditionMode.If, 0f, "Airborne");
            toTrick.AddCondition(AnimatorConditionMode.Equals, i, "Trick");

            AnimatorStateTransition toRide = trickState.AddTransition(ride);
            toRide.hasExitTime = false;
            toRide.duration = 0.1f;
            toRide.AddCondition(AnimatorConditionMode.IfNot, 0f, "Airborne");
        }

        sm.defaultState = ride;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // Rebuild the controller inside the existing asset instead of deleting it: deleting and recreating at the
    // same path left the prefab's reference pointing at a dead object, so the Animator had no controller at runtime.
    private static AnimatorController ResetController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            if (sub != null && sub != controller) UnityEngine.Object.DestroyImmediate(sub, true);
        controller.parameters = new AnimatorControllerParameter[0];
        controller.layers = new AnimatorControllerLayer[0];
        controller.AddLayer("Base Layer");
        return controller;
    }

    // looping ride: knees breathe, arms sway, the whole thing banks toward the move direction
    private static AnimationClip MakeRide(string name, Transform visualRoot, Rig rig, float lean, float roll, float yaw)
    {
        const float T = 1.2f;
        var wrapper = new Dictionary<(string, string), (float t, float v)[]>
        {
            [(Pivot, PosY)] = new[] { (0f, BoardBottomY + 0.012f), (T * 0.5f, BoardBottomY - 0.012f), (T, BoardBottomY + 0.012f) },
            [(Pivot, RotZ)] = new[] { (0f, roll + 0.8f), (T * 0.5f, roll - 0.8f), (T, roll + 0.8f) },
            [(Pivot, RotY)] = new[] { (0f, yaw), (T, yaw) },
            [(Board, RotX)] = new[] { (0f, 1.2f), (T * 0.5f, -1.2f), (T, 1.2f) },
        };
        Func<float, Dictionary<string, Vector3>> pose = t =>
        {
            float phase = Mathf.Cos(t / T * Mathf.PI * 2f);
            return Stance(0.45f + 0.07f * phase, lean, 28f + 3f * phase, 0f);
        };
        return MakeClip(name, T, true, visualRoot, rig, wrapper, pose, 24);
    }

    // The three jump tricks are one second long each (crouch, pop, trick in the air, crouch to land).
    // PlayerAnimator stretches them to the real flight time through the JumpSpeed parameter.
    // A spin that ends on a full turn jumps back to 0 right after the last key: 360 and 0 look the same,
    // and it keeps the blend back into the ride clip from unwinding the whole turn.

    // 360 shove-it: the board spins flat under the feet while the surfer stays put
    private static AnimationClip MakeShoveIt(Transform visualRoot, Rig rig)
    {
        var wrapper = new Dictionary<(string, string), (float t, float v)[]>
        {
            [(Pivot, PosY)] = new[] { (0f, BoardBottomY), (0.12f, BoardBottomY - 0.03f), (0.28f, BoardBottomY), (1f, BoardBottomY) },
            [(Pivot, RotX)] = new[] { (0f, 0f), (0.12f, 3f), (0.28f, -8f), (0.55f, -3f), (0.85f, 6f), (1f, 0f) },
            [(Board, RotY)] = new[] { (0f, 0f), (0.2f, 0f), (0.85f, 360f), (0.8501f, 0f), (1f, 0f) },
            [(Board, PosY)] = new[] { (0f, 0f), (0.28f, 0f), (0.5f, 0.1f), (0.75f, 0.07f), (0.92f, 0f), (1f, 0f) },
            [(Surfer, PosY)] = new[] { (0f, 0f), (0.28f, 0f), (0.5f, 0.1f), (0.75f, 0.07f), (0.92f, 0f), (1f, 0f) },
        };
        AnimationCurve crouch = Smooth((0f, 0.45f), (0.12f, 0.95f), (0.28f, 0.05f), (0.55f, 0.55f), (0.85f, 0.55f), (0.95f, 0.95f), (1f, 0.6f));
        AnimationCurve arms = Smooth((0f, 28f), (0.12f, 40f), (0.28f, -35f), (0.55f, -5f), (0.85f, 10f), (0.95f, 40f), (1f, 32f));
        AnimationCurve pitch = Smooth((0f, 0f), (0.12f, 18f), (0.28f, -12f), (0.55f, 10f), (0.85f, 10f), (0.95f, 20f), (1f, 6f));
        return MakeTrick("Trick_ShoveIt", visualRoot, rig, wrapper, crouch, arms, pitch);
    }

    // kickflip: the board is flicked and rolls once around its long axis, the surfer tucks his knees and catches it
    private static AnimationClip MakeKickflip(Transform visualRoot, Rig rig)
    {
        var wrapper = new Dictionary<(string, string), (float t, float v)[]>
        {
            [(Pivot, PosY)] = new[] { (0f, BoardBottomY), (0.12f, BoardBottomY - 0.03f), (0.28f, BoardBottomY), (1f, BoardBottomY) },
            [(Pivot, RotX)] = new[] { (0f, 0f), (0.12f, 3f), (0.28f, -6f), (0.55f, -2f), (0.85f, 5f), (1f, 0f) },
            [(Board, RotZ)] = new[] { (0f, 0f), (0.25f, 0f), (0.8f, 360f), (0.8001f, 0f), (1f, 0f) },
            [(Board, RotX)] = new[] { (0f, 0f), (0.25f, 0f), (0.35f, -8f), (0.8f, 4f), (1f, 0f) },
            [(Board, PosY)] = new[] { (0f, 0f), (0.25f, 0f), (0.5f, 0.2f), (0.75f, 0.12f), (0.9f, 0f), (1f, 0f) },
            [(Surfer, PosY)] = new[] { (0f, 0f), (0.25f, 0f), (0.5f, 0.16f), (0.75f, 0.1f), (0.9f, 0f), (1f, 0f) },
        };
        AnimationCurve crouch = Smooth((0f, 0.45f), (0.12f, 0.9f), (0.28f, 0.1f), (0.5f, 0.75f), (0.8f, 0.7f), (0.95f, 0.95f), (1f, 0.6f));
        AnimationCurve arms = Smooth((0f, 28f), (0.12f, 38f), (0.28f, -20f), (0.5f, 0f), (0.8f, 5f), (0.95f, 40f), (1f, 32f));
        AnimationCurve pitch = Smooth((0f, 0f), (0.12f, 16f), (0.28f, -8f), (0.5f, 14f), (0.8f, 12f), (0.95f, 20f), (1f, 6f));
        return MakeTrick("Trick_Kickflip", visualRoot, rig, wrapper, crouch, arms, pitch);
    }

    // 360 spin: surfer and board turn once together in the air with the arms wide, like a skate 360
    private static AnimationClip MakeSpin360(Transform visualRoot, Rig rig)
    {
        var wrapper = new Dictionary<(string, string), (float t, float v)[]>
        {
            [(Pivot, PosY)] = new[] { (0f, BoardBottomY), (0.12f, BoardBottomY - 0.03f), (0.28f, BoardBottomY), (1f, BoardBottomY) },
            [(Pivot, RotY)] = new[] { (0f, 0f), (0.2f, 0f), (0.85f, 360f), (0.8501f, 0f), (1f, 0f) },
            [(Pivot, RotX)] = new[] { (0f, 0f), (0.12f, 3f), (0.3f, -5f), (0.85f, 3f), (1f, 0f) },
            [(Board, PosY)] = new[] { (0f, 0f), (0.28f, 0f), (0.5f, 0.08f), (0.75f, 0.06f), (0.92f, 0f), (1f, 0f) },
            [(Surfer, PosY)] = new[] { (0f, 0f), (0.28f, 0f), (0.5f, 0.08f), (0.75f, 0.06f), (0.92f, 0f), (1f, 0f) },
        };
        AnimationCurve crouch = Smooth((0f, 0.45f), (0.12f, 0.95f), (0.28f, 0.05f), (0.5f, 0.35f), (0.85f, 0.35f), (0.95f, 0.95f), (1f, 0.6f));
        AnimationCurve arms = Smooth((0f, 28f), (0.12f, 40f), (0.28f, -10f), (0.5f, -8f), (0.85f, 0f), (0.95f, 40f), (1f, 32f));
        AnimationCurve pitch = Smooth((0f, 0f), (0.12f, 18f), (0.28f, -6f), (0.5f, 4f), (0.85f, 4f), (0.95f, 20f), (1f, 6f));
        return MakeTrick("Trick_Spin360", visualRoot, rig, wrapper, crouch, arms, pitch);
    }

    private static AnimationClip MakeTrick(string name, Transform visualRoot, Rig rig,
        Dictionary<(string, string), (float t, float v)[]> wrapper,
        AnimationCurve crouch, AnimationCurve arms, AnimationCurve pitch)
    {
        Func<float, Dictionary<string, Vector3>> pose = t => Stance(crouch.Evaluate(t), 0f, arms.Evaluate(t), pitch.Evaluate(t));
        return MakeClip(name, 1f, false, visualRoot, rig, wrapper, pose, 25);
    }

    // Surf stance, all angles are absolute (relative to the T-pose) in the character's own frame:
    // X = his right, Y = up, Z = the way his chest points. Left foot leads, head looks along the board.
    //   crouch    0..1   how far the knees are bent
    //   lean     -1..1   body weight back / forward
    //   armDrop   deg    how far the arms hang down from the T-pose
    //   extraPitch deg   extra forward bend of the spine
    private static Dictionary<string, Vector3> Stance(float crouch, float lean, float armDrop, float extraPitch)
    {
        float hip = Mathf.Lerp(0f, 38f, crouch);   // thigh swings forward
        float shin = hip * 1.15f;                  // shin swings back so the ankle stays under the hip
        float spread = 13f;                        // legs apart along the board
        float bend = 8f * crouch + 6f * lean;

        return new Dictionary<string, Vector3>
        {
            ["Hips"] = new Vector3(0.5f * bend, -25f, 0f),
            ["Spine"] = new Vector3(bend + 0.4f * extraPitch, -12f, 0f),
            ["Spine1"] = new Vector3(bend + 0.35f * extraPitch + 2f, -8f, 0f),
            ["Spine2"] = new Vector3(bend + 0.25f * extraPitch + 2f, -4f, 0f),
            ["Neck"] = new Vector3(-6f * crouch, -35f, 0f),
            ["Head"] = new Vector3(-8f * crouch, -70f, 0f),

            ["LeftArm"] = new Vector3(0f, 0f, armDrop),
            ["LeftForeArm"] = new Vector3(0f, 12f, armDrop * 0.5f),
            ["RightArm"] = new Vector3(0f, 0f, -armDrop),
            ["RightForeArm"] = new Vector3(0f, -12f, -armDrop * 0.5f),

            ["LeftUpLeg"] = new Vector3(-hip, 0f, -spread),
            ["LeftLeg"] = new Vector3(shin, 0f, -spread * 0.4f),
            ["LeftFoot"] = Vector3.zero,
            ["RightUpLeg"] = new Vector3(-hip, 0f, spread),
            ["RightLeg"] = new Vector3(shin, 0f, spread * 0.4f),
            ["RightFoot"] = Vector3.zero,
        };
    }

    // ---------------------------------------------------------------- clips

    private const string PosY = "m_LocalPosition.y";
    private const string RotX = "localEulerAnglesRaw.x";
    private const string RotY = "localEulerAnglesRaw.y";
    private const string RotZ = "localEulerAnglesRaw.z";

    // every clip animates the same set of properties, so no state leaves a stale value behind
    private static readonly (string path, string prop, float value)[] WrapperDefaults =
    {
        (Pivot, PosY, BoardBottomY), (Pivot, RotX, 0f), (Pivot, RotY, 0f), (Pivot, RotZ, 0f),
        (Board, PosY, 0f), (Board, RotX, 0f), (Board, RotY, 0f), (Board, RotZ, 0f),
        (Surfer, PosY, 0f),
    };

    private static AnimationClip MakeClip(string name, float length, bool loop, Transform visualRoot, Rig rig,
        Dictionary<(string, string), (float t, float v)[]> wrapper,
        Func<float, Dictionary<string, Vector3>> poseAt, int samples)
    {
        string path = AnimDir + "/" + name + ".anim";
        var clip = new AnimationClip { name = name, frameRate = 60f };

        // wrapper objects: pivot / board / surfer
        foreach (var d in WrapperDefaults)
        {
            (float t, float v)[] k;
            if (!wrapper.TryGetValue((d.path, d.prop), out k))
                k = new[] { (0f, d.value), (length, d.value) };

            var curve = new AnimationCurve();
            foreach (var key in k) curve.AddKey(new Keyframe(key.t, key.v));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            // a full turn followed by 0 a moment later: hold the 360 and snap to 0, it looks identical
            for (int i = 0; i + 1 < k.Length; i++)
            {
                if (k[i + 1].t - k[i].t < 0.01f && Mathf.Abs(k[i + 1].v - k[i].v) >= 359.9f)
                {
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyLeftTangentMode(curve, i + 1, AnimationUtility.TangentMode.Constant);
                }
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(d.path, typeof(Transform), d.prop), curve);
        }

        // bones: solve the pose at every sample and key the local rotations (and the hips position)
        var rot = new Dictionary<Transform, List<Quaternion>>();
        var hipsPos = new List<Vector3>();
        var times = new List<float>();
        for (int i = 0; i < samples; i++)
        {
            float t = length * i / (samples - 1);
            times.Add(t);
            rig.Solve(poseAt(t), out Dictionary<Transform, Quaternion> local, out Vector3 hp);
            foreach (var kv in local)
            {
                if (!rot.TryGetValue(kv.Key, out List<Quaternion> list)) rot[kv.Key] = list = new List<Quaternion>();
                // keep neighbouring quaternions on the same hemisphere so the blend never takes the long way round
                Quaternion q = kv.Value;
                if (list.Count > 0 && Quaternion.Dot(list[list.Count - 1], q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                list.Add(q);
            }
            hipsPos.Add(hp);
        }

        foreach (var kv in rot)
        {
            string bonePath = AnimationUtility.CalculateTransformPath(kv.Key, visualRoot);
            for (int c = 0; c < 4; c++)
            {
                var curve = new AnimationCurve();
                for (int i = 0; i < times.Count; i++)
                {
                    Quaternion q = kv.Value[i];
                    curve.AddKey(new Keyframe(times[i], c == 0 ? q.x : c == 1 ? q.y : c == 2 ? q.z : q.w));
                }
                LinearTangents(curve);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalRotation." + "xyzw"[c]), curve);
            }
        }

        string hipsPath = AnimationUtility.CalculateTransformPath(rig.Hips, visualRoot);
        for (int c = 0; c < 3; c++)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i < times.Count; i++)
                curve.AddKey(new Keyframe(times[i], hipsPos[i][c]));
            LinearTangents(curve);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(hipsPath, typeof(Transform), "m_LocalPosition." + "xyz"[c]), curve);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // overwrite an existing clip in place so its GUID (and every reference to it) survives a re-run
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(clip);
            return existing;
        }
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void LinearTangents(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
    }

    private static AnimationCurve Smooth(params (float t, float v)[] keys)
    {
        var curve = new AnimationCurve();
        foreach (var k in keys) curve.AddKey(new Keyframe(k.t, k.v));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return curve;
    }

    // ---------------------------------------------------------------- skeleton

    /// <summary>
    /// The Mixamo skeleton in its rest (T-pose) state and a small forward-kinematics solver.
    /// Poses are given as absolute rotations of chosen bones in the character's frame; every other bone just follows its parent.
    /// </summary>
    private class Rig
    {
        private readonly Dictionary<string, Transform> byShortName = new Dictionary<string, Transform>();
        private readonly Dictionary<Transform, string> shortNameOf = new Dictionary<Transform, string>();
        private readonly List<Transform> order = new List<Transform>();   // parents before children
        private Quaternion charRot = Quaternion.identity;
        private float restLowest;

        public Transform Hips { get { return byShortName["Hips"]; } }

        private static readonly string[] Required =
        {
            "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
            "LeftArm", "LeftForeArm", "RightArm", "RightForeArm",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
            "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
        };

        public static Rig Find(Transform modelRoot)
        {
            var rig = new Rig();
            foreach (Transform t in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                int colon = n.LastIndexOf(':');
                if (colon >= 0) n = n.Substring(colon + 1);
                if (!rig.byShortName.ContainsKey(n))
                {
                    rig.byShortName[n] = t;
                    rig.shortNameOf[t] = n;
                }
            }
            foreach (string name in Required)
            {
                if (!rig.byShortName.ContainsKey(name))
                {
                    Debug.LogError("SurferSetup: bone '" + name + "' not found in the model, is it a Mixamo rig?");
                    return null;
                }
            }
            rig.Collect(rig.Hips);
            return rig;
        }

        private void Collect(Transform t)
        {
            order.Add(t);
            foreach (Transform c in t) Collect(c);
        }

        // which way the toes point, from the ankle to the ball of the foot (horizontal)
        public Vector3 Facing()
        {
            Vector3 l = byShortName["LeftToeBase"].position - byShortName["LeftFoot"].position;
            Vector3 r = byShortName["RightToeBase"].position - byShortName["RightFoot"].position;
            return l + r;
        }

        // call once the model sits where it will end up, facing 'forward'
        public void Capture(Vector3 forward)
        {
            charRot = Quaternion.LookRotation(forward, Vector3.up);
            restLowest = LowestFoot(p => p.position.y);
        }

        private float LowestFoot(Func<Transform, float> y)
        {
            float low = float.MaxValue;
            foreach (string n in new[] { "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase" })
                low = Mathf.Min(low, y(byShortName[n]));
            return low;
        }

        public void Solve(Dictionary<string, Vector3> pose,
            out Dictionary<Transform, Quaternion> localOut, out Vector3 hipsLocalPos)
        {
            localOut = new Dictionary<Transform, Quaternion>();
            var world = new Dictionary<Transform, Quaternion>();
            var pos = new Dictionary<Transform, Vector3>();

            foreach (Transform t in order)
            {
                Transform p = t.parent;
                Quaternion pRot = world.ContainsKey(p) ? world[p] : p.rotation;
                Vector3 pPos = pos.ContainsKey(p) ? pos[p] : p.position;

                string key;
                Vector3 euler;
                if (shortNameOf.TryGetValue(t, out key) && pose.TryGetValue(key, out euler))
                {
                    // absolute rotation in the character frame, expressed back in world space
                    Quaternion delta = charRot * Quaternion.Euler(euler) * Quaternion.Inverse(charRot);
                    Quaternion w = delta * t.rotation;
                    world[t] = w;
                    localOut[t] = Quaternion.Inverse(pRot) * w;
                }
                else
                {
                    world[t] = pRot * t.localRotation;
                }

                // swing the bone's rest offset by however much its parent has turned
                Quaternion parentTurn = pRot * Quaternion.Inverse(p.rotation);
                pos[t] = pPos + parentTurn * (t.position - p.position);
            }

            // keep the feet where they were by moving the hips: lowering the hips for a crouch
            float lowest = LowestFoot(f => pos[f].y);
            Vector3 offset = new Vector3(0f, restLowest - lowest, 0f);
            Transform hips = Hips;
            hipsLocalPos = hips.localPosition + hips.parent.InverseTransformVector(offset);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static List<Vector3> WorldVerts(GameObject go)
    {
        var result = new List<Vector3>();
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            foreach (Vector3 v in mf.sharedMesh.vertices) result.Add(m.MultiplyPoint3x4(v));
        }
        // skinned meshes in the rest pose sit at their renderer's own transform
        foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.sharedMesh == null) continue;
            Matrix4x4 m = smr.transform.localToWorldMatrix;
            foreach (Vector3 v in smr.sharedMesh.vertices) result.Add(m.MultiplyPoint3x4(v));
        }
        return result;
    }

    private static Bounds BoundsOf(List<Vector3> verts)
    {
        var b = new Bounds(verts[0], Vector3.zero);
        foreach (Vector3 v in verts) b.Encapsulate(v);
        return b;
    }

    private static Vector2 CentroidXZ(List<Vector3> verts, float minY, float maxY)
    {
        Vector2 sum = Vector2.zero;
        int n = 0;
        foreach (Vector3 v in verts)
        {
            if (v.y < minY || v.y > maxY) continue;
            sum += new Vector2(v.x, v.z);
            n++;
        }
        return n > 0 ? sum / n : Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
