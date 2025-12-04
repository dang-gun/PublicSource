using UnityEngine;

/// <summary>
/// HP 회복 아이템에 대한 미리 저장해둘 데이터모델
/// </summary>
[System.Serializable]
public class ClockItemDataModel : ItemDataModelBase
{
    public ClockItemDataModel()
    {
        this.Name = "시간 추가";

        this.ItemType = ItemType.Clock;
        this.ItemValue = 5;
    }
}
