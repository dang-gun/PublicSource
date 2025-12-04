using UnityEngine;

/// <summary>
/// 칼질 리소스 관리 데이터모델
/// </summary>
[System.Serializable]
public class KnifeWork_ResourcesDataModel
{
    /// <summary>
    /// 칼질 애니용 스프라이트들
    /// </summary>
    [Tooltip("칼질 애니용 스프라이트들")]
    public Sprite[] Sprites;

    /// <summary>
    /// 이 애니가 사용될때 사용할 사운드들
    /// </summary>
    [Tooltip("이 애니가 사용될때 사용할 사운드들")]
    public AudioClip[] AudioClips;
}
