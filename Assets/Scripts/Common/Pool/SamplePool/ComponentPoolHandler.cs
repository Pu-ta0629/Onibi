using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ComponentPoolHandler<T> : ObjectPoolManager where T : Component
{
    protected List<T> m_objectComponent;

    public override void Initialize()
    {
        m_objectComponent = new();
        base.Initialize();
    }

    public T GetComponentFromPool()
    {
        return GetFromPool(m_objectComponent);
    }

    protected override int AddToPool()
    {
        int index = base.AddToPool();
        m_objectComponent.Add(m_objectPool.ElementAt(index).GetComponent<T>());
        return index;
    }

    public IEnumerable<T> GetActiveComponents()
    {
        if (m_inUseQue == null)
        {
            return Enumerable.Empty<T>();
        }

        IEnumerable<T> tmp = m_inUseQue.Select(i => m_objectComponent.ElementAt(i));
        return tmp;
    }
}
