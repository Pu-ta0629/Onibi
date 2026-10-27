using System.ComponentModel;
using UnityEngine;


public abstract class MasterData : ScriptableObject
{
    [Header("基本")]
    [Tooltip("ID(種類ごとに、重複しない1001以上の整数。Tools>MasterData>Newから作ると、自動振り分け)")]
    [SerializeField] int _id;

    public int Id => _id;

    public const string IdField = nameof(_id);
}
