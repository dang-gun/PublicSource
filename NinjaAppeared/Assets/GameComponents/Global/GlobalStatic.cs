using UnityEngine;

public static class GlobalStatic
{
    /// <summary>
    /// 닌자 개체
    /// </summary>
    public static GameObject Ninja = null;
    /// <summary>
    /// 닌자의 트랜스폼
    /// </summary>
    public static Transform NinjaTf = null;

    /// <summary>
    /// 닌자의 HP 시스템
    /// </summary>
    public static HealthController Ninja_Health = null;
}
