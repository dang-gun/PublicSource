using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks a simple Style counter and updates the Style image in the UI.
/// </summary>
public class StyleCounter : MonoBehaviour
{
    public static StyleCounter Instance { get; private set; }

    [Tooltip("Current accumulated style count.")]
    public int Count;

    [Tooltip("Maximum style count reached during this run.")]
    [SerializeField]
    private int _maxCount;
    public int MaxCount => _maxCount;

    [Tooltip("Current accumulated style level. Increases by1 for each5 style count.")]
    public int Level;

    [Tooltip("Maximum style level reached during this run.")]
    [SerializeField]
    private int _maxLevel;
    public int MaxLevel => _maxLevel;

    private const int CountPerLevel = 5;

    // cached Style image UI
    private GameObject _styleImgGO;
    private Image _styleImg;

    // cached Style S+ text UI (StyleSTxt)
    private GameObject _styleSPlusTxtGO;
    private Text _styleSPlusText;

    // cached resources
    private ResourceController _rc;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        TryCacheStyleImgUI();
        TryCacheStyleSPlusTxtUI();
        UpdateLevel();
        _maxCount = Mathf.Max(_maxCount, Mathf.Max(0, Count));
        _maxLevel = Mathf.Max(_maxLevel, Mathf.Max(0, Level));
        RefreshUI();
    }

    public static StyleCounter Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("StyleCounter");
        return go.AddComponent<StyleCounter>();
    }

    public static void Increment()
    {
        var inst = Instance ?? Ensure();
        inst.Count = Mathf.Max(0, inst.Count) + 1;
        // if Count exceeds previous max, update and check fever trigger
        int prevMaxCount = inst._maxCount;
        inst._maxCount = Mathf.Max(inst._maxCount, inst.Count);
        inst.UpdateLevel();
        inst._maxLevel = Mathf.Max(inst._maxLevel, inst.Level);
        inst.RefreshUI();

        // 스타일 누적 점수 규칙: 스타일 레벨이2 이상이면 +1
        if (inst.Level >= 2)
        {
            try { GameStats.IncrementStyleScore(); } catch { }
        }

        // 최대 스타일 카운트가 5의 배수에 도달할 때마다 피버 게이지 +5
        // (새로운 최대치로 갱신된 경우에만 1회 가산)
        if (inst._maxCount != prevMaxCount && inst._maxCount > 0 && inst._maxCount % CountPerLevel == 0)
        {
            try { FeverTimeModel.Instance.AddScore(5); } catch { }
        }

        if (GameConstants.DebugIs)
        {
            Debug.Log($"StyleCounter Incremented: {inst.Count} (MaxCount={inst._maxCount}, Level={inst.Level}, MaxLevel={inst._maxLevel})");
        }
    }

    public static void Decrement()
    {
        var inst = Instance ?? Ensure();
        if (0 < inst.Count)
        {//스타일은 0미만이 될 수 없다.
            inst.Count -= 1;
        }

        inst.UpdateLevel();
        // maxs are not reduced on decrement
        inst.RefreshUI();


        if (GameConstants.DebugIs)
        {
            Debug.Log($"StyleCounter Decremented: {inst.Count}");
        }
    }

    public static void ResetToZero()
    {
        var inst = Instance ?? Ensure();
        inst.Count = 0;
        inst.UpdateLevel();
        inst.RefreshUI();
    }

    private void TryCacheStyleImgUI()
    {
        // Prefer exact hierarchy: UI > RealTimeInfo_Panel > StylePanel > StyleImg
        var uiRoot = GameObject.Find("UI");
        if (uiRoot != null)
        {
            var t = uiRoot.transform.Find("RealTimeInfo_Panel/StylePanel/StyleImg");
            if (t != null)
            {
                _styleImgGO = t.gameObject;
                _styleImg = _styleImgGO.GetComponent<Image>();
            }
        }
    }

    private void TryCacheStyleSPlusTxtUI()
    {
        // Path: UI > RealTimeInfo_Panel > StylePanel > StyleSTxt
        var uiRoot = GameObject.Find("UI");
        if (uiRoot != null)
        {
            var t = uiRoot.transform.Find("RealTimeInfo_Panel/StylePanel/StyleSTxt");
            if (t != null)
            {
                _styleSPlusTxtGO = t.gameObject;
                _styleSPlusText = t.GetComponent<Text>();
            }
        }
    }

    private ResourceController GetResourceController()
    {
        if (_rc != null) return _rc;
        var mainScript = GameObject.Find("MainScript");
        if (mainScript != null)
        {
            _rc = mainScript.GetComponent<ResourceController>();
        }
        return _rc;
    }

    private void RefreshUI()
    {
        if (_styleImgGO == null || _styleImg == null)
        {
            TryCacheStyleImgUI();
        }
        if (_styleSPlusTxtGO == null && _styleSPlusText == null)
        {
            TryCacheStyleSPlusTxtUI();
        }

        // Update style image and S+ text displays
        ApplyStyleImage(Level);
        ApplyStyleSPlusText(Level);
    }

    private void UpdateLevel()
    {
        // Level rises by1 per each5 counts; never below zero even if Count is negative
        Level = Mathf.Max(0, Count / CountPerLevel);
    }

    public static string GetLevelDisplayText(int level)
    {
        if (level <= 0) return string.Empty; //0레벨: 표시 없음
        switch (level)
        {
            case 1: return "D";
            case 2: return "C";
            case 3: return "B";
            case 4: return "A";
            case 5: return "S";
            case 6: return "SS";
            case 7: return "SSS";
            default:
                //8레벨 이상: S+(level-4)
                return "S+" + (level - 4).ToString();
        }
    }

    private void ApplyStyleImage(int level)
    {
        // Hide when game start or level <1
        if (_styleImgGO == null || _styleImg == null)
        {
            return;
        }

        if (level < 1)
        {
            if (_styleImgGO.activeSelf) _styleImgGO.SetActive(false);
            return;
        }

        var rc = GetResourceController();
        if (rc == null || rc.StyleImg == null || rc.StyleImg.Count == 0)
        {
            if (_styleImgGO.activeSelf) _styleImgGO.SetActive(false);
            return;
        }

        // Map level to index:1->0,2->1, ...,7->6, >=8 ->7
        int idx = (level >= 8) ? 7 : Mathf.Max(0, level - 1);
        // Clamp to available sprites to avoid OOR
        idx = Mathf.Clamp(idx, 0, rc.StyleImg.Count - 1);
        var sprite = rc.StyleImg[idx];
        _styleImg.sprite = sprite;
        if (!_styleImgGO.activeSelf) _styleImgGO.SetActive(true);
    }

    private void ApplyStyleSPlusText(int level)
    {
        // Show only when level >=8 as '+n', where n = level -4
        if (_styleSPlusTxtGO == null && _styleSPlusText == null)
        {
            return;
        }

        if (level >= 8)
        {
            int n = level - 4;
            string s = "+" + n.ToString();
            if (_styleSPlusText != null)
            {
                _styleSPlusText.text = s;
            }
            else if (_styleSPlusTxtGO != null)
            {
                var tmp = _styleSPlusTxtGO.GetComponent("TextMeshProUGUI");
                if (tmp != null)
                {
                    var t = tmp.GetType();
                    var prop = t.GetProperty("text");
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(tmp, s, null);
                    }
                }
            }
            if (_styleSPlusTxtGO != null && !_styleSPlusTxtGO.activeSelf)
            {
                _styleSPlusTxtGO.SetActive(true);
            }
        }
        else
        {
            // hide or clear when below8
            if (_styleSPlusText != null) _styleSPlusText.text = string.Empty;
            if (_styleSPlusTxtGO != null && _styleSPlusTxtGO.activeSelf) _styleSPlusTxtGO.SetActive(false);
        }
    }
}
