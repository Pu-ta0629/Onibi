using UnityEngine;

/// <summary>
/// 火の周回の計算（純粋なC#クラス）。中心の周りの円周上を、一定の速さで回る目標点を返す
/// 動かすのはMover側。ここは「次にどの点へ向かうか」だけを決める
/// </summary>
public class FireOrbit
{
    private float _angle;   //周回中の角度（度）

    /// <param name="startAngle">最初の角度（度）。火ごとにずらして重ならないようにする</param>
    public FireOrbit(float startAngle)
    {
        _angle = startAngle;
    }

    /// <summary>
    /// 毎フレーム呼ぶ。角度を進めて、円周上の目標点を返す
    /// </summary>
    /// <param name="center">周回の中心（迷子の座標）</param>
    /// <param name="radius">円の半径</param>
    /// <param name="speed">周回の速さ（度/秒）</param>
    public Vector3 Step(Vector3 center, float radius, float speed)
    {
        _angle = Mathf.Repeat(_angle + speed * Time.deltaTime, 360f);

        float radian = _angle * Mathf.Deg2Rad;
        return center + new Vector3(Mathf.Cos(radian), 0f, Mathf.Sin(radian)) * radius;
    }
}
