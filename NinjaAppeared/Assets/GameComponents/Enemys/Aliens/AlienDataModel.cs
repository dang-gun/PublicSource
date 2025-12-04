using UnityEngine;

[System.Serializable]
public class AlienDataModel : EnemyDataModelBase
{
    public AlienDataModel()
    {
        this.Name = "기본 서있는 적(0.60*0.80)";

        // 높이 범위 설정
        this.LocationY_Min = 0.5f;
        this.LocationY_Max = 1.0f;
    }
}
