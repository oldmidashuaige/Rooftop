using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Object;

/// <summary>
/// 人机丧尸刷怪器：仅服务器运行。开局补满 maxZombies，死光 respawnDelay 后补 1 只。
/// </summary>
public class ZombieSpawner : MonoBehaviour
{
    public GameObject zombiePrefab;
    public Vector3[] spawnPoints;
    public int maxZombies = 3;
    public float respawnDelay = 5f;

    private float lastSpawnCheck;
    private bool initialized;

    private void Update()
    {
        if (!InstanceFinder.IsServerStarted) return;
        if (zombiePrefab == null) return;

        if (!initialized)
        {
            initialized = true;
            for (int i = 0; i < maxZombies; i++) SpawnOne();
            return;
        }

        var all = FindObjectsByType<SimpleZombie>();
        int alive = 0;
        foreach (var z in all) if (!z.IsDead) alive++;

        if (alive >= maxZombies)
        {
            lastSpawnCheck = 0f;
            return;
        }

        lastSpawnCheck += Time.deltaTime;
        if (lastSpawnCheck >= respawnDelay)
        {
            lastSpawnCheck = 0f;
            SpawnOne();
        }
    }

    private void SpawnOne()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        Vector3 pos = spawnPoints[Random.Range(0, spawnPoints.Length)];

        var go = Instantiate(zombiePrefab, pos, Quaternion.identity);
        var no = go.GetComponent<NetworkObject>();
        if (no != null)
        {
            InstanceFinder.ServerManager.Spawn(no);
            Debug.Log($"[Zombie] 服务器刷新人机丧尸 at {pos}");
        }
    }
}
