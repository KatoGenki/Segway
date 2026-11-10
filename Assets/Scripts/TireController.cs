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
    // 接地検出を始める位置をタイヤ中心より少し上へずらし、地面へめり込んだ初期状態でも検出できるようにする。
    [SerializeField, Min(0f)] private float groundProbeStartOffset = 0.12f;
    // タイヤ半径に加えて下方向へ調べる余裕距離。段差上のわずかな隙間を許容する。
    [SerializeField, Min(0f)] private float groundProbePadding = 0.08f;
    // ジャンプ可能な地面として扱う法線の最大角度。壁面への接触は接地に含めない。
    [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 55f;
    // 接地クエリの対象レイヤー。既定では Ignore Raycast を除くすべてのレイヤーを対象にする。
    [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;

    // 真下のRaycastによる接地結果を保持する、このタイヤ専用の接地フラグ。
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

    /// <summary>床面へタイヤが沈んだ量を返します。壁面では常に0を返します。</summary>
    public float GetGroundPenetration()
    {
        if (wheelCollider == null || !TryGetGroundHit(out RaycastHit hit))
        {
            return 0f;
        }

        // Rayの始点はタイヤ中心より上なので、そのオフセットを加えた距離が本来の接地点になる。
        float targetHitDistance = GetWorldTireRadius() + groundProbeStartOffset;
        return Mathf.Max(0f, targetHitDistance - hit.distance);
    }

    /// <summary>真下へのRaycastで、BoxCollider と MeshCollider を含む床面だけを検出します。</summary>
    private bool DetectGround()
    {
        return TryGetGroundHit(out _);
    }

    /// <summary>真下にあり、かつ上向きの法線を持つ床面だけを取得します。</summary>
    private bool TryGetGroundHit(out RaycastHit groundHit)
    {
        float tireRadius = GetWorldTireRadius();
        // プレイヤーが前後へリーンしても、接地判定は常にワールド下方向へ行う。
        Vector3 probeDirection = Vector3.down;
        Vector3 probeOrigin = transform.position - probeDirection * groundProbeStartOffset;
        float probeDistance = tireRadius + groundProbeStartOffset + groundProbePadding;
        RaycastHit[] hits = Physics.RaycastAll(
            probeOrigin,
            probeDirection,
            probeDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        bool hasGroundHit = false;
        float closestDistance = float.PositiveInfinity;
        groundHit = default;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.attachedRigidbody == vehicleBody)
            {
                continue;
            }

            if (Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle && hit.distance < closestDistance)
            {
                groundHit = hit;
                closestDistance = hit.distance;
                hasGroundHit = true;
            }
        }

        return hasGroundHit;
    }

    /// <summary>Prefabの親スケールを含めた、ワールド空間でのタイヤ半径を返します。</summary>
    private float GetWorldTireRadius()
    {
        Vector3 scale = transform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return Mathf.Max(0.01f, wheelCollider.radius * largestScale);
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
