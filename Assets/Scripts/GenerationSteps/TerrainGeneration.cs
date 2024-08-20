using UnityEngine;

[CreateAssetMenu(menuName = "GenerationSteps/TerrainGeneration", fileName = "TerrainGeneration")]
public class TerrainGeneration : GenerationStep {
    [SerializeField] private float noiseSmoothness;
    [SerializeField] private float maxHeight, minHeight;
    [SerializeField] private int dirtThickness;
    public override float Perform(BlockType[,] worldGrid, long seed){
        int worldHeight = worldGrid.GetLength(WorldGenerator.Y);
        float heightRange = maxHeight - minHeight;
        for (int x = worldGrid.GetLength(WorldGenerator.X)-1; x >= 0; x--){
            float noise = OpenSimplex2S.Noise2(seed, x / noiseSmoothness, 0);
            int elevation = (int)(worldHeight*(minHeight + heightRange*noise));
            {
                int y = worldHeight-1;
                for (; y > elevation; y--){
                    worldGrid[x, y] = BlockType.Air;
                }
                worldGrid[x, y] = BlockType.Grass;
                y--;
                for (; y > elevation - dirtThickness; y--){
                    worldGrid[x, y] = BlockType.Dirt;
                }
                for (; y >= 0; y--){
                    worldGrid[x, y] = BlockType.Rock;
                }
            }
        }
        return 1;
    }
}
