
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// Builds a connected, random 2D grid for Grid Heist.
/// Cell (0, 0) is at this GameObject's position. A cell is either floor or wall.
/// The generated grid is kept separate from the optional floor/wall GameObjects.
/// </summary>
[DefaultExecutionOrder(-100)]
public class MazeGenerator : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField, Min(5)] private int gridWidth = 21;
    [SerializeField, Min(5)] private int gridHeight = 21;
    [SerializeField, Min(0.1f)] private float tileSize = 1f;
    [SerializeField] private bool generateOnStart = true;

    [Header("Random layout")]
    [Tooltip("Use 0 for a new layout each generation. Any other value repeats the same layout.")]
    [SerializeField] private int randomSeed = 0;
    [Tooltip("Percent of possible wall cells between two connected floor cells that will be opened.")]
    [Range(0f, 100f)]
    [SerializeField] private float loopOpeningPercent = 22f;
    [Tooltip("Chance to widen a straight corridor by one tile on either side.")]
    [Range(0f, 100f)]
    [SerializeField] private float corridorWideningPercent = 15f;
    [SerializeField, Min(0)] private int roomCount = 4;
    [SerializeField, Min(2)] private int minRoomSize = 3;
    [SerializeField, Min(2)] private int maxRoomSize = 6;

    [Header("Tile prefabs")]
    [SerializeField] private GameObject wallPrefab;
    [Tooltip("Optional. Leave empty if you use a single background instead of floor tiles.")]
    [SerializeField] private GameObject floorPrefab;
    [Tooltip("Generated tiles are placed under this transform. If empty, a GeneratedMaze child is made under this object.")]
    [SerializeField] private Transform generatedParent;
    [Tooltip("Scales each prefab's X and Y local scale by Tile Size. Keep disabled if your prefabs are already sized for the grid.")]
    [SerializeField] private bool scalePrefabsToTileSize = false;

    [Header("Camera")]
    [Tooltip("Camera to fit to the generated grid. If empty, Camera.main is used.")]
    [SerializeField] private Camera mazeCamera;
    [Tooltip("Move and resize the camera so the full generated grid is visible.")]
    [SerializeField] private bool fitCameraToMaze = true;
    [Tooltip("Extra space around the grid, measured in tile widths.")]
    [SerializeField, Min(0f)] private float cameraPaddingTiles = 1f;

    [Header("Player and exit")]

    [Tooltip("Prefab to spawn as the player.")]
    [SerializeField] private GameObject playerPrefab;

    private int generatedSeed;
    private Vector2Int generatedPlayerSpawnCell;
    private Vector2Int generatedExitCell;
    private bool[,] walkableGrid;
    private int generatedWidth;
    private int generatedHeight;
    private System.Random random;
    private Transform generatedRoot;
    private GameObject spawnedPlayer;

    private static readonly Vector2Int[] MazeDirections =
    {
        new Vector2Int(0, -2),
        new Vector2Int(0, 2),
        new Vector2Int(-2, 0),
        new Vector2Int(2, 0)
    };

    private static readonly Vector2Int[] TileDirections =
    {
        new Vector2Int(0, -1),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0)
    };

    public int Width => generatedWidth;
    public int Height => generatedHeight;
    public float TileSize => tileSize;
    public GameObject SpawnedPlayer => spawnedPlayer;
    public Vector2Int ExitCell => generatedExitCell;
    public bool IsGenerated => walkableGrid != null;

    private void Start()
    {
        if (generateOnStart)
            GenerateMaze();
    }

    private void OnValidate()
    {
        gridWidth = Mathf.Max(5, gridWidth | 1);
        gridHeight = Mathf.Max(5, gridHeight | 1);
        tileSize = Mathf.Max(0.1f, tileSize);
        loopOpeningPercent = Mathf.Clamp(loopOpeningPercent, 0f, 100f);
        corridorWideningPercent = Mathf.Clamp(corridorWideningPercent, 0f, 100f);
        roomCount = Mathf.Max(0, roomCount);
        minRoomSize = Mathf.Max(2, minRoomSize);
        maxRoomSize = Mathf.Max(minRoomSize, maxRoomSize);
        cameraPaddingTiles = Mathf.Max(0f, cameraPaddingTiles);
    }

    [ContextMenu("Generate Maze")]
    public void GenerateMaze()
    {
        generatedWidth = Mathf.Max(5, gridWidth | 1);
        generatedHeight = Mathf.Max(5, gridHeight | 1);

        generatedSeed = randomSeed == 0
            ? unchecked(Environment.TickCount ^ GetInstanceID())
            : randomSeed;
        random = new System.Random(generatedSeed);
        walkableGrid = new bool[generatedWidth, generatedHeight];

        CarveConnectedMaze();
        OpenExtraConnections();
        WidenStraightCorridors();
        CarveRooms();
        ChooseExitAndPlayerSpawn();
        ValidateGeneratedLayout();
        BuildVisuals();
        PlacePlayer();

        if (fitCameraToMaze)
            FitCameraToMaze();
    }

    /// <summary>
    /// Creates a connected maze using DFS on every other grid cell.
    /// The cells between visited nodes are carved as corridors.
    /// </summary>
    private void CarveConnectedMaze()
    {
        Vector2Int start = new Vector2Int(1, 1);
        List<Vector2Int> stack = new List<Vector2Int>();

        walkableGrid[start.x, start.y] = true;
        stack.Add(start);

        while (stack.Count > 0)
        {
            Vector2Int current = stack[stack.Count - 1];
            List<Vector2Int> unvisitedNeighbors = new List<Vector2Int>(4);

            for (int i = 0; i < MazeDirections.Length; i++)
            {
                Vector2Int next = current + MazeDirections[i];
                if (IsInteriorMazeNode(next) && !walkableGrid[next.x, next.y])
                    unvisitedNeighbors.Add(next);
            }

            if (unvisitedNeighbors.Count == 0)
            {
                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            Vector2Int chosen = unvisitedNeighbors[random.Next(unvisitedNeighbors.Count)];
            Vector2Int between = new Vector2Int(
                (current.x + chosen.x) / 2,
                (current.y + chosen.y) / 2);

            walkableGrid[between.x, between.y] = true;
            walkableGrid[chosen.x, chosen.y] = true;
            stack.Add(chosen);
        }
    }

    private bool IsInteriorMazeNode(Vector2Int cell)
    {
        return cell.x >= 1 && cell.x < generatedWidth - 1 &&
               cell.y >= 1 && cell.y < generatedHeight - 1 &&
               cell.x % 2 == 1 && cell.y % 2 == 1;
    }

    /// <summary>
    /// Opens selected wall cells that sit between two floor cells.
    /// Each opening adds a cycle, creating an alternate route.
    /// </summary>
    private void OpenExtraConnections()
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 1; x < generatedWidth - 1; x++)
        {
            for (int y = 1; y < generatedHeight - 1; y++)
            {
                if (walkableGrid[x, y])
                    continue;

                bool joinsHorizontally = walkableGrid[x - 1, y] && walkableGrid[x + 1, y];
                bool joinsVertically = walkableGrid[x, y - 1] && walkableGrid[x, y + 1];

                if (joinsHorizontally || joinsVertically)
                    candidates.Add(new Vector2Int(x, y));
            }
        }

        Shuffle(candidates);
        int openingCount = Mathf.RoundToInt(candidates.Count * loopOpeningPercent / 100f);

        // If the user asks for any loops and at least one is possible, open at least one.
        if (loopOpeningPercent > 0f && candidates.Count > 0)
            openingCount = Mathf.Max(1, openingCount);

        for (int i = 0; i < openingCount; i++)
        {
            Vector2Int cell = candidates[i];
            walkableGrid[cell.x, cell.y] = true;
        }
    }

    /// <summary>
    /// Widens some straight one-tile corridors by opening one neighboring tile.
    /// Only adds floor, so it cannot disconnect the maze.
    /// </summary>
    private void WidenStraightCorridors()
    {
        if (corridorWideningPercent <= 0f)
            return;

        bool[,] source = (bool[,])walkableGrid.Clone();

        for (int x = 1; x < generatedWidth - 1; x++)
        {
            for (int y = 1; y < generatedHeight - 1; y++)
            {
                if (!source[x, y] || random.NextDouble() * 100.0 >= corridorWideningPercent)
                    continue;

                bool horizontal = source[x - 1, y] && source[x + 1, y] &&
                                  !source[x, y - 1] && !source[x, y + 1];
                bool vertical = source[x, y - 1] && source[x, y + 1] &&
                                !source[x - 1, y] && !source[x + 1, y];

                if (horizontal)
                {
                    int side = random.Next(2) == 0 ? -1 : 1;
                    walkableGrid[x, y + side] = true;
                }
                else if (vertical)
                {
                    int side = random.Next(2) == 0 ? -1 : 1;
                    walkableGrid[x + side, y] = true;
                }
            }
        }
    }

    /// <summary>
    /// Carves rectangular rooms. Each room contains an existing floor cell,
    /// so every newly carved room joins the connected maze.
    /// </summary>
    private void CarveRooms()
    {
        if (roomCount <= 0)
            return;

        int largestAllowedSize = Mathf.Min(maxRoomSize,
            Mathf.Min(generatedWidth - 2, generatedHeight - 2));
        int smallestAllowedSize = Mathf.Min(minRoomSize, largestAllowedSize);

        if (largestAllowedSize < 2)
            return;

        for (int room = 0; room < roomCount; room++)
        {
            List<Vector2Int> existingFloor = CollectWalkableCells();
            if (existingFloor.Count == 0)
                return;

            Vector2Int anchor = existingFloor[random.Next(existingFloor.Count)];
            int roomWidth = random.Next(smallestAllowedSize, largestAllowedSize + 1);
            int roomHeight = random.Next(smallestAllowedSize, largestAllowedSize + 1);

            int offsetX = random.Next(roomWidth);
            int offsetY = random.Next(roomHeight);
            int left = Mathf.Clamp(anchor.x - offsetX, 1, generatedWidth - roomWidth - 1);
            int bottom = Mathf.Clamp(anchor.y - offsetY, 1, generatedHeight - roomHeight - 1);

            for (int x = left; x < left + roomWidth; x++)
            {
                for (int y = bottom; y < bottom + roomHeight; y++)
                    walkableGrid[x, y] = true;
            }
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            T temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }

    private List<Vector2Int> CollectWalkableCells()
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        if (walkableGrid == null)
            return cells;

        for (int x = 0; x < generatedWidth; x++)
        {
            for (int y = 0; y < generatedHeight; y++)
            {
                if (walkableGrid[x, y])
                    cells.Add(new Vector2Int(x, y));
            }
        }

        return cells;
    }

    private void ChooseExitAndPlayerSpawn()
    {
        // Remove any floor that ended up on the outer border before picking the exit.
        for (int x = 0; x < generatedWidth; x++)
        {
            for (int y = 0; y < generatedHeight; y++)
            {
                bool isBorder =
                    x == 0 || x == generatedWidth - 1 ||
                    y == 0 || y == generatedHeight - 1;

                if (isBorder)
                    walkableGrid[x, y] = false;
            }
        }

        List<Vector2Int> exitCandidates = new List<Vector2Int>();

        for (int x = 0; x < generatedWidth; x++)
        {
            for (int y = 0; y < generatedHeight; y++)
            {
                bool isBorder =
                    x == 0 || x == generatedWidth - 1 ||
                    y == 0 || y == generatedHeight - 1;

                // Skip corners: they only touch the border, never an interior floor.
                bool isCorner =
                    (x == 0 || x == generatedWidth - 1) &&
                    (y == 0 || y == generatedHeight - 1);

                if (!isBorder || isCorner)
                    continue;

                // The only interior neighbor of a border cell is the one pointing inward.
                Vector2Int inward = new Vector2Int(
                    x == 0 ? 1 : x == generatedWidth - 1 ? generatedWidth - 2 : x,
                    y == 0 ? 1 : y == generatedHeight - 1 ? generatedHeight - 2 : y);

                if (walkableGrid[inward.x, inward.y])
                    exitCandidates.Add(new Vector2Int(x, y));
            }
        }

        if (exitCandidates.Count == 0)
        {
            Debug.LogError("MazeGenerator: Could not find an exit location.", this);
            return;
        }

        generatedExitCell = exitCandidates[random.Next(exitCandidates.Count)];
        walkableGrid[generatedExitCell.x, generatedExitCell.y] = true;

        int[,] distances = GetDistancesFrom(generatedExitCell);

        int farthestDistance = -1;
        List<Vector2Int> farthestCells = new List<Vector2Int>();

        for (int x = 0; x < generatedWidth; x++)
        {
            for (int y = 0; y < generatedHeight; y++)
            {
                int distance = distances[x, y];
                if (distance < 0)
                    continue;

                if (distance > farthestDistance)
                {
                    farthestDistance = distance;
                    farthestCells.Clear();
                    farthestCells.Add(new Vector2Int(x, y));
                }
                else if (distance == farthestDistance)
                {
                    farthestCells.Add(new Vector2Int(x, y));
                }
            }
        }

        generatedPlayerSpawnCell = farthestCells[random.Next(farthestCells.Count)];
    }

    private int[,] GetDistancesFrom(Vector2Int start)
    {
        int[,] distances = new int[generatedWidth, generatedHeight];

        for (int x = 0; x < generatedWidth; x++)
        {
            for (int y = 0; y < generatedHeight; y++)
                distances[x, y] = -1;
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(start);
        distances[start.x, start.y] = 0;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int direction in TileDirections)
            {
                Vector2Int next = current + direction;

                if (!IsWalkable(next) || distances[next.x, next.y] >= 0)
                    continue;

                distances[next.x, next.y] =
                    distances[current.x, current.y] + 1;

                queue.Enqueue(next);
            }
        }

        return distances;
    }

    private void PlacePlayer()
    {
        Vector3 spawnPosition = CellToWorld(generatedPlayerSpawnCell);

        // Spawn the player prefab.
        if (playerPrefab == null)
            return;

        if (spawnedPlayer != null)
        {
            if (Application.isPlaying)
                Destroy(spawnedPlayer);
            else
                DestroyImmediate(spawnedPlayer);
        }

        spawnedPlayer = Instantiate(
            playerPrefab,
            spawnPosition,
            Quaternion.identity
        );

        spawnedPlayer.name = "Player";
    }

    /// <summary>Returns true only for a floor cell inside the generated grid.</summary>
    public bool IsWalkable(Vector2Int cell)
    {
        return walkableGrid != null &&
               cell.x >= 0 && cell.x < generatedWidth &&
               cell.y >= 0 && cell.y < generatedHeight &&
               walkableGrid[cell.x, cell.y];
    }

    public bool IsInsideGrid(Vector2Int cell)
    {
        return walkableGrid != null &&
               cell.x >= 0 && cell.x < generatedWidth &&
               cell.y >= 0 && cell.y < generatedHeight;
    }

    /// <summary>Converts a grid coordinate to the world-space center of that tile.</summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        return transform.TransformPoint(new Vector3(cell.x * tileSize, cell.y * tileSize, 0f));
    }

    /// <summary>Converts a world position to the nearest grid coordinate.</summary>
    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return new Vector2Int(
            Mathf.RoundToInt(local.x / tileSize),
            Mathf.RoundToInt(local.y / tileSize));
    }

    /// <summary>Returns a copy of all floor coordinates.</summary>
    public List<Vector2Int> GetWalkableCells()
    {
        return CollectWalkableCells();
    }

    /// <summary>
    /// Picks a random floor coordinate that is not in excludedCells.
    /// Pass null when there are no cells to exclude.
    /// </summary>
    public bool TryGetRandomWalkableCell(ICollection<Vector2Int> excludedCells, out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        if (walkableGrid == null)
            return false;

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < generatedWidth; x++)
        {
            for (int y = 0; y < generatedHeight; y++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (walkableGrid[x, y] &&
                    (excludedCells == null || !excludedCells.Contains(candidate)))
                {
                    candidates.Add(candidate);
                }
            }
        }

        if (candidates.Count == 0)
            return false;

        if (random == null)
            random = new System.Random(generatedSeed);

        cell = candidates[random.Next(candidates.Count)];
        return true;
    }

    /// <summary>Checks reachability using four-way movement. Diagonal movement is not allowed.</summary>
    public bool IsReachable(Vector2Int from, Vector2Int to)
    {
        if (!IsWalkable(from) || !IsWalkable(to))
            return false;

        bool[,] visited = FloodFill(from, out _);
        return visited[to.x, to.y];
    }

    /// <summary>Finds the floor cell with the greatest BFS distance from start.</summary>
    public Vector2Int GetFarthestWalkableCell(Vector2Int start)
    {
        if (!IsWalkable(start))
            return start;

        int[,] distances = new int[generatedWidth, generatedHeight];
        for (int x = 0; x < generatedWidth; x++)
        {
            for (int y = 0; y < generatedHeight; y++)
                distances[x, y] = -1;
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        distances[start.x, start.y] = 0;

        Vector2Int farthest = start;
        int greatestDistance = 0;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            for (int i = 0; i < TileDirections.Length; i++)
            {
                Vector2Int next = current + TileDirections[i];
                if (!IsWalkable(next) || distances[next.x, next.y] >= 0)
                    continue;

                distances[next.x, next.y] = distances[current.x, current.y] + 1;
                queue.Enqueue(next);

                if (distances[next.x, next.y] > greatestDistance)
                {
                    greatestDistance = distances[next.x, next.y];
                    farthest = next;
                }
            }
        }

        return farthest;
    }

    /// <summary>Checks whether every floor cell belongs to one four-way connected area.</summary>
    public bool AreAllWalkableCellsConnected()
    {
        if (walkableGrid == null)
            return false;

        Vector2Int firstFloor = new Vector2Int(-1, -1);
        int floorCount = 0;

        for (int x = 0; x < generatedWidth; x++)
        {
            for (int y = 0; y < generatedHeight; y++)
            {
                if (!walkableGrid[x, y])
                    continue;

                floorCount++;
                if (firstFloor.x < 0)
                    firstFloor = new Vector2Int(x, y);
            }
        }

        if (floorCount == 0)
            return false;

        FloodFill(firstFloor, out int visitedCount);
        return visitedCount == floorCount;
    }

    private bool[,] FloodFill(Vector2Int start, out int visitedCount)
    {
        bool[,] visited = new bool[generatedWidth, generatedHeight];
        visitedCount = 0;

        if (!IsWalkable(start))
            return visited;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited[start.x, start.y] = true;
        visitedCount = 1;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            for (int i = 0; i < TileDirections.Length; i++)
            {
                Vector2Int next = current + TileDirections[i];
                if (!IsWalkable(next) || visited[next.x, next.y])
                    continue;

                visited[next.x, next.y] = true;
                visitedCount++;
                queue.Enqueue(next);
            }
        }

        return visited;
    }

    private void ValidateGeneratedLayout()
    {
        if (!AreAllWalkableCellsConnected())
        {
            Debug.LogError("MazeGenerator: The generated floor is not fully connected.", this);
            return;
        }

        if (!IsWalkable(generatedPlayerSpawnCell))
        {
            Debug.LogWarning("MazeGenerator: Player Spawn Cell " + generatedPlayerSpawnCell +
                             " is not a floor tile. The default (1, 1) is carved by the generator.", this);
            return;
        }

        if (!IsWalkable(generatedExitCell))
        {
            Debug.LogWarning("MazeGenerator: Exit Cell " + generatedExitCell +
                             " is a wall or outside the grid. Enable automatic exit placement or choose a floor cell.", this);
        }
        else if (!IsReachable(generatedPlayerSpawnCell, generatedExitCell))
        {
            Debug.LogError("MazeGenerator: The exit cannot be reached from the player spawn.", this);
        }
    }

    /// <summary>
    /// Centers an orthographic 2D camera on the grid and sizes it to show every tile.
    /// The bounds include half a tile beyond the outer tile centers so the wall edges remain visible.
    /// </summary>
    private void FitCameraToMaze()
    {
        Camera cam = mazeCamera != null ? mazeCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("MazeGenerator: No camera is assigned and no MainCamera was found. Assign a camera or tag one MainCamera.", this);
            return;
        }

        if (!cam.orthographic)
        {
            Debug.LogWarning("MazeGenerator: Camera fitting expects an Orthographic camera. Set the camera Projection to Orthographic.", cam);
            return;
        }

        // The center between the first and last tile centers is also the center of the full grid.
        Vector3 gridCenter = transform.TransformPoint(new Vector3(
            (generatedWidth - 1) * tileSize * 0.5f,
            (generatedHeight - 1) * tileSize * 0.5f,
            0f));

        // Shift the camera parallel to its viewing plane, keeping its distance and rotation.
        float cameraDepth = Vector3.Dot(gridCenter - cam.transform.position, cam.transform.forward);
        cam.transform.position = gridCenter - cam.transform.forward * cameraDepth;

        // Include the outer half-tiles in the bounds, not just the tile centers.
        float minX = -tileSize * 0.5f;
        float minY = -tileSize * 0.5f;
        float maxX = (generatedWidth - 0.5f) * tileSize;
        float maxY = (generatedHeight - 0.5f) * tileSize;

        Vector3[] corners =
        {
            transform.TransformPoint(new Vector3(minX, minY, 0f)),
            transform.TransformPoint(new Vector3(minX, maxY, 0f)),
            transform.TransformPoint(new Vector3(maxX, minY, 0f)),
            transform.TransformPoint(new Vector3(maxX, maxY, 0f))
        };

        float halfViewWidth = 0f;
        float halfViewHeight = 0f;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 offset = corners[i] - gridCenter;
            halfViewWidth = Mathf.Max(halfViewWidth, Mathf.Abs(Vector3.Dot(offset, cam.transform.right)));
            halfViewHeight = Mathf.Max(halfViewHeight, Mathf.Abs(Vector3.Dot(offset, cam.transform.up)));
        }

        float tileWorldWidth = Vector3.Distance(
            CellToWorld(Vector2Int.zero), CellToWorld(Vector2Int.right));
        float tileWorldHeight = Vector3.Distance(
            CellToWorld(Vector2Int.zero), CellToWorld(Vector2Int.up));
        float padding = cameraPaddingTiles * Mathf.Max(tileWorldWidth, tileWorldHeight);
        float safeAspect = Mathf.Max(0.01f, cam.aspect);

        cam.orthographicSize = Mathf.Max(halfViewHeight, halfViewWidth / safeAspect) + padding;
    }

    private void BuildVisuals()
    {
        EnsureGeneratedRoot();
        ClearGeneratedChildren();

        if (wallPrefab == null)
            Debug.LogWarning("MazeGenerator: No Wall Prefab is assigned. The grid data will still be generated.", this);

        for (int x = 0; x < generatedWidth; x++)
        {
            for (int y = 0; y < generatedHeight; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                bool isFloor = walkableGrid[x, y];
                GameObject prefab = isFloor ? floorPrefab : wallPrefab;

                if (prefab == null)
                    continue;

                GameObject tile = Instantiate(prefab, CellToWorld(cell), Quaternion.identity, generatedRoot);
                tile.name = (isFloor ? "Floor_" : "Wall_") + x + "_" + y;

                if (scalePrefabsToTileSize)
                {
                    Vector3 prefabScale = prefab.transform.localScale;
                    tile.transform.localScale = new Vector3(
                        prefabScale.x * tileSize,
                        prefabScale.y * tileSize,
                        prefabScale.z);
                }
            }
        }
    }

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
            return;

        Transform parent = generatedParent != null ? generatedParent : transform;
        generatedRoot = parent.Find("GeneratedMaze");

        if (generatedRoot == null)
        {
            GameObject rootObject = new GameObject("GeneratedMaze");
            generatedRoot = rootObject.transform;
            generatedRoot.SetParent(parent, false);
            generatedRoot.localPosition = Vector3.zero;
            generatedRoot.localRotation = Quaternion.identity;
            generatedRoot.localScale = Vector3.one;
        }
    }

    private void ClearGeneratedChildren()
    {
        if (generatedRoot == null)
            return;

        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = generatedRoot.GetChild(i).gameObject;
            child.SetActive(false);
            child.transform.SetParent(null);

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (walkableGrid == null)
            return;

        Color oldColor = Gizmos.color;
        Gizmos.color = Color.gray;

        for (int x = 0; x < generatedWidth; x++)
        {
            for (int y = 0; y < generatedHeight; y++)
            {
                if (!walkableGrid[x, y])
                {
                    Gizmos.DrawWireCube(
                        CellToWorld(new Vector2Int(x, y)),
                        new Vector3(tileSize * 0.9f, tileSize * 0.9f, 0.05f));
                }
            }
        }

        Gizmos.color = oldColor;
    }
}