using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldGenerator : MonoBehaviour {
    public const int X = 0, Y = 1;

    [SerializeField] private Grid grid;
    [SerializeField] private List<BlockTilePair> blockTilePairs;
    [SerializeField] private bool useRandomSeed;
    [SerializeField] private long seed;
    [SerializeField] private string worldSizeName;
    [SerializeField] private WorldSize[] worldSizes;
    [SerializeField] private GenerationStep[] generationSteps;
    
    private readonly Dictionary<BlockType, Tile> tileDictionary = new();
    private BlockType[,] worldGrid;
    private Seed seedReference;

    private void Awake(){
        blockTilePairs.ForEach(pair => tileDictionary.Add(pair.block, pair.tile));
    }
    private void Start(){
        GenerateWorld();
    }
    private void GenerateWorld(){
        if (useRandomSeed){
            byte[] longBytes = new byte[sizeof(long)];
            new System.Random().NextBytes(longBytes);
            seed = BitConverter.ToInt64(longBytes);
        }
        seedReference = new Seed(seed);
        WorldSize worldSize = worldSizes[0];
        foreach (WorldSize size in worldSizes){
            if (!size.name.Equals(worldSizeName)){
                continue;
            }
            worldSize = size;
            break;
        }
        worldGrid = new BlockType[worldSize.width, worldSize.height];
        float generationProgressNeeded = generationSteps.Sum(generationStep => generationStep.RelativeTimeToPerform);
        float generationProgressCompleted = 0;
        foreach (GenerationStep generationStep in generationSteps){
            float stepProgress = 0;
            while (stepProgress < 1){
                float previousStepProgress = stepProgress;
                stepProgress = generationStep.Perform(worldGrid, worldSize, seedReference);
                generationProgressCompleted += (stepProgress - previousStepProgress)*generationStep.RelativeTimeToPerform;
                print($"Progress: {generationProgressCompleted/generationProgressNeeded}");
            }
            // Pass a different seed to each step to ensure each step has unique random noise.
            seedReference.Increment();
        }
        Instantiate(grid);
        Tilemap tilemap = grid.GetComponentInChildren<Tilemap>();
        for (int x = worldGrid.GetLength(X)-1; x >= 0; x--){
            for (int y = worldGrid.GetLength(Y)-1; y >= 0; y--){
                BlockType blockType = worldGrid[x, y];
                if (tileDictionary.TryGetValue(blockType, out Tile tile)){
                    // A blockType with a null tile means don't put any tile at this location.
                    if (tile != null){
                        tilemap.SetTile(new Vector3Int(x, y), tile);
                    }
                } else {
                    Debug.LogError($"{blockType} has no matching tile!");
                }
            }
        }
    }
}

[Serializable]
public struct BlockTilePair {
    public BlockType block;
    public Tile tile;
}

[Serializable]
public struct WorldSize {
    public int width, height;
    public int undergroundTopY;
    public string name;
}