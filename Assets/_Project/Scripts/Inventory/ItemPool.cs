using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace HellpitRampage.Inventory
{
    [CreateAssetMenu(fileName = "NewPool_ItemPool", menuName = "HellpitRampage/Item Pool")]
    public class ItemPool : ScriptableObject
    {
        public List<ItemData> Items = new();
        public List<BagData> Bags = new();

        public List<ScriptableObject> DrawWeighted(int count, Random rng = null)
        {
            rng ??= new Random();
            var result = new List<ScriptableObject>(count);

            var weighted = new List<(ScriptableObject obj, int weight)>();
            foreach (var item in Items) if (item != null) weighted.Add((item, WeightForRarity(item.Rarity)));
            foreach (var bag in Bags) if (bag != null) weighted.Add((bag, WeightForRarity(bag.Rarity)));

            if (weighted.Count == 0) return result;

            int totalWeight = 0;
            foreach (var w in weighted) totalWeight += w.weight;
            if (totalWeight <= 0) return result;

            for (int i = 0; i < count; i++)
            {
                int roll = rng.Next(totalWeight);
                int cumulative = 0;
                foreach (var w in weighted)
                {
                    cumulative += w.weight;
                    if (roll < cumulative) { result.Add(w.obj); break; }
                }
            }
            return result;
        }

        public static int WeightForRarity(ItemRarity r) => r switch
        {
            ItemRarity.Common => 50,
            ItemRarity.Uncommon => 25,
            ItemRarity.Rare => 12,
            ItemRarity.Legendary => 5,
            ItemRarity.Mythic => 1,
            _ => 50
        };
    }
}
