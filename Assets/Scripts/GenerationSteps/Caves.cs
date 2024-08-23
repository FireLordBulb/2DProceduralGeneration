using System;
using System.Text;
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
        Vector2Int direction = Vector2Int.down;
        int directionIndex = Array.FindIndex(Directions, vector => vector == direction);
        
        Random.InitState((int)seed);
        Vector2Int position = new(Random.Range(0, elevations.Length), 0);
        position.y = elevations[position.x];
        
        // TEMP
        const int width = 6;
        const int diagonalWidth = 4;
        
        Vector2Int previousLeftWallPosition = position+width*Vector2Int.right;
        Vector2Int previousRightWallPosition = position-width*Vector2Int.right;
    
        for (int i = 0; i < walkMaxSteps; i++){
            Vector2Int wallOffset = (direction.sqrMagnitude == 1 ? width : diagonalWidth)*Perpendicular(direction);
            Vector2Int leftWallPosition = position+wallOffset;
            Vector2Int rightWallPosition = position-wallOffset;
            if (leftWallPosition.y < 0 || leftWallPosition.x < 0 || elevations.Length <= leftWallPosition.x){
                break;
            }
            if (rightWallPosition.y < 0 || rightWallPosition.x < 0 || elevations.Length <= rightWallPosition.x){
                break;
            }
            while ((leftWallPosition-previousLeftWallPosition).sqrMagnitude > 0){
                Vector2Int difference = leftWallPosition-previousLeftWallPosition;
                previousLeftWallPosition += GridDiagonal(difference);
                MakeCaveWall(worldGrid, previousLeftWallPosition);
                Vector2Int caveInsidePosition = previousRightWallPosition;
                while ((previousLeftWallPosition-caveInsidePosition).sqrMagnitude > 0){
                    Vector2Int insideDifference = previousLeftWallPosition-caveInsidePosition;
                    caveInsidePosition += GridDiagonal(insideDifference);
                    MakeCaveWall(worldGrid, caveInsidePosition);
                }
            }
            while ((rightWallPosition-previousRightWallPosition).sqrMagnitude > 0){
                Vector2Int difference = rightWallPosition-previousRightWallPosition;
                previousRightWallPosition += GridDiagonal(difference);
                MakeCaveWall(worldGrid, previousRightWallPosition);
                Vector2Int caveInsidePosition = previousRightWallPosition;
                while ((previousLeftWallPosition-caveInsidePosition).sqrMagnitude > 0){
                    Vector2Int insideDifference = previousLeftWallPosition-caveInsidePosition;
                    caveInsidePosition += GridDiagonal(insideDifference);
                    MakeCaveWall(worldGrid, caveInsidePosition);
                }
            }

            position += direction;

            if (Random.value < keepDirectionWeight){
                continue;
            }
            int rightIndex = directionIndex-1;
            int leftIndex = directionIndex+1;
            if (rightIndex < 0){
                directionIndex = leftIndex;
            } else if (Directions.Length <= leftIndex){
                directionIndex = rightIndex;
            } else{
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
    
        Vector2Int exampleStart = new(200, 200);
        Vector2Int exampleEnd = new(205, 214);
        while ((exampleEnd-exampleStart).sqrMagnitude > 0){
            Vector2Int differenc = exampleEnd-exampleStart;
            exampleStart += GridDiagonal(differenc);
            MakeCaveWall(worldGrid, exampleStart);
        }
        return 1;
    }

    private void DrawDiagonalBetween(){
        // TODO
    }
    
    private static Vector2Int GridDiagonal(Vector2Int difference){
        Vector2Int absDifference = new(Math.Abs(difference.x), Math.Abs(difference.y));
        Vector2Int straightOr45Diagonal = difference/Math.Max(absDifference.x, absDifference.y);
        int min = Math.Min(absDifference.x, absDifference.y);
        if (min == 0){
            return straightOr45Diagonal;
        }
        Vector2Int diagonal = new(difference.x/absDifference.x, difference.y/absDifference.y);
        return Math.Abs(Math.Atan(difference.y/(float)difference.x)) < Mathf.PI/4 ? straightOr45Diagonal : diagonal;
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
