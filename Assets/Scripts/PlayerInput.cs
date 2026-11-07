
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float tileSize = 1f;

    [Header("Maze")]
    [SerializeField] private MazeGenerator mazeGenerator;

    private Controls controls;
    private Vector2Int currentCell;

    private void Awake()
    {
        controls = new Controls();
    }

    private void Start()
    {
        if (mazeGenerator == null)
            mazeGenerator = FindFirstObjectByType<MazeGenerator>();

        if (mazeGenerator == null || !mazeGenerator.IsGenerated)
        {
            Debug.LogError("PlayerInput: No generated MazeGenerator found.", this);
            enabled = false;
            return;
        }

        // Use the actual grid position rather than assuming (0, 0).
        currentCell = mazeGenerator.WorldToCell(transform.position);

        // Match the maze's tile size.
        tileSize = mazeGenerator.TileSize;

        // Snap to the center of the current cell.
        transform.position = mazeGenerator.CellToWorld(currentCell);
    }

    private void OnEnable()
    {
        controls.Player.Move.performed += OnMove;
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Move.performed -= OnMove;
        controls.Player.Disable();
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();

        // Prevent diagonal movement.
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            input.y = 0;
        else
            input.x = 0;

        if (input == Vector2.zero)
            return;

        Vector2Int direction = new Vector2Int(
            Mathf.RoundToInt(input.x),
            Mathf.RoundToInt(input.y)
        );

        Vector2Int nextCell = currentCell + direction;

        // Prevent movement into walls or outside the grid.
        if (!mazeGenerator.IsWalkable(nextCell))
            return;

        // Update the grid position.
        currentCell = nextCell;

        // Move instantly to the exact tile center.
        transform.position = mazeGenerator.CellToWorld(currentCell);
    }
}
