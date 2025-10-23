using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LSP.Puzzles
{
    /// <summary>
    /// Creates and manages a grid of <see cref="GridCube"/> instances that implement
    /// the colour flipping puzzle described in the game design.
    /// </summary>
    public class GridPuzzleManager : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField]
        [Tooltip("Prefab used for each cube cell. Must contain a Collider component.")]
        private GridCube cubePrefab;

        [SerializeField]
        [Tooltip("Number of columns (X) and rows (Y) to build in the puzzle grid.")]
        private Vector2Int gridSize = new Vector2Int(3, 3);

        [SerializeField]
        [Tooltip("Distance between cube centres along each axis.")]
        private float spacing = 1f;

        [SerializeField]
        [Tooltip("If true, generates the puzzle grid automatically during Start().")]
        private bool generateOnStart = true;

        [Header("Gameplay")]
        [SerializeField]
        [Tooltip("Initial active state for every cube when generated.")]
        private bool startActive;

        [SerializeField]
        [Tooltip("Raised when every cube in the grid is active (green).")]
        private UnityEvent onPuzzleSolved;

        private GridCube[,] gridCubes;
        private bool hasReportedSolved;
        private readonly List<GridCube> cubeBuffer = new List<GridCube>();

        /// <summary>
        /// Invoked whenever the puzzle transitions into the solved state.
        /// </summary>
        public UnityEvent OnPuzzleSolved => onPuzzleSolved;

        /// <summary>
        /// Current grid dimensions. Values are clamped to at least 1 in each axis.
        /// </summary>
        public Vector2Int GridSize
        {
            get => gridSize;
            set
            {
                gridSize = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
            }
        }

        private void Start()
        {
            if (generateOnStart && cubePrefab != null)
            {
                GenerateGrid();
            }
        }

        /// <summary>
        /// Destroys any existing grid and rebuilds a new layout using the configured prefab.
        /// </summary>
        public void GenerateGrid()
        {
            if (cubePrefab == null)
            {
                Debug.LogError("GridPuzzleManager requires a cube prefab reference before it can generate.", this);
                return;
            }

            ClearExistingCubes();

            var size = GridSize;
            gridCubes = new GridCube[size.x, size.y];

            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    var worldPosition = transform.TransformPoint(new Vector3(x * spacing, 0f, y * spacing));
                    var cubeInstance = Instantiate(cubePrefab, worldPosition, transform.rotation, transform);
                    cubeInstance.Initialise(this, startActive);
                    gridCubes[x, y] = cubeInstance;
                }
            }

            ConfigureNeighbours();
            hasReportedSolved = false;
            EvaluateSolvedState();
        }

        /// <summary>
        /// Handles the click interaction reported by a <see cref="GridCube"/>.
        /// Toggles the clicked cube and its four orthogonal neighbours.
        /// </summary>
        public void HandleCubeActivated(GridCube cube)
        {
            if (cube == null)
            {
                return;
            }

            cube.Toggle();
            cubeBuffer.Clear();
            cubeBuffer.AddRange(cube.Neighbours);

            for (var i = 0; i < cubeBuffer.Count; i++)
            {
                cubeBuffer[i].Toggle();
            }

            EvaluateSolvedState();
        }

        /// <summary>
        /// Returns all cubes managed by this puzzle as an enumerable sequence.
        /// </summary>
        public IEnumerable<GridCube> AllCubes()
        {
            if (gridCubes == null)
            {
                yield break;
            }

            var size = gridCubes.GetLength(0);
            var height = gridCubes.GetLength(1);
            for (var x = 0; x < size; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var cube = gridCubes[x, y];
                    if (cube != null)
                    {
                        yield return cube;
                    }
                }
            }
        }

        /// <summary>
        /// Explicitly sets every cube's state.
        /// </summary>
        public void SetAllCubes(bool active)
        {
            if (gridCubes == null)
            {
                return;
            }

            foreach (var cube in AllCubes())
            {
                cube.SetState(active);
            }

            EvaluateSolvedState();
        }

        /// <summary>
        /// Checks whether every cube within the grid is in the active (green) state.
        /// </summary>
        public bool IsSolved()
        {
            if (gridCubes == null)
            {
                return false;
            }

            foreach (var cube in AllCubes())
            {
                if (!cube.IsActive)
                {
                    return false;
                }
            }

            return true;
        }

        private void ConfigureNeighbours()
        {
            if (gridCubes == null)
            {
                return;
            }

            var width = gridCubes.GetLength(0);
            var height = gridCubes.GetLength(1);

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var cube = gridCubes[x, y];
                    if (cube == null)
                    {
                        continue;
                    }

                    var up = y + 1 < height ? gridCubes[x, y + 1] : null;
                    var down = y - 1 >= 0 ? gridCubes[x, y - 1] : null;
                    var left = x - 1 >= 0 ? gridCubes[x - 1, y] : null;
                    var right = x + 1 < width ? gridCubes[x + 1, y] : null;
                    cube.SetNeighbours(up, down, left, right);
                }
            }
        }

        private void EvaluateSolvedState()
        {
            var solved = IsSolved();
            if (solved)
            {
                if (!hasReportedSolved)
                {
                    hasReportedSolved = true;
                    onPuzzleSolved?.Invoke();
                }
            }
            else
            {
                hasReportedSolved = false;
            }
        }

        private void ClearExistingCubes()
        {
            if (gridCubes == null)
            {
                foreach (Transform child in transform)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
                return;
            }

            foreach (var cube in AllCubes())
            {
                if (cube == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(cube.gameObject);
                }
                else
                {
                    DestroyImmediate(cube.gameObject);
                }
            }

            gridCubes = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            GridSize = gridSize;
            spacing = Mathf.Max(0.1f, spacing);
        }
#endif
    }
}
