using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// セグウェイ搭乗者用のAnimator Controllerを生成し、SampleSceneの人物モデルへ割り当てます。
/// </summary>
public static class SegwayRiderAnimationSetup
{
    private const string ForwardFbxPath = "Assets/Models/Businessman_SegwayAnimation.fbx";
    private const string BackwardFbxPath = "Assets/Models/Businessman_SegwayBackwardAnimation.fbx";
    private const string ControllerPath = "Assets/Animation/SegwayRider.controller";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RiderObjectName = "Businessman_SegwayAnimation";
    private const float InputThreshold = 0.01f;

    /// <summary>コマンドラインまたはメニューから、アニメーション設定を再生成します。</summary>
    [MenuItem("Tools/Segway/Configure Rider Animations")]
    public static void Configure()
    {
        ConfigureClipLooping(ForwardFbxPath);
        ConfigureClipLooping(BackwardFbxPath);
        AssetDatabase.Refresh();

        AnimationClip forwardClip = GetMainAnimationClip(ForwardFbxPath);
        AnimationClip backwardClip = GetMainAnimationClip(BackwardFbxPath);
        if (forwardClip == null || backwardClip == null)
        {
            throw new System.InvalidOperationException("前進または後退のAnimationClipが見つかりません。");
        }

        AnimatorController controller = CreateController(forwardClip, backwardClip);
        AssignControllerToSceneRider(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Segway rider animations configured successfully.");
    }

    /// <summary>生成済みアニメーターとシーンへの割り当てが指定どおりか検証します。</summary>
    public static void Validate()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip forwardClip = GetMainAnimationClip(ForwardFbxPath);
        AnimationClip backwardClip = GetMainAnimationClip(BackwardFbxPath);
        if (controller == null || forwardClip == null || backwardClip == null)
        {
            throw new System.InvalidOperationException("アニメーターまたは必要なクリップが見つかりません。");
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = machine.states.Select(item => item.state).First(state => state.name == "Idle");
        AnimatorState forward = machine.states.Select(item => item.state).First(state => state.name == "Forward");
        AnimatorState backward = machine.states.Select(item => item.state).First(state => state.name == "Backward");
        if (idle.motion != forwardClip || idle.speed != 0f || forward.motion != forwardClip || backward.motion != backwardClip)
        {
            throw new System.InvalidOperationException("Idle・Forward・Backwardのクリップ設定が不正です。");
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject rider = GameObject.Find(RiderObjectName);
        Animator animator = rider != null ? rider.GetComponent<Animator>() : null;
        SegwayRiderAnimation riderAnimation = rider != null ? rider.GetComponent<SegwayRiderAnimation>() : null;
        if (!scene.IsValid() || animator == null || riderAnimation == null || animator.runtimeAnimatorController != controller)
        {
            throw new System.InvalidOperationException("SampleSceneへの人物アニメーション割り当てが不正です。");
        }

        Debug.Log("Segway rider animation validation passed.");
    }

    /// <summary>入力中にクリップ末尾で止まらないよう、FBXのアニメーションをループさせます。</summary>
    private static void ConfigureClipLooping(string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            throw new System.InvalidOperationException($"ModelImporterが見つかりません: {assetPath}");
        }

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = false;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    /// <summary>FBXのプレビュー用ではない最初のAnimationClipを取得します。</summary>
    private static AnimationClip GetMainAnimationClip(string assetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
    }

    /// <summary>停止・前進・後退の3ステートを持つAnimator Controllerを作成します。</summary>
    private static AnimatorController CreateController(AnimationClip forwardClip, AnimationClip backwardClip)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        controller.AddParameter(SegwayRiderAnimation.ForwardInputParameter, AnimatorControllerParameterType.Float);

        AnimatorState idle = machine.AddState("Idle");
        idle.motion = forwardClip;
        idle.speed = 0f;

        AnimatorState forward = machine.AddState("Forward");
        forward.motion = forwardClip;

        AnimatorState backward = machine.AddState("Backward");
        backward.motion = backwardClip;

        machine.defaultState = idle;
        AddTransition(idle, forward, AnimatorConditionMode.Greater, InputThreshold);
        AddTransition(idle, backward, AnimatorConditionMode.Less, -InputThreshold);
        AddTransition(forward, backward, AnimatorConditionMode.Less, -InputThreshold);
        AddIdleTransition(forward, idle);
        AddTransition(backward, forward, AnimatorConditionMode.Greater, InputThreshold);
        AddIdleTransition(backward, idle);
        return controller;
    }

    /// <summary>入力が正負いずれにも閾値未満になったとき、停止ステートへ戻る遷移を追加します。</summary>
    private static void AddIdleTransition(AnimatorState source, AnimatorState idle)
    {
        AnimatorStateTransition transition = CreateTransition(source, idle);
        transition.AddCondition(AnimatorConditionMode.Less, InputThreshold, SegwayRiderAnimation.ForwardInputParameter);
        transition.AddCondition(AnimatorConditionMode.Greater, -InputThreshold, SegwayRiderAnimation.ForwardInputParameter);
    }

    /// <summary>入力方向に応じた即時ステート遷移を追加します。</summary>
    private static void AddTransition(AnimatorState source, AnimatorState destination, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition transition = CreateTransition(source, destination);
        transition.AddCondition(mode, threshold, SegwayRiderAnimation.ForwardInputParameter);
    }

    /// <summary>退出時間を待たず、短いブレンドで切り替える遷移を作成します。</summary>
    private static AnimatorStateTransition CreateTransition(AnimatorState source, AnimatorState destination)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0.08f;
        return transition;
    }

    /// <summary>SampleSceneの人物モデルにAnimatorと入力連携コンポーネントを割り当てます。</summary>
    private static void AssignControllerToSceneRider(AnimatorController controller)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject rider = GameObject.Find(RiderObjectName);
        if (rider == null)
        {
            throw new System.InvalidOperationException($"シーン内に人物モデルが見つかりません: {RiderObjectName}");
        }

        Animator animator = rider.GetComponent<Animator>();
        if (animator == null)
        {
            animator = rider.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        SegwayRiderAnimation riderAnimation = rider.GetComponent<SegwayRiderAnimation>();
        if (riderAnimation == null)
        {
            riderAnimation = rider.AddComponent<SegwayRiderAnimation>();
        }

        riderAnimation.ConfigureAnimator(animator);
        EditorUtility.SetDirty(rider);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
