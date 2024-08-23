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
        Vector2Int position = new(Random.Range(0, elevations.Length), 0);
        position.y = elevations[position.x];
        Vector2Int direction = Vector2Int.down;
        int directionIndex = Array.FindIndex(Directions, vector => vector == direction);

        // TEMP
        const int width = 6;
        const int diagonalWidth = 4;
        
        /*Vector2Int previousLeftWallPosition = position+width*Vector2Int.right;
        Vector2Int previousRightWallPosition = position-width*Vector2Int.right;*/
        
        for (int i = 0; i < walkMaxSteps; i++){

            Vector2Int wallOffset = (direction.sqrMagnitude == 1 ? width : diagonalWidth)*Perpendicular(direction);
            Vector2Int leftWallPosition = position+wallOffset;
            Vector2Int rightWallPosition = position-wallOffset;
            /*while ((previousLeftWallPosition-leftWallPosition).sqrMagnitude > 2){
                
            }
            while ((previousRightWallPosition-rightWallPosition).sqrMagnitude > 2){
                
            }
            previousLeftWallPosition = leftWallPosition;
            previousRightWallPosition = rightWallPosition;
            */
            MakeCaveWall(worldGrid, leftWallPosition);
            MakeCaveWall(worldGrid, rightWallPosition);
            position += direction;
            if (position.y < 0 || position.x < 0 || elevations.Length <= position.x){
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

    private static void MakeCaveWall(BlockType[,] worldGrid, Vector2Int position){
        worldGrid[position.x, position.y] = worldGrid[position.x, position.y] switch {
            BlockType.Rock => BlockType.RockWall,
            BlockType.Dirt or BlockType.Grass => BlockType.DirtWall,
            _ => worldGrid[position.x, position.y]
        };
    }
    
    // Gives counter-clockwise perpendicular. // Why doesn't this already exist for Vector2Int, Unity!!!!
    private static Vector2Int Perpendicular(Vector2Int vector){
        return new Vector2Int(-vector.y, vector.x);
    }
}
