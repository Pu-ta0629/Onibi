using UnityEngine;

public class ReturnObjectToPool : MonoBehaviour
{
    private ObjectPoolManager m_poolManager;
    private int m_poolIndex;
    bool m_isPrimed;

    /// <summary>
    /// Inject = ’“ü
    /// PoolManager‚ğReturnObjectToPool‚É’“ü‚µ‚Ä‚¢‚é
    /// </summary>
    public void InjectPoolManager(ObjectPoolManager poolManager, int poolIndex)
    {
        m_poolManager = poolManager;
        m_poolIndex = poolIndex;
        m_isPrimed = true;
    }

    public void ReturnToPool()
    {
        if (!m_isPrimed) return;
        m_poolManager.ReturnToPool(m_poolIndex);
    }
}
