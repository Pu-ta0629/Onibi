using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    [SerializeField] protected GameObject m_prefab;
    [SerializeField] protected int m_poolCount;
    [Tooltip("PoolCountが上限に達したときに、再利用するか、新しく生成するか")]
    [SerializeField] protected bool m_forcePoolCount = true;

    protected List<GameObject> m_objectPool;
    protected List<int> m_inUseQue;

    private Transform m_transform;

    public int ObjectsInPool => m_objectPool.Count;

    public virtual void Initialize()
    {
        m_objectPool = new();
        m_inUseQue = new();
        m_transform = transform;
        for (int i = 0; i < m_poolCount; i++)
        {
            AddToPool();
        }
    }
    public GameObject GetObjectFromPool()
    {
        return GetFromPool(m_objectPool);
    }

    protected T GetFromPool<T>(List<T> list)
    {
        int index = GetIndexFromPool();
        T obj = list.ElementAt(index);
        m_inUseQue.Add(index);
        return obj;
    }
    protected int GetIndexFromPool()
    {
        var x = m_objectPool
            .Select((obj, index) => new { obj, index })
            .FirstOrDefault(x => !m_inUseQue.Contains(x.index));
        int index;
        if (x == null)
        {
            if (m_forcePoolCount)
            {
                index = m_inUseQue.First();
                m_inUseQue.RemoveAt(0);
            }
            else
            {
                index = AddToPool();
            }
        }
        else
        {
            index = x.index;
        }
        return index;
    }
    public void ReturnToPool(int index)
    {
        GameObject obj = m_objectPool.ElementAt(index);
        m_inUseQue.Remove(index);
        obj.transform.parent = m_transform;
        obj.gameObject.SetActive(false);
    }
    protected virtual int AddToPool()
    {
        GameObject obj = Instantiate(m_prefab, m_transform);
        obj.SetActive(false);
        
        int index = m_objectPool.Count();
        obj.name += index;

        if (!obj.TryGetComponent<ReturnObjectToPool>(out var rotp))
        {
            rotp = obj.AddComponent<ReturnObjectToPool>();
        }
        rotp.InjectPoolManager(this, index);
        m_objectPool.Add(rotp.gameObject);
        return index;
    }

    public IEnumerable<GameObject> GetActiveObjects()
    {
        if (m_inUseQue == null)
        {
            return Enumerable.Empty<GameObject>();
        }
        
        IEnumerable<GameObject> tmp = m_inUseQue.Select(i => m_objectPool.ElementAt(i));
        return tmp;
    }

}
