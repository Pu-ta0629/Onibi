using System.Collections.Generic;
using UnityEngine;

// 形を、Inspector で変えながら、当たりを目で確認する
// 使い方: 空の GameObject に付け、targets に、いくつかの Transform を入れて、再生する。
//         この GameObject の位置と向き(青い軸)が、攻撃の起点と向きになる。
//         当たっている対象は赤い円、当たっていない対象は白い円で表示される。
public class HitShapeDemo : MonoBehaviour
{
    [SerializeField] HitShape shape = HitShape.Sector(4f, 90f);
    [SerializeField] Transform[] targets;
    [SerializeField] float targetRadius = 0.5f;

    HitWorld _world;
    readonly List<HitBody> _bodies = new();
    readonly List<HitBody> _hits = new();

    void Start()
    {
        _world = new HitWorld();
        foreach (var target in targets)
        {
            var body = new HitBody(target, Faction.Enemy, targetRadius);
            _world.Add(body);
            _bodies.Add(body);
        }
    }

    void Update()
    {
        for (var i = 0; i < _bodies.Count; i++)
            _bodies[i].Position = targets[i].position;

        _world.Query(shape, transform.position, transform.forward, Faction.Enemy, _hits);

        HitDebug.Draw(shape, transform.position, transform.forward, Color.yellow);
        foreach (var body in _bodies)
            HitDebug.DrawCircle(body.Position, body.Radius, _hits.Contains(body) ? Color.red : Color.white);
    }
}