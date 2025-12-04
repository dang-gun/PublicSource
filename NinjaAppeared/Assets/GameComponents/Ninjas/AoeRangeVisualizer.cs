using UnityEngine;

/// <summary>
/// AoE 반경을 원형 라인으로 표시합니다.
/// - 애니메이션 없음, 고정 원
/// - destroyWhenOffscreen=true일 때 화면에서 보이지 않게 되면 제거합니다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class AoeRangeVisualizer : MonoBehaviour
{
    [Tooltip("화면에서 보이지 않게 되면 제거")]
    public bool destroyWhenOffscreen = true;
    [Tooltip("라인 두께(월드 단위)")]
    public float lineWidth = 0.06f;
    [Tooltip("원 그리기 세그먼트 수")]
    public int segments = 48;
    [Tooltip("시간 기반 제거(초).0이면 비활성화. destroyWhenOffscreen=true면 무시됨")]
    public float duration = 0f;

    private LineRenderer _lr;
    private float _radius;
    private Vector3 _center;
    private float _t;

    public void Initialize(Vector3 center, float radius, Color color, int sortingLayerId, int sortingOrder)
    {
        _center = center;
        _radius = Mathf.Max(0f, radius);

        _lr = GetComponent<LineRenderer>();
        if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();
        _lr.useWorldSpace = true;
        _lr.loop = true;
        _lr.positionCount = Mathf.Max(3, segments);
        _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.receiveShadows = false;
        _lr.textureMode = LineTextureMode.Stretch;
        _lr.alignment = LineAlignment.TransformZ;
        var rend = _lr;
        rend.sortingLayerID = sortingLayerId;
        rend.sortingOrder = sortingOrder;

        // 기본 머티리얼: Sprites/Default
        if (_lr.material == null)
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            _lr.material = mat;
        }

        // 어두운 빨간색 고정 원 (start/end 동일, 알파 고정)
        _lr.startColor = color;
        _lr.endColor = color;
        _lr.startWidth = lineWidth;
        _lr.endWidth = lineWidth;

        BuildCircle(_center, _radius);
    }

    void Update()
    {
        if (destroyWhenOffscreen)
        {
            // 카메라 가시성 이벤트에 맡기기; 시간 기반 제거 무시
            return;
        }
        if (duration > 0f)
        {
            _t += Time.deltaTime;
            if (_t >= duration)
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnBecameInvisible()
    {
        if (destroyWhenOffscreen)
        {
            Destroy(gameObject);
        }
    }

    private void BuildCircle(Vector3 center, float radius)
    {
        int count = Mathf.Max(3, segments);
        float step = 2f * Mathf.PI / count;
        for (int i = 0; i < count; i++)
        {
            float a = i * step;
            float x = Mathf.Cos(a) * radius + center.x;
            float y = Mathf.Sin(a) * radius + center.y;
            _lr.SetPosition(i, new Vector3(x, y, center.z));
        }
    }
}
