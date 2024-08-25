using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[CreateAssetMenu(menuName = "GenerationSteps/Caves", fileName = "Caves")]
public class Caves : GenerationStep {
    [SerializeField] private RandomFloatRange worldWidthPerCave;
    [SerializeField] private bool doSpawnOnSurface;
    [SerializeField] private RandomFloatRange startingDepthFraction;
    [SerializeField] private RandomFloatRange lengthScalar;
    [SerializeField] private int maxAirBlocks;
    [Header("Cave Radius")]
    [SerializeField] private float averageRadius;
    [SerializeField] private float maxRadiusVariance;
    [SerializeField] private float noiseRoughness;
    [Header("Controlled Random Walk")]
    [Range(0, 1)]
    [SerializeField] private float keepDirectionWeight;
    [Range(0, 1)]
    [SerializeField] private float keepSidewaysDirectionWeight;
    [Range(0, 1)]
    [SerializeField] private float upwardsWeight;

    private static readonly BlockType[] CaveBreakingBlocks = {BlockType.Water, BlockType.Sand};
    private static readonly Vector2Int[] Directions = {new(-1, +1), Vector2Int.left, new(-1, -1), Vector2Int.down, new(+1, -1), Vector2Int.right, new(+1, +1)};

    private Vector2Int centerPosition;
    private Vector2Int leftPosition, rightPosition;
    
    public override float Perform(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed){
        int leftSpawnEdge, rightSpawnEdge;
        if (doSpawnOnSurface){
            leftSpawnEdge = worldSize.LeftBeachEdge;
            rightSpawnEdge = worldSize.RightBeachEdge;
        } else {
            leftSpawnEdge = 0;
            rightSpawnEdge = elevations.Length;
        }
        int numberOfCaves = Mathf.RoundToInt((rightSpawnEdge-leftSpawnEdge)/worldWidthPerCave.Value);
        for (int i = 0; i < numberOfCaves; i++){
            GenerateCave(worldGrid, elevations, worldSize, seed, Random.Range(leftSpawnEdge, rightSpawnEdge));
        }
        return 1;
    }

    private void GenerateCave(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed, int startX){
        Vector2Int startPosition = new(startX, elevations[startX]);
        int walkMaxSteps = Mathf.RoundToInt(lengthScalar.Value*worldSize.height);
        Vector2Int direction;
        if (doSpawnOnSurface){
            Vector2Int surfaceTangent = new(-1 - +1, elevations[startX-1]-elevations[startX+1]);
            // Reduces the surface tangent to one of the 8 cardinal/ordinal directions.
            surfaceTangent /= Math.Max(Math.Abs(surfaceTangent.x), Math.Abs(surfaceTangent.y));
            direction = Perpendicular(surfaceTangent);
        } else{
            direction = Directions[Random.Range(0, Directions.Length)];
            startPosition.y -= Mathf.RoundToInt(startingDepthFraction.Value*worldSize.height);
        }
        centerPosition = leftPosition = rightPosition = startPosition;
        int step = 0;
        int airBlockCount = 0;
        int directionIndex = Array.FindIndex(Directions, vector => vector == direction);
        for (; step < walkMaxSteps; step++){
            // The left and right walls use different seeds since they should be fully independent of each other.
            Vector2Int newLeftPosition = centerPosition + CalculateWallOffset(seed, step, direction);
            Vector2Int newRightPosition = centerPosition - CalculateWallOffset(seed+1, step, direction);
            if (IsBreakingBlock(worldGrid, centerPosition) || IsOffGrid(newLeftPosition, elevations.Length) ||
                IsOffGrid(newRightPosition, elevations.Length)){
                break;
            }
            if (worldGrid[centerPosition.x, centerPosition.y] == BlockType.Air){
                airBlockCount++;
                if (airBlockCount == maxAirBlocks){
                    break;
                }
            }
            TakeCaveStep(worldGrid, newLeftPosition, newRightPosition, direction);

            float weight = direction.y == 0 ? keepSidewaysDirectionWeight : keepDirectionWeight;
            if (Random.value < weight){
                continue;
            }
            directionIndex = GetNewDirectionIndex(directionIndex);
            direction = Directions[directionIndex];
        }
        int walkSteps = step;
        int endSteps = walkSteps+Mathf.RoundToInt(averageRadius);
        for (; step < endSteps; step++){
            float scaleFactor = (step-walkSteps)/averageRadius;
            scaleFactor = 1-scaleFactor*scaleFactor;
            // The left and right walls use different seeds since they should be fully independent of each other.
            Vector2Int newLeftPosition = centerPosition + CalculateWallOffset(seed, step, direction, scaleFactor);
            Vector2Int newRightPosition = centerPosition - CalculateWallOffset(seed+1, step, direction, scaleFactor);
            TakeCaveStep(worldGrid, newLeftPosition, newRightPosition, direction, true);
        }
        // Have to increment seed twice since a cave uses both seed and seed+1.
        seed.Increment();
        seed.Increment();
    }

    private Vector2Int CalculateWallOffset(long seed, int step, Vector2Int direction, float scaleFactor = 1){
        float noise = OpenSimplex2S.Noise2(seed, step*noiseRoughness, 0);
        float radius = averageRadius + maxRadiusVariance*noise*Math.Abs(noise);
        return Mathf.RoundToInt(radius*scaleFactor/direction.magnitude)*Perpendicular(direction);
    }
    
    private void TakeCaveStep(BlockType[,] worldGrid, Vector2Int newLeftPosition, Vector2Int newRightPosition, Vector2Int direction, bool beSafe = false){
        ConnectCaveWall(worldGrid, newLeftPosition, leftPosition, rightPosition, beSafe);
        leftPosition = newLeftPosition;
        ConnectCaveWall(worldGrid, newRightPosition, rightPosition, leftPosition, beSafe);
        rightPosition = newRightPosition;
        centerPosition += direction;
    }
    private static void ConnectCaveWall(BlockType[,] worldGrid, Vector2Int newPosition, Vector2Int currentPosition, Vector2Int otherWallPosition, bool beSafe){
        Action<Vector2Int> fillAction = beSafe ? caveInsidePosition => {
            MakeCaveWallSafe(worldGrid, caveInsidePosition);
            MakeCaveWallSafe(worldGrid, caveInsidePosition+Vector2Int.up);
        } : caveInsidePosition => {
            MakeCaveWall(worldGrid, caveInsidePosition);
            MakeCaveWall(worldGrid, caveInsidePosition+Vector2Int.up);
        };
        FillDiagonal(newPosition, currentPosition, currentWallPosition => {
            FillDiagonal(currentWallPosition, otherWallPosition, fillAction);
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
    private static void MakeCaveWallSafe(BlockType[,] worldGrid, Vector2Int position){
        if (IsOffGrid(position, worldGrid.GetLength(WorldGenerator.X))){
            return;
        }
        MakeCaveWall(worldGrid, position);
    }
    private static void MakeCaveWall(BlockType[,] worldGrid, Vector2Int position){
        worldGrid[position.x, position.y] = worldGrid[position.x, position.y] switch {
            BlockType.Rock => BlockType.RockWall,
            BlockType.Dirt => BlockType.DirtWall,
            BlockType.Grass => BlockType.Air,
            BlockType.Sand => BlockType.SandWall,
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
    
    private static bool IsBreakingBlock(BlockType[,] worldGrid, Vector2Int position){
        return IsOffGrid(position, worldGrid.GetLength(WorldGenerator.X)) || CaveBreakingBlocks.Contains(worldGrid[position.x, position.y]);
    }
    
    private static bool IsOffGrid(Vector2Int position, int xBound){
        return position.y < 0 || position.x < 0 || xBound <= position.x;
    }
    
    // Gives counter-clockwise perpendicular. // Why doesn't this already exist for Vector2Int, Unity!!!!
    private static Vector2Int Perpendicular(Vector2Int vector){
        return new Vector2Int(-vector.y, vector.x);
    }
    private static Vector2Int Swizzle(Vector2Int vector){
        return new Vector2Int(vector.y, vector.x);
    }
}