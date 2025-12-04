using UnityEngine;

/// <summary>
/// 적에 대한 미리 저장해둘 데이터모델
/// </summary>
[System.Serializable]
public class EnemyDataModelBase
{
    /// <summary>
    /// 구분용 이름
    /// </summary>
    public string Name;

    /// <summary>
    /// 적 생성에 사용할지 여부
    /// </summary>
    public bool UseIs = true;

    [Header("Prefab")] //**************************
    /// <summary>
    /// 이 적을 생성할 때 사용할 프리팹
    /// </summary>
    [Tooltip("*필수* 복사하여 사용할 적 프리팹")]
    public GameObject EmenyPrefab;


    [Header("Sprite Setup")] //**************************
    /// <summary>
    /// 서있는 스프라이트
    /// </summary>
    [Tooltip("이 프리팹을 공유하는 스프라이트 리스트")]
    public Sprite[] SpriteList;



    [Header("Spawn Location Y Range")] //**************************
    /// <summary>
    /// 높이 범위 최소값 (바닥 기준0)
    /// </summary>
    [Tooltip("위치 Y 최소값")] public float LocationY_Min = 0f;
    /// <summary>
    /// 높이 범위 최대값 (바닥 기준0)
    /// </summary>
    [Tooltip("위치 Y 최대값")] public float LocationY_Max = 0f;


}
