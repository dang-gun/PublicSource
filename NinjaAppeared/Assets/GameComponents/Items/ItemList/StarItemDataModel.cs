using UnityEngine;

/// <summary>
/// HP 회복 아이템에 대한 미리 저장해둘 데이터모델
/// </summary>
[System.Serializable]
public class StarItemDataModel : ItemDataModelBase
{
    public StarItemDataModel()
    {
        this.Name = "피버 점수 추가";

        this.ItemType = ItemType.Star;
        this.ItemValue = 10;
    }
}
