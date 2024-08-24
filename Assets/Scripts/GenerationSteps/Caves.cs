using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[CreateAssetMenu(menuName = "GenerationSteps/Caves", fileName = "Caves")]
public class Caves : GenerationStep {
    [SerializeField] private RandomFloatRange widthPerCave;
    [SerializeField] private RandomFloatRange startingDepthFraction;
    [SerializeField] private RandomFloatRange lengthScalar;
    [SerializeField] private float averageRadius;
    [SerializeField] private float maxRadiusVariance;
    [SerializeField] private float noiseRoughness;
    [Range(0, 1)]
    [SerializeField] private float keepDirectionWeight;
    [Range(0, 1)]
    [SerializeField] private float upwardsWeight;

    private static readonly BlockType[] CaveBreakingBlocks = {BlockType.Air, BlockType.Water, BlockType.Sand};
    private static readonly Vector2Int[] Directions = {new(-1, +1), Vector2Int.left, new(-1, -1), Vector2Int.down, new(+1, -1), Vector2Int.right, new(+1, +1)};
    
    public override float Perform(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed){
        Random.InitState((int)seed);
        Vector2Int direction = Vector2Int.down;
        int directionIndex = Array.FindIndex(Directions, vector => vector == direction);
        Vector2Int position = new(Random.Range(0, elevations.Length), 0);
        position.y = elevations[position.x];
        Vector2Int leftWallPosition = position, rightWallPosition = position;
        int walkMaxSteps = (int)(worldSize.height*lengthScalar.Value);
        for (int i = 0; i < walkMaxSteps; i++){
            // The left and right walls use different seeds since they should be fully independent of each other.
            Vector2Int newLeftWallPosition = position+CalculateWallOffset(seed, i, direction);
            Vector2Int newRightWallPosition = position-CalculateWallOffset(seed+1, i, direction);
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
        seed.Increment();
        
        // TODO: Start and end circles.
        return 1;
    }

    private Vector2Int CalculateWallOffset(long seed, int i, Vector2Int direction){
        float noise = OpenSimplex2S.Noise2(seed, i * noiseRoughness, 0);
        float radius = averageRadius + maxRadiusVariance*noise*Math.Abs(noise);
        return Mathf.RoundToInt(radius/direction.magnitude)*Perpendicular(direction);
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
        bool yIsSteeper = Math.Abs(difference.x) < Math.Abs(difference.y);
        if (yIsSteeper){
            difference = Swizzle(difference);
        }
        float length = Math.Abs(difference.x);
        for (int i = 0; i < length; i++){
            Vector2Int offset = new(i*Math.Sign(difference.x), Mathf.RoundToInt(difference.y*i/length));
            if (yIsSteeper){
                offset = Swizzle(offset);
            }
            fillAction(inclusiveEnd+offset);
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

    private static Vector2Int Swizzle(Vector2Int vector){
        return new Vector2Int(vector.y, vector.x);
    }
}