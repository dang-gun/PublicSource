using UnityEngine;

/// <summary>
/// 아이템이 닌자와 트리거로 접촉하면 HP +1 하고 자신을 제거.
/// 트리거가 동작하지 않을 경우(예: UI 캔버스 부모 등) 거리 기반으로도 픽업.
/// HP 증가는 반드시 HealthController.SetHP를 통해 수행.
/// 아이템이 닌자와 트리거로 접촉하면 효과 적용 후 자신을 제거.
/// Heart: HP 회복. Star: Fever 점수 증가.
/// 트리거 실패 시 거리 기반 픽업.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [Tooltip("HP 회복 대상 닌자")]
    public Transform ninja;
    [Tooltip("충돌 후 파괴까지 딜레이(초). <=0 이면 즉시")]
    public float destroyDelay = 0f;
    [Tooltip("기본 HP 회복량 (데이터 없을 때)")] public int healAmount = 1;
    [Tooltip("거리 기반 픽업 반경 (Trigger 실패 시 대체)")] public float proximityRadius = 0.65f;
    private bool consumed;

    // Spawner가 설정: 이 픽업이 표현하는 아이템 데이터
    public ItemDataModelBase itemData;

    private void Awake()
    {
        if (ninja == null && GlobalStatic.NinjaTf != null)
        {
            ninja = GlobalStatic.NinjaTf;
        }
    }

    void Update()
    {
        if (!consumed && ninja != null)
        {
            float d = Vector2.Distance(transform.position, ninja.position);
            if (d <= proximityRadius)
            {
                Consume();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (ninja == null) return;
        if (other.transform != ninja && other.transform.root != ninja) return;
        Consume();
    }

    /// <summary>
    /// 외부(예: AoE)에서 강제로 아이템 효과를 발동시킬 때 사용.
    /// </summary>
    public void ForceConsume()
    {
        if (consumed) return;
        if (ninja == null && GlobalStatic.NinjaTf != null)
        {
            ninja = GlobalStatic.NinjaTf;
        }
        Consume();
    }

    private void Consume()
    {
        if (itemData != null)
        {
            switch (itemData.ItemType)
            {
                case ItemType.Heart:
                    TryHealViaHealthController(itemData.ItemValue);
                    break;
                case ItemType.Star:
                    FeverTimeModel.Instance.AddScore(itemData.ItemValue);
                    // 피버타임 활성 중이면 시간 +10초 추가
                    if (FeverTimeModel.Instance != null && FeverTimeModel.Instance.IsActive && GameTimerController.Instance != null)
                    {
                        GameTimerController.Instance.AddTime(10);
                        if (GameConstants.DebugIs)
                            Debug.Log($"ItemPickup: 피버타임 중 Star 획득 - 시간 +10초 => {GameTimerController.Instance.RemainingSeconds}초");
                    }
                    if (GameConstants.DebugIs)
                        Debug.Log($"ItemPickup: Fever 점수 +{itemData.ItemValue} => {FeverTimeModel.Instance.CurrentScore}/{FeverTimeModel.MaxScore}");
                    break;
                case ItemType.Clock:
                    if (GameTimerController.Instance != null)
                    {
                        GameTimerController.Instance.AddTime(itemData.ItemValue);
                        if (GameConstants.DebugIs)
                            Debug.Log($"ItemPickup: 시간 +{itemData.ItemValue}초 => {GameTimerController.Instance.RemainingSeconds}초");
                    }
                    break;
                default:
                    // 다른 타입은 기본 healAmount 적용 혹은 추후 확장
                    TryHealViaHealthController(healAmount);
                    break;
            }
        }
        else
        {
            // 데이터 없으면 기본 Heart 동작 가정
            TryHealViaHealthController(healAmount);
        }

        consumed = true;
        if (destroyDelay <= 0f) Destroy(gameObject); else Destroy(gameObject, destroyDelay);
    }

    /// <summary>
    /// HealthController 컴포넌트를 찾아 SetHP를 통해 회복. 실패 시(없음) 로그만.
    /// HealthController 컴포넌트를 찾아 SetHP를 통해 회복. 실패 시 로그만.
    /// </summary>
    private void TryHealViaHealthController(int amount)
    {
        if (amount <= 0) return; // 0 이하면 무시
        if (ninja == null) return;
        var hc = GlobalStatic.Ninja_Health;
        if (hc == null)
        {
            if (GameConstants.DebugIs)
                Debug.Log("ItemPickup: HealthController 없음. HP 회복 미적용.");
            return;
        }

        // HP가 이미 최대치면 시간 +10초 보너스
        if (hc.HP >= hc.MaxHP)
        {
            if (GameTimerController.Instance != null)
            {
                GameTimerController.Instance.AddTime(10);
                if (GameConstants.DebugIs)
                    Debug.Log($"ItemPickup: HP 최대치 - 시간 +10초 => {GameTimerController.Instance.RemainingSeconds}초");
            }
            return;
        }

        int newHp = hc.HP + amount;
        hc.SetHP(newHp); // SetHP가 MaxHP 범위로 클램프
        if (GameConstants.DebugIs)
            Debug.Log($"ItemPickup: HP 회복 적용 => {hc.HP}/{hc.MaxHP}");
    }
}
