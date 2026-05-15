using UnityEngine;
using UnityEngine.EventSystems;

namespace HellpitRampage.UI
{
    /// <summary>
    /// WS-012.3: attached to the invisible backdrop behind the pinned tooltip panel.
    /// Click-anywhere-outside-the-panel unpins the tooltip. The backdrop is only active
    /// while the tooltip is pinned (hover mode keeps it disabled so clicks pass through).
    /// </summary>
    public class TooltipBackdropClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (TooltipController.Current != null)
                TooltipController.Current.Unpin();
        }
    }
}
