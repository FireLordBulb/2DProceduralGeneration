using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[CreateAssetMenu(menuName = "GenerationSteps/Caves", fileName = "Caves")]
public class Caves : GenerationStep {
    [SerializeField] private RandomFloatRange worldWidthPerCave;
    [SerializeField] private float minSpawnDistance;
    [SerializeField] private bool doSpawnOnSurface;
    [SerializeField] private int downFromSurfaceStickyDirectionSteps;
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
    [Range(0, 1)]
    [SerializeField] private float straightDownWeight;
    [Header("Branching")]
    [SerializeField] private bool doBranch;
    [Range(0, 1)]
    [SerializeField] private float branchChance;
    [SerializeField] private int minBranchStep;
    [SerializeField] private RandomFloatRange stepsBetweenBranches;
    [SerializeField] private int branchStickyDirectionSteps;
    
    private const int StickyDirectionMaxTurn = 1;
    private static readonly BlockType[] CaveBreakingBlocks = {BlockType.Water, BlockType.Sand, BlockType.SandWall};
    private static readonly Vector2Int[] Directions = {new(-1, +1), Vector2Int.left, new(-1, -1), Vector2Int.down, new(+1, -1), Vector2Int.right, new(+1, +1)};

    private Stack<Cave> caves;
    private Vector2Int centerPosition;
    private VectorPair wallPositions;
    
    public override float Perform(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed){
        (int, int) spawnEdges = doSpawnOnSurface ? (worldSize.LeftBeachEdge, worldSize.RightBeachEdge) : (0, elevations.Length);
        int numberOfCaves = Mathf.RoundToInt((spawnEdges.Item2-spawnEdges.Item1)/worldWidthPerCave.Value);
        caves = new Stack<Cave>(numberOfCaves);
        int minSpawnSquareDistance = Mathf.CeilToInt(minSpawnDistance*minSpawnDistance);
        for (int i = 0; i < numberOfCaves; i++){
            Cave cave = new();
            int spawnX;
            do {
                spawnX = Random.Range(spawnEdges.Item1, spawnEdges.Item2);
                cave.StartPosition = new Vector2Int(spawnX, elevations[spawnX]);
            } while (0 < minSpawnDistance && caves.Any(otherCave => (otherCave.StartPosition-cave.StartPosition).sqrMagnitude < minSpawnSquareDistance));
            cave.WalkMaxSteps = Mathf.RoundToInt(lengthScalar.Value*worldSize.height);
            if (doSpawnOnSurface){
                Vector2Int surfaceTangent = new(-1 - +1, elevations[spawnX-1]-elevations[spawnX+1]);
                // Reduces the surface tangent to one of the 8 cardinal/ordinal directions.
                surfaceTangent /= Math.Max(Math.Abs(surfaceTangent.x), Math.Abs(surfaceTangent.y));
                cave.StartDirection = Perpendicular(surfaceTangent);
                cave.StickyDirectionSteps = downFromSurfaceStickyDirectionSteps;
            } else {
                cave.StartDirection = Directions[Random.Range(0, Directions.Length)];
                cave.StartPosition.y -= Mathf.RoundToInt(startingDepthFraction.Value*worldSize.height);
            }
            caves.Push(cave);
        }
        while (0 < caves.Count){
            Cave cave = caves.Pop();
            GenerateCave(worldGrid, worldSize.height, seed, cave);
        }
        return 1;
    }

    private void GenerateCave(BlockType[,] worldGrid, int worldHeight, Seed seed, Cave cave){
        int radius = Mathf.RoundToInt(averageRadius);
        Vector2Int direction = cave.StartDirection;
        // Creates the cave opening before the cave proper starts.
        wallPositions.Left = wallPositions.Right = centerPosition = cave.StartPosition-radius*direction;
        GenerateCaveEnd(worldGrid, seed, direction, -radius, 0);
        // The cave proper.
        int branchStep = doBranch ? minBranchStep+(int)stepsBetweenBranches.Value : -1;
        int directionIndex = Array.FindIndex(Directions, vector => vector == direction);
        int stickyDirectionStepsLeft = cave.StickyDirectionSteps;
        int stickyDirectionIndex = directionIndex;
        int step, airBlockCount = 0;
        for (step = 0; step < cave.WalkMaxSteps; step++){
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

            if (step == branchStep){
                branchStep = step+(int)stepsBetweenBranches.Value;
                if (Random.value < branchChance){
                    int randomSign = Random.Range(0, 2)*2-1;
                    Vector2Int branchDirection = randomSign*Perpendicular(direction);
                    caves.Push(new Cave {
                        StartPosition = centerPosition, 
                        // If the chosen perpendicular goes straight up, use the other perpendicular (straight down).
                        StartDirection = branchDirection == Vector2Int.up ? Vector2Int.down : branchDirection,
                        WalkMaxSteps = Mathf.RoundToInt(Random.Range(0, lengthScalar.Max*worldHeight-step)),
                        StickyDirectionSteps = branchStickyDirectionSteps
                    });
                    stickyDirectionStepsLeft = branchStickyDirectionSteps;
                    stickyDirectionIndex = directionIndex;
                }
            }
            
            if (0 < stickyDirectionStepsLeft){
                stickyDirectionStepsLeft--;
            }
            float weight = direction.y == 0 ? keepSidewaysDirectionWeight : keepDirectionWeight;
            if (Random.value < weight){
                continue;
            }
            int newDirectionIndex = GetNewDirectionIndex(directionIndex);
            if (0 < stickyDirectionStepsLeft && StickyDirectionMaxTurn < Math.Abs(newDirectionIndex-stickyDirectionIndex)){
                continue;
            }
            directionIndex = newDirectionIndex;
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
        Vector2Int otherWallPosition = wallPositions.Get(!isRight);
        FillDiagonal(newPositions.Get(isRight), wallPositions.Get(isRight), currentWallPosition => {
            FillDiagonal(currentWallPosition, otherWallPosition, caveInsidePosition => {
                makeCaveWall(worldGrid, caveInsidePosition);
                if (caveInsidePosition != currentWallPosition || currentWallPosition.y < otherWallPosition.y){
                    makeCaveWall(worldGrid, caveInsidePosition+Vector2Int.up);
                }
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
        if (Directions[leftIndex] == Vector2Int.down){
            weightedMiddlePoint = straightDownWeight;
        } else if (Directions[rightIndex] == Vector2Int.down){
            weightedMiddlePoint = 1-straightDownWeight;
        } else if (0 < Directions[leftIndex].y){
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

    private struct Cave {
        public Vector2Int StartPosition;
        public Vector2Int StartDirection;
        public int WalkMaxSteps;
        public int StickyDirectionSteps;
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