using UnityEngine;
using Grid = PCG.Grid;

namespace Player
{
    public class TerrainEditor : MonoBehaviour
    {
        Grid islandGrid;

        void Start()
        {
            islandGrid = FindObjectOfType<Grid>();
        }

        void Update()
        {
            // Left Click to Break (Dig hole)
            if (Input.GetMouseButtonDown(0)) InteractWithTerrain(true);
        
            // Right Click to Build (Raise hill)
            if (Input.GetMouseButtonDown(1)) InteractWithTerrain(false);
        }

        void InteractWithTerrain(bool dig)
        {
            // Shoot ray from the camera crosshair
            Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 9f)) 
            {
                // Only modify if we actually clicked the Terrain or Edges
                if (hit.collider.gameObject.name == "Terrain" || hit.collider.gameObject.name == "Edge")
                {
                    if (islandGrid != null)
                    {
                        // Deforms the beautiful mesh up or down!
                        islandGrid.ModifyTerrain(hit.point, dig);
                    }
                }
            }
        }
    }
}