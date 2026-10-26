using UnityEngine;

public class FingerFourFormation : MonoBehaviour
{
    [Header("Leader")]
    [SerializeField] private Transform leader;

    [Header("Other Players")]
    [SerializeField] private KinematicArrive player2;
    [SerializeField] private KinematicArrive player3;
    [SerializeField] private KinematicArrive player4;

    [Header("Formation Offsets")]
    [SerializeField] private Vector2 player2Offset = new Vector2(-1.5f, -1.5f);
    [SerializeField] private Vector2 player3Offset = new Vector2(1.5f, -1.5f);

    [SerializeField] private Vector2 player4Offset = new Vector2(3f, -3f);

    void LateUpdate()
    {
        if (leader == null)
            return;

        // Rotate the formation to match the leader's heading.
        Quaternion heading = Quaternion.Euler(0f, 0f, leader.eulerAngles.z);

        UpdatePlayerTarget(player2, player2Offset, heading);
        UpdatePlayerTarget(player3, player3Offset, heading);
        UpdatePlayerTarget(player4, player4Offset, heading);
    }

    void UpdatePlayerTarget(KinematicArrive player, Vector2 offset, Quaternion heading)
    {
        if (player == null)
            return;

        // Convert the local formation slot to world coordinates.
        Vector3 worldOffset = heading * new Vector3(offset.x, offset.y, 0f);

        Vector2 slotPosition = (Vector2)(leader.position + worldOffset);

        // Tell this player where to move.
        player.SetTarget(slotPosition);
    }
}