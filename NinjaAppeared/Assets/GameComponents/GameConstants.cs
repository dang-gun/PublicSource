using UnityEngine;

public static class GameConstants
{
    #region 개발자용 설정
    /// <summary>
    /// 디버그 설정 활성화 여부
    /// </summary>
    public const bool DebugIs = false;

    /// <summary>
    /// 바닥에 다으면 죽는 것을 막을지 여부
    /// </summary>
    public const bool FloorDeath_BlockIs = false;

    /// <summary>
    /// 수동 각도기 사용 여부
    /// </summary>
    public const bool ManualProtractorIs = false;

    /// <summary>
    /// 가상 박스 표시 여부 (디버그용)
    /// </summary>
    public const bool VirtualBoxShowIs = false;

    /// <summary>
    /// 닌자가 착지하는 바닥 높이를 선으로 표시할지 여부.
    /// </summary>
    public const bool ProtractorShowGroundLine = false;
    #endregion


    /// <summary>
    /// 카메라가 따라가는 스무딩 시간(초)
    /// </summary>
    public const float CameraFollowSmoothTime = 0.50f;

    /// <summary>
    /// 카메라 흔들림 지속 시간(초)
    /// </summary>
    public const float CameraShakeDurationSec = 0.20f;

    /// <summary>
    /// 월드 유닛을 미터로 환산하는 계수 (기본1유닛=1m)
    /// </summary>
    public const float MetersPerUnit = 1f;


    /// <summary>
    /// 칼질 효과 애니메이션 스케일 배수 (1.0 = 원본 크기)
    /// </summary>
    public const float KnifeWorkVFX_Scale = 3.0f;

    /// <summary>
    /// 닌자와 적 충돌 시 주변 적에게도 적용할 범위 피해 반경(월드 단위)
    /// </summary>
    public const float AoeKillRadius = 1.5f;
    //public const float AoeKillRadius = 4f;


    #region 콤보 UI
    /// <summary>
    /// 콤보 팝업의 월드 오프셋 X (적 기준, 월드 단위)
    /// </summary>
    public const float ComboPopupOffsetX = 0.4f;
    /// <summary>
    /// 콤보 팝업의 월드 오프셋 Y (적 기준, 월드 단위)
    /// </summary>
    public const float ComboPopupOffsetY = 0.4f;


    /// <summary>
    /// 콤보 팝업 위치에 적용되는 랜덤 범위 X (±값, 월드 단위)
    /// </summary>
    public const float ComboPopupJitterXRange = 0.30f;
    /// <summary>
    /// 콤보 팝업 위치에 적용되는 랜덤 범위 Y (±값, 월드 단위)
    /// </summary>
    public const float ComboPopupJitterYRange = 0.30f;
    #endregion


    #region 적 스폰 관련    

    /// <summary>
    /// 가상 박스(화면 기준) 너비 비율(0~1)
    /// </summary>
    public const float VirtualBoxViewportWidthFactor = 0.4f;
    /// <summary>
    /// 가상 박스(화면 기준) 높이 비율(0~1). 현재 X만 사용.
    /// </summary>
    public const float VirtualBoxViewportHeightFactor = 0.75f;

    /// <summary>
    /// 가상 박스의 화면 내 기준 위치 X (0=왼쪽,0.5=가운데,1=오른쪽)
    /// </summary>
    public const float VirtualBoxViewportAnchorX = 0.5f;
    /// <summary>
    /// 가상 박스의 화면 내 기준 위치 Y (0=아래,0.5=가운데,1=위쪽)
    /// </summary>
    public const float VirtualBoxViewportAnchorY = 0.50f;

    /// <summary>
    /// 이전 스폰 지점과의 최대 허용 직선거리(2D).0이하면 비활성.
    /// </summary>
    public const float EnemySpawnMaxDistanceFromPrevious = 8f;

    /// <summary>
    /// 거리 조건을 만족하기 위해 위치를 다시 뽑는 기본 재시도 횟수.
    /// </summary>
    public const int EnemySpawnSpacingRetryCount = 5;

    /// <summary>
    /// 기본 재시도 이후 추가로 시도할 임의 재시도 횟수.
    /// </summary>
    public const int EnemySpawnExtraRetriesAfterFail = 2;



    /// <summary>
    /// 이전 적이 상반부에 있을 때 추가로 허용할 거리(+1f 기본 규칙).
    /// </summary>
    public const float EnemySpawnUpperHalfExtraDistance = 1f;

    /// <summary>
    /// 이전 적이 상반부이고 현재 모델의 최대 Y가 하반부에 머무는 경우 추가로 허용할 거리(+3f 규칙).
    /// </summary>
    public const float EnemySpawnUpperHalfLowCeilingExtraDistance = 3f;

    /// <summary>
    /// 가상 박스(스폰 영역)들 사이 겹침을 피하거나 최소 거리를 확보하기 위한 패딩(월드 단위).0이하면 비활성.
    /// 스폰 박스를 계산할 때 이전 박스의 경계에서 이 값만큼 띄워 적용합니다.
    /// </summary>
    public const float VirtualBoxSpawnPadding = -2.5f;


    #endregion


    #region 자동 각도기 관련

    /// <summary>
    /// 닌자가 발사되는 초기 속도 (초당 월드 단위)
    /// </summary>
    public const float NinjaLaunchForce = 12f;


    /// <summary>
    /// 점프 각도기의 최대 각도(도). 기본90도.0~180도 범위를 권장.
    /// </summary>
    public const float ProtractorMaxAngleDegrees = 110f;

    /// <summary>
    /// 각도기 전체 스케일 배수 (1.0 = 원본 크기)
    /// </summary>
    public const float ProtractorScale = 0.90f;

    /// <summary>
    /// 각도기 중심의 투명한 원(홀) 반지름. 이 영역과 겹치는 바늘/선 내부는 렌더링하지 않습니다.
    ///0이하면 비활성.
    /// </summary>
    public const float ProtractorInnerHoleRadius = 1.4f;

    /// <summary>
    /// 바늘 최소 가시 길이(월드 단위). 홀 반지름이 커서 선이 점처럼 보이는 것을 방지.
    /// </summary>
    public const float ProtractorNeedleMinVisibleLength = 0.3f;

    /// <summary>
    /// 자동 각도기 초기 왕복 속도(초당 각도, deg/s)
    /// </summary>
    public const float AutoProtractorAngleSpeedDegPerSec = 90f;

    /// <summary>
    /// 기본 속도만 사용하는 스타일 상한(포함). 이 값까지는 추가 가속이 없습니다.
    /// 예:5면 스타일0~5는 기본 속도만,6부터 추가 가속 적용.
    /// </summary>
    public const int AutoProtractorBaseOnlyUpToStyleCount = 5;

    /// <summary>
    /// 스타일1당 추가되는 각도기 속도(자동 모드, deg/s). Count * 이 값이 가산됩니다.
    /// </summary>
    public const float AutoProtractorSpeedPerStyle_AddDegPerSec = 2.5f;


    /// <summary>
    /// 자동 각도기에서 스타일을 낮추기위해 필요한 카운트.
    /// </summary>
    /// <remarks>
    /// 1왕복이 2다. 즉, 0→ProtractorMaxAngleDegrees→0 이므로 4히트가 되면 2왕복으로 간주합니다. \
    /// 최대치/최소치 기준이라 중간부터 시작하면 왕복이 적어보이는 현상이 있어 홀수로 설정한다.
    /// </remarks>
    public const int AutoProtractorFullOscillationBounceCount = 5;
    #endregion

    #region 궤적 미리보기
    /// <summary>
    /// 궤적 시작 지점에서 숨길 길이(월드 단위).0이하이면 비활성.
    /// </summary>
    public const float TrajectoryPreviewSkipLength = 1.95f;

    /// <summary>
    /// 궤적 표시 최대 길이(월드 단위).0이하이면 제한 없음.
    /// </summary>
    public const float TrajectoryPreviewMaxLength = 16f;

    #endregion

}
