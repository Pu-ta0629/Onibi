using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// 動作テストの欄(敵用)。案山子、攻撃、再生、時刻のスライダー、時間の帯、ログを出す
public sealed class EntityTestPanel : VisualElement
{
    private const string AutoLabel = "自動";
    private static readonly string[] SpeedLabels = { "0.25x", "0.5x", "1x", "2x" };
    private static readonly float[] SpeedValues = { 0.25f, 0.5f, 1f, 2f };

    // 案山子を指定しなかったときに使う、既定のコライダー(半径 0.5m の円の体)
    private static readonly ColliderSet DefaultScarecrowSet = new ColliderSet();

    private readonly EnemyData _enemy;
    private readonly ColliderCanvas _canvas;
    private readonly TestOverlay _overlay = new TestOverlay();
    private readonly List<Faction> _factionChoices = new List<Faction>();

    private readonly ObjectField _scarecrowField;
    private readonly DropdownField _factionField;
    private readonly DropdownField _attackField;
    private readonly DropdownField _speedField;
    private readonly FloatField _durationField;
    private readonly Button _playButton;
    private readonly Slider _slider;
    private readonly Label _timeLabel;
    private readonly AttackTimelineBar _bar;
    private readonly HelpBox _warning;
    private readonly ScrollView _logScroll;
    private readonly Label _log;

    private EntityTestResult _result;
    private float _time;
    private bool _playing;
    private float _speed = 1f;
    private double _lastTick;
    private int _dirtyCount = -1;
    private int _scarecrowDirtyCount = -1;
    private int _visibleEvents = -1;

    public EntityTestPanel(EnemyData enemy, ColliderCanvas canvas)
    {
        _enemy = enemy;
        _canvas = canvas;

        style.marginTop = 8;
        style.paddingLeft = 6;
        style.paddingRight = 6;

        var title = new Label("動作テスト(案山子に対して、索敵と攻撃を行う)");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        Add(title);

        // 案山子と、陣営
        _scarecrowField = new ObjectField("案山子") { objectType = typeof(ScarecrowData), allowSceneObjects = false };
        _scarecrowField.tooltip = "空のときは、半径 0.5m の円の、案山子を使う";
        _scarecrowField.RegisterValueChangedCallback(_ => Rebuild());
        Add(_scarecrowField);

        var factionLabels = new List<string> { AutoLabel };
        foreach (Faction value in Enum.GetValues(typeof(Faction)))
        {
            var bits = (int)value;
            if (bits == 0 || (bits & (bits - 1)) != 0) continue;   // 1つの陣営だけを選ぶ(複数の組み合わせと、None は除く)

            _factionChoices.Add(value);
            factionLabels.Add(value.ToString());
        }

        _factionField = new DropdownField("案山子の陣営", factionLabels, 0);
        _factionField.tooltip = "自動: 攻撃と索敵の、対象の陣営(Targets)に合わせる";
        _factionField.RegisterValueChangedCallback(_ => Rebuild());
        Add(_factionField);

        // 攻撃と、テストの長さ
        _attackField = new DropdownField("攻撃", new List<string> { "Attack1", "Attack2" }, 0);
        _attackField.RegisterValueChangedCallback(_ => Rebuild());
        Add(_attackField);

        _durationField = new FloatField("テストの長さ(秒)") { value = 20f };
        _durationField.tooltip = "5 から 60 秒";
        _durationField.RegisterValueChangedCallback(_ => Rebuild());
        Add(_durationField);

        // 再生の操作
        var controls = new VisualElement();
        controls.style.flexDirection = FlexDirection.Row;
        controls.style.marginTop = 4;

        _playButton = new Button(TogglePlay) { text = "再生" };
        controls.Add(_playButton);
        controls.Add(new Button(ResetTime) { text = "最初に戻す" });

        _speedField = new DropdownField(new List<string>(SpeedLabels), 2);
        _speedField.style.minWidth = 70;
        _speedField.RegisterValueChangedCallback(_ => _speed = SpeedValues[Mathf.Max(0, _speedField.index)]);
        controls.Add(_speedField);
        Add(controls);

        // 時刻のスライダーと、時間の帯
        _slider = new Slider("時刻", 0f, 20f) { showInputField = false };
        _slider.RegisterValueChangedCallback(evt =>
        {
            _time = evt.newValue;
            ApplyTime();
        });
        Add(_slider);

        _timeLabel = new Label();
        Add(_timeLabel);

        _bar = new AttackTimelineBar();
        Add(_bar);
        Add(BuildLegend());

        _warning = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        _warning.style.display = DisplayStyle.None;
        Add(_warning);

        // ログ
        _logScroll = new ScrollView();
        _logScroll.style.height = 120;
        _logScroll.style.marginTop = 4;
        _logScroll.style.backgroundColor = new Color(0f, 0f, 0f, 0.2f);
        _log = new Label();
        _log.style.whiteSpace = WhiteSpace.Normal;
        _logScroll.Add(_log);
        Add(_logScroll);

        // 図との連携
        canvas.SetTestOverlay(_overlay);
        canvas.ScarecrowMoved += OnScarecrowMoved;
        RegisterCallback<DetachFromPanelEvent>(_ =>
        {
            canvas.ScarecrowMoved -= OnScarecrowMoved;
            canvas.SetTestOverlay(null);
        });

        _lastTick = EditorApplication.timeSinceStartup;
        schedule.Execute(Tick).Every(16);
        schedule.Execute(CheckChanges).Every(250);

        Rebuild();
        _dirtyCount = EditorUtility.GetDirtyCount(_enemy);
    }

    // ---- 計算と表示 ----

    private EnemyAttack CurrentAttack() => _attackField.index == 1 ? _enemy.Attack2 : _enemy.Attack1;

    // テストの結果を、作り直す
    private void Rebuild()
    {
        var attack = CurrentAttack();
        _enemy.Colliders.TryFind(attack.ColliderKey, out var attackDef);

        var scarecrow = _scarecrowField.value as ScarecrowData;
        var scarecrowSet = scarecrow != null ? scarecrow.Colliders : DefaultScarecrowSet;

        var faction = _factionField.index <= 0
            ? EntityTestSimulation.PickScarecrowFaction(_enemy, attackDef)
            : _factionChoices[_factionField.index - 1];

        _overlay.ScarecrowSet = scarecrowSet;

        var setup = new EntityTestSetup
        {
            Enemy = _enemy,
            Attack = attack,
            ScarecrowSet = scarecrowSet,
            ScarecrowPosition = _overlay.ScarecrowPosition,
            ScarecrowFaction = faction,
            Duration = Mathf.Clamp(_durationField.value, 5f, 60f),
        };
        _result = EntityTestSimulation.Run(setup);

        _slider.highValue = _result.Duration;
        _time = Mathf.Min(_time, _result.Duration);
        _bar.SetResult(_result);

        _warning.text = _result.Warning;
        _warning.style.display = string.IsNullOrEmpty(_result.Warning) ? DisplayStyle.None : DisplayStyle.Flex;

        _visibleEvents = -1;
        ApplyTime();
    }

    // いまの時刻の状態を、スライダー、帯、図、ログに反映する
    private void ApplyTime()
    {
        if (_result == null) return;

        var frame = _result.Frames[_result.IndexAt(_time)];

        _overlay.Phase = frame.Phase;
        _overlay.Detected = frame.Detected;
        _overlay.Hitting = frame.Hitting;
        _overlay.ActiveAttackKey = frame.Phase == AttackPhase.Active ? CurrentAttack().ColliderKey : null;

        _slider.SetValueWithoutNotify(_time);
        _bar.SetTime(_time);
        _timeLabel.text = $"{_time:0.00} / {_result.Duration:0.#} 秒    段階: {PhaseLabel(frame.Phase)}    索敵: {(frame.Detected ? "見つけた" : "見つけていない")}";

        // ログは、いまの時刻までの記録だけを出す
        var visible = 0;
        while (visible < _result.Events.Count && _result.Events[visible].Time <= _time + 1e-4f) visible++;

        if (visible != _visibleEvents)
        {
            _visibleEvents = visible;

            var builder = new StringBuilder();
            for (var i = 0; i < visible; i++)
                builder.AppendLine($"{_result.Events[i].Time:0.00} 秒   {_result.Events[i].Text}");

            _log.text = builder.ToString();
            _logScroll.schedule.Execute(() => _logScroll.scrollOffset = new Vector2(0f, 100000f));   // 最後の行まで、スクロールする
        }

        _canvas.MarkDirtyRepaint();
    }

    // ---- 再生 ----

    private void TogglePlay()
    {
        _playing = !_playing;
        _playButton.text = _playing ? "一時停止" : "再生";
    }

    private void ResetTime()
    {
        _time = 0f;
        ApplyTime();
    }

    // 16 ミリ秒ごとに、再生中なら、時刻を進める(最後まで進んだら、最初に戻って、繰り返す)
    private void Tick()
    {
        var now = EditorApplication.timeSinceStartup;
        var delta = (float)(now - _lastTick);
        _lastTick = now;

        if (!_playing || _result == null) return;

        _time += delta * _speed;
        if (_time >= _result.Duration) _time = 0f;

        ApplyTime();
    }

    // 敵や案山子のデータが変わったら、結果を作り直す
    private void CheckChanges()
    {
        var dirty = EditorUtility.GetDirtyCount(_enemy);
        var scarecrow = _scarecrowField.value as ScarecrowData;
        var scarecrowDirty = scarecrow != null ? EditorUtility.GetDirtyCount(scarecrow) : 0;

        if (dirty == _dirtyCount && scarecrowDirty == _scarecrowDirtyCount) return;

        _dirtyCount = dirty;
        _scarecrowDirtyCount = scarecrowDirty;
        Rebuild();
    }

    // 図の上で、案山子を動かしたとき
    private void OnScarecrowMoved() => Rebuild();

    // ---- 凡例 ----

    private static VisualElement BuildLegend()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;

        foreach (AttackPhase phase in Enum.GetValues(typeof(AttackPhase)))
            row.Add(LegendItem(AttackTimelineBar.PhaseColor(phase), PhaseLabel(phase)));

        row.Add(LegendItem(ColliderColors.Contact, "当たった"));
        return row;
    }

    private static VisualElement LegendItem(Color color, string text)
    {
        var item = new VisualElement();
        item.style.flexDirection = FlexDirection.Row;
        item.style.marginRight = 12;

        var swatch = new VisualElement();
        swatch.style.width = 12;
        swatch.style.height = 12;
        swatch.style.marginTop = 2;
        swatch.style.marginRight = 4;
        swatch.style.backgroundColor = color;

        item.Add(swatch);
        item.Add(new Label(text));
        return item;
    }

    private static string PhaseLabel(AttackPhase phase)
    {
        switch (phase)
        {
            case AttackPhase.Charge: return "溜め";
            case AttackPhase.Active: return "有効";
            case AttackPhase.Recovery: return "硬直";
            case AttackPhase.Cooldown: return "クールタイム";
            default: return "待機";
        }
    }
}