using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Setup")]
    public Transform enemyPrefab; // legacy fallback
    public Transform ninja;

    [HideInInspector]
    public EnemyDataModelBase selectedEnemyData; // legacy fallback (used if ResourceController is empty)

    [Header("Virtual Box (relative to screen)")]
    [Range(0.1f, 1f)] public float boxViewportWidthFactor = GameConstants.VirtualBoxViewportWidthFactor;
    [Range(0.1f, 1f)] public float boxViewportHeightFactor = GameConstants.VirtualBoxViewportHeightFactor;
    [Range(0f, 1f)] public float boxViewportAnchorX = GameConstants.VirtualBoxViewportAnchorX;
    [Range(0f, 1f)] public float boxViewportAnchorY = GameConstants.VirtualBoxViewportAnchorY;

    [Header("Spawn Area (fallback if no camera)")]
    public bool useBackgroundPanelBounds = true;
    public string backgroundPanelName = "BackgroundPanel";
    public float groundY = 0f;

    [Header("Spawning")]
    [Tooltip("Maximum number of enemies alive at the same time. Tile mode spawns one per box.")]
    public int maxEnemies = 999;

    [Header("Parenting")]
    [Tooltip("Spawned enemies will be parented under this panel name (auto-created if missing).")]
    public string enemyParentName = "EnemyPanel";

    [Header("Prefab Fallback")]
    [Tooltip("If not set and ResourceController has no entries, will try to fetch from Resources path 'Enemys/EnemyPrefab'.")]
    public string enemyPrefabResourcePath = "Enemys/EnemyPrefab";

    [Header("Cleanup")]
    [Tooltip("Extra margin left of the camera view before enemies are removed (world units).")]
    public float offscreenLeftMargin = 0.5f;

    [Header("Spacing")]
    [Tooltip("If >0, when spawning the next enemy, re-roll the position if the straight-line distance to the previous spawned enemy exceeds this value (2D distance).")]
    public float maxDistanceFromPrevious = GameConstants.EnemySpawnMaxDistanceFromPrevious;
    [Tooltip("Number of attempts to re-roll a spawn position to satisfy the distance constraint before giving up.")]
    [Range(0, 20)] public int spacingRetryCount = GameConstants.EnemySpawnSpacingRetryCount;

    private readonly List<Transform> spawned = new List<Transform>();
    private Bounds spawnBounds; // used only as a fallback when no camera
    private Transform bgPanel;
    private Transform enemyParent;

    // Track which box indices have spawned an enemy to avoid duplicates
    private readonly HashSet<int> spawnedBoxIndices = new HashSet<int>();

    // Debug virtual boxes
    private LineRenderer vbRenderer0; // current box
    private LineRenderer vbRenderer1; // next box
    private LineRenderer vbRenderer2; // next+1 box
    private static readonly Color Box0Color = new Color(0f, 1f, 0f, 0.7f);
    private static readonly Color Box1Color = new Color(1f, 1f, 0f, 0.7f);
    private static readonly Color Box2Color = new Color(1f, 0.3f, 0f, 0.7f);
    private const float BoxLineWidth = 0.025f;

    // World-anchored virtual box tiling
    private bool boxWorldInitialized;
    private float boxWorldWidth;
    private float boxWorldHeight;
    private float box0StartX; // left edge of the very first box (at start)
    // 새로 추가: 박스 간 stride (width + padding). padding이 음수면 겹침, 양수면 간격.
    private float boxStrideWidth;

    // New: list of available enemies from ResourceController for random selection
    private readonly List<EnemyDataModelBase> availableEnemies = new List<EnemyDataModelBase>();

    // Spacing state
    private bool hasLastSpawnPos;
    private Vector2 lastSpawnPos;

    // Unique spawn sequence counter (monotonically increases for each spawn during play)
    private int spawnSequence = 0;

    // 마지막으로 생성된 적의 Transform/머리 위치(월드)
    public static Transform LastSpawnedEnemyTransform { get; private set; }
    public static Vector3 LastSpawnedEnemyHeadWorldPos { get; private set; }

    // Event: raised whenever a new enemy is spawned and head position is known
    public static event System.Action<Transform, Vector3> OnEnemySpawned;

    void Start()
    {
        if (useBackgroundPanelBounds)
        {
            var bg = GameObject.Find(backgroundPanelName);
            if (bg != null) bgPanel = bg.transform;
            UpdateBoundsFromBackground();
        }
        EnsureEnemyParent();
        LoadAvailableEnemies(); // New: pull enemies from ResourceController for random use
        if (availableEnemies.Count == 0)
        {
            // Fallback to legacy single prefab discovery so the game can still run
            EnsureEnemyPrefab();
        }
        InitializeWorldBoxes();

        // Pre-spawn: ensure3 boxes (current and next two) each have one enemy
        int baseIndex = GetCurrentBoxIndex();
        for (int i = 0; i < 3; i++)
        {
            EnsureSpawnInBox(baseIndex + i);
        }

        if (GameConstants.VirtualBoxShowIs)
        {
            CreateVirtualBoxRenderers();
            UpdateVirtualBoxRenderers();
        }
    }

    void Update()
    {
        // If neither list nor fallback prefab is available, do nothing
        if ((availableEnemies == null || availableEnemies.Count == 0) && enemyPrefab == null) return;

        // Remove nulls
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] == null) spawned.RemoveAt(i);
        }

        // Cleanup to the left
        CleanupOffscreenLeft();

        // Keep3 boxes ahead always populated
        int currentIndex = GetCurrentBoxIndex();
        for (int i = 0; i < 3; i++)
        {
            EnsureSpawnInBox(currentIndex + i);
        }

        if (GameConstants.VirtualBoxShowIs) UpdateVirtualBoxRenderers();
    }

    void InitializeWorldBoxes()
    {
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float screenW = cam.orthographicSize * 2f * cam.aspect;
            float screenH = cam.orthographicSize * 2f;
            boxWorldWidth = Mathf.Max(0.01f, screenW * Mathf.Clamp01(boxViewportWidthFactor));
            boxWorldHeight = Mathf.Max(0.01f, screenH * Mathf.Clamp01(boxViewportHeightFactor));
            // First box positioned so that the specified anchor aligns with the camera position
            box0StartX = cam.transform.position.x - boxWorldWidth * Mathf.Clamp01(boxViewportAnchorX);
            boxWorldInitialized = true;
        }
        else
        {
            // Fallback: use background bounds
            float width = Mathf.Max(0.01f, spawnBounds.size.x * Mathf.Clamp01(boxViewportWidthFactor));
            boxWorldWidth = width;
            boxWorldHeight = Mathf.Max(0.01f, spawnBounds.size.y * Mathf.Clamp01(boxViewportHeightFactor));
            float refX = spawnBounds.center.x; // fallback ref
            box0StartX = refX - boxWorldWidth * Mathf.Clamp01(boxViewportAnchorX);
            boxWorldInitialized = true;
        }
        // stride 계산 (패딩 포함).0이 되거나 너무 작으면 최소값 보정.
        boxStrideWidth = boxWorldWidth + GameConstants.VirtualBoxSpawnPadding;
        if (boxStrideWidth <= 0.0001f)
        {
            boxStrideWidth = boxWorldWidth; // 비정상 값이면 기본 폭 사용
        }
    }

    int GetCurrentBoxIndex()
    {
        var cam = Camera.main;
        if (!boxWorldInitialized || cam == null) return 0;
        float camX = cam.transform.position.x;
        float offset = camX - box0StartX;
        float stride = (boxStrideWidth > 0.0001f) ? boxStrideWidth : boxWorldWidth;
        int index = Mathf.FloorToInt(offset / stride);
        return Mathf.Max(0, index);
    }

    float GetBoxStartX(int boxIndex)
    {
        float stride = (boxStrideWidth > 0.0001f) ? boxStrideWidth : boxWorldWidth;
        return box0StartX + stride * boxIndex;
    }

    void EnsureSpawnInBox(int boxIndex)
    {
        if (spawnedBoxIndices.Contains(boxIndex)) return;
        SpawnInBox(boxIndex);
        spawnedBoxIndices.Add(boxIndex);
    }

    // Compute the current vertical spawn bounds based on the camera and virtual box height
    private void GetSpawnVerticalRange(out float minY, out float maxY)
    {
        var cam = Camera.main;
        if (cam != null && boxWorldInitialized)
        {
            float camY = cam.transform.position.y;
            float anchorY = Mathf.Clamp01(boxViewportAnchorY);
            minY = camY - boxWorldHeight * anchorY;
            maxY = minY + boxWorldHeight;
            return;
        }
        // Fallbacks if no camera; approximate around background bounds
        if (boxWorldHeight > 0f)
        {
            float centerY = spawnBounds.center.y;
            float anchorY = Mathf.Clamp01(boxViewportAnchorY);
            minY = centerY - boxWorldHeight * anchorY;
            maxY = minY + boxWorldHeight;
        }
        else
        {
            minY = spawnBounds.min.y;
            maxY = spawnBounds.max.y;
        }
    }

    // Tries to find a point inside the rectangle [minX,maxX]x[minY,maxY] that is within maxDist of anchor.
    private static bool TryFindPointWithinDistanceRect(Vector2 anchor, float maxDist, float minX, float maxX, float minY, float maxY, out Vector2 result)
    {
        result = default(Vector2);
        if (maxDist <= 0f) return false;

        float xMinAllow = Mathf.Max(minX, anchor.x - maxDist);
        float xMaxAllow = Mathf.Min(maxX, anchor.x + maxDist);
        if (xMinAllow > xMaxAllow) return false;
        float x = (Mathf.Approximately(xMinAllow, xMaxAllow)) ? xMinAllow : UnityEngine.Random.Range(xMinAllow, xMaxAllow);

        float dx = Mathf.Abs(x - anchor.x);
        float maxDy = Mathf.Sqrt(Mathf.Max(0f, maxDist * maxDist - dx * dx));
        float yMinAllow = Mathf.Max(minY, anchor.y - maxDy);
        float yMaxAllow = Mathf.Min(maxY, anchor.y + maxDy);
        if (yMinAllow > yMaxAllow) return false;
        float y = (Mathf.Approximately(yMinAllow, yMaxAllow)) ? yMinAllow : UnityEngine.Random.Range(yMinAllow, yMaxAllow);

        result = new Vector2(x, y);
        return true;
    }

    // Returns whether we have an anchor (last enemy or ninja) to enforce spacing, and outputs the anchor position.
    private bool TryGetSpacingAnchor(out Vector2 anchor)
    {
        if (hasLastSpawnPos)
        {
            anchor = lastSpawnPos;
            return true;
        }
        if (ninja != null)
        {
            var p = ninja.position;
            anchor = new Vector2(p.x, p.y);
            return true;
        }
        anchor = default(Vector2);
        return false;
    }

    void SpawnInBox(int boxIndex)
    {
        if (!boxWorldInitialized) InitializeWorldBoxes();
        float startX = GetBoxStartX(boxIndex);
        float minX = startX; // 박스 왼쪽 경계
        float maxX = startX + boxWorldWidth; // 폭은 그대로
        if (minX >= maxX)
        {
            // Fallback to the box center if range collapses
            minX = startX + boxWorldWidth * 0.5f;
            maxX = minX;
        }

        // Current allowed vertical range by virtual box
        float vMin, vMax;
        GetSpawnVerticalRange(out vMin, out vMax);
        float vMid = (vMin + vMax) * 0.5f;
        float xMid = (minX + maxX) * 0.5f;

        // Choose an enemy definition each time at random if available
        EnemyDataModelBase data = GetRandomEnemyData();
        if (data != null && data.EmenyPrefab != null)
        {
            var prefabTr = data.EmenyPrefab.transform;
            float baseY = GetGroundedY(prefabTr);
            // Allowed Y range based on data (absolute, world-space)
            float dataYMin = baseY + Mathf.Min(data.LocationY_Min, data.LocationY_Max);
            float dataYMax = baseY + Mathf.Max(data.LocationY_Min, data.LocationY_Max);
            // Intersect with virtual box vertical range to avoid later clamping out of data range
            float allowedYMin = Mathf.Max(dataYMin, vMin);
            float allowedYMax = Mathf.Min(dataYMax, vMax);
            bool hasIntersection = allowedYMin <= allowedYMax;
            if (!hasIntersection)
            {
                // No intersection: fall back to data range to guarantee staying within model range
                allowedYMin = dataYMin;
                allowedYMax = dataYMax;
            }

            // Compute allowed distance with conditional tolerance
            float allowedDist = maxDistanceFromPrevious;
            if (maxDistanceFromPrevious > 0f && hasLastSpawnPos && lastSpawnPos.y >= vMid)
            {
                // If current enemy's absolute max Y is below vMid, allow +3f, else +1f
                if (dataYMax < vMid) allowedDist = maxDistanceFromPrevious + GameConstants.EnemySpawnUpperHalfLowCeilingExtraDistance; else allowedDist = maxDistanceFromPrevious + GameConstants.EnemySpawnUpperHalfExtraDistance;
            }

            // Try placing within spacing constraint
            Vector3 pos = Vector3.zero;
            int attempts = Mathf.Max(0, spacingRetryCount);
            bool satisfied = false;
            for (int i = 0; i <= attempts; i++)
            {
                float x = (minX == maxX) ? minX : UnityEngine.Random.Range(minX, maxX);
                float y = (Mathf.Approximately(allowedYMin, allowedYMax)) ? allowedYMin : UnityEngine.Random.Range(allowedYMin, allowedYMax);
                pos = new Vector3(x, y, prefabTr.position.z);

                Vector2 anchor;
                bool hasAnchor = TryGetSpacingAnchor(out anchor);
                if (allowedDist <= 0f || !hasAnchor)
                {
                    satisfied = true; // no constraint or no anchor
                    break;
                }

                float dist = Vector2.Distance(new Vector2(pos.x, pos.y), anchor);
                if (dist <= allowedDist)
                {
                    satisfied = true; // satisfied
                    break;
                }
                // otherwise loop to try again
            }

            // If not satisfied, try a centered sub-range horizontally (keep Y within allowed range)
            if (!satisfied && (maxX > minX))
            {
                float width = maxX - minX;
                float halfWidth = width * 0.5f;
                float center = (minX + maxX) * 0.5f;
                float halfMinX = center - halfWidth * 0.5f;
                float halfMaxX = center + halfWidth * 0.5f;

                for (int i = 0; i <= attempts; i++)
                {
                    float x = (halfMinX == halfMaxX) ? halfMinX : UnityEngine.Random.Range(halfMinX, halfMaxX);
                    float y = (Mathf.Approximately(allowedYMin, allowedYMax)) ? allowedYMin : UnityEngine.Random.Range(allowedYMin, allowedYMax);
                    pos = new Vector3(x, y, prefabTr.position.z);

                    Vector2 anchor;
                    bool hasAnchor = TryGetSpacingAnchor(out anchor);
                    if (!hasAnchor)
                    {
                        satisfied = true;
                        break;
                    }
                    float dist = Vector2.Distance(new Vector2(pos.x, pos.y), anchor);
                    if (dist <= allowedDist)
                    {
                        satisfied = true;
                        break;
                    }
                }
            }

            // Final fallback: deterministically choose a point inside both the box and the distance circle
            if (!satisfied && allowedDist > 0f)
            {
                Vector2 anchor;
                if (TryGetSpacingAnchor(out anchor))
                {
                    Vector2 adjusted;
                    float rectMinY = allowedYMin;
                    float rectMaxY = allowedYMax;
                    if (TryFindPointWithinDistanceRect(anchor, allowedDist, minX, maxX, rectMinY, rectMaxY, out adjusted))
                    {
                        pos = new Vector3(adjusted.x, adjusted.y, prefabTr.position.z);
                        satisfied = true;
                    }
                }
            }

            // Extra: if still too far, do extra re-rolls, then force X to left edge
            if (allowedDist > 0f)
            {
                Vector2 anchor;
                if (TryGetSpacingAnchor(out anchor))
                {
                    if (!satisfied)
                    {
                        bool ok = false;
                        for (int extra = 0; extra < GameConstants.EnemySpawnExtraRetriesAfterFail && !ok; extra++)
                        {
                            float xTry = (minX == maxX) ? minX : UnityEngine.Random.Range(minX, maxX);
                            float yTry = (Mathf.Approximately(allowedYMin, allowedYMax)) ? allowedYMin : UnityEngine.Random.Range(allowedYMin, allowedYMax);
                            Vector2 p2 = new Vector2(xTry, yTry);
                            if (Vector2.Distance(p2, anchor) <= allowedDist)
                            {
                                pos = new Vector3(xTry, yTry, prefabTr.position.z);
                                ok = true;
                                satisfied = true;
                                break;
                            }
                        }
                        if (!ok)
                        {
                            float yForce = (Mathf.Approximately(allowedYMin, allowedYMax)) ? allowedYMin : UnityEngine.Random.Range(allowedYMin, allowedYMax);
                            pos = new Vector3(minX, yForce, prefabTr.position.z);
                            satisfied = true;
                        }
                    }
                }
            }

            var inst = Instantiate(prefabTr, pos, Quaternion.identity);
            GameStats.IncrementEnemiesSpawned();
            var id = ++spawnSequence;
            inst.name = $"Enemy_{id}";
            if (enemyParent != null) inst.SetParent(enemyParent, true);
            // Ensure root state exists on the spawned root for robust dead/alive tracking
            var rootState = inst.gameObject.GetComponent<EnemyRootState>();
            if (rootState == null) inst.gameObject.AddComponent<EnemyRootState>();
            ApplyBodySprite(inst.gameObject, data);
            SetRenderersEnabled(inst.gameObject, true);
            spawned.Add(inst);

            // Update spacing state
            lastSpawnPos = new Vector2(inst.position.x, inst.position.y);
            hasLastSpawnPos = true;

            // Update last spawned enemy head position
            UpdateLastEnemyHeadInfo(inst);
            return;
        }

        // Fallback to legacy single prefab path
        if (enemyPrefab == null) return;
        float baseYLegacy = GetGroundedY(enemyPrefab);
        // Compute allowed Y based on selectedEnemyData if present, else allow a small range around base
        float legacyDataMin = baseYLegacy + (selectedEnemyData != null ? Mathf.Min(selectedEnemyData.LocationY_Min, selectedEnemyData.LocationY_Max) : 0f);
        float legacyDataMax = baseYLegacy + (selectedEnemyData != null ? Mathf.Max(selectedEnemyData.LocationY_Min, selectedEnemyData.LocationY_Max) : 0f);
        float legacyAllowedMin = legacyDataMin;
        float legacyAllowedMax = legacyDataMax;
        // Intersect with virtual box
        float rectYMin = legacyAllowedMin;
        float rectYMax = legacyAllowedMax;
        bool legacyHasIntersection = rectYMin <= rectYMax;
        if (legacyHasIntersection)
        {
            rectYMin = Mathf.Max(rectYMin, vMin);
            rectYMax = Mathf.Min(rectYMax, vMax);
            if (rectYMin > rectYMax)
            {
                // No intersection with vbox: keep data range to ensure staying within model range
                rectYMin = legacyAllowedMin;
                rectYMax = legacyAllowedMax;
            }
        }

        // Compute allowed distance for legacy, with +1f or +3f tolerance depending on current model
        float allowedDistLegacy = maxDistanceFromPrevious;
        if (maxDistanceFromPrevious > 0f && hasLastSpawnPos && lastSpawnPos.y >= vMid)
        {
            if (selectedEnemyData != null)
            {
                float dataMaxAbsLegacy = baseYLegacy + Mathf.Max(selectedEnemyData.LocationY_Min, selectedEnemyData.LocationY_Max);
                if (dataMaxAbsLegacy < vMid) allowedDistLegacy = maxDistanceFromPrevious + GameConstants.EnemySpawnUpperHalfLowCeilingExtraDistance; else allowedDistLegacy = maxDistanceFromPrevious + GameConstants.EnemySpawnUpperHalfExtraDistance;
            }
            else
            {
                allowedDistLegacy = maxDistanceFromPrevious + GameConstants.EnemySpawnUpperHalfExtraDistance;
            }
        }

        Vector3 posLegacy = Vector3.zero;
        int legacyAttempts = Mathf.Max(0, spacingRetryCount);
        bool legacySatisfied = false;
        for (int i = 0; i <= legacyAttempts; i++)
        {
            float x = (minX == maxX) ? minX : UnityEngine.Random.Range(minX, maxX);
            float y = (Mathf.Approximately(rectYMin, rectYMax)) ? rectYMin : UnityEngine.Random.Range(rectYMin, rectYMax);
            posLegacy = new Vector3(x, y, enemyPrefab.position.z);

            Vector2 anchor;
            bool hasAnchor2 = TryGetSpacingAnchor(out anchor);
            if (allowedDistLegacy <= 0f || !hasAnchor2)
            {
                legacySatisfied = true;
                break;
            }
            float distLegacy = Vector2.Distance(new Vector2(posLegacy.x, posLegacy.y), anchor);
            if (distLegacy <= allowedDistLegacy)
            {
                legacySatisfied = true;
                break;
            }
        }

        // If not satisfied, try again with half box width centered (legacy)
        if (!legacySatisfied && (maxX > minX))
        {
            float width = maxX - minX;
            float halfWidth = width * 0.5f;
            float center = (minX + maxX) * 0.5f;
            float halfMinX = center - halfWidth * 0.5f;
            float halfMaxX = center + halfWidth * 0.5f;

            for (int i = 0; i <= legacyAttempts; i++)
            {
                float x = (halfMinX == halfMaxX) ? halfMinX : UnityEngine.Random.Range(halfMinX, halfMaxX);
                float y = (Mathf.Approximately(rectYMin, rectYMax)) ? rectYMin : UnityEngine.Random.Range(rectYMin, rectYMax);
                posLegacy = new Vector3(x, y, enemyPrefab.position.z);

                Vector2 anchor;
                if (!TryGetSpacingAnchor(out anchor))
                {
                    legacySatisfied = true;
                    break;
                }
                float distLegacy = Vector2.Distance(new Vector2(posLegacy.x, posLegacy.y), anchor);
                if (distLegacy <= allowedDistLegacy)
                {
                    legacySatisfied = true;
                    break;
                }
            }
        }

        // Final fallback for legacy path
        if (!legacySatisfied && allowedDistLegacy > 0f)
        {
            Vector2 anchor;
            if (TryGetSpacingAnchor(out anchor))
            {
                Vector2 adjusted;
                if (TryFindPointWithinDistanceRect(anchor, allowedDistLegacy, minX, maxX, rectYMin, rectYMax, out adjusted))
                {
                    posLegacy = new Vector3(adjusted.x, adjusted.y, enemyPrefab.position.z);
                    legacySatisfied = true;
                }
            }
        }

        // Extra: if still too far (legacy), do extra re-rolls, then force X to left edge
        if (allowedDistLegacy > 0f)
        {
            Vector2 anchor;
            if (TryGetSpacingAnchor(out anchor))
            {
                if (!legacySatisfied)
                {
                    bool ok = false;
                    for (int extra = 0; extra < GameConstants.EnemySpawnExtraRetriesAfterFail && !ok; extra++)
                    {
                        float x = (minX == maxX) ? minX : UnityEngine.Random.Range(minX, maxX);
                        float y = (Mathf.Approximately(rectYMin, rectYMax)) ? rectYMin : UnityEngine.Random.Range(rectYMin, rectYMax);
                        Vector2 p2 = new Vector2(x, y);
                        if (Vector2.Distance(p2, anchor) <= allowedDistLegacy)
                        {
                            posLegacy = new Vector3(x, y, enemyPrefab.position.z);
                            ok = true;
                            legacySatisfied = true;
                            break;
                        }
                    }
                    if (!ok)
                    {
                        float yForce = (Mathf.Approximately(rectYMin, rectYMax)) ? rectYMin : UnityEngine.Random.Range(rectYMin, rectYMax);
                        posLegacy = new Vector3(minX, yForce, enemyPrefab.position.z);
                        legacySatisfied = true;
                    }
                }
            }
        }

        var instLegacy = Instantiate(enemyPrefab, posLegacy, Quaternion.identity);
        GameStats.IncrementEnemiesSpawned();
        var idLegacy = ++spawnSequence;
        instLegacy.name = $"Enemy_{idLegacy}";
        if (enemyParent != null) instLegacy.SetParent(enemyParent, true);
        var rootState2 = instLegacy.gameObject.GetComponent<EnemyRootState>();
        if (rootState2 == null) instLegacy.gameObject.AddComponent<EnemyRootState>();
        ApplyBodySprite(instLegacy.gameObject, selectedEnemyData);
        SetRenderersEnabled(instLegacy.gameObject, true);
        spawned.Add(instLegacy);

        // Update spacing state
        lastSpawnPos = new Vector2(instLegacy.position.x, instLegacy.position.y);
        hasLastSpawnPos = true;

        // Update last spawned enemy head position (legacy)
        UpdateLastEnemyHeadInfo(instLegacy);
    }

    private void UpdateLastEnemyHeadInfo(Transform enemy)
    {
        LastSpawnedEnemyTransform = enemy;
        var r = enemy != null ? enemy.GetComponentInChildren<Renderer>() : null;
        if (r != null)
        {
            var b = r.bounds;
            LastSpawnedEnemyHeadWorldPos = new Vector3(b.center.x, b.max.y, enemy.position.z);
        }
        else
        {
            LastSpawnedEnemyHeadWorldPos = enemy != null ? enemy.position : Vector3.zero;
        }

        // Notify listeners that an enemy has spawned (and provide head world position)
        var handler = OnEnemySpawned;
        if (handler != null)
        {
            try
            {
                handler.Invoke(enemy, LastSpawnedEnemyHeadWorldPos);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    void ApplyBodySprite(GameObject enemy, EnemyDataModelBase data)
    {
        if (data == null || data.SpriteList == null || data.SpriteList.Length == 0) return;
        var list = data.SpriteList;
        var sprite = list[UnityEngine.Random.Range(0, list.Length)];
        if (sprite == null) return;
        // Prefer child named BodyImg
        var body = enemy.transform.Find("BodyImg");
        if (body != null)
        {
            var sr = body.GetComponent<SpriteRenderer>();
            if (sr != null) { sr.sprite = sprite; return; }
            var img = body.GetComponent<Image>();
            if (img != null) { img.sprite = sprite; img.SetNativeSize(); return; }
        }
        // Fallbacks
        var anySr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (anySr != null) { anySr.sprite = sprite; return; }
        var anyImg = enemy.GetComponentInChildren<Image>();
        if (anyImg != null) { anyImg.sprite = sprite; anyImg.SetNativeSize(); }
    }

    void CreateVirtualBoxRenderers()
    {
        vbRenderer0 = CreateBoxRenderer("VirtualBox_0", Box0Color);
        vbRenderer1 = CreateBoxRenderer("VirtualBox_1", Box1Color);
        vbRenderer2 = CreateBoxRenderer("VirtualBox_2", Box2Color);
    }

    LineRenderer CreateBoxRenderer(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(this.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        SetupLineRenderer(lr, color);
        return lr;
    }

    void SetupLineRenderer(LineRenderer lr, Color color)
    {
        lr.useWorldSpace = true;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.positionCount = 5;
        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = BoxLineWidth;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = 1000;
    }

    void UpdateVirtualBoxRenderers()
    {
        var cam = Camera.main;
        if (!boxWorldInitialized || cam == null)
        {
            if (vbRenderer0 != null) vbRenderer0.enabled = false;
            if (vbRenderer1 != null) vbRenderer1.enabled = false;
            if (vbRenderer2 != null) vbRenderer2.enabled = false;
            return;
        }
        int baseIndex = GetCurrentBoxIndex();
        float vMin, vMax;
        GetSpawnVerticalRange(out vMin, out vMax);
        UpdateOneBoxRenderer(vbRenderer0, baseIndex, vMin, vMax);
        UpdateOneBoxRenderer(vbRenderer1, baseIndex + 1, vMin, vMax);
        UpdateOneBoxRenderer(vbRenderer2, baseIndex + 2, vMin, vMax);
    }

    void UpdateOneBoxRenderer(LineRenderer lr, int boxIndex, float minY, float maxY)
    {
        if (lr == null) return;
        float startX = GetBoxStartX(boxIndex);
        float leftX = startX; // 왼쪽 경계
        float rightX = startX + boxWorldWidth; // 박스 폭 유지
        Vector3 a = new Vector3(leftX, minY, 0f);
        Vector3 b = new Vector3(rightX, minY, 0f);
        Vector3 c = new Vector3(rightX, maxY, 0f);
        Vector3 d = new Vector3(leftX, maxY, 0f);
        lr.SetPositions(new[] { a, b, c, d, a });
        lr.enabled = true;
    }

    void EnsureEnemyParent()
    {
        var found = GameObject.Find(enemyParentName);
        if (found != null)
        {
            enemyParent = found.transform;
            return;
        }
        var go = new GameObject(enemyParentName);
        if (bgPanel != null) go.transform.SetParent(bgPanel, false);
        enemyParent = go.transform;
    }

    void LoadAvailableEnemies()
    {
        availableEnemies.Clear();
        var mainScriptObj = GameObject.Find("MainScript");
        if (mainScriptObj == null) return;
        var rc = mainScriptObj.GetComponent<ResourceController>();
        if (rc == null || rc.EnemyList == null || rc.EnemyList.Count == 0) return;
        foreach (var data in rc.EnemyList)
        {
            if (data == null || !data.UseIs || data.EmenyPrefab == null) continue; // filter by UseIs
            availableEnemies.Add(data);
            // If the prefab is in-scene (unlikely), hide renderers on the template
            var tr = data.EmenyPrefab.transform;
            if (tr != null && tr.gameObject.scene.IsValid())
            {
                SetRenderersEnabled(tr.gameObject, false);
            }
        }
    }

    // Legacy single-prefab discovery as a fallback when no availableEnemies
    void EnsureEnemyPrefab()
    {
        if (enemyPrefab != null) return;
        // Prefer prefab from ResourceController on 'MainScript'
        var mainScriptObj = GameObject.Find("MainScript");
        if (mainScriptObj != null)
        {
            var rc = mainScriptObj.GetComponent<ResourceController>();
            if (rc != null && rc.EnemyList != null && rc.EnemyList.Count > 0)
            {
                foreach (var data in rc.EnemyList)
                {
                    if (data == null || !data.UseIs || data.EmenyPrefab == null) continue; // filter by UseIs
                    enemyPrefab = data.EmenyPrefab.transform;
                    selectedEnemyData = data;
                    if (enemyPrefab.gameObject.scene.IsValid())
                    {
                        SetRenderersEnabled(enemyPrefab.gameObject, false);
                    }
                    return;
                }
            }
        }
        // Fallbacks
        var childInPanel = enemyParent != null ? FindChildRecursive(enemyParent, "EnemyPrefab") : null;
        if (childInPanel != null)
        {
            enemyPrefab = childInPanel;
            SetRenderersEnabled(enemyPrefab.gameObject, false);
            return;
        }
        var go2 = GameObject.Find("EnemyPrefab");
        if (go2 != null)
        {
            enemyPrefab = go2.transform;
            SetRenderersEnabled(enemyPrefab.gameObject, false);
            return;
        }
        if (!string.IsNullOrEmpty(enemyPrefabResourcePath))
        {
            var loaded = Resources.Load<GameObject>(enemyPrefabResourcePath);
            if (loaded != null)
            {
                var inst = Instantiate(loaded);
                inst.name = "EnemyPrefab";
                enemyPrefab = inst.transform;
                if (enemyParent != null) enemyPrefab.SetParent(enemyParent, false);
                SetRenderersEnabled(enemyPrefab.gameObject, false);
                return;
            }
        }
        Debug.LogWarning("EnemySpawner: No enemy data or prefab found. Assign prefabs in ResourceController.EnemyList or set a fallback prefab.");
    }

    Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform t in parent)
        {
            if (t.name == name) return t;
            var r = FindChildRecursive(t, name);
            if (r != null) return r;
        }
        return null;
    }

    void UpdateBoundsFromBackground()
    {
        if (bgPanel == null) return;
        var rends = bgPanel.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return;
        var b = new Bounds(rends[0].bounds.center, Vector3.zero);
        foreach (var r in rends)
        {
            if (r == null) continue;
            b.Encapsulate(r.bounds);
        }
        spawnBounds = b;
    }

    float GetGroundedY(Transform prefabTr)
    {
        float y = groundY;
        var rend = prefabTr != null ? prefabTr.GetComponentInChildren<Renderer>() : null;
        if (rend != null) y = groundY + rend.bounds.extents.y;
        return y;
    }

    void CleanupOffscreenLeft()
    {
        var cam = Camera.main;
        if (cam == null || spawned.Count == 0) return;

        float leftX;
        if (cam.orthographic)
        {
            leftX = cam.transform.position.x - cam.orthographicSize * cam.aspect;
        }
        else
        {
            float depth = Mathf.Abs(cam.transform.position.z - spawned[0].position.z);
            leftX = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x;
        }
        leftX -= offscreenLeftMargin;

        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            var t = spawned[i];
            if (t == null)
            {
                spawned.RemoveAt(i);
                continue;
            }
            float rightmost = t.position.x;
            var r = t.GetComponentInChildren<Renderer>();
            if (r != null) rightmost = r.bounds.max.x;
            if (rightmost < leftX)
            {
                // Only break combo for enemies that left alive; do NOT break for dead ones
                bool isDead = false;

                //1) Preferred: a EnemyRootState on this spawned root
                var rs = t.GetComponent<EnemyRootState>();
                if (rs != null)
                {
                    isDead = rs.IsDead;
                }
                else
                {
                    //2) Fallbacks: query down the children
                    var ec = t.GetComponentInChildren<EnemyControler>(true);
                    if (ec != null)
                    {
                        isDead = ec.IsDead;
                    }
                    else if (t.GetComponentInChildren<EnemyDeathMarker>(true) != null)
                    {
                        isDead = true;
                    }
                }

                if (!isDead)
                {
                    var combo = GetComboControllerSafe();
                    if (combo != null)
                    {
                        combo.BreakCombo();
                        //Debug.Log("EnemySpawner: Combo broken due to enemy leaving screen alive.");
                    }
                }

                Destroy(t.gameObject);
                spawned.RemoveAt(i);
            }
        }
    }

    private static ComboController GetComboControllerSafe()
    {
        if (ComboController.Instance != null) return ComboController.Instance;
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<ComboController>();
#else
 var arr = Resources.FindObjectsOfTypeAll<ComboController>();
 return (arr != null && arr.Length >0) ? arr[0] : null;
#endif
    }

    void SetRenderersEnabled(GameObject go, bool enabled)
    {
        var srs = go.GetComponentsInChildren<Renderer>(true);
        foreach (var s in srs) s.enabled = enabled;
    }

    private EnemyDataModelBase GetRandomEnemyData()
    {
        if (availableEnemies == null || availableEnemies.Count == 0) return null;
        int idx = UnityEngine.Random.Range(0, availableEnemies.Count);
        return availableEnemies[idx];
    }

    /// <summary>
    /// 현재 카메라 기준 우측 화면 밖(boxesAhead>=3 권장)에 적을 강제로 1개 스폰하고 참조/머리좌표를 반환한다.
    /// </summary>
    public bool ForceSpawnOffscreenRight(out Transform enemy, out Vector3 headWorldPos, int boxesAhead = 3)
    {
        enemy = null;
        headWorldPos = Vector3.zero;
        if (!boxWorldInitialized) InitializeWorldBoxes();
        int current = GetCurrentBoxIndex();
        int boxIndex = Mathf.Max(0, current + Mathf.Max(3, boxesAhead));
        if (!spawnedBoxIndices.Contains(boxIndex))
        {
            SpawnInBox(boxIndex);
            spawnedBoxIndices.Add(boxIndex);
        }
        // SpawnInBox 내부에서 LastSpawnedEnemy* 이 갱신됨
        enemy = LastSpawnedEnemyTransform;
        headWorldPos = LastSpawnedEnemyHeadWorldPos;
        return enemy != null;
    }
}
