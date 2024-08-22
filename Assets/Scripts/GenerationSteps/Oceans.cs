using UnityEngine;

[CreateAssetMenu(menuName = "GenerationSteps/Oceans", fileName = "Oceans")]
public class Oceans : GenerationStep {
    private const int Left = -1, Right = +1;
    private const float QuarterTurn = Mathf.PI/2;
    
    [SerializeField] private float oceanWidthFraction, beachWidthFraction;
    [SerializeField] private float depthPerWidth;
    [SerializeField] private int seaBedThickness;
    [SerializeField] private int beachDepth;
    public override float Perform(BlockType[,] worldGrid, WorldSize worldSize, Seed seed){
        CreateOcean(worldGrid, worldSize, seed, Left);
        CreateOcean(worldGrid, worldSize, seed, Right);
        return 1;
    }

    private void CreateOcean(BlockType[,] worldGrid, WorldSize worldSize, Seed seed, int side){
        int seaLevel = worldSize.seaLevel;
        int waterWidth = (int)(oceanWidthFraction*worldSize.width);
        float beachWidthFloat = beachWidthFraction*worldSize.width;
        int beachWidth = (int)beachWidthFloat;
        int waterDepth = (int)(waterWidth*depthPerWidth);
        int edge = side == Left ? -1 : worldSize.width;
        int x = edge - side*(waterWidth+beachWidth);
        for (int i = 0; i < beachWidth; i++){
            int y = worldSize.height-1;
            int sandElevation = seaLevel;
            for (; y > sandElevation; y--){
                if (sandElevation == seaLevel && worldGrid[x, y] != BlockType.Air){
                    float closenessToWater = i/beachWidthFloat;
                    sandElevation = (int)(seaLevel*closenessToWater + y*(1-closenessToWater));
                }
                worldGrid[x, y] = BlockType.Air;
            }
            for (; y > sandElevation-beachDepth; y--){
                worldGrid[x, y] = BlockType.Sand;
            }
            x += side;
        }
        float anglePerBlock = QuarterTurn/waterWidth;
        for (int i = 0; i < waterWidth; i++){
            int localWaterBottom = seaLevel-(int)(waterDepth * Mathf.Sin(i*anglePerBlock));
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
