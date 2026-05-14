using HellpitRampage.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.5: button in BottomBar that toggles between Items and Bags drag modes.
    /// Click forwards to DragModeService.Toggle(); Tab key works too (handled by the service).
    /// The label refreshes on DragModeChangedEvent so the displayed mode always matches state.
    /// </summary>
    public class ModeToggleButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Text _label;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(HandleClick);
        }

        private void OnEnable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Subscribe<DragModeChangedEvent>(HandleModeChanged);
            Refresh();
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.Unsubscribe<DragModeChangedEvent>(HandleModeChanged);
        }

        private void HandleClick()
        {
            if (DragModeService.Current != null) DragModeService.Current.Toggle();
        }

        private void HandleModeChanged(DragModeChangedEvent _) => Refresh();

        private void Refresh()
        {
            if (_label == null) return;
            DragMode mode = DragModeService.Current != null ? DragModeService.Current.CurrentMode : DragMode.Items;
            _label.text = $"Mode: {mode}";
        }
    }
}
