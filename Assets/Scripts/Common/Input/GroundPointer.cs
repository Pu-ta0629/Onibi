// ゲーム本体のコードなので、Editor フォルダには入れない
using UnityEngine;
// マウスの位置を、地面の平面(XZ 平面)上の座標として求める。
// コライダーや物理演算は使わず、カメラから出した光線(Ray)と、数学的な平面の交点を計算するだけ。
public static class GroundPointer
{
    // 画面上のマウスの位置(Input System と、従来の Input の、どちらの設定でも動く)
    public static Vector2 ScreenPosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
        }
    }

    // マウスの位置にある、地面(高さ groundY の平面)の座標を求める
    public static bool TryGetMouseGroundPoint(Camera camera, float groundY, out Vector3 point)
    {
        return TryGetGroundPoint(camera, ScreenPosition, groundY, out point);
    }

    // 画面上の任意の位置から、地面の座標を求める
    public static bool TryGetGroundPoint(Camera camera, Vector2 screenPosition, float groundY, out Vector3 point)
    {
        point = default;
        if (camera == null) return false;

        var ray = camera.ScreenPointToRay(screenPosition);
        var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

        // 光線が平面と交わらないとき(水平線より上を指しているときなど)は、false
        if (!plane.Raycast(ray, out var distance)) return false;

        point = ray.GetPoint(distance);
        return true;
    }
}
