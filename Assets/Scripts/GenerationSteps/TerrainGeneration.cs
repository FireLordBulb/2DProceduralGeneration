using UnityEngine;

[CreateAssetMenu(menuName = "GenerationSteps/TerrainGeneration", fileName = "TerrainGeneration")]
public class TerrainGeneration : GenerationStep{
    [SerializeField] private float perlinSmoothness;
    [SerializeField] private float maxHeight, minHeight;
    [SerializeField] private int dirtThickness;
    public override float Perform(BlockType[,] worldGrid, int seed){
        int worldHeight = worldGrid.GetLength(WorldGenerator.Y);
        for (int i = worldGrid.GetLength(WorldGenerator.X)-1; i >= 0; i--){
            float noise = Mathf.PerlinNoise(i / perlinSmoothness, seed);
            
            int elevation = (int)(worldHeight * (minHeight + (maxHeight-minHeight)*noise));
            int j;
            for (j = worldHeight-1; j > elevation; j--){
                worldGrid[i, j] = BlockType.Air;
            }
            worldGrid[i, j] = BlockType.Grass;
            j--;
            for (; j > elevation-dirtThickness; j--){
                worldGrid[i, j] = BlockType.Dirt;
            }
            for (; j >= 0; j--){
                worldGrid[i, j] = BlockType.Rock;
            }
        }
        return 1;
    }
}
