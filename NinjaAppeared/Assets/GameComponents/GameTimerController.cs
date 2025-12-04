using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 남은 시간을 관리하는 컨트롤러.
/// 기본 60초에서 시작하여 0초가 되면 HP를 1 깎고(체력이 남아있으면) 10초를 추가로 부여하여 계속 진행합니다.
/// HP 감소 후 HP가 0이 되면 게임오버를 실행합니다.
/// 게임오버가 되면 전체 게임 시간을 정지(Time.timeScale=0)합니다.
/// - UI 갱신: UI 루트 하위에서 이름이 "TimeTxt"인 텍스트(UGUI/Text 또는 TextMeshProUGUI)를 찾으면 남은 시간을 mm:ss 형식으로 표시.
/// - 게임오버 실행: 씬의 닌자에 붙은 ProtractorController에 SendMessage("ForceGameOverDebug")로 위임하거나 GameOverPanel 활성화.
/// </summary>
public class GameTimerController : MonoBehaviour
{
    public static GameTimerController Instance { get; private set; }

    [Tooltip("타이머 시작 초(기본 60초)")] public float startSeconds = 60f;
    [Tooltip("남은 시간(초)")] public float remainingSeconds;
    [Tooltip("(사용안함) 게임오버 시 타임스케일을 0으로 멈출지 여부 - 항상 정지되므로 의미 없음")] public bool pauseTimeScaleOnGameOver = false;

    // 외부 참조용 읽기 전용 프로퍼티 (대문자 이름 접근 대응)
    public float RemainingSeconds => remainingSeconds;

    // 별도로 누적 진행 시간을 기록 (게임 시작~게임오버까지 실제 경과 시간)
    private float _totalRunningSeconds;
    public float TotalRunningSeconds => _totalRunningSeconds;

    private bool _running;
    private bool _gameOverTriggered; // HP가 0이 되어 실제 게임오버가 발생했는지 여부
    private bool _handlingExpiration; // 만료 처리 중 재진입 방지

    // UI 캐시
    private GameObject _timeTxtGO;
    private Text _timeTxtUI; // uGUI Text

    void Awake()
    {
        // 싱글턴 보장 (중복 생성 방지 → 중복 호출 방지)
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        ResetTimer();
        TryCacheTimeTextUI();
        RefreshTimeText();
    }

    void OnEnable() { _running = true; }
    void OnDisable() { _running = false; }

    void Update()
    {
        if (!_running || _gameOverTriggered) return;

        // 별도 진행 시간 누적
        _totalRunningSeconds += Time.deltaTime;

        if (remainingSeconds > 0f)
        {
            remainingSeconds -= Time.deltaTime;
            if (remainingSeconds < 0f) remainingSeconds = 0f;
            RefreshTimeText();
        }

        // 시간이 다 되었을 때 처리 (게임오버가 아직 발생하지 않은 경우)
        if (remainingSeconds <= 0f)
        {
            HandleTimeExpired();
        }
    }

    /// <summary>
    /// 시간을 초기화하여 다시 시작.
    /// </summary>
    public void ResetTimer()
    {
        remainingSeconds = Mathf.Max(0f, startSeconds);
        _totalRunningSeconds = 0f;
        _gameOverTriggered = false;
        _handlingExpiration = false;
        _running = true;
        RefreshTimeText();
        // 게임 재시작 시 타임스케일 복구(다른 곳에서 이미 복구했을 수도 있으므로 안전하게 처리)
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    /// <summary>
    /// 외부에서 남은 시간을 추가합니다. (Clock 아이템 등)
    /// </summary>
    /// <param name="seconds">추가할 초(양수).</param>
    public void AddTime(int seconds)
    {
        if (seconds <= 0) return;
        if (!_running || _gameOverTriggered) return; // 종료 상태면 무시
        remainingSeconds += seconds;
        RefreshTimeText();
#if UNITY_EDITOR
        if (GameConstants.DebugIs)
        {
            Debug.Log($"[GameTimerController] Time +{seconds}s => {remainingSeconds:F1}s");
        }
#endif
    }

    /// <summary>
    /// 시간이 0이 되었을 때 HP를 1 감소 후 HP가 남아있으면 10초 연장, 아니면 게임오버.
    /// FeverTime 중 HP 감소는 HealthController.SetHP 규칙에 따라 무시될 수 있음.
    /// </summary>
    private void HandleTimeExpired()
    {
        // 이미 게임오버 또는 처리 중이면 무시
        if (_gameOverTriggered || _handlingExpiration) return;

        _handlingExpiration = true;
        try
        {
            var hc = GetHealthControllerSafe();
            if (hc != null && hc.HP > 0)
            {
                // HP 감소 시도 (FeverTime 중이면 무시될 수 있음)
                int before = hc.HP;
                hc.SetHP(hc.HP - 1);
                int after = hc.HP;

                Debug.Log($"[GameTimerController] Time expired. HealthController HP before: {before}, after: {after}");

                if (after <= 0)
                {
                    // HP가 0이 되어 게임오버
                    TriggerGameOver();
                    return;
                }
                else
                {
                    // HP가 남아 있으므로 10초 연장하고 계속 진행
                    remainingSeconds += 10f;
                    RefreshTimeText();
                    return;
                }
            }
            else
            {
                // HealthController가 없거나 이미 HP가 0 → 즉시 게임오버
                TriggerGameOver();
            }
        }
        finally
        {
            // 게임오버가 발생하지 않았고 시간을 연장했다면 다음 프레임부터 정상 업데이트되도록 플래그 해제
            if (!_gameOverTriggered)
            {
                _handlingExpiration = false;
            }
        }
    }

    private void TryCacheTimeTextUI()
    {
        var uiRoot = GameObject.Find("UI");
        if (uiRoot == null) return;

        // 1) 정확히 TimeTxt 이름 전체 탐색
        Transform target = null;
        var all = uiRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == "TimeTxt") { target = all[i]; break; }
        }
        // 2) 일반적으로 예상 경로들 추가 검색
        if (target == null)
        {
            var t = uiRoot.transform.Find("RealTimeInfo_Panel/TimePanel/TimeTxt");
            if (t != null) target = t;
        }
        if (target == null)
        {
            var t = uiRoot.transform.Find("RealTimeInfo_Panel/TimeTxt");
            if (t != null) target = t;
        }

        if (target != null)
        {
            _timeTxtGO = target.gameObject;
            _timeTxtUI = _timeTxtGO.GetComponent<Text>();
        }
    }

    private void RefreshTimeText()
    {
        if (_timeTxtGO == null && _timeTxtUI == null)
        {
            TryCacheTimeTextUI();
            if (_timeTxtGO == null && _timeTxtUI == null) return; // UI가 없어도 기능은 동작
        }

        string text = FormatTime(Mathf.CeilToInt(remainingSeconds));
        if (_timeTxtUI != null)
        {
            _timeTxtUI.text = text;
            return;
        }
        // TextMeshProUGUI 대응 (직접 참조 회피 후 리플렉션)
        var tmp = _timeTxtGO.GetComponent("TextMeshProUGUI");
        if (tmp != null)
        {
            var t = tmp.GetType();
            var prop = t.GetProperty("text");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(tmp, text, null);
            }
        }
    }

    private static string FormatTime(int seconds)
    {
        if (seconds < 0) seconds = 0;
        int m = seconds / 60;
        int s = seconds % 60;
        return string.Format("{0:0}:{1:00}", m, s);
    }

    private void TriggerGameOver()
    {
        _gameOverTriggered = true;

        // ProtractorController에게 게임오버 위임 (점수 집계 포함)
        var protractor = GetProtractorSafe();
        if (protractor != null)
        {
            protractor.gameObject.SendMessage("ForceGameOverDebug", SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            var panel = FindGameOverPanel();
            if (panel != null) panel.SetActive(true);
        }

        // 항상 타임 정지
        Time.timeScale = 0f;

        _running = false;
    }

    private static ProtractorController GetProtractorSafe()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<ProtractorController>();
#else
        var arr = Resources.FindObjectsOfTypeAll<ProtractorController>();
        return (arr != null && arr.Length > 0) ? arr[0] : null;
#endif
    }

    private static HealthController GetHealthControllerSafe()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<HealthController>();
#else
        var arr = Resources.FindObjectsOfTypeAll<HealthController>();
        return (arr != null && arr.Length > 0) ? arr[0] : null;
#endif
    }

    private static GameObject FindGameOverPanel()
    {
        var uiRoot = GameObject.Find("UI");
        Transform t = null;
        if (uiRoot != null)
        {
            t = uiRoot.transform.Find("GameOverPanel");
            if (t != null) return t.gameObject;
        }
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindInChildrenRecursive(root.transform, "GameOverPanel");
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindInChildrenRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent.gameObject;
        foreach (Transform child in parent)
        {
            var r = FindInChildrenRecursive(child, name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>
    /// UI(또는 MainScript) 루트에 이 컴포넌트를 보장.
    /// </summary>
    public static void EnsureOnDefaultPath()
    {
        // 이미 존재하면 그 인스턴스를 사용
        if (Instance != null)
        {
            if (!Instance.enabled) Instance.enabled = true;
            return;
        }

        // 씬에서 검색하여 있으면 사용
#if UNITY_2023_1_OR_NEWER
        var existing = Object.FindFirstObjectByType<GameTimerController>();
#else
        GameTimerController existing = null;
        var arr = Resources.FindObjectsOfTypeAll<GameTimerController>();
        if (arr != null && arr.Length > 0) existing = arr[0];
#endif
        if (existing != null)
        {
            Instance = existing;
            if (!Instance.enabled) Instance.enabled = true;
            return;
        }

        // 없으면 생성
        var uiRoot = GameObject.Find("UI");
        GameObject host = uiRoot != null ? uiRoot : GameObject.Find("MainScript");
        if (host == null)
        {
            host = new GameObject("GameTimerControllerHost");
        }
        var timer = host.GetComponent<GameTimerController>();
        if (timer == null) timer = host.AddComponent<GameTimerController>();
        if (!timer.enabled) timer.enabled = true;
        // Awake에서 Instance/Reset 처리 수행됨
    }
}
