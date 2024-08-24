using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[CreateAssetMenu(menuName = "GenerationSteps/Caves", fileName = "Caves")]
public class Caves : GenerationStep {
    [SerializeField] private float lengthScalar;
    [Range(0, 1)]
    [SerializeField] private float keepDirectionWeight;
    [Range(0, 1)]
    [SerializeField] private float upwardsWeight;

    private static readonly BlockType[] CaveBreakingBlocks = {BlockType.Air, BlockType.Water, BlockType.Sand};
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
        
        Vector2Int leftWallPosition = position+width*Vector2Int.right;
        Vector2Int rightWallPosition = position-width*Vector2Int.right;
        for (int i = 0; i < walkMaxSteps; i++){
            Vector2Int wallOffset = (direction.sqrMagnitude == 1 ? width : diagonalWidth)*Perpendicular(direction);
            Vector2Int newLeftWallPosition = position+wallOffset;
            Vector2Int newRightWallPosition = position-wallOffset;
            // Counts the rightmost x-index as outside the grid so ConnectCaveWall's "+Vector2Int.right" doesn't cause an error.
            int xBound = elevations.Length-1;
            if (IsOffGrid(newLeftWallPosition, xBound) || IsOffGrid(newRightWallPosition, xBound) || CaveBreakingBlocks.Contains(worldGrid[position.x, position.y])){
                break;
            }
            ConnectCaveWall(worldGrid, newLeftWallPosition, leftWallPosition, rightWallPosition);
            leftWallPosition = newLeftWallPosition;
            ConnectCaveWall(worldGrid, newRightWallPosition, rightWallPosition, leftWallPosition);
            rightWallPosition = newRightWallPosition;
            position += direction;
            
            if (Random.value < keepDirectionWeight){
                continue;
            }
            directionIndex = GetNewDirectionIndex(directionIndex);
            direction = Directions[directionIndex];
        }
        // TODO: Start and end circles.
        return 1;
    }

    private static bool IsOffGrid(Vector2Int position, int xBound){
        return position.y < 0 || position.x < 0 || xBound <= position.x;
    }
    
    private static void ConnectCaveWall(BlockType[,] worldGrid, Vector2Int newWallPosition, Vector2Int wallPosition, Vector2Int otherWallPosition){
        FillDiagonal(newWallPosition, wallPosition, currentWallPosition => {
            FillDiagonal(currentWallPosition, otherWallPosition, caveInsidePosition => {
                MakeCaveWall(worldGrid, caveInsidePosition);
                MakeCaveWall(worldGrid, caveInsidePosition+Vector2Int.right);
                MakeCaveWall(worldGrid, caveInsidePosition+Vector2Int.up);
            });
        });
    }

    private static void FillDiagonal(Vector2Int inclusiveEnd, Vector2Int exclusiveEnd, Action<Vector2Int> fillAction){
        Vector2Int difference = exclusiveEnd-inclusiveEnd;
        Vector2Int currentPosition;
        if (Math.Abs(difference.y) < Math.Abs(difference.x)){
            float length = Math.Abs(difference.x);
            for (int i = 0; i < length; i++){
                currentPosition = inclusiveEnd + new Vector2Int(i*Math.Sign(difference.x), Mathf.RoundToInt(difference.y*i/length));
                fillAction(currentPosition);
            }
        } else {
            float length = Math.Abs(difference.y);
            for (int i = 0; i < length; i++){
                currentPosition = inclusiveEnd + new Vector2Int(Mathf.RoundToInt(difference.x*i/length), i*Math.Sign(difference.y));
                fillAction(currentPosition);
            }
        }
    }
    
    private static void MakeCaveWall(BlockType[,] worldGrid, Vector2Int position){
        worldGrid[position.x, position.y] = worldGrid[position.x, position.y] switch {
            BlockType.Rock => BlockType.RockWall,
            BlockType.Dirt => BlockType.DirtWall,
            BlockType.Grass => BlockType.Air,
            _ => worldGrid[position.x, position.y]
        };
    }

    private int GetNewDirectionIndex(int directionIndex){
        int rightIndex = directionIndex-1;
        int leftIndex = directionIndex+1;
        if (rightIndex < 0){
            return leftIndex;
        }
        if (Directions.Length <= leftIndex){
            return rightIndex;
        }
        float weightedMiddlePoint = 0.5f;
        if (0 < Directions[leftIndex].y){
            weightedMiddlePoint = upwardsWeight;
        } else if (0 < Directions[rightIndex].y){
            weightedMiddlePoint = 1-upwardsWeight;
        }
        return Random.value < weightedMiddlePoint ? leftIndex : rightIndex;
    }
    
    // Gives counter-clockwise perpendicular. // Why doesn't this already exist for Vector2Int, Unity!!!!
    private static Vector2Int Perpendicular(Vector2Int vector){
        return new Vector2Int(-vector.y, vector.x);
    }
}
