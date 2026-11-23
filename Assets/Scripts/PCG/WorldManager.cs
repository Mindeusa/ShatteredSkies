using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PCG
{
    public class WorldManager : MonoBehaviour
    {
        [Header("Generation Settings")]
        public GameObject islandPrefab; 
        public GameObject player; 
    
        [Header("World Rules")]
        public float chunkSize = 300f;  
        public int viewDistance = 2;    
        public float islandSpawnChance = 0.5f; 
        public float positionJitter = 80f; 

        private Dictionary<Vector2Int, GameObject> activeChunks = new Dictionary<Vector2Int, GameObject>();
        private float worldSeedX, worldSeedY;

        private bool isWorldReady = false;
        private Transform spawnedPlayerTransform;

        void Start()
        {
            if (player == null)
            {
                Debug.LogError("WORLD MANAGER: Du har inte dragit in Player i Inspektorn!!");
                return;
            }

            worldSeedX = Random.Range(-10000f, 10000f);
            worldSeedY = Random.Range(-10000f, 10000f);

            Debug.Log("WORLD MANAGER: Genererar Start-Ö...");
            GenerateChunk(new Vector2Int(0, 0), true);
        }

        IEnumerator WaitForStartIsland(GameObject startIsland)
        {
            Debug.Log("WORLD MANAGER: Väntar på att ön ska byggas...");
            // Vänta tills nästa frame så Unity hinner bygga ön
            yield return null;
            yield return null;
        
            // Letar efter PCG.Grid (Ditt ö-skript)
            PCG.Grid islandGrid = startIsland.GetComponent<PCG.Grid>(); 
        
            if (islandGrid != null)
            {
                // Ber ön räkna ut exakt var det finns gräs!
                Vector3 safeSpot = islandGrid.GetSafeSpawnPosition();
                Debug.Log("WORLD MANAGER: Hittade en säker landningsplats på: " + safeSpot);
            
                // Instantiatera (skapa) spelaren precis ovanför platsen!
                GameObject playerObj = Instantiate(player, safeSpot + new Vector3(0, 5f, 0), Quaternion.identity);
                spawnedPlayerTransform = playerObj.transform;
            
                Debug.Log("WORLD MANAGER: Spelaren har spawnats!");
            }
            else
            {
                Debug.LogError("WORLD MANAGER: Hittade inte Grid-scriptet på din Prefab!");
            }

            isWorldReady = true;
        }

        void Update()
        {
            // Fortsätt inte förrän spelet är igång och spelaren existerar
            if (!isWorldReady || spawnedPlayerTransform == null) return;

            int currentChunkX = Mathf.RoundToInt(spawnedPlayerTransform.position.x / chunkSize);
            int currentChunkZ = Mathf.RoundToInt(spawnedPlayerTransform.position.z / chunkSize);

            for (int x = currentChunkX - viewDistance; x <= currentChunkX + viewDistance; x++)
            {
                for (int z = currentChunkZ - viewDistance; z <= currentChunkZ + viewDistance; z++)
                {
                    Vector2Int chunkCoord = new Vector2Int(x, z);

                    if (!activeChunks.ContainsKey(chunkCoord))
                    {
                        GenerateChunk(chunkCoord, false); 
                    }
                }
            }
        }

        void GenerateChunk(Vector2Int coord, bool forceSpawn)
        {
            activeChunks.Add(coord, null); 

            float noiseVal = Mathf.PerlinNoise(coord.x * 0.3f + worldSeedX, coord.y * 0.3f + worldSeedY);

            if (noiseVal < islandSpawnChance || forceSpawn)
            {
                float baseX = coord.x * chunkSize;
                float baseZ = coord.y * chunkSize;

                Random.InitState((int)(coord.x * 1000 + coord.y));
                float offsetX = Random.Range(-positionJitter, positionJitter);
                float offsetZ = Random.Range(-positionJitter, positionJitter);
                float offsetY = Random.Range(-40f, 40f); 

                Vector3 spawnPosition = new Vector3(baseX + offsetX, offsetY, baseZ + offsetZ);

                if (forceSpawn)
                {
                    spawnPosition = new Vector3(0, 0, 0); 
                }

                GameObject newIsland = Instantiate(islandPrefab, spawnPosition, Quaternion.identity);
                newIsland.transform.SetParent(this.transform);
            
                activeChunks[coord] = newIsland;
            
                if (forceSpawn)
                {
                    StartCoroutine(WaitForStartIsland(newIsland));
                }
            }
        }
    }
}