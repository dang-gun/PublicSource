using UnityEngine;

/// <summary>
/// HP 회복 아이템에 대한 미리 저장해둘 데이터모델
/// </summary>
[System.Serializable]
public class HeartItemDataModel : ItemDataModelBase
{
    public HeartItemDataModel()
    {
        this.Name = "HP 증가";

        this.ItemType = ItemType.Heart;
        this.ItemValue = 1;
    }
}
