using UnityEngine;

/// <summary>
/// EnemyTypeから、その種類のクラス（Enemy_???を作成）
/// 新しい種類を足したときは、ここにclassを足す
/// </summary>
public static class EnemyFactory
{
    public static EnemyController Create(EnemyType type)
    {
        switch(type)
        {
            case EnemyType.Kappa: return new Enemy_Kappa();
            case EnemyType.Oni: return new Enemy_Oni();
            case EnemyType.Dog: return new Enemy_Dog();
            case EnemyType.Cat: return new Enemy_Cat();
            default:
                Debug.LogWarning($"[EnemyController] Create : UnKnown EnemyType = {type}.Enemy_Kappa is used");
                return new Enemy_Kappa();
        }
    }
}