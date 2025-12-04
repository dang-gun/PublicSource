using UnityEngine;

/// <summary>
/// Simple global game stats for the current run.
/// Tracks enemy spawned/killed counts and can be reset on game reset.
/// </summary>
public static class GameStats
{
    public static int EnemiesSpawned { get; private set; }
    public static int EnemiesKilled { get; private set; }

    // 누적 콤보 점수(콤보가3 이상일 때 콤보가 추가될 때마다 +1)
    public static int ComboScoreAccumulated { get; private set; }

    // 누적 스타일 점수(스타일 레벨이2 이상일 때 스타일 카운터가 증가할 때마다 +1)
    public static int StyleScoreAccumulated { get; private set; }

    public static void IncrementEnemiesSpawned()
    {
        // guard against overflow
        if (EnemiesSpawned < int.MaxValue) EnemiesSpawned++;
    }

    public static void IncrementEnemiesKilled()
    {
        if (EnemiesKilled < int.MaxValue) EnemiesKilled++;
    }

    /// <summary>
    /// 콤보 누적 점수 +1 (외부에서 콤보가 증가하여 "현재 콤보 >=3"인 경우에만 호출)
    /// </summary>
    public static void IncrementComboScore()
    {
        if (ComboScoreAccumulated < int.MaxValue)
        {
            ComboScoreAccumulated++;
            if (true == GameConstants.DebugIs)
            {
                Debug.Log($"Combo Score Incremented: {ComboScoreAccumulated}");
            }

        }
    }

    /// <summary>
    /// 스타일 누적 점수 +1 (외부에서 스타일 카운터가 증가하고, 현재 스타일 레벨이 >=2 인 경우에만 호출)
    /// </summary>
    public static void IncrementStyleScore()
    {
        if (StyleScoreAccumulated < int.MaxValue)
        {
            StyleScoreAccumulated++;
            //Debug.Log($"Style Score Incremented: {StyleScoreAccumulated}");
        }
    }

    public static void Reset()
    {
        EnemiesSpawned = 0;
        EnemiesKilled = 0;
        ComboScoreAccumulated = 0;
        StyleScoreAccumulated = 0;
    }
}
