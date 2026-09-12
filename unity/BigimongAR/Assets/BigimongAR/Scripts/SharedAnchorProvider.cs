using System;
using UnityEngine;

namespace Bigimong.AR
{
    /** Implement with ARCore Extensions Cloud Anchors after a Google Cloud project is configured. */
    public abstract class SharedAnchorProvider : MonoBehaviour
    {
        public abstract void Host(Transform localArena, Action<string> onHosted, Action<string> onError);
        public abstract void Resolve(string cloudAnchorId, Action<Pose> onResolved, Action<string> onError);
    }
}
