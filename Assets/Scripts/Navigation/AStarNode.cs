using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay.Navigation
{
    /// <summary>
    /// A simple navigation node used by the custom A* solver. Attach this component to
    /// empty transforms placed throughout the level and define their neighbour
    /// connections in the inspector.
    /// </summary>
    public class AStarNode : MonoBehaviour
    {
        [Tooltip("Neighbouring nodes that can be reached directly from this node.")]
        [SerializeField]
        private List<AStarNode> connections = new List<AStarNode>();

        [Tooltip("Additional traversal weight applied when moving through this node.")]
        [SerializeField]
        private float traversalCostMultiplier = 1f;

        public IReadOnlyList<AStarNode> Connections => connections;

        public float TraversalCostMultiplier => Mathf.Max(0.01f, traversalCostMultiplier);

        public Vector3 Position => transform.position;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.1f);

            if (connections == null)
            {
                return;
            }

            Gizmos.color = Color.blue;
            foreach (var node in connections)
            {
                if (node == null)
                {
                    continue;
                }

                Gizmos.DrawLine(transform.position, node.transform.position);
            }
        }
#endif
    }
}
