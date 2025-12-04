using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Attach this to the UI element `GameOver_Skip`.
// When the element is pressed and held for `holdDuration` seconds, the game resets to the initial state.
public class GameOverSkipHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("Hold duration in seconds required to reset the game.")]
    public float holdDuration = 1f;

    [Header("Visuals")]
    [Tooltip("Optional sprite for the progress fill. If not set, a small white sprite is created at runtime.")]
    public Sprite progressSprite;
    [Tooltip("Color of the progress fill.")]
    public Color progressColor = new Color(0.2f, 0.8f, 1f, 0.6f);

    private bool _isPressed;
    private float _pressedTime;

    // Progress fill image rendered on top of GameOver_SkipImg
    private Image _progressImage;
    // Runtime fallback sprite if none is provided
    private static Sprite _fallbackSprite;

    void Awake()
    {
        // Ensure UI prerequisites so pointer events work even if the element is an empty GameObject
        EnsureEventPrerequisites();
        // Cache or create a progress image under GameOver_SkipImg
        CacheProgressImage();
        // Initialize progress to0
        SetProgress(0f);
    }

    void OnEnable()
    {
        _isPressed = false;
        _pressedTime = 0f;
        SetProgress(0f);
    }

    void Update()
    {
        if (!_isPressed) return;
        // Use unscaled time so it works even if Time.timeScale is0 on game over
        _pressedTime += Time.unscaledDeltaTime;
        float progress = holdDuration > 0f ? Mathf.Clamp01(_pressedTime / holdDuration) : 1f;
        SetProgress(progress);
        if (_pressedTime >= holdDuration)
        {
            _isPressed = false; // prevent multiple triggers
            ResetGame();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        _pressedTime = 0f;
        SetProgress(0f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        _pressedTime = 0f;
        SetProgress(0f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // cancel when pointer leaves the control while pressing
        _isPressed = false;
        _pressedTime = 0f;
        SetProgress(0f);
    }

    private static void ResetGame()
    {
        // Ensure time scale is normal before reload
        Time.timeScale = 1f;

        // Reset run stats/counters
        try { GameStats.Reset(); } catch { }
        try { StyleCounter.ResetToZero(); } catch { }
        try { if (ComboController.Instance != null) ComboController.Instance.BreakCombo(true); } catch { } // 강제 초기화

        // Reload current scene
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    // Utility to auto-attach this handler to the default path if found in the scene.
    public static void EnsureOnDefaultPath()
    {
        // Expect hierarchy: UI > GameOverPanel > GameOver_Skip
        var uiRoot = GameObject.Find("UI");
        if (uiRoot == null) return;
        var t = uiRoot.transform.Find("GameOverPanel/GameOver_Skip");
        if (t == null) return;
        var go = t.gameObject;
        if (go.GetComponent<GameOverSkipHandler>() == null)
        {
            go.AddComponent<GameOverSkipHandler>();
        }
    }

    private void EnsureEventPrerequisites()
    {
        //1) Ensure there is a Graphic component to receive raycasts (Image is fine and can be fully transparent)
        var graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f); // invisible but raycastable
            img.raycastTarget = true;
        }

        //2) Ensure there is an EventSystem in the scene
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        //3) Ensure parent Canvas has a GraphicRaycaster and is configurable for world-space
        var canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }
        }
    }

    private void CacheProgressImage()
    {
        // Find container: GameOver_SkipImg
        Transform container = transform.Find("GameOver_SkipImg");
        if (container == null)
        {
            // search recursively
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "GameOver_SkipImg") { container = all[i]; break; }
            }
        }

        if (container == null)
        {
            // Create the container so progress can still be shown
            var go = new GameObject("GameOver_SkipImg", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            container = rt;
        }

        // Create or get overlay child for progress to ensure it renders on top
        Transform overlay = container.Find("GameOver_SkipProgress");
        if (overlay == null)
        {
            var child = new GameObject("GameOver_SkipProgress", typeof(RectTransform));
            child.transform.SetParent(container, false);
            overlay = child.transform;
            var rt = (RectTransform)overlay;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Configure Image
        var img = overlay.GetComponent<Image>();
        if (img == null) img = overlay.gameObject.AddComponent<Image>();

        // Use provided sprite if any; otherwise use a generated small white sprite (no built-in lookup to avoid console errors)
        var sprite = progressSprite != null ? progressSprite : GetOrCreateFallbackSprite();
        img.sprite = sprite;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal; // left -> right fill
        img.fillOrigin = 0; // Left
        img.fillClockwise = true;
        img.fillAmount = 0f;
        img.raycastTarget = false;
        // Apply configured color
        img.color = progressColor;
        overlay.SetAsLastSibling();

        _progressImage = img;
    }

    private static Sprite GetOrCreateFallbackSprite()
    {
        if (_fallbackSprite != null) return _fallbackSprite;
        var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        tex.name = "SkipProgress_WhiteTex";
        _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        _fallbackSprite.name = "SkipProgress_WhiteSprite";
        return _fallbackSprite;
    }

    private void SetProgress(float value)
    {
        if (_progressImage != null)
        {
            _progressImage.fillAmount = Mathf.Clamp01(value);
        }
    }
}
