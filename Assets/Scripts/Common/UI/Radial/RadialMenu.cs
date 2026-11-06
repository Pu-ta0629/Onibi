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

//using System.Collections.Generic;
//using TMPro;
//using UnityEngine;
//using UnityEngine.InputSystem;
//using UnityEngine.UI;

//[RequireComponent(typeof(SectorGraphic))]
//public class RadialMenu : MonoBehaviour
//{
//    public enum LabelType {TextMeshPro, Image}
//    private class Item
//    {
//        public string Label;
//        public Sprite Icon;
//        public System.Action OnSelected;
//    }

//    [Header("Design")]
//    [Tooltip("中心の穴の半径(Canvas単位)。0で穴のない円になる")]
//    [SerializeField, Range(0f, 500f)] private float _innerRadius = 50f;
//    [Tooltip("円の外側の半径(Canvas単位)")]
//    [SerializeField, Range(10f, 1000f)] private float _outerRadius = 180f;
//    [Tooltip("扇形どうしの隙間(度)")]
//    [SerializeField, Range(0f, 30f)] private float _gapAngle = 3f;
//    [SerializeField] private Color _normalColor;
//    [SerializeField] private Color _highlightColor;

//    [Header("Label")]
//    [SerializeField] private LabelType _labelType = LabelType.TextMeshPro;
//    [SerializeField] private TMP_FontAsset _fontAsset;
//    [SerializeField, Range(8f, 100f)] private float _fontSize = 24f;
//    [Tooltip("TextMeshPro用の表示サイズ(Canvas単位)")]
//    [SerializeField] private Vector2 _labelSize = new Vector2(160f, 60f);
//    [SerializeField, Range(8f, 300f)] private float _iconSize = 56f;

//    [Header("ActiveMenu")]
//    [Tooltip("中心に表示するTextMeshPro/Imageの大きさ(Canvas単位)。Inner Radiusの内側に収まる大きさにする")]
//    [SerializeField, Range(8f, 300f)] private float _activeSize = 80f;

//    private readonly List<Item> _items = new List<Item>();
//    private readonly List<SectorGraphic> _itemSectors = new List<SectorGraphic>();
//    private RectTransform _root;
//    private RectTransform _canvasRect;
//    private Canvas _rootCanvas;
//    private Camera _uiCamera;
//    private int _selectedIndex = -1;   // 今マウスが乗っている項目
//    private int _activeIndex = -1;     // 中心(ActiveMenu)に表示している項目。-1なら表示なし
//    private GameObject _activeMenuObject;
//    private TextMeshProUGUI _activeText;
//    private Image _activeIcon;
//    private bool _isOpen;
//    /// <summary><summart>開閉したときに通知する(trueで開いた、falseで閉じた)</summart>summary></summary>
//    public event System.Action<bool> OnOpenChanged;
//    private bool _isInitialized;

//    public bool isOpen => _isOpen;

//    #region Initialize
//    //public void Initialize()
//    public void Start()
//    {
//        if (_isInitialized)
//        {
//            Debug.LogWarning("[RadialMenu] Initialize : Already Initialized!! ");
//            return;
//        }

//        _root = (RectTransform)transform;

//        var parentCanvas = GetComponentInParent<Canvas>();
//        if (parentCanvas == null)
//        {
//            Debug.LogError("[RadialMenu] Initialize : Canvas is Null!!! ");
//            return;
//        }

//        _rootCanvas = parentCanvas.rootCanvas;
//        _canvasRect = (RectTransform)_rootCanvas.transform;

//        // Overlayはnullのまま、Screen Space - CameraはRender Cameraを使う
//        if (_rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
//        {
//            _uiCamera = _rootCanvas.worldCamera;
//            if (_uiCamera == null)
//            {
//                Debug.LogWarning("[RadialMenu] Initialize : Render Camera is Not Set!! Camera.main is used");
//                _uiCamera = Camera.main;
//            }
//        }

//        BuildItems();
//        BuildActiveMenu();   // 扇形より後に作って、手前に表示する
//        Close(false);
//        _isInitialized = true;
//    }
//    #endregion

//    #region ItemRegister;
//    /// <summary>Radialの項目を増やせる関数
//    /// <br/><param name="label">TextMeshProモードで表示する文字</param>
//    /// <br/><param name="icon">Imageモードで表示するスプライト</param>
//    /// <br/><param name="onSelected">選ばれた時に実行する関数</param>
//    /// </summary>
//    public void AddItem(string label, Sprite icon, System.Action onSelected)
//    {
//        if (onSelected == null)
//        {
//            Debug.LogWarning($"[RadialMenu] AddItem : onSelected is Null!! Label = {label}");
//        }

//        _items.Add(
//            new Item
//            {
//                Label = label,
//                Icon = icon,
//                OnSelected = onSelected
//            }
//            );

//#if UNITY_EDITOR
//        Debug.Log($"[RadialMenu] AddItem : Label = {label}, _items.Count = {_items.Count}");
//#endif

//        if (_isInitialized) RebuildItems();
//    }
//    /// <summary>中心に表示する項目を外から指定する。-1で表示なし</summary>
//    public void SetActiveItem(int index)
//    {
//        _activeIndex = (index >= 0 && index < _items.Count) ? index : -1;
//        RefreshActiveMenu();
//    }
//    #endregion

//    #region UnityEvent
//    private void Update()
//    {
//        if (!_isInitialized) return;

//        var mouse = Mouse.current;
//        if (mouse == null) return;

//        var right = mouse.rightButton;

//        // click start
//        if (right.wasPressedThisFrame) Open(mouse.position.ReadValue());

//        if (!_isOpen) return;

//        // holdding
//        if (right.isPressed) UpdateSelection(mouse.position.ReadValue());

//        // click end
//        if (right.wasReleasedThisFrame) Close(true);
//    }
//    #endregion

//    #region Build
//    // 円を項目数で等分した扇形を生成する
//    private void BuildItems()
//    {
//        int count = _items.Count;//項目数
//        if (count == 0) return;

//        float step = 360f / count;//分割数
//        float gap = count > 1 ? _gapAngle : 0f;//項目同士の間隔
//        float labelRadius = (_innerRadius + _outerRadius) * 0.5f;

//        for (int i = 0; i < count; i++)// 真上が0番目、時計回り
//        {
//            float centerAngle = i * step;

//            // 項目本体（扇形）
//            var itemObject = new GameObject("Object_Item", typeof(RectTransform), typeof(SectorGraphic));
//            itemObject.transform.SetParent(_root, false);
//            var itemRect = (RectTransform)itemObject.transform;
//            itemRect.anchoredPosition = Vector2.zero;
//            itemRect.sizeDelta = Vector2.one * _outerRadius * 2f;

//            var itemSector = itemObject.GetComponent<SectorGraphic>();
//            itemSector.color = _normalColor;
//            itemSector.raycastTarget = false;   // マウス判定は自前計算するので不要
//            itemSector.Setup(_innerRadius, _outerRadius, centerAngle - step * 0.5f + gap * 0.5f, centerAngle + step * 0.5f - gap * 0.5f);
//            _itemSectors.Add(itemSector);

//            // ラベルは扇形の中央に置く
//            float radian = centerAngle * Mathf.Deg2Rad;
//            Vector2 labelPosition = new Vector2(Mathf.Sin(radian), Mathf.Cos(radian)) * labelRadius;

//            // ラベル。表示方法はLabelTypeで切り替える
//            switch (_labelType)
//            {
//                case LabelType.TextMeshPro:
//                    {
//                        CreateTextLabel(itemObject.transform, _items[i], labelPosition);
//                        break;
//                    }
//                case LabelType.Image:
//                    {
//                        CreateImageLabel(itemObject.transform, _items[i], labelPosition);
//                        break;
//                    }
//            }
//        }
//    }

//    // TextMeshProラベルを生成
//    private void CreateTextLabel(Transform parent, Item item, Vector2 position)
//    {
//        var labelObject = new GameObject("Object_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
//        labelObject.transform.SetParent(parent, false);
//        var labelRect = (RectTransform)labelObject.transform;
//        labelRect.sizeDelta = _labelSize;
//        labelRect.anchoredPosition = position;

//        var label = labelObject.GetComponent<TextMeshProUGUI>();
//        if (_fontAsset != null) label.font = _fontAsset;   //Font適応
//        label.text = item.Label;                           //Text適応
//        label.fontSize = _fontSize;                        //サイズ適応
//        label.alignment = TextAlignmentOptions.Center;
//        label.color = Color.white;
//        label.raycastTarget = false;
//    }

//    //Imageラベル生成
//    private void CreateImageLabel(Transform parent, Item item, Vector2 position)
//    {
//        //アイコンが未設定の項目（Menuなど）は、何も表示されなくなるので、文字で代わりに表示する
//        if (item.Icon == null)
//        {
//            CreateTextLabel(parent, item, position);
//            return;
//        }

//        var iconObject = new GameObject("Object_Icon", typeof(RectTransform), typeof(Image));
//        iconObject.transform.SetParent(parent, false);
//        var iconRect = (RectTransform)iconObject.transform;
//        iconRect.sizeDelta = Vector2.one * _iconSize;
//        iconRect.anchoredPosition = position;   //扇形の中央に配置

//        var icon = iconObject.GetComponent<Image>();
//        icon.sprite = item.Icon;
//        icon.preserveAspect = true;
//        icon.raycastTarget = false;
//    }

//    // 中心(ActiveMenu)の表示用オブジェクトを生成する。表示方法はLabelTypeに合わせる
//    private void BuildActiveMenu()
//    {
//        switch (_labelType)
//        {
//            case LabelType.TextMeshPro:
//                {
//                    _activeMenuObject = new GameObject("Object_ActiveText", typeof(RectTransform), typeof(TextMeshProUGUI));
//                    _activeMenuObject.transform.SetParent(_root, false);
//                    var textRect = (RectTransform)_activeMenuObject.transform;
//                    textRect.sizeDelta = Vector2.one * _activeSize;
//                    textRect.anchoredPosition = Vector2.zero;

//                    _activeText = _activeMenuObject.GetComponent<TextMeshProUGUI>();
//                    if (_fontAsset != null) _activeText.font = _fontAsset;
//                    _activeText.fontSize = _fontSize;
//                    _activeText.alignment = TextAlignmentOptions.Center;
//                    _activeText.color = Color.white;
//                    _activeText.raycastTarget = false;
//                    break;
//                }
//            case LabelType.Image:
//                {
//                    _activeMenuObject = new GameObject("Object_ActiveIcon", typeof(RectTransform), typeof(Image));
//                    _activeMenuObject.transform.SetParent(_root, false);
//                    var iconRect = (RectTransform)_activeMenuObject.transform;
//                    iconRect.sizeDelta = Vector2.one * _activeSize;
//                    iconRect.anchoredPosition = Vector2.zero;

//                    _activeIcon = _activeMenuObject.GetComponent<Image>();
//                    _activeIcon.preserveAspect = true;
//                    _activeIcon.raycastTarget = false;
//                    break;
//                }
//        }
//    }

//    // 中心(ActiveMenu)の内容と表示/非表示を、選択中の項目に合わせて更新する
//    private void RefreshActiveMenu()
//    {
//        if (_activeMenuObject == null) return;

//        bool hasActive = _activeIndex >= 0 && _activeIndex < _items.Count;

//        if (hasActive)
//        {
//            var item = _items[_activeIndex];
//            if (_activeText != null) _activeText.text = item.Label;
//            if (_activeIcon != null)
//            {
//                _activeIcon.sprite = item.Icon;
//                _activeIcon.enabled = item.Icon != null;
//            }
//        }

//        // 開いている間だけ、選択中の項目がある時に表示する
//        _activeMenuObject.SetActive(_isOpen && hasActive);
//    }

//    // 既存の項目を破棄して作り直す
//    private void RebuildItems()
//    {
//        foreach (var itemSector in _itemSectors)
//        {
//            Destroy(itemSector.gameObject);
//        }
//        _itemSectors.Clear();

//        BuildItems();

//        // 作り直した扇形より手前に表示する
//        if (_activeMenuObject != null) _activeMenuObject.transform.SetAsLastSibling();

//        _selectedIndex = -1;
//        SetActiveAll(_isOpen);   // 開いている最中なら表示を維持する
//        Highlight(-1);
//        RefreshActiveMenu();
//    }
//    #endregion

//    #region OpenClose
//    private void Open(Vector2 mouseScreenPosition)
//    {
//        if (_items.Count == 0)
//        {
//            Debug.LogWarning("[RadialMenu] Open : Items is Empty!! ");
//            return;
//        }

//        _isOpen = true;
//        _selectedIndex = -1;   // 開いた直後は未選択
//        OnOpenChanged?.Invoke(true);

//        // 画面端で欠けないように中心を補正（Canvas単位 → 画面ピクセル）
//        //float margin = _outerRadius * _rootCanvas.scaleFactor;
//        //mouseScreenPosition.x = Mathf.Clamp(mouseScreenPosition.x, margin, Screen.width - margin);
//        //mouseScreenPosition.y = Mathf.Clamp(mouseScreenPosition.y, margin, Screen.height - margin);

//        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_canvasRect, mouseScreenPosition, _uiCamera, out Vector3 worldPosition))
//        {
//            _root.position = worldPosition;
//        }
//        else
//        {
//            Debug.LogWarning("[RadialMenu] Open : Failed to Convert Mouse Position!! ");
//        }

//        SetActiveAll(true);
//        Highlight(-1);
//        RefreshActiveMenu();   // 登録済みの項目を中心に表示
//    }

//    private void Close(bool execute)
//    {
//        bool wasOpen = _isOpen;

//        if (execute && _selectedIndex >= 0 && _selectedIndex < _items.Count)
//        {
//            // 登録済みの項目をもう一度選ぶと解除、別の項目を選ぶと切り替え
//            _activeIndex = (_selectedIndex == _activeIndex) ? -1 : _selectedIndex;

//#if UNITY_EDITOR
//            Debug.Log($"[RadialMenu] Close : _selectedIndex = {_selectedIndex}, _activeIndex = {_activeIndex}");
//#endif
//            _items[_selectedIndex].OnSelected?.Invoke();
//        }

//        _isOpen = false;
//        _selectedIndex = -1;
//        SetActiveAll(false);
//        RefreshActiveMenu();
//        if (wasOpen) OnOpenChanged?.Invoke(false);
//    }

//    private void SetActiveAll(bool active)
//    {
//        foreach (var itemSector in _itemSectors)
//        {
//            itemSector.gameObject.SetActive(active);
//        }
//    }
//    #endregion

//    #region Selection
//    // メニュー中心から見たマウスの角度で項目を選択する（扇形の上に乗っている必要はない）
//    // 中心の穴（InnerRadiusの内側）はDeadZone。そこにマウスがあるときは何も選択しない
//    private void UpdateSelection(Vector2 mouseScreenPosition)
//    {
//        int count = _items.Count;
//        if (count == 0) return;

//        // メニュー中心からの相対位置（Canvas単位）
//        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, mouseScreenPosition, _uiCamera, out Vector2 delta))
//        {
//            return;
//        }

//        int index = -1;   // DeadZoneの中は未選択
//        if (delta.magnitude >= _innerRadius)
//        {
//            // 真上を0度、時計回りに増える角度
//            float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
//            if (angle < 0f) angle += 360f;

//            index = Mathf.RoundToInt(angle / (360f / count)) % count;
//        }

//        // 選択が変わったときだけ表示を更新する
//        if (index != _selectedIndex) Highlight(index);
//    }

//    private void Highlight(int index)
//    {
//        _selectedIndex = index;
//        for (int i = 0; i < _itemSectors.Count; i++)
//        {
//            _itemSectors[i].color = (i == index) ? _highlightColor : _normalColor;
//            _itemSectors[i].rectTransform.localScale = (i == index) ? new Vector2(1.1f, 1.1f) : Vector2.one;
//        }
//    }
//    #endregion
//}