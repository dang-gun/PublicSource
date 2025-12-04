using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles kill combos and on-screen combo popup near enemies.
/// - Increments combo when an enemy is killed.
/// - Resets combo only after consecutive misses reach threshold.
/// - Spawns ResourceController.ComboPrefab above-right of the enemy and sets its Text.
/// Ensures the combo popup renders behind the protractor by using a world-space canvas with lower sorting order.
/// </summary>
public class ComboController : MonoBehaviour
{
    public static ComboController Instance { get; private set; }

    [Tooltip("World offset applied to the popup from the enemy position.")]
    public Vector3 popupWorldOffset = new Vector3(GameConstants.ComboPopupOffsetX, GameConstants.ComboPopupOffsetY, 0f);

    [Header("Popup Scale")]
    [Tooltip("Scale multiplier applied to each spawned combo popup.")]
    public float popupScale = 2.0f;

    [Tooltip("Scale of the dedicated world-space canvas used for combo UI.")]
    public float worldCanvasScale = 0.20f;

    private int _combo;
    private int _maxCombo; // track highest combo reached during this run
    private ResourceController _rc;

    // Track active combo popup instances so we can clear them on jump
    private readonly List<GameObject> _activePopups = new List<GameObject>();

    // Dedicated world-space canvas used for combo popups so we can control sorting under the protractor
    private Canvas _comboWorldCanvas;

    private const int ProtractorUnderOffset = 10; // place combo this many orders under protractor top

    // --- New Miss Tracking ---
    /// <summary>
    /// 연속으로 적을 놓쳐 콤보를 끊기 위해 필요한 미스 횟수.
    /// </summary>
    public const int MissesToResetCombo = 2; // 요청사항:2명 연속 미스 시 초기화
    private int _consecutiveMisses; // 현재 연속 미스 횟수

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Cache ResourceController on MainScript if available
        var main = GameObject.Find("MainScript");
        if (main != null) _rc = main.GetComponent<ResourceController>();
    }

    public int CurrentCombo => _combo;
    public int MaxCombo => _maxCombo;
    public int ConsecutiveMisses => _consecutiveMisses; // 디버그/표시 용도 (선택 사용)

    public static ComboController Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("ComboController");
        return go.AddComponent<ComboController>();
    }

    public void AddKill(Transform enemy)
    {
        // 적 처치 시 미스 연속 카운터 리셋
        _consecutiveMisses = 0;

        _combo = Mathf.Max(0, _combo) + 1;
        _maxCombo = Mathf.Max(_maxCombo, _combo); // update maximum reached

        // 최대 콤보가 5의 배수에 도달할 때마다 피버 게이지 +5
        if (_maxCombo % 5 == 0)
        {
            try { FeverTimeModel.Instance.AddScore(5); } catch { }
        }

        // 콤보 누적 점수 규칙: 현재 콤보가3 이상일 때마다 +1
        if (_combo >= 3)
        {
            try { GameStats.IncrementComboScore(); } catch { }
        }

        if (true == GameConstants.DebugIs)
        {
            Debug.Log($"[ComboController] AddKill: Combo={_combo}, MaxCombo={_maxCombo}, ConsecutiveMisses={_consecutiveMisses}");
        }

        // Do not show a popup for combo =1
        if (_combo <= 1) return;
        ShowPopup(enemy, _combo);
    }

    /// <summary>
    /// 콤보를 끊기 위해 호출. 기본값(force=false) 으로 호출하면 "미스1회"로 처리하고
    /// MissesToResetCombo(2회) 이상 연속되었을 때 실제 콤보를0으로 초기화합니다.
    /// force=true 로 호출하면 즉시 콤보와 미스 카운터를 모두 초기화합니다 (게임오버 등 강제 상황).
    /// 기존 외부 호출 호환을 위해 선택적 매개변수 사용.
    /// </summary>
    public void BreakCombo(bool force = false)
    {
        if (force)
        {
            _combo = 0;
            _consecutiveMisses = 0;
            return;
        }


        // 미스1회 기록
        _consecutiveMisses++;

        // 미스가 나면 무조건 콤보2을 깍는다.
        _combo = _combo - 2;
        if (0 >= _combo)
        {
            // 콤보가0 이하가 되면0으로 고정
            _combo = 0;
        }



        if (_consecutiveMisses >= MissesToResetCombo)
        {
            _combo = 0;
            _consecutiveMisses = 0; // 리셋 후 카운터도 초기화
        }

        if (true == GameConstants.DebugIs)
        {
            Debug.Log($"[ComboController] BreakCombo({force}): Combo={_combo}, MaxCombo={_maxCombo}, ConsecutiveMisses={_consecutiveMisses}");
        }
    }

    // Remove all existing combo popups (called when ninja jumps)
    public void ClearPopups()
    {
        // 변경: 점프 시 강제 제거하지 않고, null 정리만 수행하여 남겨둡니다.
        _activePopups.RemoveAll(p => p == null);
    }

    private void ShowPopup(Transform enemy, int value)
    {
        if (_rc == null)
        {
            var main = GameObject.Find("MainScript");
            if (main != null) _rc = main.GetComponent<ResourceController>();
        }
        if (_rc == null || _rc.ComboPrefab == null || enemy == null) return;

        // Base position + configurable jitter
        Vector3 jitter = new Vector3(
            UnityEngine.Random.Range(-GameConstants.ComboPopupJitterXRange, GameConstants.ComboPopupJitterXRange),
            UnityEngine.Random.Range(-GameConstants.ComboPopupJitterYRange, GameConstants.ComboPopupJitterYRange),
            0f);
        Vector3 worldPos = enemy.position + popupWorldOffset + jitter;
        var go = Instantiate(_rc.ComboPrefab);
        go.name = "ComboPopup";

        // Pick background sprite by combo tiers (>=10 ? index0, >=20 ? index1, ..., >=90 ? index8)
        TryApplyComboBackground(go.transform, value);

        // Randomly rotate BackgroundImg child (if exists)
        var bgTf = FindChildRecursive(go.transform, "BackgroundImg");
        if (bgTf != null)
        {
            float z = UnityEngine.Random.Range(0f, 360f);
            var e = bgTf.localEulerAngles;
            e.z = z;
            bgTf.localEulerAngles = e;
        }

        // Track this instance
        _activePopups.RemoveAll(p => p == null);
        _activePopups.Add(go);

        // Link popup lifetime to the owning enemy so it gets destroyed when the enemy is removed
        var lifetimeLink = go.GetComponent<DestroyWithOwner>();
        if (lifetimeLink == null) lifetimeLink = go.AddComponent<DestroyWithOwner>();
        lifetimeLink.owner = enemy;

        // Reference renderer from enemy to align sorting behind the protractor
        var refRenderer = enemy.GetComponentInChildren<Renderer>();

        // Resolve target sorting layer and order under the protractor safely
        int targetLayer = (ProtractorController.CurrentSortingLayerId != 0)
            ? ProtractorController.CurrentSortingLayerId
            : (refRenderer != null ? refRenderer.sortingLayerID : 0);
        int protractorTop = ProtractorController.CurrentTopSortingOrder;
        int baseTopOrder;
        if (protractorTop != 0)
        {
            baseTopOrder = protractorTop;
        }
        else if (refRenderer != null)
        {
            baseTopOrder = Mathf.Max(1, refRenderer.sortingOrder + 1); // protractor usually enemy+1
        }
        else
        {
            baseTopOrder = 200; // conservative default matching protractor fallback
        }
        int targetOrder = baseTopOrder - ProtractorUnderOffset; // keep below arc and needle

        // If ComboPrefab has its own (nested) Canvas or BackgroundImg (RectTransform under Canvas), switch to world-space and force sorting
        var nestedCanvases = go.GetComponentsInChildren<Canvas>(true);
        if (nestedCanvases != null && nestedCanvases.Length > 0)
        {
            foreach (var c in nestedCanvases)
            {
                if (c == null) continue;
                c.renderMode = RenderMode.WorldSpace;
                c.worldCamera = Camera.main;
                c.overrideSorting = true;
                c.sortingLayerID = targetLayer;
                c.sortingOrder = targetOrder;
            }
            // Ensure root placed near enemy; child RectTransforms (e.g., BackgroundImg) keep layout
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, popupScale);
        }
        else
        {
            // No nested canvas: decide based on presence of UI Graphics (Image/Text with RectTransform)
            bool hasUI = go.GetComponentInChildren<Graphic>(true) != null;
            if (hasUI)
            {
                var canvas = EnsureWorldSpaceComboCanvas(refRenderer);
                canvas.overrideSorting = true;
                canvas.sortingLayerID = targetLayer;
                canvas.sortingOrder = targetOrder;
                go.transform.SetParent(canvas.transform, false);
                go.transform.position = worldPos;
                go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, popupScale);
            }
            else
            {
                // Sprite-based prefab: set sprite renderers sorting
                go.transform.position = worldPos;
                go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, popupScale);
                var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in srs)
                {
                    sr.sortingLayerID = targetLayer;
                    sr.sortingOrder = targetOrder;
                }
            }
        }

        // Set number text (content only)
        if (!TrySetUnityText(go, value))
        {
            TrySetTmpText(go, value);
        }
    }

    private void TryApplyComboBackground(Transform popupRoot, int combo)
    {
        if (popupRoot == null || _rc == null) return;
        // Require at least one sprite configured
        if (_rc.ComboImg == null || _rc.ComboImg.Count == 0) return;
        // Only start changing background from combo >=10
        if (combo < 10) return;
        // Compute desired tier index (0..8), then clamp to available sprite count
        int desiredIdx = (combo / 10) - 1; //10-19 =>0,20-29 =>1, ...
        int maxIdxByRule = 8;
        int maxIdxByAssets = _rc.ComboImg.Count - 1;
        int idx = Mathf.Clamp(desiredIdx, 0, Mathf.Min(maxIdxByRule, maxIdxByAssets));
        var sprite = _rc.ComboImg[idx];
        if (sprite == null) return;
        var bgTf = FindChildRecursive(popupRoot, "BackgroundImg");
        if (bgTf == null) return;
        // Prefer UI Image
        var ui = bgTf.GetComponent<Image>();
        if (ui != null)
        {
            ui.sprite = sprite;
            ui.enabled = true;
            return;
        }
        // Fallback to SpriteRenderer
        var sr = bgTf.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = sprite;
            sr.enabled = true;
        }
    }

    private Canvas EnsureWorldSpaceComboCanvas(Renderer refRenderer)
    {
        if (_comboWorldCanvas == null)
        {
            // Try find existing by name
            var found = GameObject.Find("ComboCanvas_World");
            if (found != null)
            {
                _comboWorldCanvas = found.GetComponent<Canvas>();
            }
            if (_comboWorldCanvas == null)
            {
                var go = new GameObject("ComboCanvas_World");
                _comboWorldCanvas = go.AddComponent<Canvas>();
                _comboWorldCanvas.renderMode = RenderMode.WorldSpace;
                _comboWorldCanvas.worldCamera = Camera.main;
                _comboWorldCanvas.planeDistance = 1f;
                // Set scale from inspector to control UI size in world units
                go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, worldCanvasScale);
                // Place under BackgroundPanel if exists to keep hierarchy tidy
                var bg = GameObject.Find("BackgroundPanel");
                if (bg != null) go.transform.SetParent(bg.transform, false);
            }
        }
        else
        {
            // Keep canvas scale updated from inspector if changed at runtime
            _comboWorldCanvas.transform.localScale = Vector3.one * Mathf.Max(0.0001f, worldCanvasScale);
        }
        // If protractor sorting is known, prefer it
        if (ProtractorController.CurrentTopSortingOrder != 0)
        {
            _comboWorldCanvas.overrideSorting = true;
            _comboWorldCanvas.sortingLayerID = ProtractorController.CurrentSortingLayerId;
            _comboWorldCanvas.sortingOrder = ProtractorController.CurrentTopSortingOrder - 1;
        }
        else if (refRenderer != null)
        {
            _comboWorldCanvas.overrideSorting = true;
            _comboWorldCanvas.sortingLayerID = refRenderer.sortingLayerID;
            _comboWorldCanvas.sortingOrder = Mathf.Max(int.MinValue + 10, refRenderer.sortingOrder - 1);
        }
        return _comboWorldCanvas;
    }

    private static Canvas[] GetAllCanvasesIncludingInactive()
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        // Includes inactive objects; safe alternative to obsolete GameObject.FindObjectsOfType
        return Resources.FindObjectsOfTypeAll<Canvas>();
#endif
    }

    private Canvas FindBestCanvas()
    {
        var canvases = GetAllCanvasesIncludingInactive();
        if (canvases == null || canvases.Length == 0) return null;
        foreach (var c in canvases)
        {
            if (c != null && c.isActiveAndEnabled) return c;
        }
        return canvases[0];
    }

    private bool TrySetUnityText(GameObject go, int value)
    {
        var text = go.GetComponentInChildren<Text>(true);
        if (text == null) return false;
        text.text = value.ToString();
        return true;
    }

    private bool TrySetTmpText(GameObject go, int value)
    {
        var comps = go.GetComponentsInChildren<Component>(true);
        if (comps == null || comps.Length == 0) return false;
        foreach (var comp in comps)
        {
            if (comp == null) continue;
            var t = comp.GetType();
            // Support TextMeshProUGUI and TextMeshPro (TMP_Text-based types)
            if (t.Name == "TextMeshProUGUI" || t.Name == "TextMeshPro" || (t.BaseType != null && t.BaseType.Name == "TMP_Text"))
            {
                var prop = t.GetProperty("text");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(comp, value.ToString(), null);
                    return true;
                }
            }
        }
        return false;
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t != null && t.name == name) return t;
        }
        return null;
    }
}

/// <summary>
/// Enemy 연동 파괴 컴포넌트: owner(적)가 Destroy되면 이 팝업도 함께 제거합니다.
/// </summary>
public class DestroyWithOwner : MonoBehaviour
{
    [Tooltip("파괴를 연동할 원본(적) Transform.")]
    public Transform owner;

    [Tooltip("원본 파괴 후 팝업 제거까지 지연 시간(초).0이면 즉시 제거")]
    public float delayAfterOwnerDestroyed = 0f;

    private bool _scheduled;

    void Update()
    {
        // Unity에서 Destroy된 대상은 프레임 이후 null로 평가됩니다.
        if (!_scheduled && owner == null)
        {
            _scheduled = true;
            if (delayAfterOwnerDestroyed <= 0f)
                Destroy(gameObject);
            else
                Destroy(gameObject, delayAfterOwnerDestroyed);
        }
    }
}
