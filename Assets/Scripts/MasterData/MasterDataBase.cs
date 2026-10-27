using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;

public class MasterDataBase<T> where T : MasterData
{
    private readonly Dictionary<int, T> _byId = new();

    public MasterDataBase(IEnumerable<T> items)
    {
        foreach(var item in items)
        {
            if (item == null) continue;

            if (!_byId.TryAdd(item.Id, item))
                Debug.LogError($"[MasterData]{typeof(T).Name} : id {item.Id}が重複({item.name})", item);    
        }
    }

    public IReadOnlyCollection<T> All => _byId.Values;

    public bool TryGet(int id,out T data) => _byId.TryGetValue(id, out data);
}
