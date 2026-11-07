using UnityEngine;

public class PlayerGameState : MonoBehaviour
{
    [SerializeField] private MazeGenerator maze;
    [SerializeField] private string copTag = "Cop";
    [SerializeField] private GameObject winText;
    [SerializeField] private GameObject loseText;
    [SerializeField] private GameObject endButton;

    private GameObject player;
    private bool gameEnded;

    private void Start()
    {
        winText.SetActive(false);
        loseText.SetActive(false);
        endButton.SetActive(false);
    }

    private void Update()
    {
        if (gameEnded || maze == null || !maze.IsGenerated)
            return;

        if (player == null)
        {
            FindPlayer();
            return;
        }

        if (maze.WorldToCell(player.transform.position) == maze.ExitCell)
        {
            winText.SetActive(true);
            EndGame();
        }
    }

    private void FindPlayer()
    {
        player = maze.SpawnedPlayer;

        if (player == null)
            return;

        // The player is spawned at runtime, so a small relay on it reports collisions back here.
        PlayerContactRelay relay = player.AddComponent<PlayerContactRelay>();
        relay.Initialize(this);
    }

    public void HandleContact(GameObject other)
    {
        if (!gameEnded && other.CompareTag(copTag))
        {
            loseText.SetActive(true);
            EndGame();
        }
    }

    private void EndGame()
    {
        gameEnded = true;

        // Disable every script on the player (movement, input) except the relay.
        foreach (MonoBehaviour behaviour in player.GetComponents<MonoBehaviour>())
        {
            if (behaviour is PlayerContactRelay)
                continue;

            behaviour.enabled = false;
        }

        // Stop any physics-driven movement or sliding.
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
            body.simulated = false;
        endButton.SetActive(true);
    }
}

public class PlayerContactRelay : MonoBehaviour
{
    private PlayerGameState handler;

    public void Initialize(PlayerGameState owner)
    {
        handler = owner;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (handler != null)
            handler.HandleContact(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (handler != null)
            handler.HandleContact(other.gameObject);
    }
}