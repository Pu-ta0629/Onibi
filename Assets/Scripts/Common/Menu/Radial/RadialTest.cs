using UnityEngine;

public class RadialTest : MonoBehaviour
{
    [Header("Target")]
    [SerializeField, Tooltip("項目を登録するRadialMenu")] private RadialMenu _radialMenu;

    [Header("Plus")]
    [SerializeField] private string _plusLabel = "Plus";
    [SerializeField, Tooltip("Imageモード用のアイコン")] private Sprite _plusIcon;

    [Header("Minus")]
    [SerializeField] private string _minusLabel = "Minus";
    [SerializeField, Tooltip("Imageモード用のアイコン")] private Sprite _minusIcon;

    [Header("ResetCount")]
    [SerializeField] private string _resetLabel = "Reset";
    [SerializeField, Tooltip("Imageモード用のアイコン")] private Sprite _resetIcon;

    private int _count;

    public void Start()
    {
        if (_radialMenu == null)
        {
            Debug.LogError("[RadialTest] Start : RadialMenu is Null!!! ");
            return;
        }

        _radialMenu.AddItem(_plusLabel, _plusIcon, Red);
        _radialMenu.AddItem(_minusLabel, _minusIcon, Green);
        _radialMenu.AddItem(_resetLabel, _resetIcon, Blue);
    }
    public void Red()
    {
        Debug.Log(_count);
    }
    public void Green()
    {
        Debug.Log(_count);
    }
    public void Blue()
    {
        Debug.Log(_count);
    }
}