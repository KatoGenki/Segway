using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 前後入力に応じて、セグウェイ搭乗者の停止・前進・後退アニメーションを切り替えます。
/// 停止中はAnimator Controller側のIdleステートを速度0で再生し、前進クリップの先頭フレームを維持します。
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class SegwayRiderAnimation : MonoBehaviour
{
    // Animator Controllerの前後入力パラメーター名。設定生成側と共通に保つ。
    public const string ForwardInputParameter = "ForwardInput";

    // この人物モデルのアニメーションを再生するAnimator。
    [SerializeField] private Animator riderAnimator;
    // PlayerControllerから受け取る、-1から1までの現在の前後入力。
    private float forwardInput;
    // PlayerControllerから入力を最後に受け取った時刻。未配置時は人物側の予備入力へ切り替えるために使う。
    private float controllerInputExpiryTime;
    // PlayerControllerがまだシーンにない場合の、マウス入力基準位置。
    private Vector2 mouseInputOrigin;
    // 人物側の予備入力で最大値になるまでのマウス縦移動量。PlayerControllerの既定値と合わせる。
    [SerializeField, Min(1f)] private float fallbackMouseAccelerationSensitivity = 18f;

    /// <summary>Animator参照を補完し、停止状態をフレーム0から開始します。</summary>
    private void Awake()
    {
        if (riderAnimator == null)
        {
            riderAnimator = GetComponent<Animator>();
        }

        riderAnimator.Play("Idle", 0, 0f);
        riderAnimator.SetFloat(ForwardInputParameter, 0f);
        CaptureMouseInputOrigin();
    }

    /// <summary>現在の入力値をAnimatorへ渡し、ステート遷移を更新します。</summary>
    private void Update()
    {
        // PlayerControllerが未配置の現在のサンプルシーンでも、同じマウス／左スティック操作で確認できるようにする。
        if (Time.unscaledTime > controllerInputExpiryTime)
        {
            forwardInput = ReadFallbackForwardInput();
        }

        if (riderAnimator != null)
        {
            riderAnimator.SetFloat(ForwardInputParameter, forwardInput);
        }
    }

    /// <summary>PlayerControllerから前後入力を受け取り、-1から1の範囲へ収めます。</summary>
    public void SetForwardInput(float input)
    {
        forwardInput = Mathf.Clamp(input, -1f, 1f);
        controllerInputExpiryTime = Time.unscaledTime + 0.1f;
    }

    /// <summary>エディター設定処理からAnimator参照を割り当てます。</summary>
    public void ConfigureAnimator(Animator targetAnimator)
    {
        riderAnimator = targetAnimator;
    }

    /// <summary>PlayerController未配置時の確認用に、左スティックまたはマウス縦移動から前後入力を取得します。</summary>
    private float ReadFallbackForwardInput()
    {
        Vector2 gamepadInput = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue() : Vector2.zero;
        if (Mathf.Abs(gamepadInput.y) > 0.0001f)
        {
            return Mathf.Clamp(gamepadInput.y, -1f, 1f);
        }

        if (Mouse.current == null)
        {
            return 0f;
        }

        Vector2 mouseOffset = Mouse.current.position.ReadValue() - mouseInputOrigin;
        return Mathf.Clamp(mouseOffset.y / fallbackMouseAccelerationSensitivity, -1f, 1f);
    }

    /// <summary>人物側の予備入力用に、現在のマウス位置を中立位置として記録します。</summary>
    private void CaptureMouseInputOrigin()
    {
        if (Mouse.current != null)
        {
            mouseInputOrigin = Mouse.current.position.ReadValue();
        }
    }
}
