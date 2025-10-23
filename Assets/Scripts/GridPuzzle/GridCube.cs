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
        private Vector3 initialScale;
        private Tween hoverTween;
        private Tween clickTween;
        private bool isHovered;

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
            initialScale = transform.localScale;
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
            PlayClickFeedback();
            owner?.HandleCubeActivated(this);
        }

        private void OnMouseEnter()
        {
            isHovered = true;
            PlayHoverFeedback();
        }

        private void OnMouseExit()
        {
            isHovered = false;
            ResetHoverFeedback();
        }

        private void OnDisable()
        {
            hoverTween?.Kill();
            clickTween?.Kill();
            transform.localScale = initialScale;
            isHovered = false;
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
