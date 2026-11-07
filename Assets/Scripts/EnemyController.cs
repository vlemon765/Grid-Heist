using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private Transform player;

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
        else
            Debug.LogError("Player Object not found.");
    }

    // Update is called once per frame
    void Update()
    {

    }
}
