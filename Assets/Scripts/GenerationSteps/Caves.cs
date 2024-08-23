using System;
using UnityEngine;
using Random = UnityEngine.Random;

[CreateAssetMenu(menuName = "GenerationSteps/Caves", fileName = "Caves")]
public class Caves : GenerationStep {
    [SerializeField] private float lengthScalar;
    [Range(0, 1)]
    [SerializeField] private float keepDirectionWeight;
    [Range(0, 1)]
    [SerializeField] private float upwardsWeight;
    
    private static readonly Vector2Int[] Directions = {new(-1, +1), Vector2Int.left, new(-1, -1), Vector2Int.down, new(+1, -1), Vector2Int.right, new(+1, +1)};
    
    public override float Perform(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed){
        int walkMaxSteps = (int)(worldSize.height*lengthScalar);
        Random.InitState((int)seed);
        int x = Random.Range(0, elevations.Length);
        int y = elevations[x];
        Vector2Int direction = Vector2Int.down;
        int directionIndex = Array.FindIndex(Directions, vector => vector == direction);
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
            if (Random.value < keepDirectionWeight){
                continue;
            }
            int rightIndex = directionIndex-1;
            int leftIndex = directionIndex+1;
            if (rightIndex < 0){
                directionIndex = leftIndex;
            } else if (Directions.Length <= leftIndex){
                directionIndex = rightIndex;
            } else {
                float weightedMiddlePoint = 0.5f;
                if (0 < Directions[leftIndex].y){
                    weightedMiddlePoint = upwardsWeight;
                } else if (0 < Directions[rightIndex].y){
                    weightedMiddlePoint = 1-upwardsWeight;
                }
                directionIndex = Random.value < weightedMiddlePoint ? leftIndex : rightIndex;
            }
            direction = Directions[directionIndex];
        }
        return 1;
    }
}
