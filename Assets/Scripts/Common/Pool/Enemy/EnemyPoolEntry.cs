using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敵の種族ごとのプール設定（Inspectorで設定する）。EnemyPoolManagerが配列で持つ
/// 種族（EnemyType）はEnemyDataのTypeから決まるので、Prefab・Data・数だけを設定する
/// </summary>
[Serializable]
public class EnemyPoolEntry
{
    [Tooltip("敵のPrefab。NavMeshAgentを付けておく（Agentのチェックは外しておくと、生成時の警告が出ない）")]
    [SerializeField] private NavMeshAgent _prefab;

    [Tooltip("この敵のマスターデータ。種族（EnemyType）は、ここのType（EnemyTypeData）から決まる")]
    [SerializeField] private EnemyData _data;

    [Tooltip("プールの数（この種族が同時に出られる最大数）")]
    [SerializeField, Range(1, 200)] private int _poolCount = 10;

    public NavMeshAgent prefab => _prefab;
    public EnemyData data => _data;
    public int poolCount => _poolCount;

    /// <summary>Prefab・Data・Dataの種族がそろっているか</summary>
    public bool isValid => _prefab != null && _data != null && _data.Type != null;

    /// <summary>この設定の種族（isValidがfalseのときは意味を持たない）</summary>
    public EnemyType type => isValid ? _data.Type.enemyType : EnemyType.Kappa;
}
