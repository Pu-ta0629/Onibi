using UnityEngine;

// 動作テストの状態を、図に出すための入れ物
public sealed class TestOverlay
{
    public bool Visible = true;
    public Vector2 ScarecrowPosition = new Vector2(0f, 3f);   // 世界の座標(x は右、y は前。m)
    public ColliderSet ScarecrowSet;                           // 案山子のコライダー
    public AttackPhase Phase;                                  // いまの攻撃の段階
    public bool Detected;                                      // 索敵が、案山子を見つけているか
    public bool Hitting;                                       // 攻撃のコライダーが、案山子に当たっているか
    public string ActiveAttackKey;                             // 有効の間は、その攻撃のコライダーの名前(それ以外は null)
}