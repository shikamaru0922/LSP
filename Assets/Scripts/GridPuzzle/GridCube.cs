using System.Collections.Generic;
using DG.Tweening;
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
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        [Tooltip("Renderer used to display the cube's current color. Defaults to the first Renderer found on the object or its children.")]
        private Renderer targetRenderer;

        [Header("Emission Colors")]
        [SerializeField]
        [ColorUsage(false, true)]
        [Tooltip("Emission when inactive and not hovered (Dim Warm Yellow).")]
        private Color idleEmissionColor = new Color(1f, 0.6f, 0.2f, 1f) * 0.5f; // Dim warm yellow

        [SerializeField]
        [ColorUsage(false, true)]
        [Tooltip("Emission when hovered but inactive (Weak Blue).")]
        private Color hoverEmissionColor = new Color(0.2f, 0.4f, 1f, 1f) * 1.5f; // Weak blue

        [SerializeField]
        [ColorUsage(false, true)]
        [Tooltip("Emission when active (Strong Blue).")]
        private Color activeEmissionColor = new Color(0f, 0.4f, 1f, 1f) * 3.5f; // Strong blue

        [SerializeField]
        [ColorUsage(false, true)]
        [Tooltip("Emission when the puzzle is solved (Bright Green).")]
        private Color solvedEmissionColor = new Color(0f, 1f, 0f, 1f) * 3f; // Bright green

        [Header("Feedback Settings")]
        [SerializeField]
        [Tooltip("Duration of the hover scale tween.")]
        private float hoverTweenDuration = 0.2f;

        [SerializeField]
        [Tooltip("Scale multiplier applied while the cube is hovered by the player's ray.")]
        private float hoverScaleMultiplier = 1.1f;

        [SerializeField]
        [Tooltip("Strength of the punch scale effect when the cube is clicked.")]
        private float clickPunchStrength = 0.15f;

        [SerializeField]
        [Tooltip("Duration of the punch scale tween when the cube is clicked.")]
        private float clickPunchDuration = 0.25f;

        [SerializeField]
        [Tooltip("How many oscillations occur during the click punch scale effect.")]
        private int clickPunchVibrato = 8;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Elasticity of the click punch scale effect.")]
        private float clickPunchElasticity = 0.75f;

        private readonly List<GridCube> neighbours = new List<GridCube>(4);
        private MaterialPropertyBlock propertyBlock;
        private GridPuzzleManager owner;
        private bool isActive;
        private bool isSolved;
        private Vector3 initialScale;
        private Tween hoverTween;
        private Tween clickTween;
        private bool isHovered;

        /// <summary>
        /// Current cube state. True when the cube is active.
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
            initialScale = transform.localScale;
            UpdateVisuals();
        }

        /// <summary>
        /// Called by <see cref="GridPuzzleManager"/> after instantiation.
        /// </summary>
        public void Initialise(GridPuzzleManager puzzleManager, bool startActive)
        {
            owner = puzzleManager;
            isActive = startActive;
            UpdateVisuals();
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
            UpdateVisuals();
        }

        /// <summary>
        /// Resets the cube to an explicit state.
        /// </summary>
        public void SetState(bool active)
        {
            isActive = active;
            UpdateVisuals();
        }

        /// <summary>
        /// Set the solved state of the cube.
        /// </summary>
        public void SetSolved(bool solved)
        {
            isSolved = solved;
            UpdateVisuals();
        }

        private void OnMouseDown()
        {
            if (isSolved) return; // Prevent interaction when solved? User didn't specify, but usually good practice.
            PlayClickFeedback();
            owner?.HandleCubeActivated(this);
        }

        private void OnMouseEnter()
        {
            isHovered = true;
            PlayHoverFeedback();
            UpdateVisuals();
        }

        private void OnMouseExit()
        {
            isHovered = false;
            ResetHoverFeedback();
            UpdateVisuals();
        }

        private void OnDisable()
        {
            hoverTween?.Kill();
            clickTween?.Kill();
            transform.localScale = initialScale;
            isHovered = false;
        }

        private void UpdateVisuals()
        {
            if (targetRenderer == null)
            {
                return;
            }

            // Priority: Solved > Active > Hover > Idle
            Color targetEmission;

            if (isSolved)
            {
                targetEmission = solvedEmissionColor;
            }
            else if (isActive)
            {
                targetEmission = activeEmissionColor;
            }
            else if (isHovered)
            {
                targetEmission = hoverEmissionColor;
            }
            else
            {
                targetEmission = idleEmissionColor;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorProperty, targetEmission);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void PlayHoverFeedback()
        {
            hoverTween?.Kill();
            hoverTween = transform.DOScale(initialScale * hoverScaleMultiplier, hoverTweenDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        private void ResetHoverFeedback()
        {
            hoverTween?.Kill();
            hoverTween = transform.DOScale(initialScale, hoverTweenDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        private void PlayClickFeedback()
        {
            clickTween?.Kill();

            var baseScale = isHovered ? initialScale * hoverScaleMultiplier : initialScale;
            transform.localScale = baseScale;

            clickTween = transform.DOPunchScale(Vector3.one * clickPunchStrength, clickPunchDuration, clickPunchVibrato, clickPunchElasticity)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    transform.localScale = baseScale;
                    if (!isHovered)
                    {
                        ResetHoverFeedback();
                    }
                });
        }
    }
}
