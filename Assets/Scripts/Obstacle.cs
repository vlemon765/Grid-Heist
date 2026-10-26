using UnityEngine;
using UnityEngine.InputSystem;

public class Obstacle : MonoBehaviour
{
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private string playerTag = "Player";

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame)
            return;

        if (obstaclePrefab == null || mainCamera == null)
            return;

        // Convert mouse position to world position
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        float cameraDistance = -mainCamera.transform.position.z;
        Vector3 screenPosition = new Vector3(mousePosition.x, mousePosition.y, cameraDistance);
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        worldPosition.z = 0f;

        // Find the angle that best blocks the players
        float angle = GetBlockingAngle(worldPosition);

        // Create the obstacle at the mouse position
        Instantiate(obstaclePrefab, worldPosition, Quaternion.Euler(0f, 0f, angle));
    }

    private float GetBlockingAngle(Vector2 obstaclePosition)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);

        Vector2 averageDirection = Vector2.zero;
        Vector2 nearestDirection = Vector2.zero;

        float nearestDistance = float.PositiveInfinity;

        foreach (GameObject player in players)
        {
            Vector2 direction = (Vector2)player.transform.position - obstaclePosition;

            float distance = direction.sqrMagnitude;

            // Skip players at the obstacle's exact position
            if (distance < 0.0001f)
                continue;

            // Give each player equal weight
            averageDirection += direction.normalized;

            // Remember the nearest player as a fallback
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestDirection = direction;
            }
        }

        // No usable players were found
        if (nearestDistance == float.PositiveInfinity)
            return 0f;

        // Handle players spread in opposing directions
        Vector2 directionToBlock;

        if (averageDirection.sqrMagnitude > 0.0001f)
            directionToBlock = averageDirection.normalized;
        else
            directionToBlock = nearestDirection.normalized;

        // Rotate the rectangle's long side perpendicular
        // to the direction toward the players
        return Mathf.Atan2(directionToBlock.y, directionToBlock.x) * Mathf.Rad2Deg + 90f;
    }
}
