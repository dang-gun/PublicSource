using UnityEngine;

[System.Serializable]
public class AlienDuckDataModel : EnemyDataModelBase
{
    public AlienDuckDataModel()
    {
        this.Name = "기본 앉아있는 적(0.60*0.65)";

        // 높이 범위 설정
        LocationY_Min = 0.5f;
        LocationY_Max = 1f;
    }
}
