using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;

/// <summary>
/// 전체화면 전환 버튼 컨트롤러.
/// WebGL 포함, 에디터/스탠드얼론에서 버튼 클릭 시 전체화면/창모드 전환.
/// WebGL에서는 브라우저 전체화면 API를 직접 호출 (jslib)하여 첫 클릭에도 바로 반응하도록 처리.
/// </summary>
[RequireComponent(typeof(Button))]
public class FullScreenToggleButton : MonoBehaviour
{
    private bool _isFullScreen;
    private Button _button; // 해당 오브젝트의 Button

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void JSRequestFullscreen();
    [DllImport("__Internal")] private static extern void JSExitFullscreen();
#endif

    private void Awake()
    {
        _isFullScreen = Screen.fullScreen;
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (_button == null) _button = GetComponent<Button>();
        _button.onClick.AddListener(ToggleFullScreen); // 런타임에 클릭 이벤트 연결
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(ToggleFullScreen); // 해제
    }

    public void ToggleFullScreen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: 브라우저 API 직접 호출 (Unity Screen.fullScreen 첫 클릭 미반응 문제 대응)
        if (!_isFullScreen)
        {
            try { JSRequestFullscreen(); } catch { /* ignore */ }
        }
        else
        {
            try { JSExitFullscreen(); } catch { /* ignore */ }
        }
        // 실제 상태는 다음 프레임에서 갱신될 수 있으므로 코루틴으로 재확인
        StartCoroutine(RefreshStateNextFrame());
        Debug.Log($"[FullScreenToggleButton] Click(WebGL JS) -> Request={(!_isFullScreen)}");
#else
        _isFullScreen = !_isFullScreen;
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow; // 안정적 전체화면 모드 사용
        Screen.fullScreen = _isFullScreen; // Standalone/Editor 즉시 반영
        Debug.Log($"[FullScreenToggleButton] Click -> FullScreen={_isFullScreen}");
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private System.Collections.IEnumerator RefreshStateNextFrame()
    {
        yield return null; // 한 프레임 대기 후 상태 반영
        _isFullScreen = Screen.fullScreen; // Unity가 내부 플래그 업데이트한 값 반영
        // 일부 브라우저에서는 즉시 true로 되지 않을 수 있으므로 한 번 더 딜레이 체크
        if (!_isFullScreen)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            _isFullScreen = Screen.fullScreen;
        }
        Debug.Log($"[FullScreenToggleButton] WebGL Actual FullScreen State={_isFullScreen}");
    }
#endif
}
