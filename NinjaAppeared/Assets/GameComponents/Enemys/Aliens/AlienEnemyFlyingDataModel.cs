using UnityEngine;

[System.Serializable]
public class AlienEnemyFlyingDataModel : EnemyDataModelBase
{
    public AlienEnemyFlyingDataModel()
    {
        this.Name = "날아가고 있는 적(0.60*0.37)";

        // 높이 범위 설정
        LocationY_Min = 5f;
        LocationY_Max = 8f;
    }
}
