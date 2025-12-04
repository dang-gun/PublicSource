using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// HP/최대HP를 관리하고 Life UI를 업데이트하는 컨트롤러
/// </summary>
public class HealthController : MonoBehaviour
{
    [Header("Health")]
    [Tooltip("최대 HP")] public int MaxHP = 3;
    [Tooltip("현재 HP")] public int HP = 3;

    /// <summary>
    /// UI상 위치
    /// </summary>
    private Transform lifePanelOverride = null;

    /// <summary>
    /// 리소스 컨트롤러
    /// </summary>
    private ResourceController resourceController = null;

    private bool _initialized;

    private void Awake()
    {
        AutoFindResourceController();
        ClampValues();
    }

    private void Start()
    {
        // 씬 초기화 순서 문제를 피하기 위해 코루틴으로 재시도
        StartCoroutine(InitializeLifeUIRoutine());
    }

    private IEnumerator InitializeLifeUIRoutine()
    {
        const int maxTries = 30; // ~0.5초 (WaitForEndOfFrame 기준) 또는 더 길게 필요 시 증가
        int tries = 0;
        while (!_initialized && tries < maxTries)
        {
            if (EnsureDependenciesReady())
            {
                RefreshLifePanelUI();
                _initialized = true;
                yield break;
            }
            tries++;
            yield return null; // 다음 프레임까지 대기
        }
        if (!_initialized)
        {
            Debug.LogWarning("HealthController: LifePanel 초기화 실패 (경로 또는 리소스 없음).");
        }
    }

    private void AutoFindResourceController()
    {
        if (resourceController != null) return;
        var main = GameObject.Find("MainScript");
        if (main != null) resourceController = main.GetComponent<ResourceController>();
        if (resourceController == null)
        {
#if UNITY_2023_1_OR_NEWER
            resourceController = Object.FindFirstObjectByType<ResourceController>();
#else
            var arr = Resources.FindObjectsOfTypeAll<ResourceController>();
            resourceController = (arr != null && arr.Length > 0) ? arr[0] : null;
#endif
        }
    }

    private void ClampValues()
    {
        if (MaxHP < 0) MaxHP = 0;
        if (HP < 0) HP = 0;
        if (HP > MaxHP) HP = MaxHP;
    }

    private bool EnsureDependenciesReady()
    {
        if (resourceController == null) { AutoFindResourceController(); }
        if (resourceController == null) return false;
        if (resourceController.LifeOnePrefab == null) return false;
        if (GetLifePanel() == null) return false;
        return true;
    }

    /// <summary>
    /// 현재 설정된 MaxHP/HP 기준으로 LifePanel을 비우고 다시 채웁니다.
    /// </summary>
    public void RefreshLifePanelUI()
    {
        var lifePanel = GetLifePanel();
        if (lifePanel == null)
        {
            Debug.LogWarning("HealthController: LifePanel 찾기 실패");
            return;
        }
        if (resourceController == null)
        {
            Debug.LogWarning("HealthController: ResourceController 없음");
            return;
        }
        if (resourceController.LifeOnePrefab == null)
        {
            Debug.LogWarning("HealthController: LifeOnePrefab 미할당");
            return;
        }

        // 기존 자식 비우기
        for (int i = lifePanel.childCount - 1; i >= 0; i--)
        {
            var child = lifePanel.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        int clampedMax = Mathf.Max(0, MaxHP);
        int clampedHp = Mathf.Clamp(HP, 0, clampedMax);

        for (int i = 0; i < clampedMax; i++)
        {
            var go = Instantiate(resourceController.LifeOnePrefab, lifePanel, false);
            go.name = $"LifeOne_{i + 1}";
            var img = go.GetComponentInChildren<Image>();
            if (img != null)
            {
                if (i < clampedHp && resourceController.HeartFullSprite != null)
                {
                    img.sprite = resourceController.HeartFullSprite;
                }
                else if (resourceController.HeartEmptySprite != null)
                {
                    img.sprite = resourceController.HeartEmptySprite;
                }
            }
        }
    }

    /// <summary>
    /// 현재 HP를 변경하고 UI를 갱신합니다.
    /// </summary>
    public void SetHP(int value)
    {
        // Fever Time 동안에는 HP 감소를 무시한다 (바닥 착지 피해 방지 목적)
        // 기존 값보다 낮아지려는 경우에만 차단한다.
        if (value < HP && FeverTimeModel.Instance != null && FeverTimeModel.Instance.IsActive)
        {
            if (GameConstants.DebugIs)
            {
                Debug.Log("[Health] Fever active - HP decrease ignored. Current=" + HP + ", Attempt=" + value);
            }
            return;
        }

        HP = Mathf.Clamp(value, 0, Mathf.Max(0, MaxHP));
        RefreshLifePanelUI();
    }

    /// <summary>
    /// 최대 HP를 변경하고 현재 HP도 클램프한 후 UI를 갱신합니다.
    /// </summary>
    public void SetMaxHP(int value, bool alsoFillCurrentToMax = false)
    {
        MaxHP = Mathf.Max(0, value);
        if (alsoFillCurrentToMax)
            HP = MaxHP;
        else
            HP = Mathf.Clamp(HP, 0, MaxHP);
        RefreshLifePanelUI();
    }

    private Transform GetLifePanel()
    {
        if (lifePanelOverride != null) return lifePanelOverride;
        var ui = GameObject.Find("UI");
        if (ui == null) return null;
        return ui.transform.Find("CharacterInfoPanel/LifePanel");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampValues();
        // 에디터에서 패널이 이미 존재하면 즉시 미리보기 업데이트
        if (!Application.isPlaying && GetLifePanel() != null && resourceController != null && resourceController.LifeOnePrefab != null)
        {
            RefreshLifePanelUI();
        }
    }
#endif
}
