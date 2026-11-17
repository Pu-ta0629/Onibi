//using UnityEngine;

//public class FireEffectBinder
//{
//    private readonly FireRegistry _registry;

//    public FireEffectBinder(FireRegistry registry)
//    {
//        _registry = registry;
//    }

//    //i‰»‚È‚Ç‚ÅState‚ÌKind‚ª•Ï‚í‚Á‚½‚ÉŒÄ‚Ô
//    public void Apply(FireColor color, FireStateData data)
//    {
//        if (_registry.TryGet(color, out FireController fireController))
//            fireController.ApplyEffect(data.FireKind);
//    }
//}
