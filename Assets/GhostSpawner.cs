using UnityEngine;

public class GhostSpawner : MonoBehaviour
{
    public GameObject ghostPrefab;      // Assign ghost prefab here
    public Transform player;            // Assign the player transform
    public float spawnInterval = 10f;   // How often ghosts spawn (seconds)
    public float spawnDistanceMin = 8f;
    public float spawnDistanceMax = 12f;

    private float timer;

    void Start()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        timer = spawnInterval;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SpawnGhost();
            timer = spawnInterval + Random.Range(-2f, 3f); // Add randomness
        }
    }

    void SpawnGhost()
    {
        if (ghostPrefab == null || player == null)
        {
            Debug.LogWarning("[GhostSpawner] Missing reference.");
            return;
        }

        Vector2 randomDir = Random.insideUnitCircle.normalized;
        float distance = Random.Range(spawnDistanceMin, spawnDistanceMax);
        Vector3 spawnPos = player.position + (Vector3)(randomDir * distance);

        Instantiate(ghostPrefab, spawnPos, Quaternion.identity);
        Debug.Log("[GhostSpawner] Ghost spawned at " + spawnPos);
    }
}