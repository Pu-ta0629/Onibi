using UnityEngine;

public class HitBody
{
    public HitBody(object owner,Faction faction,float radius)
    {
        Owner = owner;
        Faction = faction;
        Radius = radius;
    }

    public object Owner { get; }
    public Faction Faction { get; }
    public float Radius { get; }
    public Vector3 Position { get; set; }
    public bool Active { get; set; } = true;
}
