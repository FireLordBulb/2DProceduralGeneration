using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public class WorldGenerator : MonoBehaviour {
    public const int X = 0, Y = 1;
    
    [SerializeField] private bool doSaveAsFile;
    [Space]
    [SerializeField] private InputAction regenerationInputAction;
    [Header("Tilemap")]
    [Range(10, 500)]
    [SerializeField] private int columnsPerFrame;
    [SerializeField] private Grid grid;
    [SerializeField] private List<BlockTilePair> blockTilePairs;
    [Header("Seed")]
    [SerializeField] private bool useRandomSeed;
    [SerializeField] private long seed;
    [Header("Generation")]
    [SerializeField] private string worldSizeName;
    [SerializeField] private WorldSize[] worldSizes;
    [SerializeField] private GenerationStep[] generationSteps;
    
    private readonly Dictionary<BlockType, Tile> tileDictionary = new();
    private BlockType[,] worldGrid;
    private int[] elevations;
    private Seed seedReference;
    
    private Tilemap tilemap;
    private bool isGenerating;
    private string folderPath;
    
    private void Awake(){
        blockTilePairs.ForEach(pair => tileDictionary.Add(pair.block, pair.tile));
        grid = Instantiate(grid);
        tilemap = grid.GetComponentInChildren<Tilemap>();
        if (doSaveAsFile){
           folderPath = $"{Application.dataPath}/../Generated Worlds";
           if (!Directory.Exists(folderPath)){
               Directory.CreateDirectory(folderPath);
           } 
        }
    }
    
    private void Start(){
        GenerateWorld();
        regenerationInputAction.performed += _ => {
            if (isGenerating){
                return;
            }
            isGenerating = true;
            tilemap.ClearAllTiles();
            GenerateWorld();
        };
        regenerationInputAction.Enable();
    }
    
    private async void GenerateWorld(){
        if (useRandomSeed){
            byte[] longBytes = new byte[sizeof(long)];
            new System.Random().NextBytes(longBytes);
            seed = BitConverter.ToInt64(longBytes);
        }
        seedReference = new Seed(seed);
        Random.InitState((int)seed);
        WorldSize worldSize = worldSizes[0];
        foreach (WorldSize size in worldSizes){
            if (!size.name.Equals(worldSizeName)){
                continue;
            }
            worldSize = size;
            break;
        }
        worldGrid = new BlockType[worldSize.width, worldSize.height];
        elevations = new int[worldSize.width];
        float generationProgressNeeded = generationSteps.Sum(generationStep => generationStep.RelativeTimeToPerform);
        float generationProgressCompleted = 0;
        foreach (GenerationStep generationStep in generationSteps){
            float stepProgress = 0;
            while (stepProgress < 1){
                float previousStepProgress = stepProgress;
                stepProgress = generationStep.Perform(worldGrid, elevations, worldSize, seedReference);
                generationProgressCompleted += (stepProgress - previousStepProgress)*generationStep.RelativeTimeToPerform;
                print($"Progress: {(int)(1000*generationProgressCompleted/generationProgressNeeded)/10f}%");
                // Wait a bit to let the print actually get written to console.
                await Task.Delay(70);
            }
            // Pass a different seed to each step to ensure each step has unique random noise.
            seedReference.Increment(); 
        }

        if (doSaveAsFile){
            Texture2D image = new(worldSize.width, worldSize.height);
            for (int x = worldSize.width-1; x >= 0; x--){
                for (int y = worldSize.height-1; y >= 0; y--){
                    BlockType blockType = worldGrid[x, y];
                    if (tileDictionary.TryGetValue(blockType, out Tile tile)){
                        image.SetPixel(x, y, tile != null ? tile.color : new Color(0, 0, 0, 0));
                    } else {
                        Debug.LogError($"{blockType} has no matching tile!");
                    }
                }
            }
            await File.WriteAllBytesAsync($"{folderPath}/{worldSizeName}, {seed}.png", image.EncodeToPNG());
        }
        
        for (int x = worldSize.width-1; x >= 0; x--){
            for (int y = worldSize.height-1; y >= 0; y--){
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
            if (x%columnsPerFrame == 0){
                await Task.Yield();
            }
        }
        // Accept all input made during the lag spike caused by the above double for-loop while isGenerating is still true so they don't do anything.
        await Task.Yield();
        isGenerating = false;
    }
}

[Serializable]
public struct BlockTilePair {
    public BlockType block;
    public Tile tile;
}

[Serializable]
public class WorldSize {
    public int width, height;
    public int seaLevel;
    public string name;
    public int LeftBeachEdge { get; set; }
    public int RightBeachEdge { get; set; }
}