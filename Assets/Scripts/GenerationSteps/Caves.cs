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

    private BlockType[,] worldGrid;
    private int worldHeight;
    private Seed seed;
    private Stack<Cave> caves;
    
    private Vector2Int centerPosition;
    private VectorPair wallPositions;
    private Vector2Int direction;
    
    public override float Perform(BlockType[,] worldGridParameter, int[] elevations, WorldSize worldSize, Seed seedParameter){
        worldGrid = worldGridParameter;
        worldHeight = worldSize.height;
        seed = seedParameter;
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
            cave.WalkMaxSteps = Mathf.RoundToInt(lengthScalar.Value*worldHeight);
            if (doSpawnOnSurface){
                Vector2Int surfaceTangent = new(-1 - +1, elevations[spawnX-1]-elevations[spawnX+1]);
                // Reduces the surface tangent to one of the 8 cardinal/ordinal directions.
                surfaceTangent /= Math.Max(Math.Abs(surfaceTangent.x), Math.Abs(surfaceTangent.y));
                cave.StartDirection = Perpendicular(surfaceTangent);
                cave.StickyDirectionSteps = downFromSurfaceStickyDirectionSteps;
            } else {
                cave.StartDirection = Directions[Random.Range(0, Directions.Length)];
                cave.StartPosition.y -= Mathf.RoundToInt(startingDepthFraction.Value*cave.StartPosition.y);
            }
            // Retry if cave is spawned on a disallowed block.
            if (CaveBreakingBlocks.Contains(worldGrid[cave.StartPosition.x, cave.StartPosition.y])){
                i--;
                continue;
            }
            caves.Push(cave);
        }
        while (0 < caves.Count){
            Cave cave = caves.Pop();
            GenerateCave(cave);
            // Have to increment seed twice since a cave uses both seed and seed+1.
            seed.Increment();
            seed.Increment();
        }
        return 1;
    }

    private void GenerateCave(Cave cave){
        int radius = Mathf.RoundToInt(averageRadius);
        direction = cave.StartDirection;
        // Creates the cave opening before the cave proper starts.
        wallPositions.Left = wallPositions.Right = centerPosition = cave.StartPosition-radius*direction;
        GenerateCaveEnd(-radius, 0);
        // If the cave opening is already off grid, don't bother with the cave proper or cave end.
        if (IsOffGrid(wallPositions.Left) || IsOffGrid(wallPositions.Right) || IsOffGrid(centerPosition)){
            return;
        }
        // The cave proper.
        int branchStep = doBranch ? minBranchStep+(int)stepsBetweenBranches.Value : -1;
        int directionIndex = Array.FindIndex(Directions, vector => vector == direction);
        int stickyDirectionStepsLeft = cave.StickyDirectionSteps;
        int stickyDirectionIndex = directionIndex;
        int step, airBlockCount = 0;
        for (step = 0; step < cave.WalkMaxSteps; step++){
            VectorPair newPositions = CalculateNewWallPositions(step);
            if (IsOffGrid(newPositions.Left) || IsOffGrid(newPositions.Right) || IsOffGrid(centerPosition)){
                break;
            }
            if (CaveBreakingBlocks.Contains(worldGrid[centerPosition.x, centerPosition.y])){
                break;
            }
            if (worldGrid[centerPosition.x, centerPosition.y] == BlockType.Air && ++airBlockCount == maxAirBlocks){
                break;
            }
            TakeCaveStep(newPositions);

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
        GenerateCaveEnd(step, step+radius, radius);
    }

    // Makes an approximately half-circle-shaped area into cave walls (or air). 
    private void GenerateCaveEnd(int startingStep, int endingStep, int radiusScaleOffset = 0){
        for (int step = startingStep; step < endingStep; step++){
            float radiusScale = (endingStep-step - radiusScaleOffset)/averageRadius;
            radiusScale = 1-radiusScale*radiusScale;
            VectorPair newPositions = CalculateNewWallPositions(step, radiusScale);
            TakeCaveStep(newPositions, true);
        }
    }

    private VectorPair CalculateNewWallPositions(int step, float radiusScale = 1){
        VectorPair newPositions;
        newPositions.Left = centerPosition+CalculateWallOffset(step, radiusScale, false);
        newPositions.Right = centerPosition+CalculateWallOffset(step, radiusScale, true);
        return newPositions;
    }
    private Vector2Int CalculateWallOffset(int step, float radiusScale, bool isRight){
        Vector2Int perpendicular = (isRight ? -1 : +1)*Perpendicular(direction);
        // The left and right walls use different seeds since they should be fully independent of each other.
        float noise = OpenSimplex2S.Noise2(seed+(isRight ? 1 : 0), step*noiseRoughness, 0);
        float scaledRadius = (averageRadius + maxRadiusVariance*noise*Math.Abs(noise))*radiusScale;
        if (direction.sqrMagnitude == 1){
            return Mathf.RoundToInt(scaledRadius)*perpendicular;
        }
        int diagonalHalfSteps = Mathf.RoundToInt(scaledRadius*2/direction.magnitude);
        return diagonalHalfSteps/2*perpendicular + (diagonalHalfSteps%2 != 0 ? (perpendicular+direction)/2 : Vector2Int.zero);
    }
    
    private void TakeCaveStep(VectorPair newPositions, bool beSafe = false){
        ConnectCaveWall(newPositions, false, beSafe);
        ConnectCaveWall(newPositions, true, beSafe);
        centerPosition += direction;
    }
    private void ConnectCaveWall(VectorPair newPositions, bool isRight, bool beSafe){
        Action<Vector2Int> makeCaveWall = beSafe ? MakeCaveWallSafe : MakeCaveWall;
        Vector2Int otherWallPosition = wallPositions.Get(!isRight);
        FillDiagonal(newPositions.Get(isRight), wallPositions.Get(isRight), currentWallPosition => {
            FillDiagonal(currentWallPosition, otherWallPosition, caveInsidePosition => {
                makeCaveWall(caveInsidePosition);
                if (caveInsidePosition != currentWallPosition || currentWallPosition.y < otherWallPosition.y){
                    makeCaveWall(caveInsidePosition+Vector2Int.up);
                }
            });
        });
        wallPositions.Set(isRight, newPositions.Get(isRight));
    }
    // Runs the "fillAction" on a diagonal line of positions between two Vector2Ints
    private static void FillDiagonal(Vector2Int inclusiveEnd, Vector2Int exclusiveEnd, Action<Vector2Int> fillAction){
        Vector2Int difference = exclusiveEnd-inclusiveEnd;
        bool yIsSteeper = Math.Abs(difference.x) < Math.Abs(difference.y);
        if (yIsSteeper){
            difference = Swizzle(difference);
        }
        Vector2Int sign = new(Math.Sign(difference.x), Math.Sign(difference.y));
        Vector2Int absoluteDelta = new(Math.Abs(difference.x), Math.Abs(difference.y));
        Vector2Int doubleAbsoluteDelta = 2*absoluteDelta;
        int yOffset = 0;
        int accumulatedError = doubleAbsoluteDelta.y - absoluteDelta.x;
        for (int i = 0; i < absoluteDelta.x; i++){
            Vector2Int offset = new(i*sign.x, yOffset);
            fillAction(inclusiveEnd + (yIsSteeper ? Swizzle(offset) : offset));
            if (0 < accumulatedError){
                yOffset += sign.y;
                accumulatedError -= doubleAbsoluteDelta.x;
            }
            accumulatedError += doubleAbsoluteDelta.y;
        }
    }
    private void MakeCaveWallSafe(Vector2Int position){
        if (IsOffGrid(position)){
            return;
        }
        MakeCaveWall(position);
    }
    private void MakeCaveWall(Vector2Int position){
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

    private bool IsOffGrid(Vector2Int position){
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