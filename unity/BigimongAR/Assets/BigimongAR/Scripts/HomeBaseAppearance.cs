using System.Collections.Generic;
using UnityEngine;

namespace Bigimong.AR
{
    public static class HomeBaseAppearance
    {
        private static readonly HashSet<string> OptionalEquipment = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "CosmeticRoot", "AccessoryRoot", "Wearables", "SummoningMedallion", "Medallion",
            "Hat", "Glasses", "Sunglasses", "Backpack", "ExplorerPack",
        };

        private static readonly HashSet<string> ProtectedRoots = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "BaseUnderlayer", "Body", "Shorts", "Head", "Face", "FaceRoot", "Hair", "HairRoot",
            "Anatomy", "AnatomyRoot", "Rig", "RigRoot", "Skeleton", "Armature",
        };

        // Call only on a resolved instance, never a source prefab. Anatomy, rig and underlayer are not equipment.
        public static void Apply(GameObject instance)
        {
            if (instance == null) return;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                if (child != instance.transform && OptionalEquipment.Contains(child.name)) StripOptionalBranch(child);
        }

        private static void StripOptionalBranch(Transform node)
        {
            // Protected roots own their base subtree. Explicit optional descendants are still
            // processed by Apply's full traversal, e.g. Rig/Body/Hat.
            if (ProtectedRoots.Contains(node.name)) return;
            if (!HasProtectedSubtree(node))
            {
                node.gameObject.SetActive(false);
                return;
            }

            // An equipment container can also be an ancestor of the underlayer or rig.
            // Keep that transform chain intact, hide only its own optional presentation, and
            // strip sibling equipment branches without reparenting or disabling anatomy.
            node.gameObject.SetActive(true);
            foreach (var renderer in node.GetComponents<Renderer>()) renderer.enabled = false;
            foreach (Transform child in node) StripOptionalBranch(child);
        }

        private static bool HasProtectedSubtree(Transform node)
        {
            foreach (var descendant in node.GetComponentsInChildren<Transform>(true))
                if (ProtectedRoots.Contains(descendant.name)) return true;
            return false;
        }
    }
}
