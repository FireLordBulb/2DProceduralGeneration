using UnityEngine;

[CreateAssetMenu(menuName = "GenerationSteps/Oceans", fileName = "Oceans")]
public class Oceans : GenerationStep {
    private const int Left = -1, Right = +1;
    private const float QuarterTurn = Mathf.PI/2;
    
    [SerializeField] private float oceanWidthFraction, beachWidthFraction;
    [SerializeField] private float depthPerWidth;
    [SerializeField] private int seaBedThickness;
    [SerializeField] private int beachDepth;
    public override float Perform(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed){
        CreateOcean(worldGrid, elevations, worldSize, Left);
        CreateOcean(worldGrid, elevations, worldSize, Right);
        return 1;
    }

    private void CreateOcean(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, int side){
        int waterWidth = (int)(oceanWidthFraction*worldSize.width);
        int beachWidth = (int)(beachWidthFraction*worldSize.width);
        int waterDepth = (int)(waterWidth*depthPerWidth);
        float anglePerBlock = QuarterTurn/waterWidth;
        int x = side == Left ? 0 : worldSize.width-1;
        for (int i = waterWidth+beachWidth; i > 0; i--){
            int surfaceElevation, waterBottom;
            // Values for the ocean.
            if (i > beachWidth){
                surfaceElevation = worldSize.seaLevel;
                waterBottom = surfaceElevation - (int)(waterDepth * Mathf.Sin((i-beachWidth)*anglePerBlock));
            // Values for the beach part.
            } else {
                surfaceElevation = elevations[x] + (worldSize.seaLevel-elevations[x])*i/beachWidth;
                // Makes the water layer's height 0, so there's no water on the beach.
                waterBottom = surfaceElevation;
            }
            int bottomY = Mathf.Min(waterBottom-seaBedThickness, surfaceElevation-beachDepth);
            FillColumn(worldGrid, x, Mathf.Max(elevations[x], surfaceElevation), 
                new BlockSection(surfaceElevation, BlockType.Air),
                new BlockSection(waterBottom, BlockType.Water),
                new BlockSection(bottomY, BlockType.Sand)
            );
            elevations[x] = surfaceElevation;
            while (worldGrid[x, bottomY] != BlockType.Rock){
                worldGrid[x, bottomY] = BlockType.Rock;
                bottomY--;
            }
            x -= side;
        }
    }
}
