using UnityEngine;

[CreateAssetMenu(menuName = "GenerationSteps/Caves", fileName = "Caves")]
public class Caves : GenerationStep {
    [SerializeField] private float lengthScalar;

    private static readonly Vector2Int[] Directions = {new(-1, +1), Vector2Int.left, new(-1, -1), Vector2Int.down, new(+1, -1), Vector2Int.right, new(+1, +1)};
    
    public override float Perform(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed){
        int walkMaxSteps = (int)(worldSize.height*lengthScalar);
        Random.InitState((int)seed);
        int x = Random.Range(0, elevations.Length);
        int y = elevations[x];
        Vector2Int direction = Vector2Int.down;
        for (int i = 0; i < walkMaxSteps; i++){
            worldGrid[x, y] = worldGrid[x, y] switch {
                BlockType.Rock => BlockType.RockWall,
                BlockType.Dirt or BlockType.Grass => BlockType.DirtWall,
                _ => worldGrid[x, y]
            };
            x += direction.x;
            y += direction.y;
            if (y < 0 || x < 0 || elevations.Length <= x){
                break;
            }
        }
        return 1;
    }
}
