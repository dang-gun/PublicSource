using UnityEngine;

/// <summary>
/// State holder placed on the root Transform of each spawned enemy instance.
/// Used to robustly determine if an enemy was killed (dead) even if children change.
/// </summary>
public class EnemyRootState : MonoBehaviour
{
    public bool IsDead;
}
