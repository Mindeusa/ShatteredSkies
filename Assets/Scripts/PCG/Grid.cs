using UnityEngine;
using System.Collections.Generic;

namespace PCG // Ligger i samma namespace som du hade!
{
    public class Grid : MonoBehaviour
    {
        // --- NYA BIOME VARIABLER ---
        [Header("Forest Biome Materials")]
        public Material forestTerrainMaterial;
        public Material forestGrassMaterial;
        public GameObject[] forestTreePrefabs;

        [Header("Desert Biome Materials")]
        public Material desertTerrainMaterial;
        public Material desertGrassMaterial;
        // Här kan du lägga till Cactus prefabs senare!

        [Header("Shared Materials")]
        public Material edgeMaterial;
        public Material pondMaterial;
        public Material waterfallMaterial;
        public Material cloudMaterial;

        // --- ENUM FÖR BIOMES ---
        public enum BiomeType { Forest, Desert }
        public BiomeType currentBiome;

        // Raderade public GameObject playerObj eftersom WorldManager hanterar spelaren!

        GameObject terrainObject, grassObject, waterObject, waterfallObject, edgeObject, cloudContainer;
        Mesh terrainMesh, grassMesh, waterMesh, waterfallMesh, edgeMesh;
        MeshCollider terrainCollider, edgeCollider;

        public Texture2D[] directionalTextures;

        [Header("Island Base Shape")]
        public int size = 80;
        public int depth = 25;

        [Header("Satellites (Peninsulas)")]
        public int minSatellites = 1;
        public int maxSatellites = 3;
        public float satelliteDistance = 0.45f;
        public float satelliteSizeMin = 0.15f;
        public float satelliteSizeMax = 0.3f;
        public float bridgeThickness = 0.4f;

        [Header("Grid Domain Warping")]
        public float warpScale = 0.03f;
        public float warpAmount = 2.5f;

        [Header("Organic Generation")]
        public float shapeNoiseScale = 0.04f;
        public float shapeDistortion = 1.0f;
        public float spikeNoiseScale = 0.08f;

        [Header("Topography Settings")]
        public float hillHeight = 10f;
        public float mountainBoost = 6f;
        public float hillNoiseScale = 0.06f;
        public float dynamicPondLevel = 3.5f;

        [Header("Low Poly Jitter Settings")]
        public float jitterAmount = 0.24f;
        public float jitterScale = 0.3f;

        [Header("Foliage")]
        public float treeNoiseScale = 0.05f;
        public float treeDensity = 0.5f;

        // --- DEFORMABLE SHELL DATA ---
        public float[,] topHeights;
        public float[,] bottomHeights;
        public bool[,] skyCells;
        public bool[,] waterCells;
        public bool[,] waterfallCells;
        public bool[,] grassCells;

        public Vector2[,] vertexOffsets;

        private float shapeOffsetX, shapeOffsetY;
        private float spikeOffsetX, spikeOffsetY;
        private float topOffsetX, topOffsetY;
        private int pondTries = 0;

        struct Satellite
        {
            public Vector2 position;
            public float radius;
        }
        private List<Satellite> satellites = new List<Satellite>();

        void Start()
        {
            InitializeGameObjects();

            // --- VÄLJ BIOME BASERAT PÅ VÄRLDSPOSITION ---
            // Gör att öar i samma "zon" får samma klimat
            float biomeNoise = Mathf.PerlinNoise(transform.position.x * 0.005f, transform.position.z * 0.005f);

            if (biomeNoise > 0.5f)
            {
                currentBiome = BiomeType.Forest;
                terrainObject.GetComponent<MeshRenderer>().material = forestTerrainMaterial;
                grassObject.GetComponent<MeshRenderer>().material = forestGrassMaterial;
            }
            else
            {
                currentBiome = BiomeType.Desert;
                terrainObject.GetComponent<MeshRenderer>().material = desertTerrainMaterial;
                grassObject.GetComponent<MeshRenderer>().material = desertGrassMaterial;
            }
            // ----------------------------------------------

            shapeOffsetX = Random.Range(-10000f, 10000f);
            shapeOffsetY = Random.Range(-10000f, 10000f);
            spikeOffsetX = Random.Range(-10000f, 10000f);
            spikeOffsetY = Random.Range(-10000f, 10000f);
            topOffsetX = Random.Range(-10000f, 10000f);
            topOffsetY = Random.Range(-10000f, 10000f);

            satellites.Clear();
            int numSat = Random.Range(minSatellites, maxSatellites + 1);
            for (int i = 0; i < numSat; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(satelliteDistance - 0.1f, satelliteDistance + 0.1f);
                float rad = Random.Range(satelliteSizeMin, satelliteSizeMax);
                satellites.Add(new Satellite { position = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist), radius = rad });
            }

            GenerateTerrainData();
            UpdateMeshes();

            DrawTexture();
            GenerateTrees();
        }

        void InitializeGameObjects()
        {
            terrainMesh = new Mesh(); grassMesh = new Mesh();
            waterMesh = new Mesh(); waterfallMesh = new Mesh();
            edgeMesh = new Mesh();

            terrainObject = new GameObject("Terrain");
            terrainObject.transform.SetParent(transform);
            terrainObject.AddComponent<MeshFilter>().mesh = terrainMesh;
            terrainObject.AddComponent<MeshRenderer>(); // Materialet sätts i Start()!
            terrainCollider = terrainObject.AddComponent<MeshCollider>();

            grassObject = new GameObject("Grass");
            grassObject.transform.SetParent(transform);
            grassObject.AddComponent<MeshFilter>().mesh = grassMesh;
            grassObject.AddComponent<MeshRenderer>(); // Materialet sätts i Start()!

            waterObject = new GameObject("Water");
            waterObject.transform.SetParent(transform);
            waterObject.AddComponent<MeshFilter>().mesh = waterMesh;
            waterObject.AddComponent<MeshRenderer>().material = pondMaterial;

            waterfallObject = new GameObject("WaterfallMesh");
            waterfallObject.transform.SetParent(transform);
            waterfallObject.AddComponent<MeshFilter>().mesh = waterfallMesh;
            waterfallObject.AddComponent<MeshRenderer>().material = waterfallMaterial;

            edgeObject = new GameObject("Edge");
            edgeObject.transform.SetParent(transform);
            edgeObject.AddComponent<MeshFilter>().mesh = edgeMesh;
            edgeObject.AddComponent<MeshRenderer>().material = edgeMaterial;
            edgeCollider = edgeObject.AddComponent<MeshCollider>();

            cloudContainer = new GameObject("Abyss Clouds");
            cloudContainer.transform.SetParent(transform);
        }

        float SmoothMax(float a, float b, float k)
        {
            k = Mathf.Max(0.0001f, k);
            float h = Mathf.Clamp01(0.5f + 0.5f * (a - b) / k);
            return Mathf.Lerp(b, a, h) + k * h * (1.0f - h);
        }

        float GetIslandMask(float x, float y)
        {
            float nx = (x - size / 2f) / (size / 2f);
            float ny = (y - size / 2f) / (size / 2f);
            Vector2 p = new Vector2(nx, ny);

            float combinedDensity = 1f - (p.magnitude / 0.5f);
            foreach (var sat in satellites)
            {
                float satDensity = 1f - (Vector2.Distance(p, sat.position) / sat.radius);
                combinedDensity = SmoothMax(combinedDensity, satDensity, bridgeThickness);
            }

            float shapeNoise = Mathf.PerlinNoise(x * shapeNoiseScale + shapeOffsetX, y * shapeNoiseScale + shapeOffsetY);
            float edgeNoise = Mathf.PerlinNoise(x * 0.15f + shapeOffsetX, y * 0.15f + shapeOffsetY);

            return combinedDensity + (shapeNoise - 0.5f) * shapeDistortion * 0.6f + (edgeNoise - 0.5f) * 0.2f;
        }

        void GenerateTerrainData()
        {
            topHeights = new float[size + 1, size + 1];
            bottomHeights = new float[size + 1, size + 1];
            skyCells = new bool[size, size];
            waterCells = new bool[size, size];
            grassCells = new bool[size, size];

            for (int cz = 0; cz < size; cz++)
            {
                for (int cx = 0; cx < size; cx++)
                {
                    skyCells[cx, cz] = GetIslandMask(cx, cz) <= 0f;
                }
            }

            CreatePond();
            GenerateGrass();
            UpdateWaterfalls();

            for (int vz = 0; vz <= size; vz++)
            {
                for (int vx = 0; vx <= size; vx++)
                {
                    bool touchesWater = IsWater(vx, vz) || IsWater(vx - 1, vz) || IsWater(vx, vz - 1) || IsWater(vx - 1, vz - 1);
                    bool touchesSky = IsSky(vx, vz) || IsSky(vx - 1, vz) || IsSky(vx, vz - 1) || IsSky(vx - 1, vz - 1);
                    bool allWater = IsWater(vx, vz) && IsWater(vx - 1, vz) && IsWater(vx, vz - 1) && IsWater(vx - 1, vz - 1);

                    float px = vx * hillNoiseScale + topOffsetX;
                    float pz = vz * hillNoiseScale + topOffsetY;
                    float noiseVal = Mathf.PerlinNoise(px, pz);

                    float nx = (vx - size / 2f) / (size / 2f);
                    float nz = (vz - size / 2f) / (size / 2f);
                    float dist = Mathf.Clamp01(Mathf.Sqrt(nx * nx + nz * nz));

                    float domeShape = Mathf.Lerp(0.2f, 1.0f, 1f - Mathf.Pow(Mathf.Clamp01(dist), 2f));
                    float rawY = (noiseVal * hillHeight * domeShape) + (domeShape * mountainBoost);

                    if (allWater)
                        topHeights[vx, vz] = dynamicPondLevel - 1.5f;
                    else if (touchesWater)
                    {
                        if (touchesSky) topHeights[vx, vz] = dynamicPondLevel - 1.5f;
                        else topHeights[vx, vz] = dynamicPondLevel + 0.5f;
                    }
                    else
                        topHeights[vx, vz] = Mathf.Max(rawY, dynamicPondLevel + 0.5f);

                    float mask = Mathf.Max(0f, GetIslandMask(vx, vz));
                    float taper = Mathf.Clamp01(mask * 2.5f);

                    float spikeNoise = Mathf.PerlinNoise(vx * spikeNoiseScale + spikeOffsetX, vz * spikeNoiseScale + spikeOffsetY);
                    float spikes = Mathf.Pow(Mathf.Clamp01(spikeNoise), 2.5f);

                    bottomHeights[vx, vz] = topHeights[vx, vz] - ((0.2f + spikes * 0.8f) * depth * taper) - 1.5f;
                }
            }

            vertexOffsets = new Vector2[size + 1, size + 1];
            for (int vz = 0; vz <= size; vz++)
                for (int vx = 0; vx <= size; vx++)
                    vertexOffsets[vx, vz] = new Vector2(vx, vz);

            for (int i = 0; i < 20; i++)
            {
                Vector2[,] newOffsets = new Vector2[size + 1, size + 1];
                for (int vz = 0; vz <= size; vz++)
                {
                    for (int vx = 0; vx <= size; vx++)
                    {
                        newOffsets[vx, vz] = vertexOffsets[vx, vz];

                        int skyCount = 0; int waterCount = 0; int landCount = 0;

                        void CheckCell(int cx, int cz)
                        {
                            if (cx >= 0 && cx < size && cz >= 0 && cz < size)
                            {
                                if (skyCells[cx, cz]) skyCount++;
                                else if (waterCells[cx, cz] || waterfallCells[cx, cz]) waterCount++;
                                else landCount++;
                            }
                            else skyCount++;
                        }

                        CheckCell(vx, vz); CheckCell(vx - 1, vz); CheckCell(vx, vz - 1); CheckCell(vx - 1, vz - 1);

                        bool isBorder = (skyCount > 0 && skyCount < 4) || (waterCount > 0 && waterCount < 4);

                        if (landCount > 0 || waterCount > 0)
                        {
                            Vector2 sum = Vector2.zero; int neighbors = 0;
                            if (vx > 0) { sum += vertexOffsets[vx - 1, vz]; neighbors++; }
                            if (vx < size) { sum += vertexOffsets[vx + 1, vz]; neighbors++; }
                            if (vz > 0) { sum += vertexOffsets[vx, vz - 1]; neighbors++; }
                            if (vz < size) { sum += vertexOffsets[vx, vz + 1]; neighbors++; }

                            float smoothStrength = isBorder ? 0.65f : 0.25f;
                            newOffsets[vx, vz] = Vector2.Lerp(vertexOffsets[vx, vz], sum / neighbors, smoothStrength);
                        }
                    }
                }
                vertexOffsets = newOffsets;
            }
        }

        bool IsWater(int cx, int cz)
        {
            if (cx < 0 || cx >= size || cz < 0 || cz >= size) return false;
            return waterCells[cx, cz];
        }
        bool IsSky(int cx, int cz)
        {
            if (cx < 0 || cx >= size || cz < 0 || cz >= size) return true;
            return skyCells[cx, cz];
        }
        bool IsWaterfall(int cx, int cz)
        {
            if (cx < 0 || cx >= size || cz < 0 || cz >= size) return false;
            return waterfallCells[cx, cz];
        }

        void UpdateWaterfalls()
        {
            waterfallCells = new bool[size, size];
            for (int cz = 0; cz < size; cz++)
            {
                for (int cx = 0; cx < size; cx++)
                {
                    if (skyCells[cx, cz])
                    {
                        if ((cx > 0 && waterCells[cx - 1, cz]) ||
                            (cx < size - 1 && waterCells[cx + 1, cz]) ||
                            (cz > 0 && waterCells[cx, cz - 1]) ||
                            (cz < size - 1 && waterCells[cx, cz + 1]))
                        {
                            waterfallCells[cx, cz] = true;
                        }
                    }
                }
            }
        }

        public void ModifyTerrain(Vector3 hitPoint, bool dig)
        {
            // Justerar träffpunkten så den matchar lokala koordinater inom prefaben!
            Vector3 localHit = hitPoint - transform.position;

            int estX = Mathf.RoundToInt(localHit.x);
            int estZ = Mathf.RoundToInt(localHit.z);

            int bestCX = estX; int bestCZ = estZ;
            float bestDist = float.MaxValue;

            for (int x = estX - 5; x <= estX + 5; x++)
            {
                for (int z = estZ - 5; z <= estZ + 5; z++)
                {
                    if (x >= 0 && x < size && z >= 0 && z < size)
                    {
                        Vector3 v0 = GetTopVertex(x, z + 1);
                        Vector3 v1 = GetTopVertex(x + 1, z + 1);
                        Vector3 v2 = GetTopVertex(x, z);
                        Vector3 v3 = GetTopVertex(x + 1, z);
                        Vector3 centerTop = (v0 + v1 + v2 + v3) / 4f;

                        Vector3 b0 = GetBottomVertex(x, z + 1);
                        Vector3 b1 = GetBottomVertex(x + 1, z + 1);
                        Vector3 b2 = GetBottomVertex(x, z);
                        Vector3 b3 = GetBottomVertex(x + 1, z);
                        Vector3 centerBot = (b0 + b1 + b2 + b3) / 4f;

                        float distTop = Vector3.Distance(centerTop, hitPoint);
                        float distBot = Vector3.Distance(centerBot, hitPoint);
                        float dist = Mathf.Min(distTop, distBot);

                        if (dist < bestDist) { bestDist = dist; bestCX = x; bestCZ = z; }
                    }
                }
            }

            float amount = dig ? -0.8f : 0.8f;
            int[] dx = { 0, 1, 0, 1 };
            int[] dz = { 0, 0, 1, 1 };

            for (int i = 0; i < 4; i++)
            {
                int nx = bestCX + dx[i];
                int nz = bestCZ + dz[i];

                if (nx >= 0 && nx <= size && nz >= 0 && nz <= size)
                {
                    if (!dig)
                    {
                        bool wasSky = false;
                        if (nx < size && nz < size && skyCells[nx, nz]) { skyCells[nx, nz] = false; waterCells[nx, nz] = false; wasSky = true; }
                        if (nx > 0 && nz < size && skyCells[nx - 1, nz]) { skyCells[nx - 1, nz] = false; waterCells[nx - 1, nz] = false; wasSky = true; }
                        if (nx < size && nz > 0 && skyCells[nx, nz - 1]) { skyCells[nx, nz - 1] = false; waterCells[nx, nz - 1] = false; wasSky = true; }
                        if (nx > 0 && nz > 0 && skyCells[nx - 1, nz - 1]) { skyCells[nx - 1, nz - 1] = false; waterCells[nx - 1, nz - 1] = false; wasSky = true; }

                        if (wasSky)
                        {
                            topHeights[nx, nz] = Mathf.Lerp(topHeights[nx, nz], localHit.y + 0.2f, 0.7f);
                            bottomHeights[nx, nz] = Mathf.Lerp(bottomHeights[nx, nz], localHit.y - 2.5f, 0.7f);
                        }
                    }

                    topHeights[nx, nz] += amount;

                    if (dig && topHeights[nx, nz] <= bottomHeights[nx, nz] + 0.5f)
                    {
                        if (nx < size && nz < size) { skyCells[nx, nz] = true; waterCells[nx, nz] = false; }
                        if (nx > 0 && nz < size) { skyCells[nx - 1, nz] = true; waterCells[nx - 1, nz] = false; }
                        if (nx < size && nz > 0) { skyCells[nx, nz - 1] = true; waterCells[nx, nz - 1] = false; }
                        if (nx > 0 && nz > 0) { skyCells[nx - 1, nz - 1] = true; waterCells[nx - 1, nz - 1] = false; }
                    }
                }
            }

            UpdateWaterfalls();
            UpdateMeshes();
        }

        public void UpdateMeshes()
        {
            DrawTerrainMesh();
            DrawEdgeMesh();
            GenerateSteam();
        }

        Vector3 ApplyJitter(int vx, float y, int vz, float yJitterMult)
        {
            float wx = vertexOffsets[vx, vz].x;
            float wz = vertexOffsets[vx, vz].y;

            float wX = (Mathf.PerlinNoise(wx * warpScale + shapeOffsetX, wz * warpScale + shapeOffsetY) - 0.5f) * 2f * warpAmount;
            float wZ = (Mathf.PerlinNoise(wz * warpScale + spikeOffsetX, wx * warpScale + spikeOffsetY) - 0.5f) * 2f * warpAmount;

            wx += wX;
            wz += wZ;

            float jx = wx * jitterScale + 100f;
            float jz = wz * jitterScale + 300f;

            float xOffset = (Mathf.PerlinNoise(jx, jz) - 0.5f) * 2f * jitterAmount;
            float zOffset = (Mathf.PerlinNoise(jz, jx) - 0.5f) * 2f * jitterAmount;
            float yOffset = (Mathf.PerlinNoise(jx + 50f, jz + 50f) - 0.5f) * 2f * jitterAmount * yJitterMult;

            return new Vector3(wx - 0.5f + xOffset, y + yOffset, wz - 0.5f + zOffset) + transform.position;
        }

        Vector3 GetTopVertex(int vx, int vz)
        {
            float y = topHeights[vx, vz];
            bool touchesWater = IsWater(vx, vz) || IsWater(vx - 1, vz) || IsWater(vx, vz - 1) || IsWater(vx - 1, vz - 1);
            bool touchesSky = IsSky(vx, vz) || IsSky(vx - 1, vz) || IsSky(vx, vz - 1) || IsSky(vx - 1, vz - 1);

            float yJit = (touchesWater && touchesSky) ? 0f : 1f;
            return ApplyJitter(vx, y, vz, yJit);
        }

        Vector3 GetBottomVertex(int vx, int vz)
        {
            return ApplyJitter(vx, bottomHeights[vx, vz], vz, 1f);
        }

        void AddFace(List<Vector3> verts, List<int> tris, List<Vector2> uvs, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, float ux, float uz, bool invert = false)
        {
            int i = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            if (uvs != null)
            {
                uvs.Add(new Vector2((ux - 0.5f) / size, (uz + 0.5f) / size));
                uvs.Add(new Vector2((ux + 0.5f) / size, (uz + 0.5f) / size));
                uvs.Add(new Vector2((ux - 0.5f) / size, (uz - 0.5f) / size));
                uvs.Add(new Vector2((ux + 0.5f) / size, (uz - 0.5f) / size));
            }
            int[] t = invert ? new int[] { 2, 3, 1, 2, 1, 0 } : new int[] { 0, 1, 2, 2, 1, 3 };
            for (int k = 0; k < 6; k++) tris.Add(i + t[k]);
        }

        void AddVerticalFace(List<Vector3> verts, List<int> tris, List<Vector2> uvs, Vector3 tL, Vector3 tR, Vector3 bL, Vector3 bR)
        {
            int i = verts.Count;
            verts.Add(tL); verts.Add(tR); verts.Add(bL); verts.Add(bR);

            if (uvs != null)
            {
                float uvScale = 1f / size;
                uvs.Add(new Vector2((tL.x + tL.z) * uvScale, tL.y * uvScale));
                uvs.Add(new Vector2((tR.x + tR.z) * uvScale, tR.y * uvScale));
                uvs.Add(new Vector2((bL.x + bL.z) * uvScale, bL.y * uvScale));
                uvs.Add(new Vector2((bR.x + bR.z) * uvScale, bR.y * uvScale));
            }
            tris.AddRange(new int[] { i, i + 1, i + 2, i + 2, i + 1, i + 3 });
        }

        Vector3 GetWaterfallPush(int vx, int vz)
        {
            Vector3 push = Vector3.zero;
            if (IsSky(vx, vz)) push += new Vector3(1, 0, 1);
            if (IsSky(vx - 1, vz)) push += new Vector3(-1, 0, 1);
            if (IsSky(vx, vz - 1)) push += new Vector3(1, 0, -1);
            if (IsSky(vx - 1, vz - 1)) push += new Vector3(-1, 0, -1);

            return push.normalized * 0.4f;
        }

        void DrawTerrainMesh()
        {
            List<Vector3> tVerts = new List<Vector3>(), gVerts = new List<Vector3>(), wVerts = new List<Vector3>(), fVerts = new List<Vector3>();
            List<int> tTris = new List<int>(), gTris = new List<int>(), wTris = new List<int>(), fTris = new List<int>();
            List<Vector2> tUVs = new List<Vector2>(), gUVs = new List<Vector2>(), wUVs = new List<Vector2>(), fUVs = new List<Vector2>();

            for (int cz = 0; cz < size; cz++)
            {
                for (int cx = 0; cx < size; cx++)
                {
                    if (skyCells[cx, cz])
                    {
                        if (waterfallCells[cx, cz])
                        {
                            float wTop = dynamicPondLevel;
                            float wfBot = -depth - 5f;

                            Vector3 wfTop0 = ApplyJitter(cx, wTop, cz + 1, 0f); Vector3 wfTop1 = ApplyJitter(cx + 1, wTop, cz + 1, 0f);
                            Vector3 wfTop2 = ApplyJitter(cx, wTop, cz, 0f); Vector3 wfTop3 = ApplyJitter(cx + 1, wTop, cz, 0f);

                            Vector3 mid0 = ApplyJitter(cx, wTop - 0.8f, cz + 1, 0f); Vector3 mid1 = ApplyJitter(cx + 1, wTop - 0.8f, cz + 1, 0f);
                            Vector3 mid2 = ApplyJitter(cx, wTop - 0.8f, cz, 0f); Vector3 mid3 = ApplyJitter(cx + 1, wTop - 0.8f, cz, 0f);

                            Vector3 wfBot0 = ApplyJitter(cx, wfBot, cz + 1, 0f); Vector3 wfBot1 = ApplyJitter(cx + 1, wfBot, cz + 1, 0f);
                            Vector3 wfBot2 = ApplyJitter(cx, wfBot, cz, 0f); Vector3 wfBot3 = ApplyJitter(cx + 1, wfBot, cz, 0f);

                            Vector3 push0 = GetWaterfallPush(cx, cz + 1);
                            Vector3 push1 = GetWaterfallPush(cx + 1, cz + 1);
                            Vector3 push2 = GetWaterfallPush(cx, cz);
                            Vector3 push3 = GetWaterfallPush(cx + 1, cz);

                            AddFace(wVerts, wTris, wUVs, wfTop0, wfTop1, wfTop2, wfTop3, cx, cz);
                            AddFace(fVerts, fTris, fUVs, wfBot2 + push2, wfBot3 + push3, wfBot0 + push0, wfBot1 + push1, cx, cz, true);

                            if (cx == 0 || (!IsWater(cx - 1, cz) && !IsWaterfall(cx - 1, cz)))
                            {
                                AddVerticalFace(fVerts, fTris, fUVs, wfTop0 + push0, wfTop2 + push2, wfBot0 + push0, wfBot2 + push2);
                                AddVerticalFace(fVerts, fTris, fUVs, wfTop0, wfTop2, wfTop0 + push0, wfTop2 + push2);
                            }
                            else if (cx > 0 && IsWater(cx - 1, cz)) AddVerticalFace(fVerts, fTris, fUVs, mid0, mid2, wfBot0 + push0, wfBot2 + push2);

                            if (cx == size - 1 || (!IsWater(cx + 1, cz) && !IsWaterfall(cx + 1, cz)))
                            {
                                AddVerticalFace(fVerts, fTris, fUVs, wfTop3 + push3, wfTop1 + push1, wfBot3 + push3, wfBot1 + push1);
                                AddVerticalFace(fVerts, fTris, fUVs, wfTop3, wfTop1, wfTop3 + push3, wfTop1 + push1);
                            }
                            else if (cx < size - 1 && IsWater(cx + 1, cz)) AddVerticalFace(fVerts, fTris, fUVs, mid3, mid1, wfBot3 + push3, wfBot1 + push1);

                            if (cz == 0 || (!IsWater(cx, cz - 1) && !IsWaterfall(cx, cz - 1)))
                            {
                                AddVerticalFace(fVerts, fTris, fUVs, wfTop2 + push2, wfTop3 + push3, wfBot2 + push2, wfBot3 + push3);
                                AddVerticalFace(fVerts, fTris, fUVs, wfTop2, wfTop3, wfTop2 + push2, wfTop3 + push3);
                            }
                            else if (cz > 0 && IsWater(cx, cz - 1)) AddVerticalFace(fVerts, fTris, fUVs, mid2, mid3, wfBot2 + push2, wfBot3 + push3);

                            if (cz == size - 1 || (!IsWater(cx, cz + 1) && !IsWaterfall(cx, cz + 1)))
                            {
                                AddVerticalFace(fVerts, fTris, fUVs, wfTop1 + push1, wfTop0 + push0, wfBot1 + push1, wfBot0 + push0);
                                AddVerticalFace(fVerts, fTris, fUVs, wfTop1, wfTop0, wfTop1 + push1, wfTop0 + push0);
                            }
                            else if (cz < size - 1 && IsWater(cx, cz + 1)) AddVerticalFace(fVerts, fTris, fUVs, mid1, mid0, wfBot1 + push1, wfBot0 + push0);
                        }
                        continue;
                    }

                    bool isShoreCell = false;
                    if (!waterCells[cx, cz] && !skyCells[cx, cz])
                    {
                        isShoreCell = IsWater(cx + 1, cz) || IsWater(cx - 1, cz) || IsWater(cx, cz + 1) || IsWater(cx, cz - 1) ||
                                      IsWater(cx + 1, cz + 1) || IsWater(cx - 1, cz - 1) || IsWater(cx - 1, cz + 1) || IsWater(cx + 1, cz - 1);
                    }

                    if (waterCells[cx, cz] || isShoreCell)
                    {
                        float wTop = dynamicPondLevel;
                        Vector3 wTop0 = ApplyJitter(cx, wTop, cz + 1, 0f);
                        Vector3 wTop1 = ApplyJitter(cx + 1, wTop, cz + 1, 0f);
                        Vector3 wTop2 = ApplyJitter(cx, wTop, cz, 0f);
                        Vector3 wTop3 = ApplyJitter(cx + 1, wTop, cz, 0f);

                        AddFace(wVerts, wTris, wUVs, wTop0, wTop1, wTop2, wTop3, cx, cz);

                        if (waterCells[cx, cz])
                        {
                            float wBot = dynamicPondLevel - 1.5f;
                            Vector3 wBot0 = ApplyJitter(cx, wBot, cz + 1, 0f);
                            Vector3 wBot1 = ApplyJitter(cx + 1, wBot, cz + 1, 0f);
                            Vector3 wBot2 = ApplyJitter(cx, wBot, cz, 0f);
                            Vector3 wBot3 = ApplyJitter(cx + 1, wBot, cz, 0f);

                            if (cx == 0 || (!IsWater(cx - 1, cz) && !IsWaterfall(cx - 1, cz))) AddVerticalFace(wVerts, wTris, wUVs, wTop0, wTop2, wBot0, wBot2);
                            if (cx == size - 1 || (!IsWater(cx + 1, cz) && !IsWaterfall(cx + 1, cz))) AddVerticalFace(wVerts, wTris, wUVs, wTop3, wTop1, wBot3, wBot1);
                            if (cz == 0 || (!IsWater(cx, cz - 1) && !IsWaterfall(cx, cz - 1))) AddVerticalFace(wVerts, wTris, wUVs, wTop2, wTop3, wBot2, wBot3);
                            if (cz == size - 1 || (!IsWater(cx, cz + 1) && !IsWaterfall(cx, cz + 1))) AddVerticalFace(wVerts, wTris, wUVs, wTop1, wTop0, wBot1, wBot0);
                        }
                    }

                    Vector3 v0 = GetTopVertex(cx, cz + 1); Vector3 v1 = GetTopVertex(cx + 1, cz + 1);
                    Vector3 v2 = GetTopVertex(cx, cz); Vector3 v3 = GetTopVertex(cx + 1, cz);

                    AddFace(tVerts, tTris, tUVs, v0, v1, v2, v3, cx, cz);
                    AddFace(tVerts, tTris, tUVs, v0, v1, v2, v3, cx, cz, true);

                    if (grassCells[cx, cz]) AddFace(gVerts, gTris, gUVs, v0, v1, v2, v3, cx, cz);
                }
            }

            terrainMesh.Clear(); terrainMesh.vertices = tVerts.ToArray(); terrainMesh.triangles = tTris.ToArray(); terrainMesh.uv = tUVs.ToArray(); terrainMesh.RecalculateNormals();
            grassMesh.Clear(); grassMesh.vertices = gVerts.ToArray(); grassMesh.triangles = gTris.ToArray(); grassMesh.uv = gUVs.ToArray(); grassMesh.RecalculateNormals();
            waterMesh.Clear(); waterMesh.vertices = wVerts.ToArray(); waterMesh.triangles = wTris.ToArray(); waterMesh.uv = wUVs.ToArray(); waterMesh.RecalculateNormals();
            waterfallMesh.Clear(); waterfallMesh.vertices = fVerts.ToArray(); waterfallMesh.triangles = fTris.ToArray(); waterfallMesh.uv = fUVs.ToArray(); waterfallMesh.RecalculateNormals();

            terrainCollider.sharedMesh = terrainMesh;
        }

        void DrawEdgeMesh()
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            for (int cz = 0; cz < size; cz++)
            {
                for (int cx = 0; cx < size; cx++)
                {
                    if (!skyCells[cx, cz])
                    {
                        Vector3 t0 = GetTopVertex(cx, cz + 1); Vector3 t1 = GetTopVertex(cx + 1, cz + 1);
                        Vector3 t2 = GetTopVertex(cx, cz); Vector3 t3 = GetTopVertex(cx + 1, cz);

                        Vector3 b0 = GetBottomVertex(cx, cz + 1); Vector3 b1 = GetBottomVertex(cx + 1, cz + 1);
                        Vector3 b2 = GetBottomVertex(cx, cz); Vector3 b3 = GetBottomVertex(cx + 1, cz);

                        AddFace(verts, tris, null, b0, b1, b2, b3, cx, cz, true);

                        if (cx == 0 || (skyCells[cx - 1, cz] && !waterfallCells[cx - 1, cz])) AddFace(verts, tris, null, t0, t2, b0, b2, cx, cz);
                        if (cx == size - 1 || (skyCells[cx + 1, cz] && !waterfallCells[cx + 1, cz])) AddFace(verts, tris, null, t3, t1, b3, b1, cx, cz);
                        if (cz == 0 || (skyCells[cx, cz - 1] && !waterfallCells[cx, cz - 1])) AddFace(verts, tris, null, t2, t3, b2, b3, cx, cz);
                        if (cz == size - 1 || (skyCells[cx, cz + 1] && !waterfallCells[cx, cz + 1])) AddFace(verts, tris, null, t1, t0, b1, b0, cx, cz);
                    }
                }
            }

            edgeMesh.Clear(); edgeMesh.vertices = verts.ToArray(); edgeMesh.triangles = tris.ToArray(); edgeMesh.RecalculateNormals();
            edgeCollider.sharedMesh = edgeMesh;
        }

        void CreatePond()
        {
            if (currentBiome == BiomeType.Desert) return;
            if (pondTries > 10) return;

            bool[,] pondGrid = new bool[size, size];
            List<Vector2Int> validLand = new List<Vector2Int>();

            for (int z = 15; z < size - 15; z++)
                for (int x = 15; x < size - 15; x++)
                    if (!skyCells[x, z]) validLand.Add(new Vector2Int(x, z));

            if (validLand.Count == 0) return;

            Vector2Int startPoint = validLand[Random.Range(0, validLand.Count)];
            float pondRadius = Random.Range(5f, 9f);

            float pOffsetX = Random.Range(-1000f, 1000f);
            float pOffsetY = Random.Range(-1000f, 1000f);

            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!skyCells[x, z])
                    {
                        float dist = Vector2.Distance(new Vector2(x, z), new Vector2(startPoint.x, startPoint.y));
                        float noiseVal = Mathf.PerlinNoise(x * 0.15f + pOffsetX, z * 0.15f + pOffsetY);
                        float organicDist = dist + (noiseVal - 0.5f) * 6f;

                        if (organicDist <= pondRadius)
                        {
                            pondGrid[x, z] = true;
                        }
                    }
                }
            }

            EnsureConnectedPond(pondGrid);
            FillIsolatedGrass(pondGrid);

            if (!IsPondSizeSufficient(pondGrid, 12))
            {
                pondTries++;
                CreatePond();
                return;
            }
            pondTries = 0;

            float totalElevation = 0f;
            int waterCount = 0;

            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (pondGrid[x, z] && !skyCells[x, z])
                    {
                        waterCells[x, z] = true;

                        float px = x * hillNoiseScale + topOffsetX;
                        float pz = z * hillNoiseScale + topOffsetY;
                        float noiseVal = Mathf.PerlinNoise(px, pz);

                        float nx = (x - size / 2f) / (size / 2f);
                        float nz = (z - size / 2f) / (size / 2f);
                        float dist = Mathf.Clamp01(Mathf.Sqrt(nx * nx + nz * nz));
                        float domeShape = Mathf.Lerp(0.2f, 1.0f, 1f - Mathf.Pow(Mathf.Clamp01(dist), 2f));

                        totalElevation += (noiseVal * hillHeight * domeShape) + (domeShape * mountainBoost);
                        waterCount++;
                    }
                }
            }

            if (waterCount > 0) dynamicPondLevel = (totalElevation / waterCount) - 0.4f;
        }

        void EnsureConnectedPond(bool[,] pondGrid)
        {
            bool[,] visited = new bool[size, size];
            List<(int, int)> wCells = new List<(int, int)>();
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                    if (pondGrid[x, z]) wCells.Add((x, z));

            if (wCells.Count == 0) return;

            Queue<(int, int)> queue = new Queue<(int, int)>();
            queue.Enqueue(wCells[0]);
            visited[wCells[0].Item1, wCells[0].Item2] = true;

            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                foreach (var (nx, nz) in GetNeighbors(x, z))
                    if (IsValidCoordinate(nx, nz) && pondGrid[nx, nz] && !visited[nx, nz])
                    {
                        visited[nx, nz] = true;
                        queue.Enqueue((nx, nz));
                    }
            }
            foreach (var (x, z) in wCells) if (!visited[x, z]) pondGrid[x, z] = false;
        }

        void FillIsolatedGrass(bool[,] pondGrid)
        {
            bool[,] visited = new bool[size, size];
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                    if (!pondGrid[x, z] && !visited[x, z] && CountWaterNeighbors(pondGrid, x, z) >= 3)
                    {
                        pondGrid[x, z] = true;
                        visited[x, z] = true;
                    }
        }

        bool IsPondSizeSufficient(bool[,] pondGrid, int minSize)
        {
            int count = 0;
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                    if (pondGrid[x, z]) count++;
            return count >= minSize;
        }

        List<(int, int)> GetNeighbors(int x, int z) => new List<(int, int)> { (x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1) };

        int CountWaterNeighbors(bool[,] grid, int x, int z)
        {
            int count = 0;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dz != 0)
                        if (IsValidCoordinate(x + dx, z + dz) && grid[x + dx, z + dz]) count++;
            return count;
        }

        bool IsValidCoordinate(int x, int z) => x >= 0 && x < size && z >= 0 && z < size;

        void GenerateSteam()
        {
            if (cloudContainer != null)
            {
                foreach (Transform child in cloudContainer.transform) Destroy(child.gameObject);
            }

            GameObject ambientObj = new GameObject("AmbientClouds");
            ambientObj.transform.SetParent(cloudContainer.transform);
            // Sätt molnen rakt under DEN HÄR öns position!
            ambientObj.transform.position = transform.position + new Vector3(0, -depth - 8f, 0);

            ParticleSystem ambientPS = ambientObj.AddComponent<ParticleSystem>();
            var main = ambientPS.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(15f, 30f);
            main.startColor = new Color(1f, 1f, 1f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ambientPS.emission; emission.rateOverTime = 40f;
            var shape = ambientPS.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(size * 1.5f, 5f, size * 1.5f);

            var vol = ambientPS.velocityOverLifetime;
            vol.enabled = true;
            vol.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vol.y = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            if (cloudMaterial != null) ambientObj.GetComponent<ParticleSystemRenderer>().material = cloudMaterial;

            for (int cz = 0; cz < size; cz++)
            {
                for (int cx = 0; cx < size; cx++)
                {
                    if (skyCells[cx, cz] && waterfallCells[cx, cz])
                    {
                        GameObject splashObj = new GameObject("WaterfallSplash");
                        splashObj.transform.SetParent(cloudContainer.transform);

                        Vector3 v0 = GetTopVertex(cx, cz + 1);
                        Vector3 v1 = GetTopVertex(cx + 1, cz + 1);
                        Vector3 v2 = GetTopVertex(cx, cz);
                        Vector3 v3 = GetTopVertex(cx + 1, cz);
                        Vector3 splashPos = (v0 + v1 + v2 + v3) / 4f;
                        splashPos.y = transform.position.y - depth - 5f; // Korrigerat Y-värde baserat på öns pos

                        splashObj.transform.position = splashPos;

                        ParticleSystem splashPS = splashObj.AddComponent<ParticleSystem>();
                        var smain = splashPS.main;
                        smain.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                        smain.startSize = new ParticleSystem.MinMaxCurve(4f, 8f);
                        smain.startColor = new Color(1f, 1f, 1f, 0.4f);
                        smain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);

                        var semit = splashPS.emission; semit.rateOverTime = 15f;
                        var sshape = splashPS.shape; sshape.shapeType = ParticleSystemShapeType.Cone; sshape.radius = 1.5f; sshape.angle = 25f;

                        var svol = splashPS.velocityOverLifetime;
                        svol.enabled = true;
                        svol.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                        svol.y = new ParticleSystem.MinMaxCurve(2f, 6f);
                        svol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                        if (cloudMaterial != null) splashObj.GetComponent<ParticleSystemRenderer>().material = cloudMaterial;
                    }
                }
            }
        }

        void GenerateGrass()
        {
            // --- NY SPÄRR: Det växer inget gräs i öknen! ---
            if (currentBiome == BiomeType.Desert)
            {
                // Fyll hela grassCells-arrayen med 'false' för säkerhets skull
                for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                    grassCells[x, z] = false;
                    
                return; // Avbryt metoden
            }
        
            // ... (Resten av din gamla GenerateGrass-kod ligger kvar här under!)
            for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
                if (!skyCells[x, z] && !waterCells[x, z]) grassCells[x, z] = Random.value < 0.7f;

            for (int iteration = 0; iteration < 5; iteration++)
            {
                bool[,] next = new bool[size, size];
                for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                {
                    if (!skyCells[x, z] && !waterCells[x, z])
                    {
                        int n = CountGrassNeighbors(x, z);
                        next[x, z] = grassCells[x, z] ? (n == 2 || n == 3) : (n == 3);
                    }
                }
                grassCells = next;
            }
        }

        int CountGrassNeighbors(int x, int z)
        {
            int count = 0;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dz != 0)
                        if (IsValidCoordinate(x + dx, z + dz) && grassCells[x + dx, z + dz]) count++;
            return count;
        }

        // --- NY METOD: Letar upp en säker landningsplats på denna specifika ö ---
        public Vector3 GetSafeSpawnPosition() 
        {
            int cx = size / 2;
            int cz = size / 2;
            int searchRadius = size / 2;

            for (int r = 0; r < searchRadius; r++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    for (int z = cz - r; z <= cz + r; z++)
                    {
                        if (x >= 0 && x < size && z >= 0 && z < size)
                        {
                            if (!skyCells[x, z] && !waterCells[x, z])
                            {
                                Vector3 v0 = GetTopVertex(x, z + 1);
                                Vector3 v1 = GetTopVertex(x + 1, z + 1);
                                Vector3 v2 = GetTopVertex(x, z);
                                Vector3 v3 = GetTopVertex(x + 1, z);
                                
                                Vector3 localPos = (v0 + v1 + v2 + v3) / 4f;
                                return localPos; 
                            }
                        }
                    }
                }
            }
            return transform.position + new Vector3(0, 20f, 0); 
        }

        void GenerateTrees()
        {
            if (currentBiome == BiomeType.Desert) return;

            float treeOffsetX = Random.Range(-10000f, 10000f);
            float treeOffsetY = Random.Range(-10000f, 10000f);

            int minTrees = Random.Range(5, 11);
            int treesPlaced = 0;

            float minDistanceBetweenTrees = 3.5f;
            List<Vector3> placedTreePositions = new List<Vector3>();
            List<Vector2Int> validSpots = new List<Vector2Int>();

            for (int cz = 0; cz < size; cz++)
            {
                for (int cx = 0; cx < size; cx++)
                {
                    if (!skyCells[cx, cz] && !waterCells[cx, cz] && grassCells[cx, cz])
                    {
                        validSpots.Add(new Vector2Int(cx, cz));
                    }
                }
            }

            for (int i = 0; i < validSpots.Count; i++)
            {
                Vector2Int temp = validSpots[i];
                int randomIndex = Random.Range(i, validSpots.Count);
                validSpots[i] = validSpots[randomIndex];
                validSpots[randomIndex] = temp;
            }

            for (int i = validSpots.Count - 1; i >= 0; i--)
            {
                int cx = validSpots[i].x;
                int cz = validSpots[i].y;

                float noiseVal = Mathf.PerlinNoise(cx * treeNoiseScale + treeOffsetX, cz * treeNoiseScale + treeOffsetY);
                if (noiseVal > treeDensity)
                {
                    Vector3 v0 = GetTopVertex(cx, cz + 1);
                    Vector3 v1 = GetTopVertex(cx + 1, cz + 1);
                    Vector3 v2 = GetTopVertex(cx, cz);
                    Vector3 v3 = GetTopVertex(cx + 1, cz);
                    Vector3 pos = (v0 + v1 + v2 + v3) / 4f;

                    bool tooClose = false;
                    foreach (Vector3 placedPos in placedTreePositions)
                    {
                        if (Vector3.Distance(pos, placedPos) < minDistanceBetweenTrees)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        Instantiate(forestTreePrefabs[Random.Range(0, forestTreePrefabs.Length)], pos, Quaternion.identity, terrainObject.transform);
                        placedTreePositions.Add(pos);
                        treesPlaced++;
                        validSpots.RemoveAt(i);
                    }
                }
            }

            while (treesPlaced < minTrees && validSpots.Count > 0)
            {
                int lastIndex = validSpots.Count - 1;
                Vector2Int spot = validSpots[lastIndex];
                validSpots.RemoveAt(lastIndex);

                Vector3 v0 = GetTopVertex(spot.x, spot.y + 1);
                Vector3 v1 = GetTopVertex(spot.x + 1, spot.y + 1);
                Vector3 v2 = GetTopVertex(spot.x, spot.y);
                Vector3 v3 = GetTopVertex(spot.x + 1, spot.y);
                Vector3 pos = (v0 + v1 + v2 + v3) / 4f;

                bool tooClose = false;
                foreach (Vector3 placedPos in placedTreePositions)
                {
                    if (Vector3.Distance(pos, placedPos) < minDistanceBetweenTrees)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    Instantiate(forestTreePrefabs[Random.Range(0, forestTreePrefabs.Length)], pos, Quaternion.identity, terrainObject.transform);
                    placedTreePositions.Add(pos);
                    treesPlaced++;
                }
            }
        }

        void DrawTexture()
        {
            // Om det är en öken, rita INGEN textur! Låt Unitys material styra färgen.
            if (currentBiome == BiomeType.Desert)
            {
                // Vi raderar den tilldelade texturen från materialet så att din 
                // valda HEX-färg (sandfärgen) faktiskt syns!
                terrainObject.GetComponent<MeshRenderer>().materials[0].mainTexture = null;
                return; 
            }

            // --- SKOGS-TEXTURERING (Originalkoden) ---
            Texture2D tex = new Texture2D(size, size); Color[] cMap = new Color[size * size];
            for (int z = 0; z < size; z++) for (int x = 0; x < size; x++) {
                float u = (float)x / size; float v = (float)z / size;
                Color b = Color.Lerp(directionalTextures[0].GetPixelBilinear(u, v), directionalTextures[0].GetPixelBilinear(u, v), 0.01f);
                b = Color.Lerp(b, directionalTextures[1].GetPixelBilinear(u, v), 0.01f); 
                b = Color.Lerp(b, directionalTextures[2].GetPixelBilinear(u, v), 0.01f);
                cMap[z * size + x] = Color.Lerp(b, directionalTextures[3].GetPixelBilinear(u, v), 0.01f);
            }
            tex.filterMode = FilterMode.Point; tex.SetPixels(cMap); tex.Apply(); 
        
            // Applicera den gröna texturen
            terrainObject.GetComponent<MeshRenderer>().materials[0].mainTexture = tex;
        }
    }
}