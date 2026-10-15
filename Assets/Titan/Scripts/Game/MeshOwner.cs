using System.Collections.Generic;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>Destroys the meshes built for its GameObject (a combined falling piece, FEEL-1) when it goes away.</summary>
    public sealed class MeshOwner : MonoBehaviour
    {
        public readonly List<Mesh> Meshes = new List<Mesh>();

        void OnDestroy()
        {
            foreach (var m in Meshes)
                if (m != null) Destroy(m);
            Meshes.Clear();
        }
    }
}
