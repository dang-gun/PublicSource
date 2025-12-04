using UnityEngine;

/// <summary>
/// 아이템에 대한 미리 저장해둘 데이터모델
/// </summary>
/// <remarks>
/// 아이템의 위치는 외부에서 지정한다.
/// </remarks>
[System.Serializable]
public class ItemDataModelBase
{
    /// <summary>
    /// 구분용 이름
    /// </summary>
    public string Name;

    /// <summary>
    /// 서있는 스프라이트
    /// </summary>
    /// <remarks>
    /// 나중에 애니메이션이 추가될 수 있으므로 배열로 둠.
    /// </remarks>
    [Tooltip("아이템으로 사용할 스프라이트")]
    public Sprite[] SpriteList;





    /// <summary>
    /// 아이템 타입
    /// </summary>
    public ItemType ItemType = ItemType.None;

    /// <summary>
    /// 이 데이터가 처리될때 사용될 값
    /// </summary>
    public int ItemValue { get; set; }
}
