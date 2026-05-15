using UnityEngine;

namespace HellpitRampage.Core
{
    /// <summary>
    /// Minimal hero descriptor scaffolded for WS-013. Only carries the stable Id and a display
    /// name so the save layer can round-trip "which hero is this run." Per-hero stats / starting
    /// loadout / portraits land with the hero-unlock spec; nothing in the runtime consumes those
    /// fields yet.
    /// </summary>
    [CreateAssetMenu(fileName = "NewHero_Hero", menuName = "HellpitRampage/Hero Data")]
    public class HeroData : ScriptableObject
    {
        [Header("Identity (stable across rebuilds — keep snake_case)")]
        [SerializeField] private string _id;
        public string Id => _id;

        [SerializeField] private string _displayName = "Unnamed Hero";
        public string DisplayName => _displayName;
    }
}
