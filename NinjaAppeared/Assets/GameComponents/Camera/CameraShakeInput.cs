using UnityEngine;

/// <summary>
/// 숫자 키(1~9, 키패드 포함)로 카메라 흔들림 테스트 트리거.
/// </summary>
public class CameraShakeInput : MonoBehaviour
{
    [SerializeField] private CameraShaker _shaker; // 수동 할당 가능, 씬 검색(FindObjectOfType) 제거

    private void Awake()
    {
        // 디버그 비활성 시 테스트도 비활성화
        if (!GameConstants.DebugIs)
        {
            enabled = false;
            return;
        }

        // 기존 FindObjectOfType 제거: Inspector에서 설정하거나 MainCamera에서 확보/자동 추가
        if (_shaker == null && Camera.main != null)
        {
            _shaker = Camera.main.GetComponent<CameraShaker>();
            if (_shaker == null) _shaker = Camera.main.gameObject.AddComponent<CameraShaker>();
        }
    }

    private void Update()
    {
        if (_shaker == null) return;

        // 상단 숫자 키
        if (Input.GetKeyDown(KeyCode.Alpha1)) _shaker.Shake(1);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) _shaker.Shake(2);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) _shaker.Shake(3);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) _shaker.Shake(4);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) _shaker.Shake(5);
        else if (Input.GetKeyDown(KeyCode.Alpha6)) _shaker.Shake(6);
        else if (Input.GetKeyDown(KeyCode.Alpha7)) _shaker.Shake(7);
        else if (Input.GetKeyDown(KeyCode.Alpha8)) _shaker.Shake(8);
        else if (Input.GetKeyDown(KeyCode.Alpha9)) _shaker.Shake(9);

    }
}
