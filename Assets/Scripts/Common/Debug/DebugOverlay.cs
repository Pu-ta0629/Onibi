using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// デバッグ表示（OnGUI）。マウス操作・選択中の火・迷子の追従先・各火の固定Stateを画面に表示する
/// F1で表示／非表示を切り替える
/// このクラスだけで完結する。シーンに置かなくても起動時に自動生成され、
/// InitializeManager（TypeManagerの取得元）/ InputManager / FireManager をシーンから探して接続する
/// （InitializeManagerからのInitialize呼び出しは不要）
/// </summary>
public class DebugOverlay : MonoBehaviour
{
    private const int LogMax = 8;               //操作ログの表示件数
    private const float FlashSeconds = 1f;      //「更新された」を強調表示する時間
    private const float PanelWidth = 620f;
    private const float FindInterval = 0.5f;    //参照がそろうまで探し直す間隔

    private struct LogEntry
    {
        public float Time;
        public string Text;
    }

    [Serializable]
    public class IconEntry
    {
        public FireColor Color;
        public Sprite Icon;
    }

    [Header("Display")]
    [SerializeField] private bool _isVisible = true;
    [SerializeField, Range(10, 40)] private int _fontSize = 16;
    [SerializeField] private Vector2 _position = new Vector2(10f, 10f);
    [Tooltip("火のアイコン。設定のない火は、色付きの四角で表示する（省略可）")]
    [SerializeField] private IconEntry[] _icons;

    [Header("References（未設定ならシーンから自動で探す）")]
    [SerializeField] private InitializeManager _initializeManager;
    [SerializeField] private InputManager _inputManager;
    [SerializeField] private FireManager _fireManager;

    private readonly List<LogEntry> _log = new List<LogEntry>();
    private TypeManager _typeManager;   //純粋なC#クラス。InitializeManagerから受け取る
    private GUIStyle _labelStyle;
    private GUIStyle _headerStyle;
    private float _followChangedTime = -100f;
    private int _followChangeCount;
    private float _nextFindTime;
    private bool _isInitialized;

    #region AutoCreate
    /// <summary>
    /// シーンにDebugOverlayが無ければ、起動時に自動で生成する
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<DebugOverlay>() != null)
        {
            return;
        }

        GameObject obj = new GameObject("DebugOverlay");
        obj.AddComponent<DebugOverlay>();
        DontDestroyOnLoad(obj);
    }
    #endregion

    #region UnityLifeCycle
    private void Update()
    {
        if (!_isInitialized)
        {
            TryConnect();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            _isVisible = !_isVisible;
        }

        //ホイールは、回した瞬間だけ値が出るのでログに残す
        if (Mouse.current != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (scrollY != 0f)
            {
                AddLog(scrollY > 0f ? "ホイール ↑" : "ホイール ↓");
            }
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnGUI()
    {
        if (!_isInitialized || !_isVisible)
        {
            return;
        }

        EnsureStyles();

        GUILayout.BeginArea(new Rect(_position.x, _position.y, PanelWidth, Screen.height - _position.y * 2f));
        GUILayout.BeginVertical(GUI.skin.box);
        DrawMouse();
        DrawSelectedFire();
        DrawFollow();
        DrawFireStates();
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    #endregion

    #region Connect
    //必要な参照をシーンから探して、全部そろったらイベントを購読する
    private void TryConnect()
    {
        if (Time.unscaledTime < _nextFindTime)
        {
            return;
        }

        _nextFindTime = Time.unscaledTime + FindInterval;

        if (_initializeManager == null) { _initializeManager = FindFirstObjectByType<InitializeManager>(); }
        if (_inputManager == null) { _inputManager = FindFirstObjectByType<InputManager>(); }
        if (_fireManager == null) { _fireManager = FindFirstObjectByType<FireManager>(); }

        if (_initializeManager == null || _inputManager == null || _fireManager == null)
        {
            return;
        }

        //TypeManagerはInitializeManager.Awakeで生成される。FireInventoryはFireManager.Initializeで生成される
        _typeManager = _initializeManager.typeManager;
        if (_typeManager == null || _fireManager.fireInventory == null)
        {
            return;
        }

        _inputManager.OnLeftClick += HandleLeftClick;
        _inputManager.OnMiddleClick += HandleMiddleClick;
        _inputManager.OnRightClickDown += HandleRightClickDown;
        _inputManager.OnRightClickUp += HandleRightClickUp;
        _typeManager.OnFollowTargetChanged += HandleFollowTargetChanged;
        _typeManager.OnFollowModeChanged += HandleFollowModeChanged;
        _typeManager.OnFireStateChanged += HandleFireStateChanged;
        _isInitialized = true;
    }

    private void Disconnect()
    {
        if (!_isInitialized)
        {
            return;
        }

        _isInitialized = false;

        if (_inputManager != null)
        {
            _inputManager.OnLeftClick -= HandleLeftClick;
            _inputManager.OnMiddleClick -= HandleMiddleClick;
            _inputManager.OnRightClickDown -= HandleRightClickDown;
            _inputManager.OnRightClickUp -= HandleRightClickUp;
        }

        if (_typeManager != null)
        {
            _typeManager.OnFollowTargetChanged -= HandleFollowTargetChanged;
            _typeManager.OnFollowModeChanged -= HandleFollowModeChanged;
            _typeManager.OnFireStateChanged -= HandleFireStateChanged;
        }
    }
    #endregion

    #region Draw
    private void DrawMouse()
    {
        GUILayout.Label("■ マウス操作（F1で表示切替）", _headerStyle);

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            GUILayout.Label($"ボタン : 左 {Dot(mouse.leftButton.isPressed)}　中 {Dot(mouse.middleButton.isPressed)}　右 {Dot(mouse.rightButton.isPressed)}", _labelStyle);
        }

        GUILayout.Label($"ラジアル : {(_typeManager.IsRadialOpen ? "<color=#66FF66>開いている</color>" : "閉じている")}", _labelStyle);

        string ground = _inputManager.TryGetGroundPoint(out Vector3 point) ? $"X {point.x:F1} / Z {point.z:F1}" : "なし";
        GUILayout.Label($"マウス位置（地面） : {ground}", _labelStyle);

        GUILayout.Label("操作ログ（新しい順）", _labelStyle);
        for (int i = _log.Count - 1; i >= 0; i--)
        {
            GUILayout.Label($"　{Time.unscaledTime - _log[i].Time:F1}秒前　{_log[i].Text}", _labelStyle);
        }
    }

    private void DrawSelectedFire()
    {
        GUILayout.Label("■ 選択中の火", _headerStyle);
        DrawFireLine("選択中", _typeManager.SelectedColor);
        DrawFireLine("最後に選択", _typeManager.LastSelectedColor);
    }

    private void DrawFollow()
    {
        GUILayout.Label("■ 迷子の追従", _headerStyle);
        DrawFireLine("追従先", _typeManager.FollowTarget);
        GUILayout.Label($"モード : {ModeName(_typeManager.FollowMode)}", _labelStyle);

        DarknessSensor darkness = _fireManager.darkness;
        if (darkness != null)
        {
            GUILayout.Label($"周りの光量 : {darkness.LightLevel:F1}（{(darkness.IsDark ? "<color=#FF6666>暗い：追従停止</color>" : "<color=#66FF66>明るい</color>")}）", _labelStyle);
        }

        bool isFlash = Time.unscaledTime - _followChangedTime < FlashSeconds;
        GUILayout.Label($"追従先の更新 : {(isFlash ? "<color=#FFFF66>更新された!</color>" : "－")}（計{_followChangeCount}回）", _labelStyle);
    }

    private void DrawFireStates()
    {
        GUILayout.Label("■ 各火のState", _headerStyle);

        if (_fireManager.fireInventory == null)
        {
            return;
        }

        foreach (FireColor color in _fireManager.fireInventory.ownedColors)
        {
            FireState state = _typeManager.GetFireState(color);

            GUILayout.BeginHorizontal();
            DrawIcon(color);
            GUILayout.Label(color.ToString(), _labelStyle, GUILayout.Width(80f));
            GUILayout.Label($"選択 {Flag(state.IsSelected)}", _labelStyle, GUILayout.Width(90f));
            GUILayout.Label($"固定 {Flag(state.IsFixed)}", _labelStyle, GUILayout.Width(90f));
            GUILayout.Label($"明るさ {Level(state.Brightness)}", _labelStyle, GUILayout.Width(150f));
            GUILayout.Label(color == _typeManager.FollowTarget ? "<color=#FFFF66>＞ 追従先</color>" : "", _labelStyle);
            GUILayout.EndHorizontal();
        }
    }

    //「タイトル : [アイコン] 名前」を1行で表示する
    private void DrawFireLine(string title, FireColor color)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{title} :", _labelStyle, GUILayout.Width(100f));
        DrawIcon(color);
        GUILayout.Label(color == FireColor.None ? "なし" : color.ToString(), _labelStyle);
        GUILayout.EndHorizontal();
    }

    //火のアイコンを表示する。アイコンの設定がなければ、火の色の四角を表示する
    private void DrawIcon(FireColor color)
    {
        float size = _fontSize + 8f;
        Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        if (color == FireColor.None)
        {
            return;
        }

        Sprite icon = FindIcon(color);
        if (icon != null)
        {
            Texture2D texture = icon.texture;
            Rect area = icon.textureRect;
            GUI.DrawTextureWithTexCoords(rect, texture, new Rect(area.x / texture.width, area.y / texture.height, area.width / texture.width, area.height / texture.height));
            return;
        }

        Color previous = GUI.color;
        GUI.color = TypeManager.ToUnityColor(color);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
    #endregion

    #region Private
    private void EnsureStyles()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false };
            _headerStyle = new GUIStyle(_labelStyle) { fontStyle = FontStyle.Bold };
        }

        _labelStyle.fontSize = _fontSize;
        _headerStyle.fontSize = _fontSize + 2;
    }

    private Sprite FindIcon(FireColor color)
    {
        if (_icons == null)
        {
            return null;
        }

        for (int i = 0; i < _icons.Length; i++)
        {
            if (_icons[i] != null && _icons[i].Color == color)
            {
                return _icons[i].Icon;
            }
        }

        return null;
    }

    private void AddLog(string text)
    {
        _log.Add(new LogEntry { Time = Time.unscaledTime, Text = text });
        if (_log.Count > LogMax)
        {
            _log.RemoveAt(0);
        }
    }

    private static string Dot(bool isPressed)
    {
        return isPressed ? "<color=#FF6666>●</color>" : "○";
    }

    private static string Flag(bool isOn)
    {
        return isOn ? "<color=#66FF66>ON</color>" : "<color=#888888>OFF</color>";
    }

    //明るさを「Lv2 ■■□」の形で表示する
    private static string Level(FireBrightness brightness)
    {
        int level = (int)brightness + 1;
        string bar = new string('■', level) + new string('□', 3 - level);
        return $"<color=#FFDD66>Lv{level} {bar}</color>";
    }

    private static string ModeName(LostChildFollowMode mode)
    {
        switch (mode)
        {
            case LostChildFollowMode.UpdateTarget: return "随時更新する";
            case LostChildFollowMode.KeepTarget: return "更新しない（対象固定）";
            default: return "対象なし";
        }
    }
    #endregion

    #region Handler
    private void HandleLeftClick() { AddLog("左クリック（固定／固定解除の切替）"); }
    private void HandleMiddleClick() { AddLog("中クリック（追従モードの切替）"); }
    private void HandleRightClickDown() { AddLog("右クリック押下（ラジアルを開く）"); }
    private void HandleRightClickUp() { AddLog("右クリック離す（ラジアルを閉じる）"); }

    private void HandleFollowTargetChanged(FireColor color)
    {
        _followChangedTime = Time.unscaledTime;
        _followChangeCount++;
        AddLog($"追従先を更新 → {color}");
    }

    private void HandleFollowModeChanged(LostChildFollowMode mode)
    {
        AddLog($"追従モード → {ModeName(mode)}");
    }

    private void HandleFireStateChanged(FireColor color, FireState state)
    {
        AddLog($"{color} : 選択 {(state.IsSelected ? "ON" : "OFF")} / 固定 {(state.IsFixed ? "ON" : "OFF")} / 明るさ Lv{(int)state.Brightness + 1}");
    }
    #endregion
}