using UnityEngine;

/// <summary>
/// WheelCollider を持つ一本のタイヤを制御します。
/// 接地中のタイヤだけに駆動トルクを与えるため、片輪だけが地面に残った状態でも
/// 出力を上げて段差や脱輪から抜け出せる挙動を作れます。
/// </summary>
[RequireComponent(typeof(WheelCollider))]
public sealed class TireController : MonoBehaviour
{
    [Header("References")]
    // このタイヤの接地判定・摩擦・駆動力を計算する WheelCollider。
    [SerializeField] private WheelCollider wheelCollider;

    [Header("Ground check")]
    // 壁や段差の角を地面として誤検出しないための、下向き接地プローブ半径。
    [SerializeField, Min(0.01f)] private float groundProbeRadius = 0.1f;
    // 接地検出を始める位置をタイヤ中心より少し上へずらし、地面へめり込んだ初期状態でも検出できるようにする。
    [SerializeField, Min(0f)] private float groundProbeStartOffset = 0.12f;
    // タイヤ半径に加えて下方向へ調べる余裕距離。段差上のわずかな隙間を許容する。
    [SerializeField, Min(0f)] private float groundProbePadding = 0.08f;
    // ジャンプ可能な地面として扱う法線の最大角度。壁面への接触は接地に含めない。
    [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 55f;
    // 接地クエリの対象レイヤー。既定では Ignore Raycast を除くすべてのレイヤーを対象にする。
    [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;

    // SphereCastの接地結果を保持する、このタイヤ専用の接地フラグ。
    [SerializeField] private bool isGrounded;
    // 自分自身のColliderを接地対象から除外するために保持する親Rigidbody。
    private Rigidbody vehicleBody;

    /// <summary>このタイヤが現在地面に接地しているかどうかを返します。</summary>
    public bool IsGrounded => isGrounded;

    /// <summary>Inspector へ追加した直後に WheelCollider 参照を自動設定します。</summary>
    private void Reset()
    {
        wheelCollider = GetComponent<WheelCollider>();
    }

    /// <summary>必要な WheelCollider 参照を補完します。</summary>
    private void Awake()
    {
        if (wheelCollider == null)
        {
            wheelCollider = GetComponent<WheelCollider>();
        }

        vehicleBody = GetComponentInParent<Rigidbody>();
    }

    /// <summary>BoxCollider と MeshCollider を含む物理クエリで、このタイヤの接地状態を更新します。</summary>
    private void FixedUpdate()
    {
        if (wheelCollider == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = DetectGround();
    }

    /// <summary>小さな球を下方向へ掃引し、BoxCollider と MeshCollider を含む真下の地面Colliderを検出します。</summary>
    private bool DetectGround()
    {
        float tireRadius = Mathf.Max(0.01f, wheelCollider.radius);
        float probeRadius = Mathf.Min(groundProbeRadius, tireRadius);
        // プレイヤーが前後へリーンしても、接地判定は常にワールド下方向へ行う。
        Vector3 probeDirection = Vector3.down;
        Vector3 probeOrigin = transform.position - probeDirection * groundProbeStartOffset;
        float probeDistance = tireRadius + groundProbeStartOffset + groundProbePadding;
        RaycastHit[] hits = Physics.SphereCastAll(
            probeOrigin,
            probeRadius,
            probeDirection,
            probeDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.attachedRigidbody == vehicleBody)
            {
                continue;
            }

            if (Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>無効化時に WheelCollider の駆動トルクを確実に取り除きます。</summary>
    private void OnDisable()
    {
        if (wheelCollider != null)
        {
            wheelCollider.motorTorque = 0f;
        }
    }
}
