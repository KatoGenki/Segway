using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// セグウェイ風の慣性移動を行うプレイヤー用コントローラーです。
/// マウスまたはゲームパッド左スティックで、前後加速だけを操作します。
/// カメラ操作はこのスクリプトでは扱わず、別コンポーネントに任せます。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("References")]
    // 物理移動・衝突判定を担当するプレイヤー本体のRigidbody。
    [SerializeField] private Rigidbody body;
    // 本体の物理姿勢とは分けて、クリック旋回とリーン演出を表示する子モデル。
    [SerializeField] private Transform visualRoot;
    // 人物モデルの停止・前進・後退アニメーションへ前後入力を渡すコンポーネント。
    [SerializeField] private SegwayRiderAnimation riderAnimation;

    [Header("Movement")]
    // 前進時に到達できる最高速度。
    [SerializeField, Min(0f)] private float maxForwardSpeed = 5f;
    // 左右クリックを押している間に変化する見出し角度の速さ（度/秒）。
    [SerializeField, Min(0f)] private float clickTurnSpeed = 120f;
    // Inputが最大値のときに加える前後の駆動力。Inputは0.05刻みでこの力へ反映される。
    [SerializeField, Min(0f)] private float maxDriveForce = 20f;
    // 同じ前後方向のままInputを弱めたとき、目標速度まで一定で加える減速力。
    [SerializeField, Min(0f)] private float inputReductionDecelerationForce = 10f;
    // デッドゾーン内で前後の水平速度を落とす強さ。駆動中より強めに設定する。
    [SerializeField, Min(0f)] private float deadZoneDeceleration = 8f;

    [Header("Jump")]
    // 地面から押し出すジャンプの瞬間的な力。
    [SerializeField, Min(0f)] private float jumpImpulse = 6.5f;
    // 着地直前に押したジャンプ入力を受け付ける猶予時間。
    [SerializeField, Min(0f)] private float jumpBufferSeconds = 0.12f;

    [Header("Mouse input")]
    // 相対マウス移動を仮想スティック最大入力へ変換するための移動距離。小さいほど敏感。
    [SerializeField, Min(1f)] private float mouseAccelerationSensitivity = 18f;
    // この範囲内の前後入力は速度0として扱う安全圏。入力の微小な揺れを移動へ反映しない。
    [SerializeField, Range(0f, 0.95f)] private float forwardInputDeadZone = 0.12f;
    // マウスを画面外へ出さず、連続した軌跡入力を受け取るための設定。
    [SerializeField] private bool lockCursorForMouseSteering = true;

    [Header("Visual lean")]
    // 前後入力でプレイヤーが傾く最大角度。Inspectorで0〜30度の範囲を調整できる。
    [SerializeField, Range(0f, 30f)] private float maxLeanAngle = 30f;
    // 見た目の傾きが目標角度へ追従する速さ。
    [SerializeField, Min(0f)] private float leanResponse = 12f;

    [Header("Virtual stick display")]
    // 再生中に画面左下へ、現在の操作用スティック入力を表示するかどうか。
    [SerializeField] private bool showVirtualStick = true;
    // 画面上へ描画する仮想スティック外周の直径（ピクセル）。
    [SerializeField, Min(64f)] private float virtualStickDisplaySize = 150f;
    // 画面左下から仮想スティック外周までの余白（ピクセル）。
    [SerializeField] private Vector2 virtualStickDisplayMargin = new(24f, 24f);

    // 現在フレームで物理移動へ渡す、前後方向だけの入力値。
    private float forwardInput;
    // デッドゾーン適用前の前後入力。UIで実際のマウス／スティック位置を表示する。
    private float rawForwardInput;
    // 現在フレームで見出し角を変える、左クリック=-1・右クリック=1の旋回入力値。
    private float clickTurnInput;
    // 物理接触の回転と切り離した、前後移動に使うプレイヤーの見出し角度。
    private float headingYaw;
    // モデルに設定済みの向きや傾きを失わないため、開始時に保存するローカル回転。
    private Quaternion visualRootBaseLocalRotation;
    // フレームごとの相対マウス移動を積み上げた、前後用の仮想スティック入力。
    private float relativeMouseForwardInput;
    // ジャンプボタンが押された後に残る入力受付時間。
    private float bufferedJumpTime;
    // ジャンプ中は壁へ押し付ける前後駆動を停止するための状態。
    private bool isJumping;
    // 子タイヤから取得する、現在ジャンプ可能かどうかの状態。
    private TireController[] tires;
    // 外周円を描くために実行時だけ生成する半透明テクスチャ。
    private Texture2D virtualStickBaseTexture;
    // 入力位置を示すノブを描くために実行時だけ生成するテクスチャ。
    private Texture2D virtualStickKnobTexture;

    // 前後入力を駆動力へ換算するときの固定刻み幅。
    private const float ForwardInputSpeedStep = 0.05f;

    /// <summary>Inspectorから追加した直後に必要なRigidbody参照を自動設定します。</summary>
    private void Reset()
    {
        body = GetComponent<Rigidbody>();
    }

    /// <summary>必要な参照を補完し、物理的に倒れないプレイヤー本体を設定します。</summary>
    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        // 衝突による予期しない回転を防ぐため、物理シミュレーション上の回転をすべて固定する。
        body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        body.rotation = Quaternion.identity;
        headingYaw = 0f;
        if (visualRoot != null)
        {
            visualRootBaseLocalRotation = visualRoot.localRotation;
        }

        if (riderAnimation == null)
        {
            riderAnimation = GetComponentInChildren<SegwayRiderAnimation>(true);
        }

        if (riderAnimation == null)
        {
            riderAnimation = FindAnyObjectByType<SegwayRiderAnimation>();
        }

        tires = GetComponentsInChildren<TireController>(true);

        //SetCursorLock(lockCursorForMouseSteering);
    }

    /// <summary>前後入力とクリック旋回を読み取り、ジャンプの入力バッファと見た目のリーンを更新します。</summary>
    private void Update()
    {
        UpdateCursorLockState();
        forwardInput = ReadForwardInput();
        clickTurnInput = ReadClickTurnInput();
        riderAnimation?.SetForwardInput(forwardInput);

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            bufferedJumpTime = jumpBufferSeconds;
        }
        else
        {
            bufferedJumpTime -= Time.deltaTime;
        }

        UpdateVisualLean();
    }

    /// <summary>物理更新のタイミングでクリック旋回、前後移動、ジャンプを適用します。</summary>
    private void FixedUpdate()
    {
        ApplyClickTurn();
        TryJump();
        ApplyForwardMovement();
    }

    /// <summary>左スティックまたは相対マウス移動を、前後入力として読み取ります。</summary>
    private float ReadForwardInput()
    {
        Vector2 gamepadInput = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue() : Vector2.zero;

        // 実機の左スティックを前後へ倒している間は、その縦軸入力を優先する。
        if (Mathf.Abs(gamepadInput.y) > 0.0001f)
        {
            rawForwardInput = Mathf.Clamp(gamepadInput.y, -1f, 1f);
            return ApplyForwardInputDeadZone(rawForwardInput);
        }

        if (Mouse.current == null)
        {
            rawForwardInput = 0f;
            return 0f;
        }

        // 毎フレームの相対移動量を積み上げるため、ゲーム開始時の画面座標を基準にしない。
        float relativeVerticalMovement = Mouse.current.delta.ReadValue().y;
        relativeMouseForwardInput = Mathf.Clamp(
            relativeMouseForwardInput + relativeVerticalMovement / mouseAccelerationSensitivity,
            -1f,
            1f);
        rawForwardInput = relativeMouseForwardInput;
        return ApplyForwardInputDeadZone(rawForwardInput);
    }

    /// <summary>前後入力の安全圏を適用し、範囲外の入力は最大値1まで滑らかに再スケールします。</summary>
    private float ApplyForwardInputDeadZone(float input)
    {
        float clampedInput = Mathf.Clamp(input, -1f, 1f);
        float absoluteInput = Mathf.Abs(clampedInput);
        if (absoluteInput <= forwardInputDeadZone)
        {
            return 0f;
        }

        float rescaledInput = (absoluteInput - forwardInputDeadZone) / (1f - forwardInputDeadZone);
        return Mathf.Sign(clampedInput) * rescaledInput;
    }

    /// <summary>左クリックを左旋回、右クリックを右旋回として読み取ります。同時押しは旋回なしです。</summary>
    private static float ReadClickTurnInput()
    {
        if (Mouse.current == null)
        {
            return 0f;
        }

        bool turnLeft = Mouse.current.leftButton.isPressed;
        bool turnRight = Mouse.current.rightButton.isPressed;
        return turnLeft == turnRight ? 0f : (turnLeft ? -1f : 1f);
    }

    /// <summary>Escでカーソルを解放し、意図しないゲーム操作を停止します。</summary>
    private void UpdateCursorLockState()
    {
        if (!lockCursorForMouseSteering || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(false);
        }
    }

    /// <summary>マウス軌跡を連続して取得できるように、カーソルの表示とロック状態を切り替えます。</summary>
    private static void SetCursorLock(bool shouldLock)
    {
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }

    /// <summary>クリック旋回のY角度と、前後入力に応じたX方向の傾きをプレイヤーへ反映します。</summary>
    private void ApplyClickTurn()
    {
        headingYaw += clickTurnInput * clickTurnSpeed * Time.fixedDeltaTime;

        if (visualRoot == null)
        {
            // 表示専用の子モデルがない場合は、プレイヤー本体へ直接リーンを反映する。
            body.rotation = Quaternion.Euler(forwardInput * maxLeanAngle, headingYaw, 0f);
        }
        else if (clickTurnInput != 0f)
        {
            // 子モデルにリーンを任せる場合は、本体にはクリック旋回のY角度だけを反映する。
            body.rotation = Quaternion.Euler(0f, headingYaw, 0f);
        }
    }

    /// <summary>左右どちらかのタイヤが接地中なら、0.05刻みの前後入力に応じた駆動力を加えます。</summary>
    private void ApplyForwardMovement()
    {
        if (isJumping)
        {
            // 上昇中、または空中にいる間は前後駆動を止めて壁登りを防ぐ。
            if (body.linearVelocity.y > 0f || !HasGroundedTire())
            {
                return;
            }

            // 下降してタイヤが再び地面へ触れた時点で、通常の前後駆動を再開する。
            isJumping = false;
        }

        // 空中では新しい移動速度を与えず、現在の慣性だけを維持する。
        if (!HasGroundedTire())
        {
            return;
        }

        Vector3 headingForward = Quaternion.Euler(0f, headingYaw, 0f) * Vector3.forward;
        // 前後入力を0.05刻みに丸め、その刻みに対応する駆動力だけを使用する。
        float quantizedForwardInput = QuantizeForwardInput(forwardInput);
        if (Mathf.Abs(quantizedForwardInput) <= 0.0001f)
        {
            ApplyDeadZoneDeceleration();
            return;
        }

        float currentForwardSpeed = Vector3.Dot(body.linearVelocity, headingForward);
        float targetForwardSpeed = quantizedForwardInput * maxForwardSpeed;
        bool inputOpposesCurrentMotion = currentForwardSpeed * quantizedForwardInput < 0f;
        bool inputRequestsLowerSameDirectionSpeed = currentForwardSpeed * quantizedForwardInput > 0f
            && Mathf.Abs(currentForwardSpeed) > Mathf.Abs(targetForwardSpeed);

        if (inputOpposesCurrentMotion)
        {
            // 前後を切り替える場合も、前進加速と同じくInputに比例した駆動力を使う。
            body.AddForce(headingForward * (quantizedForwardInput * maxDriveForce), ForceMode.Force);
            return;
        }

        if (inputRequestsLowerSameDirectionSpeed)
        {
            // 同方向のままInputを戻したときは、速度の向きと反対へ一定の減速力を加える。
            body.AddForce(headingForward * (-Mathf.Sign(currentForwardSpeed) * inputReductionDecelerationForce), ForceMode.Force);
            return;
        }

        bool isAtRequestedSpeed = Mathf.Sign(quantizedForwardInput) == Mathf.Sign(currentForwardSpeed)
            && Mathf.Abs(currentForwardSpeed) >= Mathf.Abs(targetForwardSpeed);
        if (!isAtRequestedSpeed)
        {
            // 0.05ごとに変化するInputを、前後へ加える駆動力へ変換する。
            body.AddForce(headingForward * (quantizedForwardInput * maxDriveForce), ForceMode.Force);
        }
    }

    /// <summary>入力を0.05刻みへ丸め、刻みごとの安定した目標速度を作ります。</summary>
    private static float QuantizeForwardInput(float input)
    {
        return Mathf.Clamp(
            Mathf.Round(input / ForwardInputSpeedStep) * ForwardInputSpeedStep,
            -1f,
            1f);
    }

    /// <summary>デッドゾーン内では駆動せず、水平速度へ強めの減速を掛けます。</summary>
    private void ApplyDeadZoneDeceleration()
    {
        Vector3 horizontalVelocity = new(body.linearVelocity.x, 0f, body.linearVelocity.z);
        Vector3 nextHorizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            Vector3.zero,
            deadZoneDeceleration * Time.fixedDeltaTime);
        body.linearVelocity = nextHorizontalVelocity + Vector3.up * body.linearVelocity.y;
    }

    /// <summary>左右いずれかのタイヤが接地中かつ、バッファ中のジャンプ入力があるときだけジャンプします。</summary>
    private void TryJump()
    {
        if (!HasGroundedTire() || bufferedJumpTime <= 0f)
        {
            return;
        }

        bufferedJumpTime = 0f;
        isJumping = true;
        // ジャンプ開始時は前後の慣性を消し、その場から垂直に跳び上がる。
        body.linearVelocity = Vector3.up * body.linearVelocity.y;
        body.AddForce(Vector3.up * jumpImpulse, ForceMode.Impulse);
    }

    /// <summary>左右どちらかのタイヤが接地しているときだけ true を返します。</summary>
    private bool HasGroundedTire()
    {
        foreach (TireController tire in tires)
        {
            if (tire.IsGrounded)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>物理本体を倒さず、子モデルへクリック旋回と前後リーンを反映します。</summary>
    private void UpdateVisualLean()
    {
        if (visualRoot == null)
        {
            return;
        }

        // 初期のモデル姿勢を基準に、見出し角のY回転と前後入力のXリーンを合成する。Rigidbody本体の物理回転は使わない。
        Quaternion targetLean = Quaternion.Euler(0f, headingYaw, 0f)
            * visualRootBaseLocalRotation
            * Quaternion.Euler(forwardInput * maxLeanAngle, 0f, 0f);
        visualRoot.localRotation = Quaternion.Slerp(
            visualRoot.localRotation,
            targetLean,
            1f - Mathf.Exp(-leanResponse * Time.deltaTime));
    }

    /// <summary>再生画面の左下に、マウスまたは左スティックから得た現在の仮想入力を描画します。</summary>
    private void OnGUI()
    {
        if (!showVirtualStick)
        {
            return;
        }

        EnsureVirtualStickTextures();

        float size = virtualStickDisplaySize;
        Rect baseRect = new(
            virtualStickDisplayMargin.x,
            Screen.height - virtualStickDisplayMargin.y - size,
            size,
            size);
        GUI.DrawTexture(baseRect, virtualStickBaseTexture);

        // 前後入力だけを、仮想スティックの縦方向ノブ位置へ反映する。
        Vector2 virtualStickInput = new(0f, forwardInput);
        float knobSize = size * 0.28f;
        float movableRadius = (size - knobSize) * 0.5f;
        Vector2 center = baseRect.center;
        Vector2 knobCenter = center + new Vector2(virtualStickInput.x, -virtualStickInput.y) * movableRadius;
        Rect knobRect = new(knobCenter.x - knobSize * 0.5f, knobCenter.y - knobSize * 0.5f, knobSize, knobSize);
        GUI.DrawTexture(knobRect, virtualStickKnobTexture);

        float currentForwardSpeed = body == null
            ? 0f
            : Vector3.Dot(
                body.linearVelocity,
                Quaternion.Euler(0f, headingYaw, 0f) * Vector3.forward);
        Rect infoRect = new(baseRect.x, baseRect.y - 88f, size, 84f);
        GUI.Box(infoRect, GUIContent.none);
        GUI.Label(new Rect(infoRect.x + 6f, infoRect.y + 4f, size - 12f, 20f), "Mouse / Left Stick");
        GUI.Label(new Rect(infoRect.x + 6f, infoRect.y + 24f, size - 12f, 20f), $"Input: {rawForwardInput:F2}");
        GUI.Label(new Rect(infoRect.x + 6f, infoRect.y + 44f, size - 12f, 20f), $"Dead Zone: +/-{forwardInputDeadZone:F2}");
        GUI.Label(new Rect(infoRect.x + 6f, infoRect.y + 64f, size - 12f, 20f), $"Speed: {currentForwardSpeed:F2}");
    }

    /// <summary>仮想スティック描画に必要な円形テクスチャを、最初の描画時だけ生成します。</summary>
    private void EnsureVirtualStickTextures()
    {
        if (virtualStickBaseTexture == null)
        {
            virtualStickBaseTexture = CreateCircleTexture(128, new Color(0.08f, 0.08f, 0.08f, 0.62f));
        }

        if (virtualStickKnobTexture == null)
        {
            virtualStickKnobTexture = CreateCircleTexture(128, new Color(0.9f, 0.95f, 1f, 0.9f));
        }
    }

    /// <summary>GUIで使う単色の円形テクスチャを生成します。</summary>
    private static Texture2D CreateCircleTexture(int size, Color color)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear
        };
        Color transparent = new(color.r, color.g, color.b, 0f);
        float radius = size * 0.5f;
        Vector2 center = new(radius - 0.5f, radius - 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), center) <= radius ? color : transparent);
            }
        }

        texture.Apply();
        return texture;
    }

    /// <summary>実行時に生成した仮想スティック用テクスチャを破棄します。</summary>
    private void OnDestroy()
    {
        Destroy(virtualStickBaseTexture);
        Destroy(virtualStickKnobTexture);
    }

}
