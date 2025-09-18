using System;
using System.Collections.Generic;
using UnityEngine;

namespace LSP.Gameplay.Navigation
{
    /// <summary>
    /// Holds a collection of <see cref="AStarNode"/> objects and provides a lightweight
    /// A* pathfinding implementation that can be used by monsters to pursue the player
    /// while honouring designer-authored navigation links.
    /// </summary>
    public class AStarNavigationGraph : MonoBehaviour
    {
        [SerializeField]
        private List<AStarNode> nodes = new List<AStarNode>();

        private readonly List<AStarNode> runtimeNodes = new List<AStarNode>();

        private void Awake()
        {
            runtimeNodes.Clear();

            if (nodes.Count == 0)
            {
                GetComponentsInChildren(true, runtimeNodes);
            }
            else
            {
                runtimeNodes.AddRange(nodes);
            }
        }

        /// <summary>
        /// Attempts to build a navigation path from <paramref name="start"/> to
        /// <paramref name="goal"/>. The resulting waypoints are written into
        /// <paramref name="result"/>. Returns <c>false</c> when no valid path exists.
        /// </summary>
        public bool TryBuildPath(Vector3 start, Vector3 goal, List<Vector3> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Clear();

            if (runtimeNodes.Count == 0)
            {
                return false;
            }

            var startNode = FindClosestNode(start);
            var goalNode = FindClosestNode(goal);

            if (startNode == null || goalNode == null)
            {
                return false;
            }

            if (startNode == goalNode)
            {
                result.Add(goal);
                return true;
            }

            var nodePath = SolveAStar(startNode, goalNode);
            if (nodePath == null)
            {
                return false;
            }

            foreach (var node in nodePath)
            {
                result.Add(node.Position);
            }

            result.Add(goal);
            return result.Count > 0;
        }

        private AStarNode FindClosestNode(Vector3 position)
        {
            AStarNode closest = null;
            float closestSqr = float.MaxValue;

            foreach (var node in runtimeNodes)
            {
                if (node == null)
                {
                    continue;
                }

                float sqrDistance = (node.Position - position).sqrMagnitude;
                if (sqrDistance < closestSqr)
                {
                    closest = node;
                    closestSqr = sqrDistance;
                }
            }

            return closest;
        }

        private List<AStarNode> SolveAStar(AStarNode start, AStarNode goal)
        {
            var openSet = new List<NodeRecord>();
            var closedSet = new HashSet<AStarNode>();

            var startRecord = new NodeRecord
            {
                Node = start,
                CostSoFar = 0f,
                EstimatedTotalCost = Heuristic(start, goal)
            };

            openSet.Add(startRecord);

            while (openSet.Count > 0)
            {
                var current = ExtractBestNode(openSet);

                if (current.Node == goal)
                {
                    return ReconstructPath(current);
                }

                closedSet.Add(current.Node);

                var connections = current.Node.Connections;
                if (connections == null)
                {
                    continue;
                }

                foreach (var neighbour in connections)
                {
                    if (neighbour == null || closedSet.Contains(neighbour))
                    {
                        continue;
                    }

                    float costToNeighbour = current.CostSoFar + Cost(current.Node, neighbour);
                    var neighbourRecord = FindRecord(openSet, neighbour);

                    if (neighbourRecord == null)
                    {
                        neighbourRecord = new NodeRecord
                        {
                            Node = neighbour
                        };
                        openSet.Add(neighbourRecord);
                    }
                    else if (costToNeighbour >= neighbourRecord.CostSoFar)
                    {
                        continue;
                    }

                    neighbourRecord.CostSoFar = costToNeighbour;
                    neighbourRecord.Connection = current;
                    neighbourRecord.EstimatedTotalCost = costToNeighbour + Heuristic(neighbour, goal);
                }
            }

            return null;
        }

        private static NodeRecord ExtractBestNode(List<NodeRecord> openSet)
        {
            int bestIndex = 0;
            float bestScore = openSet[0].EstimatedTotalCost;

            for (int i = 1; i < openSet.Count; i++)
            {
                float score = openSet[i].EstimatedTotalCost;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            var record = openSet[bestIndex];
            openSet.RemoveAt(bestIndex);
            return record;
        }

        private static NodeRecord FindRecord(List<NodeRecord> records, AStarNode node)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Node == node)
                {
                    return records[i];
                }
            }

            return null;
        }

        private static float Heuristic(AStarNode from, AStarNode to)
        {
            return Vector3.Distance(from.Position, to.Position);
        }

        private static float Cost(AStarNode from, AStarNode to)
        {
            float baseDistance = Vector3.Distance(from.Position, to.Position);
            float multiplier = (from.TraversalCostMultiplier + to.TraversalCostMultiplier) * 0.5f;
            return baseDistance * multiplier;
        }

        private static List<AStarNode> ReconstructPath(NodeRecord goalRecord)
        {
            var result = new List<AStarNode>();
            var current = goalRecord;

            while (current != null)
            {
                result.Add(current.Node);
                current = current.Connection;
            }

            result.Reverse();
            return result;
        }

        private class NodeRecord
        {
            public AStarNode Node;
            public NodeRecord Connection;
            public float CostSoFar;
            public float EstimatedTotalCost;
        }
    }
}
