using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Transform))]
public class ProtractorController : MonoBehaviour
{
    [Header("Appearance")]
    public float radius = 2f; // base radius, final size uses GameConstants.ProtractorScale
    [Range(8, 128)] public int segments = 36;
    /// <summary>
    /// 각도기 두께
    /// </summary>
    public float lineWidth = 0.05f;
    public Color arcColor = Color.yellow;
    public Color needleColor = Color.red;


    [Header("Layout")]
    /// <summary>
    /// 각도기 시각적 오프셋 (로컬 공간)
    /// </summary> 
    public Vector3 localOffset = new Vector3(0f, 0f, 0f);

    [Header("Input")]
    public KeyCode increaseKey = KeyCode.UpArrow;
    public KeyCode decreaseKey = KeyCode.DownArrow;

    /// <summary>
    /// degrees per second when key held (also used in auto mode)
    /// </summary>
    [Min(GameConstants.AutoProtractorAngleSpeedDegPerSec)]
    public float angleStepPerSecond = GameConstants.AutoProtractorAngleSpeedDegPerSec;

    [Header("State")]
    [Range(0f, GameConstants.ProtractorMaxAngleDegrees)] public float angleDegrees = 0f; //0..ProtractorMaxAngleDegrees
    private bool launched;
    // When ManualProtractorIs is false, oscillate angle automatically between0 and GameConstants.ProtractorMaxAngleDegrees
    private bool autoIncreasing = true;
    // Count edge hits at0 or max while aiming automatically; one full oscillation =2 bounces
    private int autoBounceCount = 0;

    [Header("Landing")]
    [Tooltip("Prevent falling below bottom of the camera view.")]
    public bool clampToScreenBottom = true;
    [Tooltip("Small offset above the bottom of the screen to rest on.")]
    public float bottomOffset = 0.02f;
    [Tooltip("Reset angle to0 when landed.")]
    public bool resetAngleOnLand = false;
    [Tooltip("Landing tolerance to avoid immediate re-landing after launch (world units).")]
    public float groundTolerance = 0.01f;

    [Header("World Ground")]
    [Tooltip("Use a world-space ground Y instead of camera bottom.")]
    public bool useWorldGround = false;
    public float groundY = 0f;

    [Header("UI")]
    [Tooltip("Optional reference to GameOverPanel in the scene. If not set, will be searched (even if inactive).")]
    public GameObject gameOverPanel;

    [Header("VFX")]
    [Tooltip("KnifeWork sprites sets provided by GameMainController for hit VFX.")]
    public List<KnifeWork_ResourcesDataModel> knifeWorkResources;
    public float knifeWorkFrameRate = 24f;
    [Tooltip("칼질 효과 회전 각도 범위(도). 최소~최대 사이에서 랜덤 적용")]
    public float knifeWorkRotationMin = 0f;
    public float knifeWorkRotationMax = 360f;

    [Header("Tilt")]
    [Tooltip("닌자가 멈춘 상태에서 높이에 따라 각도기를 아래로 기울입니다 (하단=0°, 상단=최대도).")]
    public bool tiltByHeight = true;
    [Range(0f, 90f)] public float tiltMaxDegrees = 90f;

    [Header("Sorting")]
    [Tooltip("Ensure the protractor renders above combo UI (combo uses 'Order in Layer'20). Arc will be >= this order; needle is +1.")]
    public int minSortingOrder = 21;

    [Header("Trajectory Preview")]
    [Tooltip("각도기에 따라 예상 점프 궤적(포물선)을 미리 그립니다.")]
    public bool showTrajectory = true;
    [Tooltip("궤적 샘플 개수 (최대 세그먼트 수)")]
    [Range(4, 256)] public int trajectorySegments = 40;
    [Tooltip("시뮬레이션 시간 간격(초). 세그먼트 * 이 값 만큼 최대 시간을 예측")]
    public float trajectoryTimeStep = 0.05f;
    [Tooltip("예측 궤적 최대 시간(초).0이면 세그먼트 기반")]
    public float trajectoryMaxTime = 0f;
    [Tooltip("궤적 라인 두께")] public float trajectoryLineWidth = 0.04f;
    public Color trajectoryColor = Color.white;
    [Tooltip("지면 충돌 시 샘플 정지 여부")] public bool trajectoryStopOnGround = true;
    [Tooltip("궤적이 그려질 때 Y 점프 높이만 표시 (X 진행 고정) 비활성화 시 정상 포물선")]
    public bool trajectoryVerticalOnly = false;

    [Header("Fever Time")] public FeverTimeModel feverTime; // Use global singleton instance
    private RectTransform feverBarRect; // Bar2 RectTransform
    private const float FeverBarMaxWidth = 182f; // UI 바 최대 폭

    private LineRenderer arcRenderer;
    private LineRenderer needleRenderer;
    private LineRenderer trajectoryRenderer;
    private LineRenderer groundLineRenderer; // new: shows ninja ground line
    private Rigidbody2D rb;

    // Distance UI
    private GameObject TraveledTextGO;
    private Text uiText;
    private Vector3 launchStartPos;
    private bool hasLaunchStart;
    /// <summary>
    /// 누적 이동 거리(m)
    /// </summary>
    private float cumulativeMeters = 0f;
    // 이번 점프(발사) 동안 도달한 최대 X (오른쪽으로만 진행하므로 감소 방지용)
    private float launchMaxX;
    // Fever distance chunk tracking (10m per fever point)
    private int feverDistanceChunkAwarded = 0; // floor(cumulativeDistance/10) already converted to fever points

    // Kill UI
    private GameObject killTextGO;
    private Text killUIFont;
    private int killCount = 0;

    // Expose current sorting so other systems (e.g., ComboController) can render below the protractor
    public static int CurrentSortingLayerId { get; private set; }
    public static int CurrentTopSortingOrder { get; private set; }

    void Awake()
    {
        // Ensure Rigidbody2D exists and is prepped for launch (no gravity before launch)
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic; // do not simulate gravity before launch
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // never allow rotation
        rb.SetRotation(0f);
        transform.rotation = Quaternion.identity;

        // Create child GameObjects for the renderers
        var arcObj = new GameObject("ProtractorArc");
        arcObj.transform.SetParent(transform, false);
        arcObj.transform.localPosition = localOffset;
        arcRenderer = arcObj.AddComponent<LineRenderer>();

        var needleObj = new GameObject("ProtractorNeedle");
        needleObj.transform.SetParent(transform, false);
        needleObj.transform.localPosition = localOffset;
        needleRenderer = needleObj.AddComponent<LineRenderer>();

        // Trajectory preview object
        var trajObj = new GameObject("ProtractorTrajectory");
        trajObj.transform.SetParent(transform, false);
        // 이전에는 localOffset을 적용했으나, 닌자 위치 기준으로 바로 시작하도록0으로 설정
        trajObj.transform.localPosition = Vector3.zero;
        trajectoryRenderer = trajObj.AddComponent<LineRenderer>();
        SetupLineRenderer(trajectoryRenderer, trajectoryColor);
        trajectoryRenderer.startWidth = trajectoryRenderer.endWidth = trajectoryLineWidth;
        // 궤적은 월드 좌표로 직접 계산하여 위치 문제 해결
        trajectoryRenderer.useWorldSpace = true;
        trajectoryRenderer.enabled = showTrajectory; // 시작 시 표시 상태 반영

        // Ground line renderer (world space)
        var groundObj = new GameObject("ProtractorGroundLine");
        groundObj.transform.SetParent(transform, false);
        groundObj.transform.localPosition = Vector3.zero;
        groundLineRenderer = groundObj.AddComponent<LineRenderer>();
        SetupLineRenderer(groundLineRenderer, Color.blue); // changed to blue
        groundLineRenderer.useWorldSpace = true;
        groundLineRenderer.startWidth = groundLineRenderer.endWidth = trajectoryLineWidth; // use trajectory thickness
        groundLineRenderer.enabled = GameConstants.ProtractorShowGroundLine;

        // Basic LineRenderer configuration
        SetupLineRenderer(arcRenderer, arcColor);
        SetupLineRenderer(needleRenderer, needleColor);

        // Make visuals keep constant on-screen/world size regardless of parent's scale
        UpdateProtractorTransform();

        RebuildArc();
        UpdateNeedle();
        UpdateTrajectory();
        RefreshSorting(); // 정렬 우선 적용해 가려지지 않도록

        // Init UIs
        InitializeTraveledUI();
        SetDistanceText($"{cumulativeMeters:0} m");
        InitializeKillUI();
        SetKillText(killCount);

        // Use the global FeverTime model
        feverTime = FeverTimeModel.Instance;
        // Subscribe only to width updates for local bar if needed
        feverTime.OnScoreChanged += UpdateFeverBarWidth;
        TryCacheFeverBar();
        UpdateFeverBarWidth(feverTime.CurrentScore, FeverTimeModel.MaxScore);
    }

    void OnValidate()
    {
        // Keep settings in sensible ranges in editor changes
        radius = Mathf.Max(0.01f, radius);
        segments = Mathf.Clamp(segments, 8, 256);
        lineWidth = Mathf.Max(0.001f, lineWidth);
        bottomOffset = Mathf.Max(0f, bottomOffset);
        groundTolerance = Mathf.Max(0f, groundTolerance);
        knifeWorkFrameRate = Mathf.Max(1f, knifeWorkFrameRate);
        // Ensure the minimum auto speed is not below the base constant
        angleStepPerSecond = Mathf.Max(angleStepPerSecond, GameConstants.AutoProtractorAngleSpeedDegPerSec);

        if (arcRenderer != null)
        {
            arcRenderer.startWidth = arcRenderer.endWidth = lineWidth;
            RebuildArc();
        }
        if (needleRenderer != null)
        {
            needleRenderer.startWidth = needleRenderer.endWidth = lineWidth;
            UpdateNeedle();
        }
        if (trajectoryRenderer != null)
        {
            trajectoryLineWidth = Mathf.Max(0.001f, trajectoryLineWidth);
            trajectorySegments = Mathf.Clamp(trajectorySegments, 4, 512);
            trajectoryRenderer.startWidth = trajectoryRenderer.endWidth = trajectoryLineWidth;
            UpdateTrajectory();
        }

        // Ensure transform compensation also updates in editor
        UpdateProtractorTransform();
    }

    void Update()
    {
        // Debug: Force Game Over on F11 when DebugIs enabled
        if (GameConstants.DebugIs && Input.GetKeyDown(KeyCode.F11))
        {
            ForceGameOverDebug();
            return;
        }

        // Maintain constant size/offset even if parent scale changes at runtime
        UpdateProtractorTransform();

        if (launched)
        {
            // Update distance UI while flying (누적 + 현재 비행거리)
            if (hasLaunchStart)
            {
                UpdateDistanceDuringFlight();
            }

            if (useWorldGround)
            {
                ClampToWorldGroundAndStopIfNeeded();
            }
            else if (clampToScreenBottom)
            {
                ClampToBottomAndStopIfNeeded();
            }
            return; // no input or drawing updates after launch
        }

        HandleInput();
        UpdateNeedle();
        UpdateTrajectory();
        UpdateGroundLine(); // <-- Update the ground line in regular mode

        // Launch on Space, left mouse click, or first touch begin
        bool launchKey = Input.GetKeyDown(KeyCode.Space);
        bool launchClick = Input.GetMouseButtonDown(0);
        bool launchTouch = TouchLaunchTriggered();
        if (launchKey || (launchClick && !IsPointerOverUI()) || launchTouch)
        {
            Launch();
        }
        // Fever timer is updated centrally by FeverTimeUIController



        //임시 테스트용
        //if (Input.GetKeyDown(KeyCode.F4))
        //{
        //    //FeverTimeModel.Instance.AddScore(99);
        //    FeverTimeModel.Instance.AddScore(100);
        //}
    }

    // 모바일 터치 시작을 Space 입력과 동일하게 처리
    private bool TouchLaunchTriggered()
    {
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                // UI 위에서 시작된 터치는 무시
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                    return false;
                return true;
            }
        }
        return false;
    }

    void FixedUpdate()
    {
        // Enforce non-rotating ninja every physics step
        if (rb != null)
        {
            rb.angularVelocity = 0f;
            rb.SetRotation(0f);
        }
    }

    public void RefreshSorting()
    {
        // Try to match the parent's renderer sorting so the protractor draws above the ninja
        var parentRenderer = GetComponentInChildren<SpriteRenderer>() as Renderer;
        if (parentRenderer == null)
        {
            parentRenderer = GetComponentInChildren<Renderer>();
        }
        int layerId = 0;
        int order = 200; // default high
        if (parentRenderer != null)
        {
            layerId = parentRenderer.sortingLayerID;
            order = parentRenderer.sortingOrder + 1;
        }

        // Enforce minimum so we are always above combo (Order in Layer20)
        order = Mathf.Max(order, minSortingOrder);

        if (arcRenderer != null)
        {
            arcRenderer.sortingLayerID = layerId;
            arcRenderer.sortingOrder = order;
        }
        if (needleRenderer != null)
        {
            needleRenderer.sortingLayerID = layerId;
            needleRenderer.sortingOrder = order + 1;
        }
        if (trajectoryRenderer != null)
        {
            trajectoryRenderer.sortingLayerID = layerId;
            // draw below arc but above other lower UI: arc is 'order', so subtract1 but keep >= minSortingOrder -1
            int trajOrder = Mathf.Max(order - 1, minSortingOrder - 1);
            trajectoryRenderer.sortingOrder = trajOrder;
        }
        if (groundLineRenderer != null)
        {
            groundLineRenderer.sortingLayerID = layerId;
            // draw below arc as background guide
            int gOrder = Mathf.Max(order - 1, minSortingOrder - 1);
            groundLineRenderer.sortingOrder = gOrder;
        }

        // Publish current layer/order so other systems can render below the protractor
        CurrentSortingLayerId = layerId;
        // Needle is drawn on top
        CurrentTopSortingOrder = (needleRenderer != null) ? needleRenderer.sortingOrder : order + 1;
    }

    private void HandleInput()
    {
        // If manual control is disabled, automatically oscillate the angle between0 and max degrees.
        if (!GameConstants.ManualProtractorIs)
        {
            float before = angleDegrees;
            // Base speed plus style-based acceleration
            float style = (StyleCounter.Instance != null) ? Mathf.Max(0, StyleCounter.Instance.Count) : 0;
            float extraStyle = Mathf.Max(0, style - GameConstants.AutoProtractorBaseOnlyUpToStyleCount); // style0..N ->0.. when style <= threshold
                                                                                                         // Enforce minimum base speed not lower than GameConstants.AutoProtractorAngleSpeedDegPerSec
            float baseSpeed = Mathf.Max(angleStepPerSecond, GameConstants.AutoProtractorAngleSpeedDegPerSec);
            float speed = baseSpeed + GameConstants.AutoProtractorSpeedPerStyle_AddDegPerSec * extraStyle;
            float delta = speed * Time.deltaTime * (autoIncreasing ? 1f : -1f);
            angleDegrees += delta;
            if (angleDegrees >= GameConstants.ProtractorMaxAngleDegrees)
            {
                angleDegrees = GameConstants.ProtractorMaxAngleDegrees;
                // count a bounce when hitting the boundary
                if (before < GameConstants.ProtractorMaxAngleDegrees)
                {
                    autoBounceCount++;
                    int needed = GameConstants.AutoProtractorFullOscillationBounceCount;
                    if (needed > 0 && (autoBounceCount % needed) == 0)
                    {
                        StyleCounter.Decrement(); // every4 bounces (2 round trips) => -1
                                                  //Debug.Log($"[스타일 카운터1] 자동 각도기 최대 도달로 스타일 -1 (누적 바운스: {autoBounceCount})");
                    }
                }
                autoIncreasing = false;
            }
            else if (angleDegrees <= 0f)
            {
                angleDegrees = 0f;
                if (before > 0f)
                {
                    autoBounceCount++;
                    int needed = GameConstants.AutoProtractorFullOscillationBounceCount;
                    if (needed > 0 && (autoBounceCount % needed) == 0)
                    {
                        StyleCounter.Decrement(); // every4 bounces (2 round trips) => -1
                                                  //Debug.Log($"[스타일 카운터2] 자동 각도기 최대 도달로 스타일 -1 (누적 바운스: {autoBounceCount})");
                    }
                }
                autoIncreasing = true;
            }

            return; // block arrow key control
        }

        float d = 0f;
        if (Input.GetKey(increaseKey)) d += angleStepPerSecond * Time.deltaTime;
        if (Input.GetKey(decreaseKey)) d -= angleStepPerSecond * Time.deltaTime;
        if (Mathf.Approximately(d, 0f)) return;

        angleDegrees = Mathf.Clamp(angleDegrees + d, 0f, GameConstants.ProtractorMaxAngleDegrees);
    }

    private void Launch()
    {
        // Clear any existing combo popups when ninja launches (jumps)
        var combo = GetComboControllerSafe();
        if (combo != null)
        {
            combo.ClearPopups();
        }

        // Style rule: if auto mode was active and less than2 full oscillations (i.e., <4 bounces) happened, increment StyleCounter
        if (!GameConstants.ManualProtractorIs)
        {
            int needed = GameConstants.AutoProtractorFullOscillationBounceCount;
            if (autoBounceCount < needed)
            {
                StyleCounter.Increment();
            }
            // 두 바운스가 필요해도 자동 모드에서 스위치 시 즉시 적용 않도록 지연
            //else if (autoBounceCount == needed)
            //{
            // StyleCounter.Decrement(); // 풀 오실레이션 도달 시 스타일 -1
            //}
        }

        // Compute world launch direction based on visual tilt
        float launchTilt = tiltByHeight ? ComputeTiltDegrees() : 0f; // when launching, ninja is not yet marked as launched
        float worldAngle = angleDegrees - launchTilt; // visuals rotated by -tilt, so subtract to get world angle

        // Direction from angle (0 deg = +X,90 deg = +Y) after applying tilt
        float rad = worldAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;

        // Switch to dynamic physics with gravity and set initial velocity
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1f;
        rb.angularVelocity = 0f;
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // keep frozen even during flight
        rb.SetRotation(0f);

        rb.linearVelocity = dir * GameConstants.NinjaLaunchForce;

        launched = true;

        // mark start position for measuring distance
        launchStartPos = transform.position;
        hasLaunchStart = true;
        // 이번 발사 중 최대 X 초기화
        launchMaxX = launchStartPos.x;
        // keep showing current cumulative, not reset to0
        SetDistanceText($"{cumulativeMeters:0} m");

        // Hide protractor visuals after launch
        if (arcRenderer != null) arcRenderer.enabled = false;
        if (needleRenderer != null) needleRenderer.enabled = false;
        if (trajectoryRenderer != null) trajectoryRenderer.enabled = false;
    }

    // --- Enemy collision handling ---
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null) return;
        // 먼저 같은 객체에서 찾고, 없으면 부모에서 찾는다 (구조 변경 대응)
        var enemy = collision.collider.GetComponent<EnemyControler>();
        if (enemy == null)
        {
            enemy = collision.collider.GetComponentInParent<EnemyControler>();
        }
        if (enemy != null)
        {
            HandleHitEnemy(enemy);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        // 먼저 같은 객체에서 찾고, 없으면 부모에서 찾는다 (구조 변경 대응)
        var enemy = other.GetComponent<EnemyControler>();
        if (enemy == null)
        {
            enemy = other.GetComponentInParent<EnemyControler>();
        }
        if (enemy != null)
        {
            HandleHitEnemy(enemy);
        }
    }

    private void HandleHitEnemy(EnemyControler enemy)
    {
        // 이미 죽은 적이면 처리하지 않음
        if (enemy == null) return;
        if (enemy.IsDead) return;
        var rootState = enemy.GetComponentInParent<EnemyRootState>();
        if (rootState != null && rootState.IsDead) return;

        // Play knife work VFX at enemy position with sorting above the enemy
        TryPlayKnifeWorkVFX(enemy);

        // Mark enemy as dead (apply grayscale)
        if (enemy != null)
        {
            enemy.MarkDead();
        }

        // Disable enemy BoxCollider2D to prevent further hits
        DisableEnemyBoxColliders(enemy);

        // Stop movement immediately and freeze at current spot
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.SetRotation(0f);
        transform.rotation = Quaternion.identity;

        // Count kill and update UI
        killCount++;
        feverTime.AddScore(feverTime.ScorePerKill); // 적 처치 기여
        SetKillText(killCount);

        // Combo: increment and show popup using ComboController
        var combo = GetComboControllerSafe();
        if (combo == null)
        {
            // lazily ensure one exists so feature works without setup
            var goCombo = new GameObject("ComboController");
            combo = goCombo.AddComponent<ComboController>();
        }
        combo.AddKill(enemy.transform);

        // Camera shake level based on combo/style rules
        //1) combo: +1 per10 combo (max5)
        //2) style: +1 per5 style (max5)
        int comboVal = (combo != null) ? Mathf.Max(0, combo.CurrentCombo) : 0;
        int styleVal = (StyleCounter.Instance != null) ? Mathf.Max(0, StyleCounter.Instance.Count) : 0;
        int levelFromCombo = Mathf.Clamp(comboVal / 10, 0, 5);
        int levelFromStyle = Mathf.Clamp(styleVal / 5, 0, 5);
        int sum = levelFromCombo + levelFromStyle;
        int appliedLevel = (sum <= 0) ? 0 : Mathf.Clamp(sum, 1, 10);
        //적을 죽이면 약한 진동이라도 있게 하려고 단계를10까지 늘림
        //그에 따라서 최종 값에 +1해줌
        appliedLevel = appliedLevel + 1;

        // 디버그 로그: 두 값과 합계/적용레벨 표시 (합이0이면 적용레벨0)
        if (GameConstants.DebugIs)
        {
            Debug.Log($"[카메라 흔들림] 콤보 기여: {levelFromCombo} (콤보 {comboVal}), 스타일 기여: {levelFromStyle} (스타일 {styleVal}), 합계: {sum}, 적용레벨(+1): {appliedLevel}");
        }
        // 합이0이면 흔들림 없음
        if (appliedLevel > 0)
        {
            CameraShaker.ShakeGlobal(appliedLevel);
        }

        // 현재 점프의 진행 거리 누적(적 처치로 정지된 경우에도 누적)
        if (hasLaunchStart)
        {
            // 오른쪽으로만 진행한다고 가정 → 발사 후 도달한 최대 X를 기준으로 계산
            launchMaxX = Mathf.Max(launchMaxX, transform.position.x);
            float dx = Mathf.Max(0f, launchMaxX - launchStartPos.x);
            cumulativeMeters += dx * GameConstants.MetersPerUnit;
            hasLaunchStart = false;
            SetDistanceText($"{cumulativeMeters:0} m");
            AwardFeverDistance(cumulativeMeters);
        }

        // End current launch and allow aiming again
        launched = false;
        hasLaunchStart = false; // stop distance accumulation mid-air

        // --- Added: reset autoBounceCount when enemy is killed so next aim starts fresh ---
        autoBounceCount = 0;
        if (GameConstants.DebugIs)
        {
            Debug.Log($"[스타일 카운터] 적 처치로 자동 각도기 바운스 카운트 초기화 (누적 바운스: {autoBounceCount})");
        }

        ShowProtractor();
        if (trajectoryRenderer != null) trajectoryRenderer.enabled = showTrajectory; // re-enable after hit
    }

    private void DisableEnemyBoxColliders(EnemyControler enemy)
    {
        if (enemy == null) return;
        var boxes = enemy.GetComponentsInChildren<BoxCollider2D>(true);
        foreach (var box in boxes)
        {
            if (box != null) box.enabled = false;
        }
    }

    private void ClampToWorldGroundAndStopIfNeeded()
    {
        float groundLine = groundY + bottomOffset;

        // Don't land if still moving upward (prevents immediate re-landing)
        if (rb != null && rb.linearVelocity.y > 0f) return;

        // Use collider bottom offset from center
        float bottomOffsetFromCenter = GetBottomOffsetFromCenter();
        float ninjaBottomY = transform.position.y - bottomOffsetFromCenter;
        if (ninjaBottomY <= groundLine + groundTolerance)
        {
            Vector3 pos = transform.position;
            pos.y = groundLine + bottomOffsetFromCenter;
            transform.position = pos;

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.SetRotation(0f);

            HandleLanding();
        }
    }

    private void ClampToBottomAndStopIfNeeded()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Don't land if still moving upward (prevents immediate re-landing)
        if (rb != null && rb.linearVelocity.y > 0f) return;

        // Compute world Y for the bottom of the view at ninja's Z depth
        float distance = transform.position.z - cam.transform.position.z;
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        float bottomY = bottomLeft.y + bottomOffset;

        // Use collider bottom offset from center
        float bottomOffsetFromCenter = GetBottomOffsetFromCenter();
        float ninjaBottomY = transform.position.y - bottomOffsetFromCenter;
        if (ninjaBottomY <= bottomY + groundTolerance)
        {
            // Place ninja so its collider bottom sits on bottomY
            Vector3 pos = transform.position;
            pos.y = bottomY + bottomOffsetFromCenter;
            transform.position = pos;

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.SetRotation(0f);

            HandleLanding();
        }
    }

    private void HandleLanding()
    {
        if (!launched) return;

        // finalize distance on landing as well
        if (hasLaunchStart) // fixed variable name (was hasLaunchStarted)
        {
            float dx;
            launchMaxX = Mathf.Max(launchMaxX, transform.position.x);
            dx = Mathf.Max(0f, launchMaxX - launchStartPos.x);
            cumulativeMeters += dx * GameConstants.MetersPerUnit;
            hasLaunchStart = false;
            SetDistanceText($"{cumulativeMeters:0} m");
            AwardFeverDistance(cumulativeMeters);
        }

        // Consume 1 HP on landing; open game over only when HP is now 0
        var hc = GetHealthControllerSafe();
        if (hc != null)
        {
            if (hc.HP > 0)
            {
                hc.SetHP(hc.HP - 1);
                //HP가 줄어드는 상황이면 콤보도 끊는다.
                ComboController.Instance.BreakCombo(true);
            }
            if (hc.HP <= 0)
            {
                ActivateGameOverPanel();
                if (arcRenderer != null) arcRenderer.enabled = false;
                if (needleRenderer != null) needleRenderer.enabled = false;
                launched = false;
                this.enabled = false;
                return;
            }
        }

        // Optionally reset angle
        if (resetAngleOnLand)
        {
            angleDegrees = 0f;
        }

        launched = false; // allow input again
                          // reset auto oscillation progress for next aim
        autoBounceCount = 0;
        //Debug.Log($"[스타일 카운터] 착지로 자동 각도기 바운스 카운트 초기화 (누적 바운스: {autoBounceCount})");

        // Show protractor again for next aim
        ShowProtractor();
        if (trajectoryRenderer != null) trajectoryRenderer.enabled = showTrajectory; // show again after landing
    }

    private void ShowProtractor()
    {
        if (arcRenderer != null) arcRenderer.enabled = true;
        if (needleRenderer != null) needleRenderer.enabled = true;
        if (trajectoryRenderer != null) trajectoryRenderer.enabled = showTrajectory;
        RefreshSorting();
        UpdateNeedle();
        UpdateTrajectory();
    }

    private void SetupLineRenderer(LineRenderer lr, Color color)
    {
        lr.useWorldSpace = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.positionCount = 0;
        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = lineWidth;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = minSortingOrder; // baseline; RefreshSorting will enforce minimum and layer
    }

    private void RebuildArc()
    {
        if (arcRenderer == null) return;
        int points = segments + 1;
        arcRenderer.positionCount = points;
        float effRadius = radius * GameConstants.ProtractorScale; // 적용된 최종 반지름
                                                                  // 홀은 프로트랙터 스케일과 함께 동기화하여 직관적인 비율 유지
        float hole = Mathf.Max(0f, GameConstants.ProtractorInnerHoleRadius) * GameConstants.ProtractorScale;
        // 홀 크기가 반지름 이상이면 전체 호를 숨김
        if (hole >= effRadius - 1e-4f)
        {
            // hide arc entirely when hole covers it
            arcRenderer.positionCount = 0;
            return;
        }
        float holeSqr = hole * hole;
        bool useHole = hole > 0f;
        for (int i = 0; i < points; i++)
        {
            float t = i / (float)segments; //0..1
            float deg = GameConstants.ProtractorMaxAngleDegrees * t;
            float rad = deg * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * effRadius;
            float y = Mathf.Sin(rad) * effRadius;
            Vector3 p = new Vector3(x, y, 0f);
            if (useHole)
            {
                float r2 = x * x + y * y;
                if (r2 < holeSqr)
                {
                    // Clamp to hole boundary so interior appears transparent (gap from center)
                    float r = Mathf.Sqrt(r2);
                    if (r > 1e-4f)
                    {
                        float scale = hole / r;
                        p.x *= scale;
                        p.y *= scale;
                    }
                    else
                    {
                        p = new Vector3(hole, 0f, 0f); // arbitrary on boundary if exactly center
                    }
                }
            }
            arcRenderer.SetPosition(i, p);
        }
    }

    private void UpdateNeedle()
    {
        if (needleRenderer == null) return;
        if (needleRenderer.positionCount != 2) needleRenderer.positionCount = 2;

        float rad = angleDegrees * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);

        // Hole radius that clips the inner part (scale with protractor)
        float holeWorld = Mathf.Max(0f, GameConstants.ProtractorInnerHoleRadius) * GameConstants.ProtractorScale;
        float minVisible = Mathf.Max(0f, GameConstants.ProtractorNeedleMinVisibleLength);
        float effRadius = radius * GameConstants.ProtractorScale; // 적용된 최종 반지름

        // 이전에는 hole >= effRadius 시 바늘을 숨겼으나, 최소 가시 길이를 강제하기 위해 항상 표시
        // 바늘 시작점은 홀 경계 밖에서 시작 (holeWorld). 너무 크면 화면 밖으로갈 수 있어 제한 옵션를 둘 수 있으나 현재 요구는 항상 보이도록.
        // 시작 거리: holeWorld
        // 끝점은 최소 가시 길이 보장
        float startDist = holeWorld;
        float endDist = startDist + minVisible;

        // 시각적으로 과도하게 멀어지는 것을 방지하기 위해 상한(효과 반지름 + 최소길이) 선택적 적용
        // effRadius가 hole보다 작은 경우에도 바늘은 hole 밖에서부터 연장되어 보임
        // 필요시 조정: endDist = Mathf.Max(endDist, effRadius); (현재는 명시적 최소길이 우선)

        Vector3 start = dir * startDist;
        Vector3 end = dir * endDist;

        // Keep renderer enabled; we always show at least minVisible length
        if (!needleRenderer.enabled) needleRenderer.enabled = true;
        needleRenderer.SetPosition(0, start);
        needleRenderer.SetPosition(1, end);
    }

    private void UpdateProtractorTransform()
    {
        if (arcRenderer == null && needleRenderer == null && trajectoryRenderer == null) return;
        var parentLossy = transform.lossyScale;
        Vector3 inv = new Vector3(SafeInv(parentLossy.x), SafeInv(parentLossy.y), SafeInv(parentLossy.z));
        float tilt = (!launched && tiltByHeight) ? ComputeTiltDegrees() : 0f;
        Quaternion rot = Quaternion.Euler(0f, 0f, -tilt);
        if (arcRenderer != null)
        {
            var t = arcRenderer.transform;
            t.localScale = inv; // 반지름 자체에 ProtractorScale을 적용하므로 여기서는 그대로 유지
            t.localRotation = rot;
            t.localPosition = new Vector3(localOffset.x * inv.x, localOffset.y * inv.y, localOffset.z * inv.z);
        }
        if (needleRenderer != null)
        {
            var t = needleRenderer.transform;
            t.localScale = inv; // 동일
            t.localRotation = rot;
            t.localPosition = new Vector3(localOffset.x * inv.x, localOffset.y * inv.y, localOffset.z * inv.z);
        }
        // trajectoryRenderer는 worldSpace이므로 스케일/로테이션/포지션을 보정하지 않음
    }

    private float ComputeTiltDegrees()
    {
        var cam = Camera.main;
        if (cam == null) return 0f;
        //0(bottom) ..1(top) in viewport space
        float vy = cam.WorldToViewportPoint(transform.position).y;
        float t = Mathf.Clamp01(vy);
        return t * tiltMaxDegrees;
    }

    private float SafeInv(float v)
    {
        if (Mathf.Approximately(v, 0f)) return 1f; // avoid divide-by-zero; assume1
        return 1f / v;
    }

    private static ComboController GetComboControllerSafe()
    {
        if (ComboController.Instance != null) return ComboController.Instance;
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<ComboController>();
#else
 var arr = Resources.FindObjectsOfTypeAll<ComboController>();
 return (arr != null && arr.Length >0) ? arr[0] : null;
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

    // Returns the ground line Y (the Y where ninja bottom sits after landing)
    private float GetGroundLineY()
    {
        if (useWorldGround)
        {
            return groundY + bottomOffset; // same as ClampToWorldGroundAndStopIfNeeded groundLine
        }
        if (clampToScreenBottom)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                float distance = transform.position.z - cam.transform.position.z;
                Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
                return bottomLeft.y + bottomOffset; // same as ClampToBottomAndStopIfNeeded bottomY
            }
        }
        // Fallback very low line to avoid premature stop
        return transform.position.y - 100f;
    }

    private void UpdateGroundLine()
    {
        if (groundLineRenderer == null) return;
        groundLineRenderer.enabled = GameConstants.ProtractorShowGroundLine;
        if (!GameConstants.ProtractorShowGroundLine) return;
        if (groundLineRenderer.positionCount != 2) groundLineRenderer.positionCount = 2;
        float y = GetGroundLineY(); // draw at the actual ground line the ninja uses (bottom)
                                    // Draw a wide line across the screen width at the ninja's Z depth
        var cam = Camera.main;
        if (cam == null)
        {
            // fallback small segment around ninja
            Vector3 a = new Vector3(transform.position.x - 5f, y, transform.position.z);
            Vector3 b = new Vector3(transform.position.x + 5f, y, transform.position.z);
            groundLineRenderer.SetPosition(0, a);
            groundLineRenderer.SetPosition(1, b);
            return;
        }
        float distance = transform.position.z - cam.transform.position.z;
        Vector3 left = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, distance));
        Vector3 right = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, distance));
        left.y = y; right.y = y; left.z = transform.position.z; right.z = transform.position.z;
        groundLineRenderer.SetPosition(0, left);
        groundLineRenderer.SetPosition(1, right);
    }

    // Helper: distance from ninja center to collider's bottom (in world units)
    private float GetBottomOffsetFromCenter()
    {
        var col = GetComponentInChildren<Collider2D>();
        if (col != null)
        {
            var b = col.bounds; // world-space bounds
            return transform.position.y - b.min.y; // offset so that centerY - offset = bottomY
        }
        // Fallback to renderer half-height if no collider found
        var spriteRend = GetComponentInChildren<SpriteRenderer>();
        if (spriteRend != null) return spriteRend.bounds.extents.y;
        var anyRend = GetComponentInChildren<Renderer>();
        if (anyRend != null) return anyRend.bounds.extents.y;
        return 0f;
    }

    private void UpdateTrajectory()
    {
        if (!showTrajectory || trajectoryRenderer == null) return;
        if (launched) return; // only show while aiming
        if (trajectoryRenderer.positionCount < 2) trajectoryRenderer.positionCount = 2;

        float launchTilt = tiltByHeight ? ComputeTiltDegrees() : 0f;
        float worldAngle = angleDegrees - launchTilt;
        float rad = worldAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        Vector2 startPos = transform.position;
        float speed = GameConstants.NinjaLaunchForce;
        Vector2 v0 = dir * speed;
        Vector2 g = Physics2D.gravity;
        int maxSeg = Mathf.Max(2, trajectorySegments);

        // 진행 거리에 따른 궤적 길이 감소 계산
        float baseVisibleLen = GameConstants.TrajectoryPreviewMaxLength - GameConstants.TrajectoryPreviewSkipLength;
        float distanceFactor = Mathf.Clamp01(1f - (cumulativeMeters / 200f)); //0m =100%,200m =0%
        float desiredLen = baseVisibleLen * distanceFactor;

        // 궤적이 너무 짧으면 표시하지 않음
        bool skipTrajectory = desiredLen < 0.01f;
        if (skipTrajectory)
        {
            trajectoryRenderer.positionCount = 0;
            return;
        }

        bool enforceLen = desiredLen > 0f;
        float skipLen = Mathf.Max(0f, GameConstants.TrajectoryPreviewSkipLength);

        Vector3[] positions = new Vector3[maxSeg];
        int written = 0;
        float accVisibleLen = 0f; // length after skip included
        float rawTraveledLen = 0f; // includes skipped portion
        Vector2 lastRawPos = startPos;

        float tStep = Mathf.Max(0.001f, trajectoryTimeStep);
        float maxTime = (trajectoryMaxTime > 0f) ? trajectoryMaxTime : (tStep * maxSeg);

        for (int i = 0; i < maxSeg; i++)
        {
            float t = i * tStep;
            if (t > maxTime) break;
            Vector2 physPos = trajectoryVerticalOnly
            ? new Vector2(startPos.x, startPos.y + v0.y * t + 0.5f * g.y * t * t)
            : startPos + v0 * t + 0.5f * g * t * t;
            float segRaw = (physPos - lastRawPos).magnitude;
            rawTraveledLen += segRaw;

            // Handle skip region
            if (rawTraveledLen < skipLen)
            {
                lastRawPos = physPos;
                continue;
            }
            if (rawTraveledLen - segRaw < skipLen)
            {
                // First visible point lies within this segment; interpolate
                float remainIntoSeg = skipLen - (rawTraveledLen - segRaw);
                float factorAdj = Mathf.Clamp01(remainIntoSeg / Mathf.Max(1e-4f, segRaw));
                physPos = lastRawPos + (physPos - lastRawPos) * factorAdj;
                segRaw = (physPos - lastRawPos).magnitude;
                rawTraveledLen = skipLen; // clamp
            }

            // Add segment length to visible length
            float segVisibleLen = (physPos - lastRawPos).magnitude;

            if (enforceLen && accVisibleLen + segVisibleLen > desiredLen && segVisibleLen > 1e-4f)
            {
                // Clip this segment to reach exactly desiredLen
                float remain = desiredLen - accVisibleLen;
                float factor = Mathf.Clamp01(remain / segVisibleLen);
                physPos = lastRawPos + (physPos - lastRawPos) * factor;
                segVisibleLen = (physPos - lastRawPos).magnitude; // equals remain
            }

            positions[written++] = physPos;
            accVisibleLen += segVisibleLen;
            lastRawPos = physPos;

            // If enforcing length and reached (allow small epsilon)
            if (enforceLen && accVisibleLen >= desiredLen - 1e-4f) break;
        }

        if (written < 2)
        {
            // Ensure at least two points
            positions[0] = startPos;
            positions[1] = startPos;
            written = 2;
        }
        trajectoryRenderer.positionCount = written;
        for (int i = 0; i < written; i++)
        {
            trajectoryRenderer.SetPosition(i, positions[i]);
        }
    }

    private void InitializeTraveledUI()
    {
        var go = GameObject.Find("TraveledTxt");
        if (go == null) return;
        TraveledTextGO = go;
        uiText = go.GetComponent<Text>();
    }

    private void SetDistanceText(string text)
    {
        if (TraveledTextGO == null && uiText == null) return;
        if (uiText != null)
        {
            uiText.text = text;
            return;
        }
        var tmp = TraveledTextGO.GetComponent("TextMeshProUGUI");
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

    private void InitializeKillUI()
    {
        var go = GameObject.Find("KillTxt");
        if (go == null) return;
        killTextGO = go;
        killUIFont = go.GetComponent<Text>();
    }

    private void SetKillText(int value)
    {
        if (killTextGO == null && killUIFont == null) return;
        string text = value.ToString();
        if (killUIFont != null)
        {
            killUIFont.text = text;
            return;
        }
        var tmp = killTextGO.GetComponent("TextMeshProUGUI");
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

    private void UpdateDistanceDuringFlight()
    {
        // 오른쪽으로만 이동 가능한 구조 → 발사 이후 도달한 최대 X를 사용
        launchMaxX = Mathf.Max(launchMaxX, transform.position.x);
        float dx = Mathf.Max(0f, launchMaxX - launchStartPos.x);
        float meters = cumulativeMeters + dx * GameConstants.MetersPerUnit;
        SetDistanceText($"{meters:0} m");
        AwardFeverDistance(meters);
    }

    private void ActivateGameOverPanel()
    {
        if (gameOverPanel == null)
        {
            gameOverPanel = GameObject.Find("GameOverPanel");
        }
        if (gameOverPanel == null)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindInChildrenRecursive(root.transform, "GameOverPanel");
                if (found != null)
                {
                    gameOverPanel = found;
                    break;
                }
            }
        }
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            UpdateGameOverComboMax();
            UpdateGameOverComboMaxScore();
            UpdateGameOverStyleMax();
            UpdateGameOverStyleMaxScore();
            UpdateGameOverStyleScore();
            UpdateGameOverKill();
            UpdateGameOverKillScore();
            UpdateGameOverTraveled();
            UpdateGameOverTraveledScore();
            UpdateGameOverComboScore();
            UpdateGameOverFinalScore();
            // 진행 시간 표시 및 점수
            UpdateGameOverRunningTime();
            UpdateGameOverRunningTimeScore();
        }
        else
        {
            Debug.LogWarning("ProtractorController: GameOverPanel not found in scene.");
        }

        // Stop in-game time on game over so gameplay halts; UI can still use unscaled time
        Time.timeScale = 0f;
    }

    private void ForceGameOverDebug()
    {
        if (hasLaunchStart)
        {
            launchMaxX = Mathf.Max(launchMaxX, transform.position.x);
            float dx = Mathf.Max(0f, launchMaxX - launchStartPos.x);
            cumulativeMeters += dx * GameConstants.MetersPerUnit;
            hasLaunchStart = false;
            SetDistanceText($"{cumulativeMeters:0} m");
            AwardFeverDistance(cumulativeMeters);
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.SetRotation(0f);
        }
        transform.rotation = Quaternion.identity;

        if (arcRenderer != null) arcRenderer.enabled = false;
        if (needleRenderer != null) needleRenderer.enabled = false;
        if (trajectoryRenderer != null) trajectoryRenderer.enabled = false;

        ActivateGameOverPanel();

        launched = false;
        this.enabled = false;
    }

    private void UpdateGameOverComboMax()
    {
        if (gameOverPanel == null) return;
        var combo = GetComboControllerSafe();
        int maxCombo = (combo != null) ? Mathf.Max(0, combo.MaxCombo) : 0;
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_ComboMax/ComboMaxTxt");
        if (tf == null)
        {
            var tAll = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < tAll.Length; i++)
            {
                if (tAll[i] != null && tAll[i].name == "ComboMaxTxt") { tf = tAll[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        string text = maxCombo.ToString();
        if (ui != null)
        {
            ui.text = text;
            return;
        }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    private void UpdateGameOverComboMaxScore()
    {
        if (gameOverPanel == null) return;
        var combo = GetComboControllerSafe();
        int maxCombo = (combo != null) ? Mathf.Max(0, combo.MaxCombo) : 0;
        int mult = Mathf.Clamp(maxCombo / 5, 0, 9);
        int score = 0;
        try { score = checked(maxCombo * mult); } catch { score = int.MaxValue; }
        score = score * 100;
        string text = score.ToString();
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_ComboMax/ComboMaxScoreTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++
            )
            {
                if (all[i] != null && all[i].name == "ComboMaxScoreTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = text; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    private void UpdateGameOverStyleMax()
    {
        if (gameOverPanel == null) return;
        int maxStyleLevel = (StyleCounter.Instance != null) ? Mathf.Max(0, StyleCounter.Instance.MaxLevel) : 0;
        string display = StyleCounter.GetLevelDisplayText(maxStyleLevel);
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_StyleMax/StyleMaxTxt");
        if (tf == null)
        {
            var tAll = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < tAll.Length; i++)
            {
                if (tAll[i] != null && tAll[i].name == "StyleMaxTxt") { tf = tAll[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null)
        {
            ui.text = display;
            return;
        }
        var tmp = go.GetComponent("TextMeshProUGUI");
        if (tmp != null)
        {
            var t = tmp.GetType();
            var prop = t.GetProperty("text");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(tmp, display, null);
            }
        }
    }

    private void UpdateGameOverStyleMaxScore()
    {
        if (gameOverPanel == null) return;
        int level = (StyleCounter.Instance != null) ? Mathf.Max(0, StyleCounter.Instance.MaxLevel) : 0;
        int multiplier = (level <= 0) ? 0 : Mathf.Clamp(level, 1, 9);
        int value = 0;
        try { value = checked(level * multiplier); } catch { value = int.MaxValue; }
        int display = 0;
        try { display = checked(value * 100); } catch { display = int.MaxValue; }
        string text = display.ToString();
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_StyleMax/StyleMaxScoreTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "StyleMaxScoreTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = text; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    private void UpdateGameOverStyleScore()
    {
        if (gameOverPanel == null) return;
        int value = 0;
        try { value = checked(GameStats.StyleScoreAccumulated * 2); } catch { value = int.MaxValue; }
        int display = 0;
        try { display = checked(value * 100); } catch { display = int.MaxValue; }
        string text = display.ToString();
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_StyleScore/StyleScoreTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "StyleScoreTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = text; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    private void UpdateGameOverKill()
    {
        if (gameOverPanel == null) return;
        int killed = GameStats.EnemiesKilled;
        int spawned = GameStats.EnemiesSpawned;
        string text = killed.ToString() + "/" + spawned.ToString();
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_Kill/KillCountTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "KillCountTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = text; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    private void UpdateGameOverKillScore()
    {
        if (gameOverPanel == null) return;
        int killed = GameStats.EnemiesKilled;
        int spawned = GameStats.EnemiesSpawned;
        int value = 0;
        try
        {
            int missed = spawned - killed;
            value = checked(killed * 3 - missed * 2);
        }
        catch { value = int.MaxValue; }
        if (value < 0) value = 0;
        int display = 0;
        try { display = checked(value * 100); } catch { display = int.MaxValue; }
        string text = display.ToString();
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOverview_Kill/KillScoreTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "KillScoreTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = text; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    private void UpdateGameOverTraveled()
    {
        if (gameOverPanel == null) return;
        string text = string.Format("{0:0} m", cumulativeMeters);
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_Traveled/TraveledTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "TraveledTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = text; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    private void UpdateGameOverTraveledScore()
    {
        if (gameOverPanel == null) return;
        float meters = cumulativeMeters;
        int multiplier = Mathf.Clamp(Mathf.FloorToInt(meters / 50f), 0, 9);
        int value = 0;
        try
        {
            int metersInt = Mathf.RoundToInt(meters);
            value = checked(metersInt * multiplier);
        }
        catch { value = int.MaxValue; }
        if (value < 0) value = 0;
        int display = 0;
        try { display = checked(value * 100); } catch { display = int.MaxValue; }
        string text = display.ToString();
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_Traveled/TraveledScoreTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "TraveledScoreTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = text; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    private void UpdateGameOverComboScore()
    {
        if (gameOverPanel == null) return;
        int value = 0;
        try { value = checked(GameStats.ComboScoreAccumulated * 3); }
        catch { value = int.MaxValue; }
        int display = 0;
        try { display = checked(value * 100); } catch { display = int.MaxValue; }
        string text = display.ToString();
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_ComboScore/ComboScoreTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "ComboScoreTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = text; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    private void UpdateGameOverFinalScore()
    {
        if (gameOverPanel == null) return;
        int total = 0;
        try
        {
            int comboAccum = 0;
            try { comboAccum = checked(GameStats.ComboScoreAccumulated * 3); } catch { comboAccum = int.MaxValue; }
            int comboScoreDisplay = 0;
            try { comboScoreDisplay = checked(comboAccum * 100); } catch { comboScoreDisplay = int.MaxValue; }
            total = checked(total + comboScoreDisplay);

            var combo = GetComboControllerSafe();
            int maxCombo = (combo != null) ? Mathf.Max(0, combo.MaxCombo) : 0;
            int mult = Mathf.Clamp(maxCombo / 5, 0, 9);
            int comboMaxScoreDisplay = 0;
            try { comboMaxScoreDisplay = checked((maxCombo * mult) * 100); } catch { comboMaxScoreDisplay = int.MaxValue; }
            total = checked(total + comboMaxScoreDisplay);

            int level = (StyleCounter.Instance != null) ? Mathf.Max(0, StyleCounter.Instance.MaxLevel) : 0;
            int levelMult = (level <= 0) ? 0 : Mathf.Clamp(level, 1, 9);
            int styleMaxBase = 0;
            try { styleMaxBase = checked(level * levelMult); } catch { styleMaxBase = int.MaxValue; }
            int styleMaxScoreDisplay = 0;
            try { styleMaxScoreDisplay = checked(styleMaxBase * 100); } catch { styleMaxScoreDisplay = int.MaxValue; }
            total = checked(total + styleMaxScoreDisplay);

            int kills = GameStats.EnemiesKilled;
            int spawned = GameStats.EnemiesSpawned;
            int killBase = 0;
            try { killBase = checked(kills * 3 - (spawned - kills) * 2); } catch { killBase = int.MaxValue; }
            if (killBase < 0) killBase = 0;
            int killScoreDisplay = 0;
            try { killScoreDisplay = checked(killBase * 100); } catch { killScoreDisplay = int.MaxValue; }
            total = checked(total + killScoreDisplay);

            float meters = cumulativeMeters;
            int meterMult = Mathf.Clamp(Mathf.FloorToInt(meters / 50f), 0, 9);
            int metersInt = Mathf.RoundToInt(meters);
            int traveledBase = 0;
            try { traveledBase = checked(metersInt * meterMult); } catch { traveledBase = int.MaxValue; }
            if (traveledBase < 0) traveledBase = 0;
            int traveledScoreDisplay = 0;
            try { traveledScoreDisplay = checked(traveledBase * 100); } catch { traveledScoreDisplay = int.MaxValue; }
            total = checked(total + traveledScoreDisplay);

            // NEW: Running time score (10 sec per 1 pt, then * 100)
            var timer = GameTimerController.Instance;
            float secs = (timer != null) ? timer.TotalRunningSeconds : 0f;
            int rtPtsBase = Mathf.FloorToInt(secs / 10f);
            if (rtPtsBase < 0) rtPtsBase = 0;
            int runningTimeScoreDisplay = 0;
            try { runningTimeScoreDisplay = checked(rtPtsBase * 100); } catch { runningTimeScoreDisplay = int.MaxValue; }
            total = checked(total + runningTimeScoreDisplay);
        }
        catch { total = int.MaxValue; }

        string totalText = total.ToString();
        var tf = gameOverPanel.transform.Find("GameOverScore_Final/FinalScoreTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "FinalScoreTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = totalText; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
        if (tmp != null)
        {
            var t = tmp.GetType();
            var prop = t.GetProperty("text");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(tmp, totalText, null);
            }
        }
    }

    // NEW: 게임 진행 시간 표시 (mm:ss)
    private void UpdateGameOverRunningTime()
    {
        if (gameOverPanel == null) return;
        var timer = GameTimerController.Instance;
        float secs = (timer != null) ? timer.TotalRunningSeconds : 0f;
        int totalSeconds = Mathf.FloorToInt(secs);
        string text = FormatTimeMMSS(totalSeconds);
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_RunningTime/RunningTimeTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "RunningTimeTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = text; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    // NEW: 게임 진행 시간 점수 표시 (10초당 1점 → *100 표시)
    private void UpdateGameOverRunningTimeScore()
    {
        if (gameOverPanel == null) return;
        var timer = GameTimerController.Instance;
        float secs = (timer != null) ? timer.TotalRunningSeconds : 0f;
        int ptsBase = Mathf.FloorToInt(secs / 10f);
        if (ptsBase < 0) ptsBase = 0;
        int display = 0;
        try { display = checked(ptsBase * 100); } catch { display = int.MaxValue; }
        string text = display.ToString();
        var tf = gameOverPanel.transform.Find("GameOverScore/GameOver_RunningTime/RunningTimeScoreTxt");
        if (tf == null)
        {
            var all = gameOverPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "RunningTimeScoreTxt") { tf = all[i]; break; }
            }
        }
        if (tf == null) return;
        var go = tf.gameObject;
        var ui = go.GetComponent<Text>();
        if (ui != null) { ui.text = text; return; }
        var tmp = go.GetComponent("TextMeshProUGUI");
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

    private static string FormatTimeMMSS(int seconds)
    {
        if (seconds < 0) seconds = 0;
        int m = seconds / 60;
        int s = seconds % 60;
        return string.Format("{0:0}:{1:00}", m, s);
    }

    // --- Restored helpers that were trimmed by mistake during previous edit ---
    private void AwardFeverDistance(float totalMeters)
    {
        int chunk = Mathf.FloorToInt(totalMeters / 10f); // 1 point per 10m progressed
        int diff = chunk - feverDistanceChunkAwarded;
        if (diff > 0 && feverTime != null)
        {
            feverTime.AddScore(diff);
            feverDistanceChunkAwarded = chunk;
            if (GameConstants.DebugIs)
            {
                Debug.Log($"[Fever] Distance Award: +{diff} (TotalMeters={totalMeters:0.0}, Chunk={chunk})");
            }
        }
    }

    private GameObject FindInChildrenRecursive(Transform parent, string name)
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

    private void TryPlayKnifeWorkVFX(EnemyControler enemy)
    {
        if (knifeWorkResources == null || knifeWorkResources.Count == 0 || enemy == null) return;
        var sets = new List<KnifeWork_ResourcesDataModel>();
        foreach (var set in knifeWorkResources)
        {
            if (set == null || set.Sprites == null || set.Sprites.Length == 0) continue;
            sets.Add(set);
        }
        if (sets.Count == 0) return;
        var chosen = sets[Random.Range(0, sets.Count)];
        if (chosen.AudioClips != null && chosen.AudioClips.Length > 0)
        {
            var clip = chosen.AudioClips[Random.Range(0, chosen.AudioClips.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, enemy.transform.position, 1f);
        }
        var sprites = chosen.Sprites;
        Renderer refRenderer = enemy.GetComponentInChildren<Renderer>();
        if (refRenderer == null) refRenderer = GetComponentInChildren<Renderer>();
        var go = new GameObject("KnifeWorkVFX");
        go.transform.position = enemy.transform.position;
        float min = Mathf.Min(knifeWorkRotationMin, knifeWorkRotationMax);
        float max = Mathf.Max(knifeWorkRotationMin, knifeWorkRotationMax);
        float angle = Random.Range(min, max);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        var sr = go.AddComponent<SpriteRenderer>();
        if (refRenderer != null)
        {
            sr.sortingLayerID = refRenderer.sortingLayerID;
            sr.sortingOrder = refRenderer.sortingOrder + 1;
        }
        var anim = go.AddComponent<OneShotSpriteAnimation>();
        anim.framesPerSecond = knifeWorkFrameRate;
        anim.loop = false;
        anim.autoDestroy = true;
        anim.sprites = sprites;
    }

    private void TryCacheFeverBar()
    {
        if (feverBarRect != null) return;
        var uiRoot = GameObject.Find("UI");
        if (uiRoot == null) return;
        var t = uiRoot.transform.Find("CharacterInfoPanel/FeverTimePanel/BarPanel/Bar2");
        if (t != null) feverBarRect = t.GetComponent<RectTransform>();
    }
    private void UpdateFeverBarWidth(int current, int max)
    {
        if (feverBarRect == null) { TryCacheFeverBar(); if (feverBarRect == null) return; }
        float ratio = (max <= 0) ? 0f : Mathf.Clamp01(current / (float)max);
        var size = feverBarRect.sizeDelta;
        size.x = FeverBarMaxWidth * ratio;
        feverBarRect.sizeDelta = size;
    }

    // UI 위 포인터/터치 여부 체크
    private bool IsPointerOverUI()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        // Mouse
        if (es.IsPointerOverGameObject()) return true;
        // Touches
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (es.IsPointerOverGameObject(t.fingerId)) return true;
            }
        }
        return false;
    }
}
