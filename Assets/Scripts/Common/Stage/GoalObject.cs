using UnityEngine;

/// <summary> ƒS[ƒ‹‚Ì–ÚˆóB”¼Œa‚ÍStageData‚ÌGoalPlacement‚©‚çó‚¯æ‚é </summary>
public class GoalObject : MonoBehaviour
{
    public float Radius {  get; private set; }

    public void Setup(float radius)
    {
        Radius = radius;

        transform.localScale = new Vector3(radius * 2f, transform.localScale.y, radius * 2);
    }
}
