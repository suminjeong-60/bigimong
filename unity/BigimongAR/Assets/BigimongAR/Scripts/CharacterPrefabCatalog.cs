using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bigimong.AR
{
    [Serializable]
    public sealed class CharacterStagePrefabs
    {
        [Range(1, 30)] public int artId = 1;
        public GameObject baby;
        public GameObject teen;
        public GameObject adult;

        public GameObject Resolve(string stage)
        {
            return stage switch
            {
                "YOUTH" => teen,
                "ADULT" => adult,
                _ => baby,
            };
        }
    }

    [CreateAssetMenu(menuName = "Bigimong/AR Character Catalog", fileName = "ArCharacterCatalog")]
    public sealed class CharacterPrefabCatalog : ScriptableObject
    {
        [SerializeField] private GameObject fallbackPrefab;
        [SerializeField] private List<CharacterStagePrefabs> characters = new();

        public GameObject Resolve(int artId, string stage)
        {
            var entry = characters.Find(candidate => candidate.artId == Mathf.Clamp(artId, 1, 30));
            return entry?.Resolve(stage) ?? fallbackPrefab;
        }

        public bool TryResolve(int artId, string stage, out GameObject prefab)
        {
            var entry = characters.Find(candidate => candidate.artId == Mathf.Clamp(artId, 1, 30));
            prefab = entry?.Resolve(stage);
            return prefab != null;
        }

        private void OnValidate()
        {
            var seen = new HashSet<int>();
            foreach (var entry in characters)
            {
                entry.artId = Mathf.Clamp(entry.artId, 1, 30);
                if (!seen.Add(entry.artId)) Debug.LogWarning($"Duplicate Bigimong artId: {entry.artId}", this);
            }
        }
    }
}
