// 重ねて比較する Entity 1つ分(名前と、コライダーの一覧)
public sealed class CompareEntry
{
    public readonly string Label;
    public readonly ColliderSet Set;

    public CompareEntry(string label, ColliderSet set)
    {
        Label = label;
        Set = set;
    }
}