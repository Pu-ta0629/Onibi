using PlasticGui.WorkspaceWindow.Locks;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TMPro;
using Unity.Plastic.Antlr3.Runtime;
using Unity.VisualScripting;
using UnityEngine;

public readonly struct TestFrame
{
    public readonly AttackPhase Phase;  // 攻撃の段階
    public readonly bool Detected;      // 索敵が、案山子を見つけているか
    public readonly bool Hitting;       // 有効の間、攻撃のコライダーが、案山子に重なっているか

    public TestFrame(AttackPhase phase,bool detected,bool hitting)
    {
        Phase = phase;
        Detected = detected;
        Hitting = hitting;
    }
}

public sealed class TestEvent
{
    public readonly float Time;
    public readonly string Text;

    public TestEvent(float time,string text)
    {
        Time = time;
        Text = text;
    }
}

public sealed class EntityTestResult
{
    public readonly float Step;
    public readonly TestFrame[] Frames;
    public readonly List<TestEvent> Events;
    public readonly string Warning;     // 結果に関する注意(なければ空)

    public EntityTestResult(float step, TestFrame[] frames, List<TestEvent> events, string warning)
    {
        Step = step;
        Frames = frames;
        Events = events;
        Warning = warning;
    }

    public float Duration => (Frames.Length - 1) * Step;

    // 時刻に対応する、コマの番号
    public int IndexAt(float time)
    {
         return Mathf.Clamp(Mathf.RoundToInt(time / Step), 0, Frames.Length - 1);
    }
}

public sealed class EntityTestSetup
{
    public EnemyData Enemy;
    public EnemyAttack Attack;
    public ColliderSet ScarecrowSet;
    public Vector2 ScarecrowPosition;
    public Faction ScarecrowFaction;
    public float Duration = 20f;
}

public class EntityTestSimulation
{
    public const float Step = 1f / 60f;
    private const int MaxEvents = 300;

    public static EntityTestResult Run(EntityTestSetup setup)
    {
        var enemy = setup.Enemy;
        var attack = setup.Attack;
        var frameCount = Mathf.Max(2, Mathf.RoundToInt(setup.Duration / Step) + 1);

        if (!enemy.Colliders.TryFind(attack.ColliderKey, out var attackDef))
            return Failed(frameCount, $"攻撃のコライダー {attack.ColliderKey} が、コライダーの一覧にありません");

        var timeline = AttackTimeline.From(attack);
        if (timeline.Total <= 0f)
            return Failed(frameCount, "攻撃の時間(溜め、有効、硬直、クールタイム)が、すべて 0 です");

        var warnings = new List<string>();
        if (setup.ScarecrowFaction == Faction.None)
            warnings.Add("案山子の陣営が、決まりません(攻撃か索敵の、対象の陣営(Targets)を、設定してください)");
        if (attack.ActiveTime <= 0f)
            warnings.Add("有効時間が、0 以下です(攻撃のコライダーが、判定されません)");

        var searchDefs = new List<ColliderDef>();
        enemy.Colliders.Collect(ColliderRole.Search, searchDefs);

        //世界を作る。敵は、原点、前を向く。案山子は指定位置
        var world = new HitWorld();
        var scarecrowOwner = new object();

        using var entity = new ColliderRuntime(world, new object(), Faction.Enemy, enemy.Colliders);
        using var scarecrow = new ColliderRuntime(world, scarecrowOwner, setup.ScarecrowFaction, setup.ScarecrowSet);
        entity.SetTransform(Vector3.zero, 0f);
        scarecrow.SetTransform(new Vector3(setup.ScarecrowPosition.x, 0f, setup.ScarecrowPosition.y), 0f);

        var frames = new TestFrame[frameCount];
        var events = new List<TestEvent>();
        var hits = new List<HitBody>();

        var damage = enemy.CalDamage(attack);
        var totalDamage = 0f;
        var attacking = false;
        var attackStart = 0f;
        var hitCounted = false;
        var everDetected = false;
        var previousDetected = false;
        var previousPhase = AttackPhase.Ready;

        for (var i = 0; i < frameCount; i++)
        {
            var time = i * Step;

            // 索敵
            var detected = searchDefs.Count == 0 || IsDetected(searchDefs, world, hits, scarecrowOwner);
            everDetected |= detected;
            if (detected != previousDetected)
            {
                Add(events, time, detected ? "索敵に入った(案山子を、見つけた)" : "索敵から外れた");
                previousDetected = detected;
            }

            // 見つけたら、攻撃を開始
            if (!attacking && detected)
            {
                attacking = true;
                attackStart = time;
                hitCounted = false;
                Add(events, time, $"攻撃開始(溜め {attack.ChargeTime:0.##} 秒)");
            }

            var phase = AttackPhase.Ready;
            var hitting = false;

            if (attacking)
            {
                var elapsed = time - attackStart;
                phase = timeline.PhaseAt(elapsed);

                // 有効の間だけ、攻撃のコライダーを判定する
                if (phase == AttackPhase.Active)
                {
                    attackDef.Query(world, Vector3.zero, 0f, hits);
                    hitting = ContainsOwner(hits, scarecrowOwner);

                    // 1回の攻撃で、数えるのは1回だけ
                    if (hitting && !hitCounted)
                    {
                        hitCounted = true;
                        totalDamage += damage;
                        Add(events, time, $"当たった  ダメージ {damage:0.##}(合計 {totalDamage:0.##})");
                    }
                }

                // 段階が変わったときの記録
                if (phase != previousPhase)
                {
                    switch (phase)
                    {
                        case AttackPhase.Active:
                            Add(events, time, "有効開始(攻撃のコライダーの、判定を始めた)");
                            break;
                        case AttackPhase.Recovery:
                            Add(events, time, hitCounted ? "硬直開始" : "硬直開始(有効の間に、案山子に当たらなかった)");
                            break;
                        case AttackPhase.Cooldown:
                            Add(events, time, "クールタイム開始");
                            break;
                    }
                }

                // クールタイムが終わったら、待機に戻る
                if (elapsed >= timeline.Total)
                {
                    attacking = false;
                    phase = AttackPhase.Ready;
                    Add(events, time, "クールタイム終了(次の攻撃を、始められる)");
                }
            }
           

            previousPhase = phase;
            frames[i] = new TestFrame(phase, detected, hitting);
        }

        if (!everDetected)
            warnings.Add("案山子が、一度も索敵に入りませんでした(案山子を、索敵の範囲に動かしてください)");

        return new EntityTestResult(Step, frames, events, string.Join("\n", warnings));
    }

    // 案山子の陣営を、攻撃と索敵の、対象の陣営から、自動で選ぶ
    public static Faction PickScarecrowFaction(EnemyData enemy,ColliderDef attackDef)
    {
        var attackMask = attackDef != null ? attackDef.Targets : Faction.None;

        var searchDefs = new List<ColliderDef>();
        enemy.Colliders.Collect(ColliderRole.Search, searchDefs);

        var searchMask = Faction.None;
        foreach (var def in searchDefs) searchMask |= def.Targets;

        var common = attackMask & searchMask;
        return LowestFaction(common != Faction.None ? common : attackMask | searchMask);
    }

    //指定した陣営のうち、一番小さい1つ
    private static Faction LowestFaction(Faction mask)
    {
        var value = (int)mask;
        for (var bit = 1; bit <= 128; bit <<= 1)   // Faction は、byte 型の値
        {
            if ((value & bit) != 0) return (Faction)bit;
        }
        return Faction.None;
    }

    private static bool IsDetected(List<ColliderDef> searchDefs,HitWorld world,List<HitBody> hits,object scarecrowOwner)
    {
        foreach (var def in searchDefs)
        {
            def.Query(world, Vector3.zero, 0f, hits);
            if (ContainsOwner(hits, scarecrowOwner)) return true;
        }
        return false;
    }

    private static bool ContainsOwner(List<HitBody> hits,object owner)
    {
        foreach(var hit in hits)
        {
            if(hit.Owner == owner) return true;
        }
        return false;
    }

    private static void Add(List<TestEvent> events,float time,string text)
    {
        if (events.Count < MaxEvents) events.Add(new TestEvent(time, text));
    }

    private static EntityTestResult Failed(int frameCount,string warning)
    {
        return new EntityTestResult(Step, new TestFrame[frameCount], new List<TestEvent>(), warning);
    }


}
