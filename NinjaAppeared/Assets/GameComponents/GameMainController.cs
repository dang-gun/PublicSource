using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // added for Text
using TMPro; // added for TextMeshProUGUI

public class GameMainController : MonoBehaviour
{


    [Header("Ninja Setup")] //**************************

    /// <summary>
    /// 닌자 프리팹
    /// </summary>
    /// <remarks>
    /// 꼭 미리 설정해야 한다.
    /// </remarks>
    [Tooltip("*필수* 사용자가 사용할 캐릭터 프리팹")]
    public GameObject NinjaPrefab;

    [Tooltip("Padding from the screen edges when positioning the ninja at bottom-left (world units).")]
    public Vector2 screenPadding = new Vector2(0.5f,0.5f);


    [Header("Protractor Setup")] 
    [Tooltip("Radius of the90-degree protractor (world units.).")]
    public float protractorRadius =1.0f;


    [Header("Camera Follow")] 
    [Tooltip("Smooth time for camera movement.")]
    public float cameraSmoothTime = GameConstants.CameraFollowSmoothTime;

    [Header("Ground (Scene-based)")]
    [Tooltip("Scene ground Y position where the ninja should land and stop.")]
    public float sceneGroundY =0f;

    [Header("UI")]
    [Tooltip("Reference to GameOverPanel in the scene (can be inactive).")]
    public GameObject gameOverPanel;

    

    private GameObject _ninja;

    private void Awake()
    {
        GlobalStatic.Ninja_Health = Object.FindFirstObjectByType<HealthController>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Reset fever score/state at game initialization
        try { FeverTimeModel.Instance.Reset(); } catch { }

        //1) Always instantiate Ninja from prefab under CharacterPanel
        if (NinjaPrefab == null)
        {
            Debug.LogError("GameMainController: NinjaPrefab is not assigned. Please assign it in the inspector.");
            return;
        }

        // Ensure GameOverPanel is hidden at game start if it exists
        HideGameOverPanelIfActive();

        // Hide style image and S+ text at game start
        HideStyleImgAtStart();
        HideStyleSPlusTxtAtStart();

        // Ensure Game Timer exists and starts from 60s
        try { GameTimerController.EnsureOnDefaultPath(); } catch { }

        var characterPanel = EnsureCharacterPanel();
        GlobalStatic.Ninja = Instantiate(NinjaPrefab);
        GlobalStatic.NinjaTf = GlobalStatic.Ninja.transform;

        _ninja = GlobalStatic.Ninja;
        _ninja.name = "Ninja_Defult"; // normalize name for later lookups
        if (characterPanel != null)
        {
            _ninja.transform.SetParent(characterPanel, false);
        }

        // Ensure FeverTime UI controller exists and let it hide FeverTime_Background now
        try { FeverTimeUIController.EnsureOnDefaultPath(); } catch { }
        try { FeverTimeUIController.EnsureBackgroundHiddenNow(); } catch { }

        // Attach AoE-on-hit behaviour
        if (_ninja.GetComponent<NinjaAoeOnHit>() == null) _ninja.AddComponent<NinjaAoeOnHit>();
        if (_ninja.GetComponent<NinjaAoeOnHitTrigger>() == null) _ninja.AddComponent<NinjaAoeOnHitTrigger>();

        //2) Place ninja at bottom-left of the main camera's view
        PositionNinjaBottomLeft(_ninja, screenPadding);

        //2.5) Ensure ninja renders in front of BackgroundPanel
        EnsureNinjaInFrontOfBackground(_ninja);

        //2.6) Attach background looper on BackgroundPanel so camera always sees background tiles
        AttachBackgroundLooper();

        //2.75) Compute ground from BackgroundPanel bottom if available
        float bgBottomY;
        if (TryGetBackgroundBottomY(out bgBottomY))
        {
            sceneGroundY = bgBottomY;
        }

        //3) Attach a protractor controller to the ninja (drawn to its right)
        var protractor = _ninja.GetComponent<ProtractorController>();
        if (protractor == null)
        {
            protractor = _ninja.AddComponent<ProtractorController>();
        }
        protractor.radius = protractorRadius;
        protractor.localOffset = new Vector3(0f,0f,0f);
        protractor.segments =36;
        protractor.lineWidth =0.03f;
        // Use scene-based ground (BackgroundPanel bottom if found)
        protractor.useWorldGround = true;
        protractor.groundY = sceneGroundY;
        // Inject UI refs (supports inactive objects assigned via inspector)
        if (gameOverPanel != null)
        {
            protractor.gameOverPanel = gameOverPanel;
        }
        // Inject VFX sprite sets from ResourceController on MainScript
        var mainScript = GameObject.Find("MainScript");
        if (mainScript != null)
        {
            var rc = mainScript.GetComponent<ResourceController>();
            if (rc != null && rc.KnifeWorkResources != null && rc.KnifeWorkResources.Count >0)
            {
                protractor.knifeWorkResources = rc.KnifeWorkResources;
            }
        }
        // Make landing height equal to current start height (override ground to match start)
        AlignGroundToCurrentStart(protractor);
        // refresh protractor sorting after ninja sorting changes
        protractor.RefreshSorting();

        //4) Camera follow framing so ninja stays at10% from left and10% from bottom with smoothing
        var cam = Camera.main;
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollowFraming2D>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollowFraming2D>();
            follow.target = _ninja.transform;
            follow.viewportAnchor = new Vector2(0.1f,0.10f);
            follow.smoothTime = cameraSmoothTime; // uses constant default
        }

        //5) Enemy spawner
        SetupEnemySpawner();

        //6) Ensure a single ComboController exists so combo value persists across kills
        if (GetComboControllerSafe() == null)
        {
            ComboController.Ensure();
        }

        //7) Ensure the GameOver long-press skip handler is attached to UI > GameOverPanel > GameOver_Skip
        // This allows holding the button for2 seconds to reset the game to initial state.
        try { GameOverSkipHandler.EnsureOnDefaultPath(); } catch { }

        //8) Item spawner
        var itemSpawner = ItemSpawner.Ensure();
        if (itemSpawner.target == null) itemSpawner.target = _ninja.transform;
        // 자동 해상에 맡김: itemPanel, resourceController
    }

    private void HideGameOverPanelIfActive()
    {
        // Prefer the assigned reference
        if (gameOverPanel == null)
        {
            // Try to resolve from default path: UI > GameOverPanel
            var uiRoot = GameObject.Find("UI");
            if (uiRoot != null)
            {
                var t = uiRoot.transform.Find("GameOverPanel");
                if (t != null) gameOverPanel = t.gameObject;
            }
        }

        if (gameOverPanel != null && gameOverPanel.activeSelf)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private Transform EnsureCharacterPanel()
    {
        var go = GameObject.Find("CharacterPanel");
        if (go != null) return go.transform;
        var created = new GameObject("CharacterPanel");
        return created.transform;
    }

    private void SetupEnemySpawner()
    {
        var worldRoot = GameObject.Find("BackgroundPanel");
        var spawner = worldRoot != null ? worldRoot.GetComponent<EnemySpawner>() : null;
        if (spawner == null)
        {
            var host = worldRoot != null ? worldRoot : this.gameObject;
            spawner = host.AddComponent<EnemySpawner>();
        }
        spawner.ninja = _ninja.transform;
        spawner.groundY = sceneGroundY;

        // Prefer prefab from ResourceController.EnemyList and also pass its data for SpriteList usage
        var mainScript = GameObject.Find("MainScript");
        GameObject chosenEnemyPrefab = null;
        EnemyDataModelBase chosenData = null;
        if (mainScript != null)
        {
            var rc = mainScript.GetComponent<ResourceController>();
            if (rc != null && rc.EnemyList != null && rc.EnemyList.Count >0)
            {
                foreach (var data in rc.EnemyList)
                {
                    if (data != null && data.EmenyPrefab != null)
                    {
                        chosenEnemyPrefab = data.EmenyPrefab;
                        chosenData = data;
                        break;
                    }
                }
            }
        }

        // Assign if found; otherwise EnemySpawner will resolve its own fallback in Start()
        if (chosenEnemyPrefab != null)
        {
            spawner.enemyPrefab = chosenEnemyPrefab.transform;
            spawner.selectedEnemyData = chosenData; // pass along for BodyImg sprite selection
        }
    }

    private static void PositionNinjaBottomLeft(GameObject ninja, Vector2 padding)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("GameMainController: No main camera found to position the ninja.");
            return;
        }

        // For2D setups: sprites at z =0, camera typically at z = -10
        float targetZ =0f;
        float distance = targetZ - cam.transform.position.z;

        // Bottom-left viewport point (0,0). Add small padding in world units afterwards.
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0f,0f, distance));

        // Try to keep sprite fully visible by adding padding
        Vector3 pos = new Vector3(bottomLeft.x + padding.x, bottomLeft.y + padding.y, targetZ);

        // If the ninja has renderers, push it fully on-screen using its bounds
        var rend = ninja.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            var ext = rend.bounds.extents;
            pos.x += ext.x;
            pos.y += ext.y;
            // keep z at targetZ to avoid depth issues
        }

        ninja.transform.position = pos;
    }

    private static void EnsureNinjaInFrontOfBackground(GameObject ninja)
    {
        // Try to find a background panel and use its sorting info
        int targetLayerId =0; // Default
        int targetOrder =100; // Fallback high order

        var bg = GameObject.Find("BackgroundPanel");
        if (bg != null)
        {
            var bgRenderers = bg.GetComponentsInChildren<Renderer>();
            int maxOrder = int.MinValue;
            int layerId =0;
            foreach (var r in bgRenderers)
            {
                if (r == null) continue;
                if (r.sortingOrder > maxOrder)
                {
                    maxOrder = r.sortingOrder;
                    layerId = r.sortingLayerID;
                }
            }

            //강제로 배경보다 위에 있게 하는 코드인데 이것은 프리팹에서 처리해야 한다.
            //if (maxOrder != int.MinValue)
            //{
            //    targetLayerId = layerId;
            //    targetOrder = maxOrder +1;
            //}
        }

        var ninjaRenderers = ninja.GetComponentsInChildren<Renderer>();
        foreach (var r in ninjaRenderers)
        {
            if (r == null) continue;
            // Put ninja on the same layer as background (if found) and in front
            r.sortingLayerID = targetLayerId;
            r.sortingOrder = targetOrder;
        }
    }

    private void AttachBackgroundLooper()
    {
        var bg = GameObject.Find("BackgroundPanel");
        if (bg == null) return;
        var looper = bg.GetComponent<BackgroundLooper>();
        if (looper == null) looper = bg.AddComponent<BackgroundLooper>();
        looper.targetCamera = Camera.main;
        // Try to auto-assign a child named Background_Loop1 as the tile
        if (looper.sourceTile == null)
        {
            var t = bg.transform.Find("Background_Loop1");
            if (t != null) looper.sourceTile = t;
        }
    }

    private static bool TryGetBackgroundBottomY(out float bottomY)
    {
        bottomY =0f;
        var bg = GameObject.Find("BackgroundPanel");
        if (bg == null) return false;
        var rends = bg.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length ==0) return false;
        float min = float.PositiveInfinity;
        foreach (var r in rends)
        {
            if (r == null) continue;
            float y = r.bounds.min.y;
            if (y < min) min = y;
        }
        if (float.IsInfinity(min)) return false;
        bottomY = min;
        return true;
    }

    private void AlignGroundToCurrentStart(ProtractorController protractor)
    {
        // Make the landing height exactly match the current start height
        var rend = _ninja.GetComponentInChildren<Renderer>();
        float halfHeight =0f;
        if (rend != null)
        {
            halfHeight = rend.bounds.extents.y;
        }
        // Target landing y equals current position.y
        float currentY = _ninja.transform.position.y;
        // We want groundLine (= groundY + bottomOffset) to equal ninja bottom (currentY - halfHeight)
        // Set bottomOffset to0 so groundY equals ninja bottom exactly.
        protractor.bottomOffset =0f;
        sceneGroundY = currentY - halfHeight;
        protractor.groundY = sceneGroundY;
        protractor.useWorldGround = true;
    }

    // Update is called once per frame
    void Update()
    {
        // Nothing needed here; ProtractorController handles input.
    }

    private static ComboController GetComboControllerSafe()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<ComboController>();
#else
        var arr = Resources.FindObjectsOfTypeAll<ComboController>();
        return (arr != null && arr.Length >0) ? arr[0] : null;
#endif
    }

    private void HideStyleImgAtStart()
    {
        var uiRoot = GameObject.Find("UI");
        if (uiRoot == null) return;
        var styleImgTr = uiRoot.transform.Find("RealTimeInfo_Panel/StylePanel/StyleImg");
        if (styleImgTr == null) return;
        var go = styleImgTr.gameObject;
        if (go.activeSelf) go.SetActive(false);
    }

    private void HideStyleSPlusTxtAtStart()
    {
        var uiRoot = GameObject.Find("UI");
        if (uiRoot == null) return;
        var styleSTxtTr = uiRoot.transform.Find("RealTimeInfo_Panel/StylePanel/StyleSTxt");
        if (styleSTxtTr == null) return;
        var go = styleSTxtTr.gameObject;
        if (go.activeSelf) go.SetActive(false);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (text != null) text.text = string.Empty;
        var legacy = go.GetComponent<Text>();
        if (legacy != null) legacy.text = string.Empty;
    }
}
