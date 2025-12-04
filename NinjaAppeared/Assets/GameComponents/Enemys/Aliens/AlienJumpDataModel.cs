using UnityEngine;

[System.Serializable]
public class AlienJumpDataModel : EnemyDataModelBase
{
    public AlienJumpDataModel()
    {
        this.Name = "기본 점프 하고있는 적(0.60*0.80)";

        // 높이 범위 설정
        LocationY_Min = 1.5f;
        LocationY_Max = 2f;
    }
}
