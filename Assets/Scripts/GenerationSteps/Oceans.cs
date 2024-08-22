using UnityEngine;

[CreateAssetMenu(menuName = "GenerationSteps/Oceans", fileName = "Oceans")]
public class Oceans : GenerationStep {
    private const int Left = -1, Right = +1;
    private const float QuarterTurn = Mathf.PI/2;
    
    [SerializeField] private int waterWidth, waterDepth;
    [SerializeField] private int seaBedThickness;
    [SerializeField] private int beachWidth, beachDepth;
    public override float Perform(BlockType[,] worldGrid, WorldSize worldSize, Seed seed){
        CreateOcean(worldGrid, worldSize, seed, Left);
        CreateOcean(worldGrid, worldSize, seed, Right);
        return 1;
    }

    private void CreateOcean(BlockType[,] worldGrid, WorldSize worldSize, Seed seed, int side){
        int seaLevel = worldSize.seaLevel;
        int edge = side == Left ? -1 : worldSize.width;
        int x = edge - side*(waterWidth+beachWidth);
        for (int i = 0; i < beachWidth; i++){
            int y = worldSize.height-1;
            for (; y > seaLevel; y--){
                worldGrid[x, y] = BlockType.Air;
            }
            for (; y > seaLevel-beachDepth; y--){
                worldGrid[x, y] = BlockType.Sand;
            }
            x += side;
        }
        for (int i = 0; i < waterWidth; i++){
            int localWaterBottom = seaLevel-(int)(waterDepth * Mathf.Sin(i*QuarterTurn/waterWidth));
            int localSeaBedBottom = Mathf.Min(localWaterBottom - seaBedThickness, seaLevel-beachDepth);
            int y = worldSize.height-1;
            for (; y > worldSize.seaLevel; y--){
                worldGrid[x, y] = BlockType.Air;
            }
            for (; y > localWaterBottom; y--){
                worldGrid[x, y] = BlockType.Water;
            }
            for (; y > localSeaBedBottom; y--){
                worldGrid[x, y] = BlockType.Sand;
            }
            x += side;
        }
    }
}
