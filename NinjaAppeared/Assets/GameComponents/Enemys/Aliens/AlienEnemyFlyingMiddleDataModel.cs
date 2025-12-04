using UnityEngine;

[System.Serializable]
public class AlienEnemyFlyingMiddleDataModel : EnemyDataModelBase
{
    public AlienEnemyFlyingMiddleDataModel()
    {
        this.Name = "날아가고(중간) 있는 적(0.60*0.37)";

        // 높이 범위 설정
        LocationY_Min = 2.5f;
        LocationY_Max = 3.5f;
    }
}
