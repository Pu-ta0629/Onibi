using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(SectorGraphic))]
public class RadialMenu : MonoBehaviour
{
    public enum LabelType { TextMeshPro, Image }

    private class Item
    {
        public string Label;
        public Sprite Icon;
        public Action OnSelected;
    }

    [Header("Design")]
    [Tooltip("中心の穴の半径(Canvas単位)。0で穴のない円になる")]
    [SerializeField, Range(0f, 500f)] private float _innerRadius = 50f;
    [Tooltip("円の外側の半径(Canvas単位)")]
    [SerializeField, Range(10f, 1000f)] private float _outerRadius = 180f;
    [Tooltip("扇形どうしの隙間(度)")]
    [SerializeField, Range(0f, 30f)] private float _gapAngle = 3f;
    [SerializeField] private Color _normalColor;
    [SerializeField] private Color _highlightColor;

    [Header("Label")]
    [SerializeField] private LabelType _labelType = LabelType.TextMeshPro;
    [SerializeField] private TMP_FontAsset _fontAsset;
    [SerializeField, Range(8f, 100f)] private float _fontSize = 24f;
    [Tooltip("TextMeshPro用の表示サイズ(Canvas単位)")]
    [SerializeField] private Vector2 _labelSize = new Vector2(160f, 60f);
    [SerializeField, Range(8f, 300f)] private float _iconSize = 56f;

    [Header("ActiveMenu")]
    [Tooltip("中心に表示するTextMeshPro/Imageの大きさ(Canvas単位)。Inner Radiusの内側に収まる大きさにする")]
    [SerializeField, Range(8f, 300f)] private float _activeSize = 80f;

    private readonly List<Item> _items = new List<Item>();
    private readonly List<SectorGraphic> _sectors = new List<SectorGraphic>();
    private RectTransform _root;
    private RectTransform _canvasRect;
    private Camera _uiCamera;
    private GameObject _activeLabel;   // 中心に表示しているラベル
    private int _selectedIndex = -1;   // 今マウスが乗っている項目
    private int _activeIndex = -1;     // 中心に表示する項目。-1なら表示なし
    private bool _isOpen;
    private bool _ready;

    /// <summary>開閉したときに通知する(trueで開いた、falseで閉じた)</summary>
    public event Action<bool> OnOpenChanged;
    public bool isOpen => _isOpen;

    #region Public
    /// <summary>項目を追加する</summary>
    /// <param name="label">TextMeshProモードで表示する文字</param>
    /// <param name="icon">Imageモードで表示するスプライト</param>
    /// <param name="onSelected">選ばれた時に実行する関数</param>
    public void AddItem(string label, Sprite icon, Action onSelected)
    {
        _items.Add(new Item { Label = label, Icon = icon, OnSelected = onSelected });
        if (_ready) Build();
    }

    /// <summary>中心に表示する項目を外から指定する。-1で表示なし</summary>
    public void SetActiveItem(int index)
    {
        _activeIndex = (index >= 0 && index < _items.Count) ? index : -1;
        RefreshActiveLabel();
    }
    #endregion

    #region Unity
    private void Start()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[RadialMenu] Canvas is Null!!");
            return;
        }

        _root = (RectTransform)transform;
        canvas = canvas.rootCanvas;
        _canvasRect = (RectTransform)canvas.transform;

        // Overlayはnullのまま、それ以外はRender Cameraを使う
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            _uiCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        Build();
        _ready = true;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (!_ready || mouse == null) return;

        var right = mouse.rightButton;
        var pos = mouse.position.ReadValue();

        if (right.wasPressedThisFrame) Open(pos);
        if (!_isOpen) return;
        if (right.isPressed) UpdateSelection(pos);
        if (right.wasReleasedThisFrame) Close();
    }
    #endregion

    #region Build
    // 円を項目数で等分した扇形を作り直す(真上が0番目、時計回り)
    private void Build()
    {
        foreach (var s in _sectors) Destroy(s.gameObject);
        _sectors.Clear();

        int count = _items.Count;
        float step = 360f / count;
        float gap = count > 1 ? _gapAngle : 0f;
        float labelRadius = (_innerRadius + _outerRadius) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float center = i * step;

            var go = new GameObject("Object_Item", typeof(RectTransform), typeof(SectorGraphic));
            go.transform.SetParent(_root, false);
            ((RectTransform)go.transform).sizeDelta = Vector2.one * _outerRadius * 2f;

            var sector = go.GetComponent<SectorGraphic>();
            sector.color = _normalColor;
            sector.raycastTarget = false;   // マウス判定は角度で自前計算する
            sector.Setup(_innerRadius, _outerRadius, center - step * 0.5f + gap * 0.5f, center + step * 0.5f - gap * 0.5f);
            _sectors.Add(sector);

            // ラベルは扇形の中央に置く
            float rad = center * Mathf.Deg2Rad;
            var pos = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * labelRadius;
            CreateLabel(go.transform, _items[i], pos, _labelSize, _iconSize);
        }

        SetSectorsActive(_isOpen);
        Highlight(-1);
        RefreshActiveLabel();
    }

    // LabelTypeに応じて文字かアイコンを作る(アイコン未設定の項目は文字で代用)
    private GameObject CreateLabel(Transform parent, Item item, Vector2 pos, Vector2 textSize, float iconSize)
    {
        var go = new GameObject("Object_Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = pos;

        if (_labelType == LabelType.Image && item.Icon != null)
        {
            rect.sizeDelta = Vector2.one * iconSize;
            var icon = go.AddComponent<Image>();
            icon.sprite = item.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        else
        {
            rect.sizeDelta = textSize;
            var text = go.AddComponent<TextMeshProUGUI>();
            if (_fontAsset != null) text.font = _fontAsset;
            text.text = item.Label;
            text.fontSize = _fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }
        return go;
    }

    // 中心のラベルを作り直す。開いていて、項目が選ばれているときだけ表示する
    private void RefreshActiveLabel()
    {
        if (_activeLabel != null) Destroy(_activeLabel);   // 後から作るので、扇形より手前に表示される
        if (_isOpen && _activeIndex >= 0 && _activeIndex < _items.Count)
            _activeLabel = CreateLabel(_root, _items[_activeIndex], Vector2.zero, Vector2.one * _activeSize, _activeSize);
    }

    private void SetSectorsActive(bool active)
    {
        foreach (var s in _sectors) s.gameObject.SetActive(active);
    }
    #endregion

    #region OpenClose
    private void Open(Vector2 screenPos)
    {
        if (_items.Count == 0) return;

        _isOpen = true;
        OnOpenChanged?.Invoke(true);

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_canvasRect, screenPos, _uiCamera, out var world))
            _root.position = world;

        SetSectorsActive(true);
        Highlight(-1);
        RefreshActiveLabel();
    }

    private void Close()
    {
        if (_selectedIndex >= 0)
        {
            // 登録済みの項目をもう一度選ぶと解除、別の項目を選ぶと切り替え
            _activeIndex = (_selectedIndex == _activeIndex) ? -1 : _selectedIndex;
            _items[_selectedIndex].OnSelected?.Invoke();
        }

        _isOpen = false;
        _selectedIndex = -1;
        SetSectorsActive(false);
        RefreshActiveLabel();
        OnOpenChanged?.Invoke(false);
    }
    #endregion

    #region Selection
    // メニュー中心から見たマウスの角度で選択する。InnerRadiusの内側はDeadZone(未選択)
    private void UpdateSelection(Vector2 screenPos)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPos, _uiCamera, out var delta)) return;

        int index = -1;
        if (delta.magnitude >= _innerRadius)
        {
            float angle = Mathf.Repeat(Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, 360f);   // 真上が0度、時計回り
            index = Mathf.RoundToInt(angle / (360f / _items.Count)) % _items.Count;
        }

        if (index != _selectedIndex) Highlight(index);
    }

    private void Highlight(int index)
    {
        _selectedIndex = index;
        for (int i = 0; i < _sectors.Count; i++)
        {
            bool on = i == index;
            _sectors[i].color = on ? _highlightColor : _normalColor;
            _sectors[i].rectTransform.localScale = on ? Vector3.one * 1.1f : Vector3.one;
        }
    }
    #endregion
}

