using UnityEngine;

/// <summary>
/// 아이템에 대한 미리 저장해둘 데이터모델
/// </summary>
/// <remarks>
/// 아이템의 위치는 외부에서 지정한다.
/// </remarks>
public enum ItemType
{
    /// <summary>
    /// 지정 없음
    /// </summary>
    None = 0,

    /// <summary>
    /// HP 회복 아이템
    /// </summary>
    Heart,

    /// <summary>
    /// 피버 점수 아이템
    /// </summary>
    Star,

    /// <summary>
    /// 남은 시간 추가 아이템
    /// </summary>
    Clock,

}