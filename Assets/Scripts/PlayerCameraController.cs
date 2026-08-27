using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤーを後方から追従する三人称カメラです。
/// WASD/矢印キーまたはゲームパッド右スティックで視点を回転します。
/// </summary>
public sealed class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    // カメラが後方から追従し、画面中央に置く対象のTransform。
    [SerializeField] private Transform target;

    [Header("Follow")]
    // プレイヤー原点からカメラが注視する高さ・位置の補正値。
    [SerializeField] private Vector3 targetOffset = new(0f, 1.5f, 0f);
    // 注視点からカメラまでの後方距離。
    [SerializeField, Min(0.1f)] private float distance = 6f;
    // カメラ位置だけにかける追従遅延。0なら即時追従。
    [SerializeField, Min(0f)] private float positionSmoothTime = 0f;

    [Header("Rotation")]
    // キーボード操作時の1秒あたりの視点回転角度。
    [SerializeField, Min(0f)] private float keyboardRotationSpeed = 120f;
    // 右スティック操作時の1秒あたりの視点回転角度。
    [SerializeField, Min(0f)] private float stickRotationSpeed = 180f;
    // カメラを見下ろせる下向き角度の下限。
    [SerializeField, Range(-89f, 0f)] private float minPitch = -30f;
    // カメラを見上げられる上向き角度の上限。
    [SerializeField, Range(0f, 89f)] private float maxPitch = 60f;

    // プレイヤーの周囲を回る水平方向の視点角度。
    private float yaw;
    // プレイヤーを見上げ・見下ろす垂直方向の視点角度。
    private float pitch = 15f;
    // SmoothDampが位置の追従速度を内部的に保持するための変数。
    private Vector3 followVelocity;

    /// <summary>追従対象を補完し、開始時のカメラ方角をプレイヤーに合わせます。</summary>
    private void Awake()
    {
        if (target == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }
    }

    /// <summary>入力後に追従位置と注視方向を更新し、プレイヤーを常に画面中央へ置きます。</summary>
    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateRotation();

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + targetOffset;
        Vector3 desiredPosition = pivot - rotation * Vector3.forward * distance;

        if (positionSmoothTime <= 0f)
        {
            transform.position = desiredPosition;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref followVelocity,
                positionSmoothTime);
        }

        // 追従位置が少しでも遅れた場合も、プレイヤーの追従点は常に画面中央に置く。
        transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
    }

    /// <summary>キーボードまたは右スティックの入力からカメラの水平・垂直角度を更新します。</summary>
    private void UpdateRotation()
    {
        Vector2 keyboardInput = ReadKeyboardInput();
        Vector2 stickInput = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;

        Vector2 input;
        float rotationSpeed;
        if (stickInput.sqrMagnitude > 0.01f)
        {
            input = Vector2.ClampMagnitude(stickInput, 1f);
            rotationSpeed = stickRotationSpeed;
        }
        else
        {
            input = keyboardInput;
            rotationSpeed = keyboardRotationSpeed;
        }

        yaw += input.x * rotationSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch - input.y * rotationSpeed * Time.deltaTime, minPitch, maxPitch);
    }

    /// <summary>WASDと矢印キーをカメラ回転用の2軸入力へ変換します。</summary>
    private static Vector2 ReadKeyboardInput()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            horizontal += 1f;
        }

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
        {
            vertical += 1f;
        }

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        {
            vertical -= 1f;
        }

        return new Vector2(horizontal, vertical).normalized;
    }
}
