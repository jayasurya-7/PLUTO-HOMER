#region Includes
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#endregion

namespace TS.DoubleSlider
{
    [RequireComponent(typeof(RectTransform))]
    public class DoubleSlider : MonoBehaviour
    {
        #region Variables

        [Header("References")]
        [SerializeField] private SingleSlider _sliderMin;
        [SerializeField] private SingleSlider _sliderMax;
        [SerializeField] public Slider _currePostion;
        [SerializeField] private Text warningText;
        [SerializeField] private RectTransform _fillArea;
        [SerializeField] private RectTransform oldROMArea;

        [Header("Configuration")]
        [SerializeField] private bool _setupOnStart;
        [SerializeField] private float _minValue;
        [SerializeField] private float _maxValue;
        [SerializeField] private float _minDistance;
        [SerializeField] private bool _wholeNumbers;
        [SerializeField] private float _initialMinValue;
        [SerializeField] private float _initialMaxValue;

        [Header("Events")]
        public UnityEvent<float, float> OnValueChanged;

        public float minAng, maxAng;
        public bool UpdateMinMaxvalues;

        public PROMsceneHandler promSlider;
        public PROMsceneHandler aromSlider;

        public bool IsEnabled
        {
            get { return _sliderMax.IsEnabled && _sliderMin.IsEnabled; }
            set
            {
                _sliderMin.IsEnabled = value;
                _sliderMax.IsEnabled = value;
            }
        }

        public float MinValue => _sliderMin.Value;
        public float MaxValue => _sliderMax.Value;

        public SingleSlider SliderMin => _sliderMin;
        public SingleSlider SliderMax => _sliderMax;

        public bool WholeNumbers
        {
            get => _wholeNumbers;
            set
            {
                _wholeNumbers = value;
                _sliderMin.WholeNumbers = _wholeNumbers;
                _sliderMax.WholeNumbers = _wholeNumbers;
            }
        }

        public bool IsDisabled { get; internal set; }

        private RectTransform _fillRect;
        private RectTransform _oldROMRect;

        #endregion

        private void Awake()
        {
            _fillRect = _fillArea.transform.GetChild(0).transform as RectTransform;
            _oldROMRect = oldROMArea.transform.GetChild(0).transform as RectTransform;

            if (warningText != null)
                warningText.gameObject.SetActive(false);
        }

        private void Start()
        {
            _currePostion.gameObject.SetActive(true);
        }

        private void Update()
        {
            currentPositonUpdate();

            if (UpdateMinMaxvalues)
            {
                updateMinMaxVal();
            }
        }

        public void currentPositonUpdate()
        {
            _currePostion.value = PlutoComm.angle;
            float Currevalue = _currePostion.value;
        }

        public void updateMinMaxVal()
        {
            if (_currePostion.value < minAng)
            {
                minAng = Mathf.Clamp(_currePostion.value, _minValue, _maxValue);
                _sliderMin.setSliderVal(minAng);
            }
            if (_currePostion.value > maxAng)
            {
                maxAng = Mathf.Clamp(_currePostion.value, _minValue, _maxValue);
                _sliderMax.setSliderVal(maxAng);
            }
        }

        public void Setup(float minValue, float maxValue, float initialMinValue, float initialMaxValue)
        {
            _minValue = minValue;
            _maxValue = maxValue;
            _initialMinValue = initialMinValue;
            _initialMaxValue = initialMaxValue;

            _sliderMin.Setup(_initialMinValue, minValue, maxValue, MinValueChanged);
            _sliderMax.Setup(_initialMaxValue, minValue, maxValue, MaxValueChanged);

            MinValueChanged(_initialMinValue);
            MaxValueChanged(_initialMaxValue);

            _currePostion.minValue = minValue;
            _currePostion.maxValue = maxValue;

            OldROMRECT();
        }

        public void startAssessment(float val)
        {
            minAng = val;
            maxAng = val;
            _initialMinValue = val;
            _initialMaxValue = val;

            _sliderMin.Setup(_initialMinValue, _minValue, _maxValue, MinValueChanged);
            _sliderMax.Setup(_initialMaxValue, _minValue, _maxValue, MaxValueChanged);

            MinValueChanged(val);
            MaxValueChanged(val);

            _currePostion.minValue = _minValue;
            _currePostion.maxValue = _maxValue;

            // Hide old ROM reference during assessment (focus on new assessment)
            oldROMArea.gameObject.SetActive(false);
        }

        private void OldROMRECT()
        {
            float offset = ((MinValue - _minValue) / (_maxValue - _minValue)) * _fillArea.rect.width;
            _oldROMRect.offsetMin = new Vector2(offset, _oldROMRect.offsetMin.y);
            offset = (1 - ((MaxValue - _minValue) / (_maxValue - _minValue))) * _fillArea.rect.width;
            _oldROMRect.offsetMax = new Vector2(-offset, _oldROMRect.offsetMax.y);
        }

        private void MinValueChanged(float value)
        {
            float offset = ((MinValue - _minValue) / (_maxValue - _minValue)) * _fillArea.rect.width;
            _fillRect.offsetMin = new Vector2(offset, _fillRect.offsetMin.y);

            if ((MaxValue - value) < _minDistance)
            {
                _sliderMin.Value = MaxValue - _minDistance;
            }

            OnValueChanged.Invoke(MinValue, MaxValue);
            _sliderMin.transform.SetAsLastSibling();
        }

        private void MaxValueChanged(float value)
        {
            float offset = (1 - ((MaxValue - _minValue) / (_maxValue - _minValue))) * _fillArea.rect.width;
            _fillRect.offsetMax = new Vector2(-offset, _fillRect.offsetMax.y);

            if ((value - MinValue) < _minDistance)
            {
                _sliderMax.Value = MinValue + _minDistance;
            }

            OnValueChanged.Invoke(MinValue, MaxValue);
            _sliderMax.transform.SetAsLastSibling();
        }

        // ----- Cycle marker API (called from AROMsceneHandler) -----

        public void HideHandles()
        {
            _sliderMin.gameObject.SetActive(false);
            _sliderMax.gameObject.SetActive(false);
        }

        public void AddCycleMarker(float lo, float hi, Color color, int cycleNum = -1)
        {
            CreateLineMarker(lo, color, cycleNum);
            CreateLineMarker(hi, color, cycleNum);
        }

        public void ClearCycleMarkers()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("CycleMarker"))
                    Destroy(child.gameObject);
            }
        }

        public void ShowOldROM()
        {
            _oldROMRect.gameObject.SetActive(true);
        }

        private void CreateLineMarker(float angle, Color color, int cycleNum)
        {
            float n = (_maxValue == _minValue)
                ? 0.5f
                : Mathf.Clamp01((angle - _minValue) / (_maxValue - _minValue));

            // Create line marker
            var go = new GameObject("CycleMarker");
            go.transform.SetParent(transform, false);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color         = color;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin        = new Vector2(n, 0.05f);
            rt.anchorMax        = new Vector2(n, 0.95f);
            rt.sizeDelta        = new Vector2(3f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.SetAsLastSibling();

            // Add cycle number label above the line
            if (cycleNum > 0)
            {
                var labelGo = new GameObject($"CycleLabel_{cycleNum}");
                labelGo.transform.SetParent(go.transform, false);

                var textComp = labelGo.AddComponent<UnityEngine.UI.Text>();
                textComp.text = cycleNum.ToString();
                textComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                textComp.fontSize = 12;
                textComp.fontStyle = FontStyle.Bold;
                textComp.alignment = TextAnchor.MiddleCenter;
                textComp.color = color;
                textComp.raycastTarget = false;

                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0.5f, 1.0f);
                labelRt.anchorMax = new Vector2(0.5f, 1.0f);
                labelRt.sizeDelta = new Vector2(20f, 16f);
                labelRt.anchoredPosition = new Vector2(0f, 5f);
            }
        }
    }
}
