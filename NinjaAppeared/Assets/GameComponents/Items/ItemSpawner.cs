using UnityEngine;
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// 지정된 대상(닌자/카메라)의 진행 거리(X축)를 기준으로 일정 거리마다 아이템 스폰을 예약하고,
/// 다음에 스폰되는 적의 머리 위에 아이템을 생성한다.
/// 닌자와 충돌(Trigger)하면 HP +1 후 아이템 제거.
/// </summary>
public class ItemSpawner : MonoBehaviour
{
    [Tooltip(" 거리 측정에 사용할 대상 (닌자 등). null이면 이름으로 자동 탐색.")] public Transform target;
    [Tooltip("몇 미터마다 스폰할지 (GameConstants.MetersPerUnit 기준).")]
    public float spawnEveryMeters = 40f;

    [Tooltip("아이템을 배치할 패널(계층: ItemPanel). null이면 자동 탐색.")] public Transform itemPanel;
    [Tooltip("리소스 컨트롤러 (MainScript > ResourceController). null이면 자동 탐색.")] public ResourceController resourceController;

    [Tooltip("시작 시 첫 아이템을 즉시 생성할지 여부 (이벤트 대기 방식으로 예약)")] public bool spawnImmediatelyOnStart = false;

    private float _lastSpawnX;
    private bool _initialized;
    private int _pendingItemCount; // 거리 충족으로 예약된 아이템 개수

    void OnEnable()
    {
        EnemySpawner.OnEnemySpawned += HandleEnemySpawned; // 다음 적 스폰 시 소비
    }

    void OnDisable()
    {
        EnemySpawner.OnEnemySpawned -= HandleEnemySpawned;
    }

    void Start()
    {
        AutoResolve();
        if (target != null)
        {
            _lastSpawnX = target.position.x;
            if (spawnImmediatelyOnStart)
            {
                // 즉시 생성 대신, 다음 적 스폰 때 1개 생성되도록 예약
                _pendingItemCount++;
            }
        }
    }

    void Update()
    {
        if (!_initialized) AutoResolve();
        if (target == null) return;

        float traveled = (target.position.x - _lastSpawnX) * GameConstants.MetersPerUnit;
        if (traveled >= spawnEveryMeters)
        {
            // 바로 생성하는 대신, 다음 적 스폰 이벤트 때 생성
            _pendingItemCount++;
            _lastSpawnX = target.position.x; // 기준 갱신
        }
    }

    /// <summary>
    /// 적 스폰 이벤트 수신 시, 예약된 아이템이 있으면 1개를 해당 적 머리 위에 생성한다.
    /// </summary>
    private void HandleEnemySpawned(Transform enemy, Vector3 headWorldPos)
    {
        if (_pendingItemCount <= 0) return;
        if (resourceController == null || resourceController.ItmePrefab == null || itemPanel == null) return;
        // 1개만 소비(이벤트 1회당 최대 1개)
        _pendingItemCount--;
        SpawnItemAtHead(headWorldPos);
    }

    /// <summary>
    /// 필드 자동 해상
    /// </summary>
    private void AutoResolve()
    {
        if (target == null)
        {
            var ninjaGo = GameObject.Find("Ninja_Defult");
            if (ninjaGo != null) target = ninjaGo.transform;
        }
        if (itemPanel == null)
        {
            var panelGo = GameObject.Find("ItemPanel");
            if (panelGo != null) itemPanel = panelGo.transform;
        }
        if (resourceController == null)
        {
            var mainScript = GameObject.Find("MainScript");
            if (mainScript != null) resourceController = mainScript.GetComponent<ResourceController>();
        }
        _initialized = (target != null && itemPanel != null && resourceController != null);
    }

    private void SpawnItemAtHead(Vector3 headWorldPos)
    {
        // 랜덤 데이터 선택
        ItemDataModelBase data = null;
        if (resourceController.ItemList != null && resourceController.ItemList.Count > 0)
        {
            var usable = resourceController.ItemList.Where(w => w != null && w.SpriteList != null && w.SpriteList.Length > 0).ToList();
            if (usable.Count > 0)
            {
                int idx = Random.Range(0, usable.Count);
                data = usable[idx];
            }
        }

        var go = Instantiate(resourceController.ItmePrefab);
        go.name = resourceController.ItmePrefab.name + "_Spawned";
        go.transform.SetParent(itemPanel, false); // UI 부모 하위로 배치

        if (data != null)
        {
            Sprite sprite = data.SpriteList.Length == 1 ? data.SpriteList[0] : data.SpriteList[Random.Range(0, data.SpriteList.Length)];

            // 아이템 프리팹의 'Image' 자식(일반 GameObject)에 부착된 SpriteRenderer에 할당
            var imgTr = go.transform.Find("Image");
            bool spriteAssigned = false;
            if (imgTr != null)
            {
                var sr = imgTr.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = sprite;
                    spriteAssigned = true;
                }
                else
                {
                    // 동일 자식의 하위에서도 SpriteRenderer 검색
                    sr = imgTr.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.sprite = sprite;
                        spriteAssigned = true;
                    }
                }
            }
            if (!spriteAssigned)
            {
                // 프리팹 전체 하위에서 SpriteRenderer 검색 후 할당
                var sr = go.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = sprite;
                    spriteAssigned = true;
                }
            }
#if UNITY_EDITOR
            if (!spriteAssigned)
            {
                Debug.LogWarning("ItemSpawner: SpriteRenderer를 찾지 못해 스프라이트를 할당하지 못했습니다. 프리팹에 SpriteRenderer를 추가하세요.");
            }
#endif
        }

        // 위치: 이번에 스폰된 적의 머리 위
        PositionItemOnUIOverHead(go.transform, headWorldPos);

        // 픽업 동작 부착 (UI 프리팹이어도 월드 트리거를 위해 별도 콜라이더 추가)
        AttachPickupBehaviour(go, data);
    }

    private void PositionItemOnUIOverHead(Transform item, Vector3 headWorld)
    {
        if (item == null || itemPanel == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(headWorld);
        var panelRt = itemPanel as RectTransform;
        var itemRt = item as RectTransform;
        if (panelRt != null && itemRt != null)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRt, screenPos, cam, out localPoint))
            {
                // 살짝 위 오프셋(픽셀)
                localPoint.y += 20f;
                itemRt.anchoredPosition = localPoint;
            }
        }
        else
        {
            // 비-UI 환경 대비: 월드 좌표 그대로 사용
            item.position = new Vector3(headWorld.x, headWorld.y + 0.5f, item.position.z);
        }
    }

    private void AttachPickupBehaviour(GameObject itemGo, ItemDataModelBase data)
    {
        if (itemGo == null) return;
        var pickup = itemGo.GetComponent<ItemPickup>();
        if (pickup == null) pickup = itemGo.AddComponent<ItemPickup>();
        pickup.ninja = target; // 대상 닌자 전달
        pickup.itemData = data; // pass selected item data so pickup can apply effect

        // 콜라이더 준비 (이미 있으면 isTrigger로 변경)
        var col = itemGo.GetComponent<Collider2D>();
        if (col == null)
        {
            col = itemGo.AddComponent<CircleCollider2D>();
            ((CircleCollider2D)col).radius = 0.5f; // 기본 반경
        }
        col.isTrigger = true; // 물리 충돌 영향 제거

        // 물리 반응을 막기 위해(Trigger 이벤트만) kinematic 리지드바디 추가
        var rb = itemGo.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = itemGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }
    }

    /// <summary>
    /// 씬에 없으면 자동 생성 보장.
    /// </summary>
    public static ItemSpawner Ensure()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<ItemSpawner>(FindObjectsInactive.Exclude);
        if (existing != null) return existing;
        var host = new GameObject("ItemSpawner");
        return host.AddComponent<ItemSpawner>();
    }
}
