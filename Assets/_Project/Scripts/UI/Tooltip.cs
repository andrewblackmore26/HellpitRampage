using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    public class Tooltip : MonoBehaviour
    {
        public static Tooltip Instance { get; private set; }

        [Header("UI wiring")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _panelRT;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _rarityLabel;
        [SerializeField] private Image _rarityBar;
        [SerializeField] private Text _statsLabel;
        [SerializeField] private Text _descriptionLabel;
        [SerializeField] private Text _synergiesLabel;
        [SerializeField] private Canvas _canvas;

        [Header("Behavior")]
        [SerializeField] private float _hoverDelay = 0.3f;
        [SerializeField] private Vector2 _cursorOffset = new(20f, 10f);

        private bool _pending;
        private float _pendingTimer;
        private TooltipContent _pendingContent;
        private bool _shown;

        private void OnEnable()
        {
            // L-007: OnEnable assigns Instance idempotently so a domain reload during Play
            // (which skips Awake) still leaves Tooltip.Instance non-null.
            if (Instance != null && Instance != this) return;
            Instance = this;
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDisable()
        {
            if (_panel != null) _panel.SetActive(false);
            _pending = false;
            _shown = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RequestShow(TooltipContent content)
        {
            _pending = true;
            _pendingTimer = _hoverDelay;
            _pendingContent = content;
        }

        public void Hide()
        {
            _pending = false;
            _shown = false;
            if (_panel != null) _panel.SetActive(false);
        }

        private void Update()
        {
            if (_pending && !_shown)
            {
                _pendingTimer -= Time.unscaledDeltaTime;
                if (_pendingTimer <= 0f) { Display(_pendingContent); _shown = true; }
            }
            if (_shown) PositionAtCursor();
        }

        private void Display(TooltipContent content)
        {
            if (_panel == null) return;
            _panel.SetActive(true);
            if (_titleLabel != null) _titleLabel.text = content.Title;
            if (_rarityLabel != null) _rarityLabel.text = content.RarityLabel;
            if (_rarityBar != null) _rarityBar.color = content.RarityColor;
            if (_statsLabel != null) _statsLabel.text = content.StatLines;
            if (_descriptionLabel != null) _descriptionLabel.text = content.Description;
            if (_synergiesLabel != null)
            {
                if (string.IsNullOrEmpty(content.SynergiesText))
                {
                    _synergiesLabel.gameObject.SetActive(false);
                }
                else
                {
                    _synergiesLabel.gameObject.SetActive(true);
                    _synergiesLabel.text = content.SynergiesText;
                }
            }
            _panel.transform.SetAsLastSibling();
        }

        private void PositionAtCursor()
        {
            if (_canvas == null || _panelRT == null) return;
            RectTransform canvasRT = _canvas.transform as RectTransform;
            if (canvasRT == null) return;

            // L-002: legacy Input.mousePosition is dead in this project (Active Input Handling =
            // Input System Package). Read the new Input System's mouse position instead.
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, mousePos, null, out Vector2 local);

            Vector2 offset = _cursorOffset;
            Vector2 size = _panelRT.sizeDelta;
            Vector2 canvasSize = canvasRT.rect.size;

            if (local.x + offset.x + size.x > canvasSize.x * 0.5f) offset.x = -offset.x - size.x;
            if (local.y + offset.y + size.y > canvasSize.y * 0.5f) offset.y = -offset.y - size.y;

            _panelRT.anchoredPosition = local + offset;
        }
    }
}
