using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.1: placeholder modal shown when the book icon is clicked in the detail tooltip.
    /// Builds its own backdrop + panel + label + OK button in <see cref="EnsureBuilt"/>; called
    /// from both Awake and OnEnable (L-007 hot-reload safety). To be replaced with a real codex /
    /// recipe view in WS-014.
    /// </summary>
    public class RecipesComingSoonModal : MonoBehaviour
    {
        private RectTransform _selfRT;
        private GameObject _backdropGO;
        private GameObject _panelGO;
        private Button _okButton;
        private bool _built;

        private void Awake()
        {
            EnsureBuilt();
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EnsureBuilt();
            if (_okButton != null) _okButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            if (_okButton != null) _okButton.onClick.RemoveListener(Close);
        }

        public void Show()
        {
            EnsureBuilt();
            gameObject.SetActive(true);
            // Render above the detail tooltip's panel: the tooltip's ShowPanelAtCursor calls
            // SetAsLastSibling on its own panel, so without this we end up beneath it.
            transform.SetAsLastSibling();
            if (_panelGO != null) _panelGO.transform.SetAsLastSibling();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _selfRT = transform as RectTransform;
            if (_selfRT == null) return;

            // Make self full-screen so backdrop and centered panel can both anchor to us.
            _selfRT.anchorMin = Vector2.zero;
            _selfRT.anchorMax = Vector2.one;
            _selfRT.offsetMin = Vector2.zero;
            _selfRT.offsetMax = Vector2.zero;

            BuildBackdrop();
            BuildPanel();

            _built = true;
        }

        private void BuildBackdrop()
        {
            _backdropGO = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _backdropGO.transform.SetParent(_selfRT, false);
            var rt = (RectTransform)_backdropGO.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = _backdropGO.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.45f);
            img.raycastTarget = true;

            var click = _backdropGO.AddComponent<ModalBackdropClickHandler>();
            click.Owner = this;
        }

        private void BuildPanel()
        {
            _panelGO = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelGO.transform.SetParent(_selfRT, false);
            var rt = (RectTransform)_panelGO.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 140f);
            rt.anchoredPosition = Vector2.zero;

            var img = _panelGO.GetComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.14f, 0.98f);
            // L-006: filled-style image needs a sprite — we use Simple here so {fileID:0} is fine.
            img.raycastTarget = true;

            BuildLabel(rt);
            BuildOKButton(rt);
        }

        private void BuildLabel(RectTransform parent)
        {
            var labelGO = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGO.transform.SetParent(parent, false);
            var rt = (RectTransform)labelGO.transform;
            rt.anchorMin = new Vector2(0f, 0.4f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(12f, 0f);
            rt.offsetMax = new Vector2(-12f, -12f);

            var text = labelGO.GetComponent<Text>();
            text.text = "Recipes for this item — coming soon";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 18;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void BuildOKButton(RectTransform parent)
        {
            var btnGO = new GameObject("OKButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parent, false);
            var rt = (RectTransform)btnGO.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(96f, 32f);
            rt.anchoredPosition = new Vector2(0f, 12f);

            var img = btnGO.GetComponent<Image>();
            img.color = new Color(0.25f, 0.35f, 0.5f, 1f);
            img.raycastTarget = true;

            _okButton = btnGO.GetComponent<Button>();
            _okButton.targetGraphic = img;

            var labelGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGO.transform.SetParent(rt, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var text = labelGO.GetComponent<Text>();
            text.text = "OK";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
        }

        /// <summary>Click-outside-modal-panel handler attached to the backdrop only.</summary>
        private class ModalBackdropClickHandler : MonoBehaviour, IPointerClickHandler
        {
            public RecipesComingSoonModal Owner;
            public void OnPointerClick(PointerEventData eventData)
            {
                if (Owner != null) Owner.Close();
            }
        }
    }
}
