using UnityEngine;

namespace HellpitRampage.Inventory
{
    [CreateAssetMenu(fileName = "NewBag_Bag", menuName = "HellpitRampage/Bag Data")]
    public class BagData : ScriptableObject
    {
        [Header("Identity")]
        public string BagName = "Unnamed Bag";

        [Tooltip("Sprite drawn under the bag's cells in the inventory grid.")]
        public Sprite Icon;

        [Tooltip("Cell footprint of this bag. Standard sizes start at 2x2 or 3x3.")]
        public ItemShape Shape;

        [Header("Passive (placeholder — wiring comes with effects system)")]
        [TextArea(2, 3)]
        public string PassiveDescription = "";

        [Header("Economy")]
        public ItemRarity Rarity = ItemRarity.Common;

        [Tooltip("Price to buy. If 0, derived from Rarity.")]
        public int BasePrice = 0;

        public int EffectivePrice => BasePrice > 0 ? BasePrice : ItemData.DefaultPriceForRarity(Rarity);
    }
}
