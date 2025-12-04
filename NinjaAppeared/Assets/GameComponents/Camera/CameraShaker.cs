using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 카메라 흔들림을 관리하는 컴포넌트.
/// 같은 프레임의 다른 스크립트가 카메라 위치를 변경해도
/// 기준 위치를 추적하여 그 위에 흔들림 오프셋만 더합니다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)] // 카메라 추적 스크립트들보다 늦게 실행되어 최종 오프셋이 적용되도록
public class CameraShaker : MonoBehaviour
{
    [Header("Duration & Strength")]
    [Tooltip("흔들림 지속 시간(초)")]
    public float duration = GameConstants.CameraShakeDurationSec; //0.2s

    [Tooltip("레벨1당 기본 진폭(월드 단위)")]
    public float amplitudePerLevel = 0.08f; // 낮은 레벨에서도 보이도록 기본값 상향

    [Range(1, 10)]
    [Tooltip("현재(또는 마지막) 흔들림 레벨(1~10)")]
    public int currentLevel = 1;

    [Header("Noise")]
    [Tooltip("노이즈 주파수(Hz)")]
    public float frequency = 25f;

    [Tooltip("세기 감쇠 곡선(0=시작,1=끝)")]
    public AnimationCurve envelope = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    // 진행 상태
    private float _timeLeft;
    private Vector3 _currentOffset;

    // 시드 (Perlin Noise)
    private float _seedX;
    private float _seedY;

    // 내부 버퍼
    private static readonly Vector2 _half = new Vector2(0.5f, 0.5f);

    // 등록된 모든 shaker (FindObjectOfType 제거용)
    private static readonly List<CameraShaker> _all = new List<CameraShaker>(2);

    private void Awake()
    {
        if (!_all.Contains(this)) _all.Add(this);
    }

    private void OnDestroy()
    {
        _all.Remove(this);
    }

    /// <summary>
    /// 레벨(0~9)로 흔들림 시작.0이면 흔들림 없음.
    /// </summary>
    public void Shake(int level)
    {
        if (level <= 0)
        {
            // 요청이0이면 아무 것도 하지 않음 (기존 흔들림 유지)
            return;
        }
        level = Mathf.Clamp(level, 1, 9);
        currentLevel = level;
        _timeLeft = Mathf.Max(0f, duration);

        // 새로운 시드로 시작
        _seedX = Random.value * 1000f + 1f;
        _seedY = Random.value * 1000f + 2f;
    }

    private void LateUpdate()
    {
        // 이전 프레임 기준 위치 확보(다른 스크립트의 이동 반영)
        var basePos = transform.localPosition - _currentOffset;

        if (_timeLeft > 0f)
        {
            float t = 1f - (_timeLeft / Mathf.Max(0.0001f, duration));
            float env = envelope != null ? Mathf.Clamp01(envelope.Evaluate(t)) : 1f - t;
            float amp = amplitudePerLevel * currentLevel * env;

            float tNoise = Time.unscaledTime * frequency;
            float nx = Mathf.PerlinNoise(_seedX, tNoise);
            float ny = Mathf.PerlinNoise(_seedY, tNoise);
            Vector2 n = new Vector2(nx, ny);
            Vector2 centered = (n - _half) * 2f; // [-1,1]

            _currentOffset = new Vector3(centered.x, centered.y, 0f) * amp;

            _timeLeft -= Time.unscaledDeltaTime;
        }
        else
        {
            // 끝났으면 오프셋 제거
            _currentOffset = Vector3.zero;
        }

        transform.localPosition = basePos + _currentOffset;
    }

    /// <summary>
    /// 씬 내 임의의 CameraShaker를 찾아 흔들림 실행(편의용).
    /// level <=0이면 아무 것도 하지 않음. 없으면 MainCamera에 자동 추가.
    /// </summary>
    public static void ShakeGlobal(int level)
    {
        if (level <= 0) return;

        // 기존 등록된 것 중 첫 번째 사용 (FindObjectOfType 사용 안 함)
        CameraShaker shaker = null;
        if (_all.Count > 0)
        {
            // 살아있는 첫번째 찾기
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i] != null)
                {
                    shaker = _all[i];
                    break;
                }
            }
        }

        if (shaker == null)
        {
            var cam = Camera.main != null ? Camera.main.gameObject : null;
            if (cam == null)
            {
                // 아무 메인 카메라도 없을 경우 활성 카메라 한번 스캔 (FindObjectOfType<Camera>는 허용? 요구가 CameraShaker만 제한된 경우) -> 제거 위해 Camera.allCameras 활용
                if (Camera.allCamerasCount > 0)
                {
                    cam = Camera.allCameras[0].gameObject;
                }
            }
            if (cam != null)
            {
                shaker = cam.GetComponent<CameraShaker>();
                if (shaker == null) shaker = cam.AddComponent<CameraShaker>();
            }
        }

        if (shaker != null)
            shaker.Shake(level);
    }
}
