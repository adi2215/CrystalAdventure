using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>
    /// Какой префаб показывать для архетипа. Имя архетипа = имя в EntitySpec
    /// (SmallBallScout, ForestDrone, WoodMechGuardian...). Create → Crystal Adventure → Entity Prefabs.
    /// </summary>
    [CreateAssetMenu(menuName = "Crystal Adventure/Entity Prefabs")]
    public class EntityPrefabs : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string archetype;
            public EntityView prefab;
        }

        public EntityView hero;
        public EntityView crystal;
        public EntityView fallbackEnemy;
        public List<Entry> enemies = new List<Entry>();

        public EntityView For(CrystalAdventure.Core.Entity e)
        {
            switch (e.Kind)
            {
                case CrystalAdventure.Core.EntityKind.Hero: return hero;
                case CrystalAdventure.Core.EntityKind.Crystal: return crystal;
            }
            foreach (var en in enemies) if (en.archetype == e.Archetype) return en.prefab;
            return fallbackEnemy;
        }
    }
}
