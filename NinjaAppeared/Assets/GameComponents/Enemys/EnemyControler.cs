using UnityEngine;

public class EnemyControler : MonoBehaviour
{
    // Marker component for enemy; behavior can be extended later

    [Header("Death Effect")]
    [Tooltip("Optional material to apply when this enemy is dead (grayscale). If not set, will try ResourceController.DeadShader then fall back to 'Custom/GrayscaleSprite'.")]
    public Material deadMaterial;

    [Range(0f, 1f), Tooltip("밝기 배율 (낮을수록 더 어둡게). Grayscale brightness multiplier.")]
    public float deadGrayBrightness = 0.5f;

    private Material originalMaterial;
    private SpriteRenderer sr;
    private bool isDead;

    void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            originalMaterial = sr.sharedMaterial;
        }
        // Ensure a root state component exists on the enemy instance root
        // At Awake time, this object is the instantiated enemy hierarchy; attach to the highest ancestor in this hierarchy
        var rs = GetComponentInParent<EnemyRootState>();
        if (rs == null)
        {
            // add to this gameObject; EnemySpawner also ensures it on the spawned root
            gameObject.AddComponent<EnemyRootState>();
        }
    }

    public void MarkDead()
    {
        if (isDead) return;
        isDead = true;

        // 부모 이름을 우선 출력 (부모가 없으면 자신의 이름 사용)
        //Debug.Log($"EnemyControler: MarkDead called on { (transform.parent != null ? transform.parent.name : gameObject.name) }");

        EnsureDeadMaterial();
        if (sr != null && deadMaterial != null)
        {
            sr.material = deadMaterial;
            // 적용: 밝기 속성(_GrayBrightness) 존재하면 더 어둡게 설정
            if (deadMaterial.HasProperty("_GrayBrightness"))
            {
                deadMaterial.SetFloat("_GrayBrightness", deadGrayBrightness);
            }
        }
        // Attach a death marker so spawner can reliably distinguish dead removals
        if (GetComponent<EnemyDeathMarker>() == null)
        {
            gameObject.AddComponent<EnemyDeathMarker>();
        }
        // Set IsDead on the nearest ancestor that carries EnemyRootState (the enemy instance root)
        var rs = GetComponentInParent<EnemyRootState>();
        if (rs != null)
        {
            rs.IsDead = true;
        }
        // Count kill globally
        GameStats.IncrementEnemiesKilled();
    }

    private void EnsureDeadMaterial()
    {
        if (deadMaterial != null) return;

        Shader shader = null;

        //1) Prefer ResourceController.DeadShader if available on MainScript
        var main = GameObject.Find("MainScript");
        if (main != null)
        {
            var rc = main.GetComponent<ResourceController>();
            if (rc != null && rc.DeadShader != null)
            {
                shader = rc.DeadShader;
            }
        }

        //2) Fallback to default shader lookup by name
        if (shader == null)
        {
            shader = Shader.Find("Custom/GrayscaleSprite");
        }

        if (shader != null)
        {
            deadMaterial = new Material(shader);
            if (deadMaterial.HasProperty("_GrayBrightness"))
            {
                deadMaterial.SetFloat("_GrayBrightness", deadGrayBrightness);
            }
        }
    }

    // Expose read-only state so spawner can know if the enemy was killed or escaped alive
    public bool IsDead => isDead;
}
