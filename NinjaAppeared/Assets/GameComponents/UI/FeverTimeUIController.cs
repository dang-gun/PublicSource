using UnityEngine;

/// <summary>
/// Controls Fever Time UI elements.
/// Paths used:
///  - UI/CharacterInfoPanel/FeverTimePanel/FeverTimeImage (visibility + animation during active)
///  - UI/CharacterInfoPanel/FeverTimePanel/BarPanel/Bar2 (fill width proportional to CurrentScore)
/// </summary>
public class FeverTimeUIController : MonoBehaviour
{
    // Animation params
    [Header("Animation")]
    [Tooltip("Enable animated pulsing & stretch while fever image is visible.")]
    public bool animate = true;
    [Tooltip("Base pulsing speed (Hz-like). Higher = faster.")]
    public float pulseSpeed = 4.0f;
    [Tooltip("Horizontal stretch speed.")]
    public float stretchSpeedX = 2.2f;
    [Tooltip("Vertical stretch speed.")]
    public float stretchSpeedY = 3.1f;
    [Tooltip("Uniform pulse amplitude (scale delta).")]
    public float pulseAmplitude = 0.06f;
    [Tooltip("Horizontal stretch amplitude (scale delta).")]
    public float stretchAmplitudeX = 0.10f;
    [Tooltip("Vertical stretch amplitude (scale delta).")]
    public float stretchAmplitudeY = 0.10f;

    [Header("Bar Fill")]
    [Tooltip("If true, automatically update Bar2 width to reflect fever gauge fill.")]
    public bool updateBarFill = true;
    [Tooltip("Optional width padding added AFTER proportional fill (pixels). Can be negative.")]
    public float barWidthExtra = 0f;
    [Tooltip("Minimum visible width (pixels) to avoid disappearing when score > 0.")]
    public float barMinVisibleWidth = 2f;

    // Cached references (UI image)
    private GameObject _feverImage;
    private RectTransform _feverRect;
    private RectTransform _bar2Rect;
    private RectTransform _barPanelRect;

    // Fever background under Ninja prefab
    private GameObject _feverBackground; // NinjaPrefab/FeverTime_Background
    private Transform _feverBackgroundTf; // transform for scaling

    private bool _subscribed;
    private bool _isAnimating;
    private float _animTime;
    private Vector3 _originalScale = Vector3.one;
    private Vector3 _backgroundOriginalScale = Vector3.one; // store background original scale

    void Awake()
    {
        ResolveImage();
        ResolveBar();
        ResolveFeverBackground();
        HideFeverImage();
        HideFeverBackground();
    }

    void OnEnable()
    {
        Subscribe();
        HideFeverImage();
        ResolveFeverBackground();
        HideFeverBackground();
        UpdateBarFillImmediate(FeverTimeModel.Instance.CurrentScore, FeverTimeModel.MaxScore);
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Update()
    {
        FeverTimeModel.Instance.Update(Time.deltaTime);

        if (animate && _isAnimating && _feverRect != null)
        {
            _animTime += Time.deltaTime;
            float u = 1f + Mathf.Sin(_animTime * pulseSpeed * Mathf.PI * 2f) * pulseAmplitude;
            float sx = 1f + Mathf.Sin(_animTime * stretchSpeedX * Mathf.PI * 2f + 0.6f) * stretchAmplitudeX;
            float sy = 1f + Mathf.Sin(_animTime * stretchSpeedY * Mathf.PI * 2f + 1.7f) * stretchAmplitudeY;
            Vector3 s = new Vector3(u * sx, u * sy, 1f);
            // UI fever image
            _feverRect.localScale = new Vector3(_originalScale.x * s.x, _originalScale.y * s.y, 1f);
            // Background scale (world transform)
            if (_feverBackgroundTf != null)
            {
                _feverBackgroundTf.localScale = new Vector3(_backgroundOriginalScale.x * s.x, _backgroundOriginalScale.y * s.y, _backgroundOriginalScale.z);
            }
        }
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        FeverTimeModel.Instance.OnActivated += HandleActivated;
        FeverTimeModel.Instance.OnEnded += HandleEnded;
        FeverTimeModel.Instance.OnScoreChanged += HandleScoreChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        FeverTimeModel.Instance.OnActivated -= HandleActivated;
        FeverTimeModel.Instance.OnEnded -= HandleEnded;
        FeverTimeModel.Instance.OnScoreChanged -= HandleScoreChanged;
        _subscribed = false;
    }

    private void HandleActivated()
    {
        ShowFeverImage();
        ShowFeverBackground();
        StartAnim();
        UpdateBarFillImmediate(FeverTimeModel.Instance.CurrentScore, FeverTimeModel.MaxScore);
    }

    private void HandleEnded()
    {
        StopAnim();
        HideFeverImage();
        HideFeverBackground();
        UpdateBarFillImmediate(FeverTimeModel.Instance.CurrentScore, FeverTimeModel.MaxScore);
    }

    private void HandleScoreChanged(int current, int max)
    {
        UpdateBarFillImmediate(current, max);
    }

    private void ResolveImage()
    {
        var uiRoot = GameObject.Find("UI");
        if (uiRoot == null) return;
        var t = uiRoot.transform.Find("CharacterInfoPanel/FeverTimePanel/FeverTimeImage");
        _feverImage = t != null ? t.gameObject : null;
        if (_feverImage != null)
        {
            _feverRect = _feverImage.GetComponent<RectTransform>();
            if (_feverRect != null) _originalScale = _feverRect.localScale;
        }
    }

    private void ResolveBar()
    {
        var uiRoot = GameObject.Find("UI");
        if (uiRoot == null) return;
        var bar2 = uiRoot.transform.Find("CharacterInfoPanel/FeverTimePanel/BarPanel/Bar2");
        if (bar2 != null) _bar2Rect = bar2.GetComponent<RectTransform>();
        var barPanel = uiRoot.transform.Find("CharacterInfoPanel/FeverTimePanel/BarPanel");
        if (barPanel != null) _barPanelRect = barPanel.GetComponent<RectTransform>();
    }

    private void ResolveFeverBackground()
    {
        if (_feverBackground != null && _feverBackgroundTf != null) return;
        if (GlobalStatic.Ninja == null) return;
        var t = GlobalStatic.Ninja.transform.Find("FeverTime_Background");
        if (t != null)
        {
            _feverBackground = t.gameObject;
            _feverBackgroundTf = t.transform;
            _backgroundOriginalScale = _feverBackgroundTf.localScale;
        }
    }

    private void ShowFeverImage()
    {
        if (_feverImage == null) { ResolveImage(); }
        if (_feverImage != null && !_feverImage.activeSelf)
        {
            _feverImage.SetActive(true);
        }
    }

    private void HideFeverImage()
    {
        if (_feverImage == null) { ResolveImage(); }
        if (_feverRect != null) { _feverRect.localScale = _originalScale; }
        if (_feverImage != null && _feverImage.activeSelf)
        {
            _feverImage.SetActive(false);
        }
    }

    private void ShowFeverBackground()
    {
        if (_feverBackground == null) ResolveFeverBackground();
        if (_feverBackground != null && !_feverBackground.activeSelf)
        {
            // refresh original scale if not animating yet
            if (_feverBackgroundTf != null) _backgroundOriginalScale = _feverBackgroundTf.localScale;
            _feverBackground.SetActive(true);
        }
    }

    private void HideFeverBackground()
    {
        if (_feverBackground == null) ResolveFeverBackground();
        if (_feverBackground != null && _feverBackground.activeSelf)
        {
            if (_feverBackgroundTf != null) _feverBackgroundTf.localScale = _backgroundOriginalScale;
            _feverBackground.SetActive(false);
        }
    }

    private void StartAnim()
    {
        _animTime = 0f;
        _isAnimating = true;
        if (_feverRect == null && _feverImage != null) _feverRect = _feverImage.GetComponent<RectTransform>();
        if (_feverRect != null) _originalScale = _feverRect.localScale;
        if (_feverBackgroundTf != null) _backgroundOriginalScale = _feverBackgroundTf.localScale;
    }

    private void StopAnim()
    {
        _isAnimating = false;
        if (_feverRect != null) _feverRect.localScale = _originalScale;
        if (_feverBackgroundTf != null) _feverBackgroundTf.localScale = _backgroundOriginalScale;
    }

    private void UpdateBarFillImmediate(int current, int max)
    {
        if (!updateBarFill) return;
        if (_bar2Rect == null || _barPanelRect == null)
        {
            ResolveBar();
            if (_bar2Rect == null || _barPanelRect == null) return;
        }
        float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        float baseWidth = _barPanelRect.rect.width;
        float targetWidth = baseWidth * ratio + barWidthExtra;
        if (ratio > 0f && targetWidth < barMinVisibleWidth) targetWidth = barMinVisibleWidth;
        var size = _bar2Rect.sizeDelta;
        size.x = targetWidth;
        _bar2Rect.sizeDelta = size;
    }

    /// <summary>
    /// Ensures a controller exists in scene under the UI root and is enabled.
    /// </summary>
    public static void EnsureOnDefaultPath()
    {
        var uiRoot = GameObject.Find("UI");
        if (uiRoot == null) return;
        var host = uiRoot.GetComponent<FeverTimeUIController>();
        if (host == null) host = uiRoot.AddComponent<FeverTimeUIController>();
        if (!host.enabled) host.enabled = true;
    }

    /// <summary>
    /// Ensure the ninja's FeverTime_Background is resolved and hidden immediately.
    /// Useful right after the ninja prefab is instantiated so background stays in sync with UI.
    /// </summary>
    public static void EnsureBackgroundHiddenNow()
    {
        var uiRoot = GameObject.Find("UI");
        if (uiRoot == null) return;
        var host = uiRoot.GetComponent<FeverTimeUIController>();
        if (host == null) host = uiRoot.AddComponent<FeverTimeUIController>();
        if (!host.enabled) host.enabled = true;
        host.ResolveFeverBackground();
        host.HideFeverBackground();
    }
}
