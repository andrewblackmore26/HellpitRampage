using System.Collections.Generic;
using HellpitRampage.Combat;
using HellpitRampage.Inventory;
using UnityEngine;

namespace HellpitRampage.Core
{
    /// <summary>
    /// WS-013: hand-authored ScriptableObject that lists every data-only SO in the project,
    /// grouped by type. The DataRegistry consumes this at Boot to index assets by their
    /// stable string Id without needing the Resources folder convention. Drop new content
    /// into the appropriate list in the Inspector when authoring it; the DataIdValidator
    /// menu will flag missing Ids on those assets.
    /// </summary>
    [CreateAssetMenu(fileName = "DataManifest", menuName = "HellpitRampage/Data Manifest")]
    public class DataManifest : ScriptableObject
    {
        public List<ItemData> Items = new();
        public List<BagData> Bags = new();
        public List<HeroData> Heroes = new();
        public List<EnemyData> Enemies = new();
    }
}
