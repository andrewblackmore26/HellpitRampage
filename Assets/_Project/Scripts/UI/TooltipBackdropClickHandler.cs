using UnityEngine;
using UnityEngine.EventSystems;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.1: attached to the invisible backdrop behind the detail tooltip panel.
    /// Click-anywhere-outside-the-panel dismisses the tooltip.
    /// </summary>
    public class TooltipBackdropClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (DetailTooltipController.Current != null)
                DetailTooltipController.Current.Hide();
        }
    }
}
