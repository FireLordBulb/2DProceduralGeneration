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
    private VectorPair wallPositions;
    
    public override float Perform(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed){
        (int, int) spawnEdges = doSpawnOnSurface ? (worldSize.LeftBeachEdge, worldSize.RightBeachEdge) : (0, elevations.Length);
        int numberOfCaves = Mathf.RoundToInt((spawnEdges.Item2-spawnEdges.Item1)/worldWidthPerCave.Value);
        for (int i = 0; i < numberOfCaves; i++){
            GenerateCave(worldGrid, elevations, worldSize, seed, Random.Range(spawnEdges.Item1, spawnEdges.Item2));
        }
        return 1;
    }

    private void GenerateCave(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed, int startX){
        centerPosition = new Vector2Int(startX, elevations[startX]);
        int walkMaxSteps = Mathf.RoundToInt(lengthScalar.Value*worldSize.height);
        int radius = Mathf.RoundToInt(averageRadius);
        Vector2Int direction;
        if (doSpawnOnSurface){
            Vector2Int surfaceTangent = new(-1 - +1, elevations[startX-1]-elevations[startX+1]);
            // Reduces the surface tangent to one of the 8 cardinal/ordinal directions.
            surfaceTangent /= Math.Max(Math.Abs(surfaceTangent.x), Math.Abs(surfaceTangent.y));
            direction = Perpendicular(surfaceTangent);
        } else {
            direction = Directions[Random.Range(0, Directions.Length)];
            centerPosition.y -= Mathf.RoundToInt(startingDepthFraction.Value*worldSize.height);
        }
        // Creates the cave opening before the cave proper starts.
        centerPosition = wallPositions.Left = wallPositions.Right = centerPosition-radius*direction;
        GenerateCaveEnd(worldGrid, seed, direction, -radius, 0);
        // The cave proper.
        int step, airBlockCount = 0, directionIndex = Array.FindIndex(Directions, vector => vector == direction);
        for (step = 0; step < walkMaxSteps; step++){
            VectorPair newPositions = CalculateNewWallPositions(seed, step, direction);
            if (IsOffGrid(worldGrid, newPositions.Left) || IsOffGrid(worldGrid, newPositions.Right)){
                break;
            }
            if (CaveBreakingBlocks.Contains(worldGrid[centerPosition.x, centerPosition.y])){
                break;
            }
            if (worldGrid[centerPosition.x, centerPosition.y] == BlockType.Air && ++airBlockCount == maxAirBlocks){
                break;
            }
            TakeCaveStep(worldGrid, newPositions, direction);

            float weight = direction.y == 0 ? keepSidewaysDirectionWeight : keepDirectionWeight;
            if (Random.value < weight){
                continue;
            }
            directionIndex = GetNewDirectionIndex(directionIndex);
            direction = Directions[directionIndex];
        }
        // Creates the cave end after the cave proper ends.
        GenerateCaveEnd(worldGrid, seed, direction, step, step+radius, radius);
        // Have to increment seed twice since a cave uses both seed and seed+1.
        seed.Increment();
        seed.Increment();
    }

    // Makes an approximately half-circle-shaped area into cave walls (or air). 
    private void GenerateCaveEnd(BlockType[,] worldGrid, Seed seed, Vector2Int direction, int startingStep, int endingStep, int radiusScaleOffset = 0){
        for (int step = startingStep; step < endingStep; step++){
            float radiusScale = (endingStep-step - radiusScaleOffset)/averageRadius;
            radiusScale = 1-radiusScale*radiusScale;
            VectorPair newPositions = CalculateNewWallPositions(seed, step, direction, radiusScale);
            TakeCaveStep(worldGrid, newPositions, direction, true);
        }
    }

    private VectorPair CalculateNewWallPositions(long seed, int step, Vector2Int direction, float radiusScale = 1){
        VectorPair newPositions;
        // The left and right walls use different seeds since they should be fully independent of each other.
        newPositions.Left = centerPosition+CalculateWallOffset(seed, step, direction, radiusScale, false);
        newPositions.Right = centerPosition+CalculateWallOffset(seed+1, step, direction, radiusScale, true);
        return newPositions;
    }
    private Vector2Int CalculateWallOffset(long seed, int step, Vector2Int direction, float radiusScale, bool isRight){
        Vector2Int perpendicular = (isRight ? -1 : +1)*Perpendicular(direction);
        float noise = OpenSimplex2S.Noise2(seed, step*noiseRoughness, 0);
        float scaledRadius = (averageRadius + maxRadiusVariance*noise*Math.Abs(noise))*radiusScale;
        if (direction.sqrMagnitude == 1){
            return Mathf.RoundToInt(scaledRadius)*perpendicular;
        }
        int diagonalHalfSteps = Mathf.RoundToInt(scaledRadius*2/direction.magnitude);
        return diagonalHalfSteps/2*perpendicular + (diagonalHalfSteps%2 != 0 ? (perpendicular+direction)/2 : Vector2Int.zero);
    }
    
    private void TakeCaveStep(BlockType[,] worldGrid, VectorPair newPositions, Vector2Int direction, bool beSafe = false){
        ConnectCaveWall(worldGrid, newPositions, false, beSafe);
        ConnectCaveWall(worldGrid, newPositions, true, beSafe);
        centerPosition += direction;
    }
    private void ConnectCaveWall(BlockType[,] worldGrid, VectorPair newPositions, bool isRight, bool beSafe){
        Action<BlockType[,], Vector2Int> makeCaveWall = beSafe ? MakeCaveWallSafe : MakeCaveWall;
        FillDiagonal(newPositions.Get(isRight), wallPositions.Get(isRight), currentWallPosition => {
            FillDiagonal(currentWallPosition, wallPositions.Get(!isRight), caveInsidePosition => {
                makeCaveWall(worldGrid, caveInsidePosition);
                makeCaveWall(worldGrid, caveInsidePosition+Vector2Int.up);
            });
        });
        wallPositions.Set(isRight, newPositions.Get(isRight));
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
        if (IsOffGrid(worldGrid, position)){
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

    private static bool IsOffGrid(BlockType[,] worldGrid, Vector2Int position){
        return position.y < 0 || position.x < 0 || worldGrid.GetLength(WorldGenerator.X) <= position.x;
    }
    
    // Gives counter-clockwise perpendicular. // Why doesn't this already exist for Vector2Int, Unity!!!!
    private static Vector2Int Perpendicular(Vector2Int vector){
        return new Vector2Int(-vector.y, vector.x);
    }
    private static Vector2Int Swizzle(Vector2Int vector){
        return new Vector2Int(vector.y, vector.x);
    }

    private struct VectorPair {
        public Vector2Int Left;
        public Vector2Int Right;

        public Vector2Int Get(bool isRight){
            return isRight ? Right : Left;
        }
        public void Set(bool isRight, Vector2Int value){
            if (isRight){
                Right = value;
            } else {
                Left = value;
            }
        }
    }
}