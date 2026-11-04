
/// <summary>
/// コライダー役割
/// </summary>
public enum ColliderRole : byte
{
    Hit = 0,    
    Attack = 1,
    Search = 2,
}

public static class ColliderRules
{
    public static bool IsBody(ColliderRole role) => role == ColliderRole.Hit;

    public static bool NeedsTargets(ColliderRole role) => !IsBody(role);

    public static bool Allows(ColliderRole role,ColliderShapeKind kind)
    {
        return !IsBody(role) || kind != ColliderShapeKind.Sector;
    }
}