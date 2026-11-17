using UnityEngine;

[CreateAssetMenu(menuName = "Game/MasterData/FireEffectColorData", fileName = "FireEffectColorData_New")]
public class FireEffectColorData : ScriptableObject
{
    [Tooltip("‚±‚ÌF‚ªA‚Ç‚ÌFireColor‚©w’è‚·‚é")]
    [SerializeField] private FireColor _color;
    [Tooltip("GradientEditor‚Åì‚Á‚½Gradient")]
    [SerializeField] private Gradient _gradient = new Gradient();

    public FireColor Color => _color;
    public Gradient Gradient => _gradient;
}
