using System;
using System.Collections.Generic;
using UnityEngine;

public class MiddlemanBase<T_Enum, T_Pool, T_Component> : MonoBehaviour 
    where T_Enum : struct, IComparable, IFormattable, IConvertible
    where T_Component : Component
    where T_Pool : ComponentPoolHandler<T_Component>
{
    [System.Serializable]
    private struct Entry
    {
        public T_Enum key;
        public T_Pool pool;
    }

    [SerializeField] private List<Entry> pools;

    public void InitializePool()
    {
        foreach (var pool in pools)
        {
            pool.pool.Initialize();
        }
    }

    public T_Component GetComponent(T_Enum searchKey)
    {
        var pool = pools.Find(x => x.key.Equals(searchKey)).pool;
        if (pool == null)
        {
            //Console‚Å‚Ç‚±‚ÅƒoƒO‚ð“f‚¢‚Ä‚¢‚é‚©‚í‚©‚è‚â‚·‚­‚·‚é
            Debug.LogError("Missing Pool or Key", this);
            return null;
        }
        return pool.GetComponentFromPool();
    }
}
