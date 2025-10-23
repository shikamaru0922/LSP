using System.Collections.Generic;
using UnityEngine;

namespace LSP.Puzzles
{
    /// <summary>
    /// Represents a single cube within the grid-based color flipping puzzle.
    /// Handles visual state, neighbour tracking and click forwarding to the manager.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GridCube : MonoBehaviour
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        [SerializeField]
        [Tooltip("Renderer used to display the cube's current color. Defaults to the first Renderer found on the object or its children.")]
        private Renderer targetRenderer;

        [SerializeField]
        [Tooltip("Color used when the cube is inactive (white state).")]
        private Color inactiveColor = Color.white;

        [SerializeField]
        [Tooltip("Color used when the cube is active (green state).")]
        private Color activeColor = Color.green;

        private readonly List<GridCube> neighbours = new List<GridCube>(4);
        private MaterialPropertyBlock propertyBlock;
        private GridPuzzleManager owner;
        private bool isActive;

        /// <summary>
        /// Current cube state. True when the cube is green (active).
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Neighbours (up to four) used during toggling logic.
        /// </summary>
        public IReadOnlyList<GridCube> Neighbours => neighbours;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
            ApplyColor();
        }

        /// <summary>
        /// Called by <see cref="GridPuzzleManager"/> after instantiation.
        /// </summary>
        public void Initialise(GridPuzzleManager puzzleManager, bool startActive)
        {
            owner = puzzleManager;
            isActive = startActive;
            ApplyColor();
        }

        /// <summary>
        /// Configures the neighbouring cubes for this cell.
        /// Null references are ignored.
        /// </summary>
        public void SetNeighbours(GridCube up, GridCube down, GridCube left, GridCube right)
        {
            neighbours.Clear();

            TryAddNeighbour(up);
            TryAddNeighbour(down);
            TryAddNeighbour(left);
            TryAddNeighbour(right);
        }

        private void TryAddNeighbour(GridCube neighbour)
        {
            if (neighbour != null && !neighbours.Contains(neighbour))
            {
                neighbours.Add(neighbour);
            }
        }

        /// <summary>
        /// Flips this cube's state.
        /// </summary>
        public void Toggle()
        {
            isActive = !isActive;
            ApplyColor();
        }

        /// <summary>
        /// Resets the cube to an explicit state.
        /// </summary>
        public void SetState(bool active)
        {
            isActive = active;
            ApplyColor();
        }

        private void OnMouseDown()
        {
            owner?.HandleCubeActivated(this);
        }

        private void ApplyColor()
        {
            if (targetRenderer == null)
            {
                return;
            }

            var targetColour = isActive ? activeColor : inactiveColor;

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            targetRenderer.GetPropertyBlock(propertyBlock);

            var material = targetRenderer.sharedMaterial;
            var appliedThroughPropertyBlock = false;

            if (material != null)
            {
                if (material.HasProperty(BaseColorProperty))
                {
                    propertyBlock.SetColor(BaseColorProperty, targetColour);
                    appliedThroughPropertyBlock = true;
                }
                else if (material.HasProperty(ColorProperty))
                {
                    propertyBlock.SetColor(ColorProperty, targetColour);
                    appliedThroughPropertyBlock = true;
                }
            }

            if (appliedThroughPropertyBlock)
            {
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
            else
            {
                // Fallback for custom shaders without colour properties supported by MaterialPropertyBlock.
                targetRenderer.material.color = targetColour;
            }
        }
    }
}
