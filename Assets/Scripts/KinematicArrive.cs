using NUnit.Framework.Constraints;
using Unity.VisualScripting;
using UnityEngine;

public class KinematicArrive : MonoBehaviour
{
    public class Steering
    {
        public Vector3 velocity;
        public float rotation;

        public Steering()
        {
            velocity = new Vector3(0, 0, 0);
            rotation = 0f;
        }
    }


    [SerializeField] private Rigidbody2D rb;
    public float timeToTarget = 0.25f;
    public float radius = 0.1f;
    public float maxSpeed = 10f;

    private Vector2 target;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        target = rb.position;
    }

    Steering GetSteering(Vector2 target)
    {
        Steering steering = new Steering();

        // Direction to target
        Vector2 direction = target - rb.position;

        // Check if we're in radius
        if (direction.magnitude < radius)
        {
            steering.velocity = Vector2.zero;
            return steering;
        }

        // Reach target in timeToTarget seconds
        steering.velocity = direction / timeToTarget;

        // Clamp speed
        if (steering.velocity.magnitude > maxSpeed)
        {
            steering.velocity = steering.velocity.normalized * maxSpeed;
        }

        // Rotation in 2D
        steering.rotation = Vector2.SignedAngle(Vector2.right, direction);

        return steering;
    }


    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector3 screenPos = Input.mousePosition;
            screenPos.z = -Camera.main.transform.position.z;

            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

            target = new Vector2(worldPos.x, worldPos.y);
        }
    }

    void FixedUpdate()
    {
        Steering steering = GetSteering(target);

        Debug.Log($"Target: {target}, Velocity: {steering.velocity}");

        // Actually move the player
        rb.linearVelocity = steering.velocity;

        // Face movement direction
        if (steering.velocity.sqrMagnitude > 0.001f)
        {
            rb.MoveRotation(steering.rotation);
        }
    }
}
