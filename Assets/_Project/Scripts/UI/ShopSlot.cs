using HellpitRampage.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace HellpitRampage.UI
{
    public class ShopSlot : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _itemIcon;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _priceLabel;

        public ScriptableObject CurrentOffer { get; private set; }
        public bool IsSold { get; private set; }
        public bool CanAfford { get; private set; }
        public int CurrentPrice { get; private set; }

        public void SetOffer(ScriptableObject offer)
        {
            CurrentOffer = offer;
            IsSold = false;
            UpdateDisplay();
        }

        public void MarkSold()
        {
            IsSold = true;
            CurrentOffer = null;
            UpdateDisplay();
        }

        public void UpdateAffordability(int currentGold)
        {
            CanAfford = (CurrentOffer != null) && (currentGold >= CurrentPrice);
            if (_priceLabel != null)
                _priceLabel.color = (IsSold || CanAfford) ? Color.white : new Color(0.9f, 0.3f, 0.3f, 1f);
        }

        private void UpdateDisplay()
        {
            if (IsSold || CurrentOffer == null)
            {
                if (_background != null) _background.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
                if (_itemIcon != null) _itemIcon.color = new Color(1, 1, 1, 0);
                if (_nameLabel != null) _nameLabel.text = IsSold ? "SOLD" : "";
                if (_priceLabel != null) _priceLabel.text = "";
                CurrentPrice = 0;
                CanAfford = false;
                return;
            }

            string name = "";
            int price = 0;
            ItemRarity rarity = ItemRarity.Common;
            Sprite icon = null;

            if (CurrentOffer is ItemData item)
            {
                name = item.ItemName;
                price = item.EffectivePrice;
                rarity = item.Rarity;
                icon = item.Icon;
            }
            else if (CurrentOffer is BagData bag)
            {
                name = bag.BagName + " (Bag)";
                price = bag.EffectivePrice;
                rarity = bag.Rarity;
                icon = bag.Icon;
            }

            CurrentPrice = price;
            if (_background != null) _background.color = ColorForRarity(rarity);
            if (_itemIcon != null)
            {
                _itemIcon.color = icon == null ? new Color(0.8f, 0.7f, 0.4f, 1f) : Color.white;
                _itemIcon.sprite = icon;
            }
            if (_nameLabel != null) _nameLabel.text = name;
            if (_priceLabel != null) _priceLabel.text = $"{price}g";
        }

        public static Color ColorForRarity(ItemRarity r) => r switch
        {
            ItemRarity.Common => new Color(0.4f, 0.4f, 0.4f, 0.9f),
            ItemRarity.Uncommon => new Color(0.04f, 0.6f, 0.27f, 0.9f),
            ItemRarity.Rare => new Color(0.04f, 0.4f, 0.66f, 0.9f),
            ItemRarity.Legendary => new Color(0.52f, 0.2f, 0.8f, 0.9f),
            ItemRarity.Mythic => new Color(0.9f, 0.45f, 0.13f, 0.9f),
            _ => new Color(0.4f, 0.4f, 0.4f, 0.9f)
        };
    }
}
