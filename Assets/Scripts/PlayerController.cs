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

    [Header("Movement")]
    // 前進時に到達できる最高速度。
    [SerializeField, Min(0f)] private float maxForwardSpeed = 5f;
    // 左右クリックを押している間に変化する見出し角度の速さ（度/秒）。
    [SerializeField, Min(0f)] private float clickTurnSpeed = 120f;

    [Header("Jump")]
    // 地面から押し出すジャンプの瞬間的な力。
    [SerializeField, Min(0f)] private float jumpImpulse = 6.5f;
    // この角度以下の面だけをジャンプ可能な地面として扱う。
    [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 55f;
    // 着地直前に押したジャンプ入力を受け付ける猶予時間。
    [SerializeField, Min(0f)] private float jumpBufferSeconds = 0.12f;

    [Header("Mouse input")]
    // マウス入力の開始位置から、スティック最大入力になるまでの移動距離。小さいほど敏感。
    [SerializeField, Min(1f)] private float mouseAccelerationSensitivity = 18f;
    // マウスを画面外へ出さず、連続した軌跡入力を受け取るための設定。
    [SerializeField] private bool lockCursorForMouseSteering = true;

    [Header("Visual lean")]
    // 見た目のモデルだけが傾く最大角度。
    [SerializeField, Range(0f, 35f)] private float maxLeanAngle = 18f;
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
    // 現在フレームで見出し角を変える、左クリック=-1・右クリック=1の旋回入力値。
    private float clickTurnInput;
    // 物理接触の回転と切り離した、前後移動に使うプレイヤーの見出し角度。
    private float headingYaw;
    // モデルに設定済みの向きや傾きを失わないため、開始時に保存するローカル回転。
    private Quaternion visualRootBaseLocalRotation;
    // マウスを仮想スティック中央として扱う、ゲーム開始時の画面上の位置。
    private Vector2 mouseInputOrigin;
    // マウス入力の開始位置を取得済みかどうか。
    private bool hasMouseInputOrigin;
    // ジャンプボタンが押された後に残る入力受付時間。
    private float bufferedJumpTime;
    // 接地判定から得る、現在ジャンプ可能かどうかの状態。
    private bool isGrounded;
    // 外周円を描くために実行時だけ生成する半透明テクスチャ。
    private Texture2D virtualStickBaseTexture;
    // 入力位置を示すノブを描くために実行時だけ生成するテクスチャ。
    private Texture2D virtualStickKnobTexture;

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
        CaptureMouseInputOrigin();
        //SetCursorLock(lockCursorForMouseSteering);
    }

    /// <summary>前後入力とクリック旋回を読み取り、ジャンプの入力バッファと見た目のリーンを更新します。</summary>
    private void Update()
    {
        UpdateCursorLockState();
        forwardInput = ReadForwardInput();
        clickTurnInput = ReadClickTurnInput();

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
        ApplyForwardMovement();
        TryJump();
    }

    /// <summary>左スティックまたはマウス開始位置からの縦方向変位を、前後入力として読み取ります。</summary>
    private float ReadForwardInput()
    {
        Vector2 gamepadInput = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue() : Vector2.zero;

        // 実機の左スティックを前後へ倒している間は、その縦軸入力を優先する。
        if (Mathf.Abs(gamepadInput.y) > 0.0001f)
        {
            return Mathf.Clamp(gamepadInput.y, -1f, 1f);
        }

        if (Mouse.current == null)
        {
            return 0f;
        }

        if (!hasMouseInputOrigin)
        {
            CaptureMouseInputOrigin();
        }

        // 開始位置からの縦方向距離だけを、最大値1の前後入力へ変換する。入力時間は速度へ影響しない。
        Vector2 mouseOffset = Mouse.current.position.ReadValue() - mouseInputOrigin;
        return Mathf.Clamp(mouseOffset.y / mouseAccelerationSensitivity, -1f, 1f);
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

    /// <summary>現在のマウス位置を、仮想スティック中央として保存します。</summary>
    private void CaptureMouseInputOrigin()
    {
        if (Mouse.current == null)
        {
            hasMouseInputOrigin = false;
            return;
        }

        mouseInputOrigin = Mouse.current.position.ReadValue();
        hasMouseInputOrigin = true;
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

    /// <summary>クリック中だけプレイヤー本体のY角度を更新し、物理トルクを発生させずにその場旋回させます。</summary>
    private void ApplyClickTurn()
    {
        headingYaw += clickTurnInput * clickTurnSpeed * Time.fixedDeltaTime;

        // 左右クリック中にだけRigidbody自身へ角度を加える。回転拘束中でもプレイヤーオブジェクトの向きへ確実に反映する。
        if (clickTurnInput != 0f)
        {
            body.rotation = Quaternion.Euler(0f, headingYaw, 0f);
        }
    }

    /// <summary>現在の見出し方向へ、前後入力とその絶対値に比例した水平速度を設定します。</summary>
    private void ApplyForwardMovement()
    {
        Vector3 headingForward = Quaternion.Euler(0f, headingYaw, 0f) * Vector3.forward;
        Vector3 horizontalVelocity = headingForward * (forwardInput * maxForwardSpeed);
        body.linearVelocity = horizontalVelocity + Vector3.up * body.linearVelocity.y;
    }

    /// <summary>接地中かつバッファ中のジャンプ入力があるときだけジャンプします。</summary>
    private void TryJump()
    {
        if (!isGrounded || bufferedJumpTime <= 0f)
        {
            return;
        }

        bufferedJumpTime = 0f;
        isGrounded = false;
        body.AddForce(Vector3.up * jumpImpulse, ForceMode.Impulse);
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
            * Quaternion.Euler(-forwardInput * maxLeanAngle, 0f, 0f);
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

        GUI.Label(new Rect(baseRect.x, baseRect.y - 22f, size, 20f), "Mouse / Left Stick");
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

    /// <summary>傾斜が許容範囲の接触面を検出し、接地状態にします。</summary>
    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (Vector3.Angle(contact.normal, Vector3.up) <= maxGroundAngle)
            {
                isGrounded = true;
                return;
            }
        }
    }

    /// <summary>地面との接触が終わった時点でジャンプ不可にします。</summary>
    private void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}
