using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 닌자가 적과 충돌하여 적을 처치하는 순간 충돌 지점을 기준으로 주위 적들도 함께 죽게 하는 컴포넌트.
/// 반경은 GameConstants.AoeKillRadius 를 사용합니다.
/// 요구사항: 닌자와 적이 부딛친 포인트를 기준으로 원형으로2f 거리에 영향을 줍니다.
/// </summary>
[DisallowMultipleComponent]
public class NinjaAoeOnHit : MonoBehaviour
{
    [Tooltip("AoE 반경 (GameConstants.AoeKillRadius 기본 사용). Inspector에서 재정의 가능.")]
    public float radius = GameConstants.AoeKillRadius;

    [Tooltip("적 탐색 시 사용할 LayerMask (비워두면 모든 Collider 대상).")]
    public LayerMask enemyLayerMask = ~0; // default all

    [Tooltip("적 객체에서 EnemyControler 컴포넌트를 찾을 때 자식까지 검사할지 여부.")]
    public bool searchChildren = true;

    [Tooltip("이미 죽은 적은 다시 처리하지 않음 (true 권장).")]
    public bool skipDead = true;

    [Tooltip("충돌 당 한 번만 AoE 실행 (중복 충돌 스팸 방지).")]
    public bool singleExecutionPerPhysicsStep = true;

    [Header("AoE Item Interaction")]
    [Tooltip("AoE 범위 내 아이템도 함께 소비되도록 처리")]
    public bool affectItemsInAoe = true;
    [Tooltip("아이템 탐색에 사용할 LayerMask (비워두면 모든 Collider 대상)")]
    public LayerMask itemLayerMask = ~0; // default all

    [Header("VFX")]
    [Tooltip("AoE 표시 색 (어두운 빨간색 추천).")]
    public Color visualizeColor = new Color(0.5f, 0f, 0f, 0.85f);
    [Tooltip("AoE 시각화 표시 여부")]
    public bool visualizeRange = true;
    [Tooltip("AoE 시각화가 화면에서 벗어날 때까지 유지 (권장)")]
    public bool visualizeHoldUntilOffscreen = true;
    [Tooltip("AoE 시각화 유지 시간(초). visualizeHoldUntilOffscreen=false일 때만 사용")]
    public float visualizeDuration = 1.0f;

    private int _lastProcessedFrame = -1; // 중복 처리 방지용

    /// <summary>
    /// 외부(예: ProtractorController)가 적 처리를 수행한 직후 호출하여 AoE 확장 처리를 실행할 수 있게 함.
    /// </summary>
    /// <param name="center">충돌 지점(접촉 포인트) 월드 좌표</param>
    /// <param name="primaryEnemy">기본으로 닌자가 직접 죽인 적 (콤보/UI 처리 시 제외하기 위해 전달)
    /// </param>
    public void ProcessAoe(Vector3 center, EnemyControler primaryEnemy)
    {
        if (singleExecutionPerPhysicsStep && _lastProcessedFrame == Time.frameCount)
        {
            return; // 동일 프레임 재실행 방지
        }
        _lastProcessedFrame = Time.frameCount;

        float r = Mathf.Max(0f, radius);
        if (r <= 0f) return;

        // 시각화 (고정 원)
        if (visualizeRange)
        {
            TryVisualize(center, r);
        }

        // Physics2D overlap 사용.2D 게임이므로 CircleCast 아닌 OverlapCircle 활용.
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, r, enemyLayerMask);
        if (hits == null || hits.Length == 0)
        {
            // 적이 없어도 아이템은 처리할 수 있도록 계속 진행
        }
        else
        {
            ComboController combo = ComboController.Instance; // null 허용
            int extraKills = 0;
            var processed = new HashSet<EnemyControler>();
            if (primaryEnemy != null) processed.Add(primaryEnemy); // 기본 대상은 콤보 증가 이미 처리됨

            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null) continue;
                EnemyControler enemy = searchChildren ? col.GetComponentInChildren<EnemyControler>() : col.GetComponent<EnemyControler>();
                if (enemy == null) continue;
                if (processed.Contains(enemy)) continue; // 이미 기본 혹은 이전에 처리된 동일 Enemy
                processed.Add(enemy);
                bool wasDead = enemy.IsDead;
                if (skipDead && wasDead) continue; // 이미 죽은 경우 무시

                enemy.MarkDead();

                // AoE로 죽은 적도 더 이상 충돌하지 않도록 BoxCollider2D 비활성화
                var boxes = enemy.GetComponentsInChildren<BoxCollider2D>(true);
                for (int b = 0; b < boxes.Length; b++)
                {
                    var box = boxes[b];
                    if (box != null) box.enabled = false;
                }

                if (!wasDead)
                {
                    extraKills++;
                    if (combo != null) combo.AddKill(enemy.transform); // 새롭게 죽인 경우만 콤보 증가
                }
            }

            if (GameConstants.DebugIs && extraKills > 0)
            {
                Debug.Log($"[NinjaAoeOnHit] AoE 추가 처치 수: {extraKills} (반경 {r})");
            }
        }

        // AoE 범위 내 아이템 처리
        if (affectItemsInAoe)
        {
            var itemCols = Physics2D.OverlapCircleAll(center, r, itemLayerMask);
            if (itemCols != null && itemCols.Length > 0)
            {
                int consumedCount = 0;
                var processedItems = new HashSet<ItemPickup>();
                for (int i = 0; i < itemCols.Length; i++)
                {
                    var col = itemCols[i];
                    if (col == null) continue;
                    // 자신 또는 자식에서 ItemPickup 찾기
                    ItemPickup item = col.GetComponent<ItemPickup>();
                    if (item == null) item = col.GetComponentInChildren<ItemPickup>();
                    if (item == null) continue;
                    if (processedItems.Contains(item)) continue;
                    processedItems.Add(item);
                    item.ForceConsume();
                    consumedCount++;
                }
                if (GameConstants.DebugIs && consumedCount > 0)
                {
                    Debug.Log($"[NinjaAoeOnHit] AoE로 아이템 {consumedCount}개 소비");
                }
            }
        }
    }

    private void TryVisualize(Vector3 center, float r)
    {
        if (false == GameConstants.DebugIs)
        {
            return;
        }

        int layerId = ProtractorController.CurrentSortingLayerId;
        int order = ProtractorController.CurrentTopSortingOrder + 1; // 항상 최상단보다 위
        var go = new GameObject("AoeRangeVisualizer");
        var vis = go.AddComponent<AoeRangeVisualizer>();
        vis.destroyWhenOffscreen = visualizeHoldUntilOffscreen;
        vis.duration = visualizeHoldUntilOffscreen ? 0f : Mathf.Max(0.05f, visualizeDuration);
        vis.segments = 48;
        vis.lineWidth = 0.05f;
        vis.Initialize(center, r, visualizeColor, layerId, order);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
