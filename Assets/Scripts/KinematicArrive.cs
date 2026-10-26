using UnityEngine;
using UnityEngine.InputSystem;

public class KinematicArrive : MonoBehaviour
{
    public class Steering
    {
        public Vector2 velocity;
        public float rotation;
    }

    [Header("Movement")]
    [SerializeField] private Rigidbody2D rb;
    public float timeToTarget = 0.25f;
    public float radius = 0.1f;
    public float maxSpeed = 10f;
    public float rotationSpeed = 360f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private float avoidanceRadius = 0.4f;
    [SerializeField] private float waypointMargin = 0.25f;
    [SerializeField] private float waypointReachRadius = 0.35f;
    [SerializeField] private float stuckTime = 1.25f;
    [SerializeField] private float stuckDistance = 0.08f;
    [SerializeField] private float replanMargin = 0.5f;
    private Vector2 avoidanceWaypoint;
    private bool hasAvoidanceWaypoint;
    private Vector2 lastPosition;
    private Vector2 failedWaypoint;
    private bool hasFailedWaypoint;
    private float stuckTimer;

    [Header("Input")]
    [SerializeField] private bool useMouseInput = true;

    private Vector2 target;
    private Camera mainCamera;
    private float targetRotation;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        target = rb.position;
        targetRotation = rb.rotation;
        mainCamera = Camera.main;
        lastPosition = rb.position;
    }

    public void SetTarget(Vector2 newTarget)
    {
        target = newTarget;
    }

    void Update()
    {
        if (!useMouseInput || Mouse.current == null || mainCamera == null)
            return;

        if (Mouse.current.leftButton.isPressed)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            float cameraDistance = -mainCamera.transform.position.z;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, cameraDistance));
            target = new Vector2(worldPos.x, worldPos.y);
        }
    }

    private bool HasClearPath(Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        float distance = direction.magnitude;

        if (distance < 0.05f)
            return true;

        RaycastHit2D hit = Physics2D.CircleCast(
            from,
            avoidanceRadius,
            direction / distance,
            distance,
            obstacleLayers
        );

        return hit.collider == null;
    }

    private bool TryFindWaypoint(Collider2D obstacle, Vector2 targetPosition, out Vector2 bestWaypoint)
    {
        bestWaypoint = rb.position;

        Bounds bounds = obstacle.bounds;
        float margin = avoidanceRadius + waypointMargin;

        float bestScore = float.PositiveInfinity;
        bool found = false;

        // Search at increasing distances from the wall.
        for (int ring = 1; ring <= 3; ring++)
        {
            float m = margin + (ring - 1) * replanMargin;

            Vector2[] candidates = {
            new Vector2(bounds.min.x - m, bounds.min.y - m),
            new Vector2(bounds.min.x - m, bounds.max.y + m),
            new Vector2(bounds.max.x + m, bounds.min.y - m),
            new Vector2(bounds.max.x + m, bounds.max.y + m),

            new Vector2(bounds.min.x - m, bounds.center.y),
            new Vector2(bounds.max.x + m, bounds.center.y),
            new Vector2(bounds.center.x, bounds.min.y - m),
            new Vector2(bounds.center.x, bounds.max.y + m)
        };

            foreach (Vector2 candidate in candidates)
            {
                // Do not choose a point inside an obstacle.
                if (Physics2D.OverlapCircle(candidate, avoidanceRadius, obstacleLayers))
                    continue;

                // Do not choose a point the player cannot reach.
                if (!HasClearPath(rb.position, candidate))
                    continue;

                // Avoid immediately selecting the waypoint that failed.
                if (hasFailedWaypoint && Vector2.Distance(candidate, failedWaypoint) < 0.2f)
                    continue;

                bool targetVisible = HasClearPath(candidate, targetPosition);
                float travelDistance = Vector2.Distance(rb.position, candidate);
                float remainingDistance = Vector2.Distance(candidate, targetPosition);

                // Strongly prefer a candidate from which the target
                // can be reached directly.
                float score = travelDistance + remainingDistance;

                if (!targetVisible)
                    score += 1000f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestWaypoint = candidate;
                    found = true;
                }
            }

            // A direct route to the target is better than a detour.
            if (found && HasClearPath(bestWaypoint, targetPosition))
                break;
        }

        return found;
    }

    Steering GetSteering(Vector2 targetPosition)
    {
        Steering steering = new Steering();
        Vector2 destination = targetPosition;

        // Keep moving toward the waypoint until it is reached.
        if (hasAvoidanceWaypoint)
        {
            if (Vector2.Distance(rb.position, avoidanceWaypoint) <= waypointReachRadius)
            {
                hasAvoidanceWaypoint = false;
                hasFailedWaypoint = false;
            }
            else if (!HasClearPath(rb.position, avoidanceWaypoint))
            {
                failedWaypoint = avoidanceWaypoint;
                hasFailedWaypoint = true;
                hasAvoidanceWaypoint = false;
            }
            else
            {
                destination = avoidanceWaypoint;
            }
        }

        Vector2 direction = destination - rb.position;

        // If the target is blocked, find a waypoint around the wall.
        if (!hasAvoidanceWaypoint && direction.sqrMagnitude > 0.001f && !HasClearPath(rb.position, destination))
        {
            RaycastHit2D hit = Physics2D.CircleCast(
                rb.position,
                avoidanceRadius,
                direction.normalized,
                direction.magnitude,
                obstacleLayers
            );

            if (hit.collider != null && TryFindWaypoint(hit.collider, targetPosition, out Vector2 waypoint))
            {
                avoidanceWaypoint = waypoint;
                hasAvoidanceWaypoint = true;

                destination = waypoint;
                direction = destination - rb.position;
            }
            else
            {
                // No reachable waypoint was found. Stop rather than drive into the wall.
                return steering;
            }
        }

        if (direction.magnitude <= radius)
            return steering;

        steering.velocity = Vector2.ClampMagnitude(direction / Mathf.Max(timeToTarget, 0.01f), maxSpeed);

        if (steering.velocity.sqrMagnitude > 0.001f)
            steering.rotation = Mathf.Atan2(steering.velocity.y, steering.velocity.x) * Mathf.Rad2Deg - 90f;

        return steering;
    }

    void FixedUpdate()
    {
        Steering steering = GetSteering(target);

        // Detect when movement is blocked by physics.
        float movedDistance = Vector2.Distance(rb.position, lastPosition);

        if (steering.velocity.sqrMagnitude > 0.1f && movedDistance < stuckDistance * Time.fixedDeltaTime)
        {
            stuckTimer += Time.fixedDeltaTime;

            if (stuckTimer >= stuckTime && hasAvoidanceWaypoint)
            {
                failedWaypoint = avoidanceWaypoint;
                hasFailedWaypoint = true;
                hasAvoidanceWaypoint = false;
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = rb.position;
        rb.linearVelocity = steering.velocity;

        if (steering.velocity.sqrMagnitude > 0.001f)
            targetRotation = steering.rotation;

        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }
}