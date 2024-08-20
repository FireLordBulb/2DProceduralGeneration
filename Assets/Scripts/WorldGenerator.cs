using System;
using System.Linq;
using UnityEngine;

public class WorldGenerator : MonoBehaviour {
    public const int X = 0, Y = 1;

    [SerializeField] private Sprite square;
    [SerializeField] private bool useRandomSeed;
    [SerializeField] private long seed;
    [SerializeField] private string worldSizeName;
    [SerializeField] private WorldSize[] worldSizes;
    [SerializeField] private GenerationStep[] generationSteps;
    
    private BlockType[,] worldGrid;
    
    private void Start(){
        GenerateWorld();
    }
    private void GenerateWorld(){
        if (useRandomSeed){
            byte[] longBytes = new byte[sizeof(long)];
            new System.Random().NextBytes(longBytes);
            seed = BitConverter.ToInt64(longBytes);
        }
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
                stepProgress = generationStep.Perform(worldGrid, seed);
                generationProgressCompleted += (stepProgress - previousStepProgress)*generationStep.RelativeTimeToPerform;
                print($"Progress: {generationProgressCompleted/generationProgressNeeded}");
            }
            // Pass a different seed to each step to ensure each step has unique random noise.
            seed++;
        }
        Vector3 center = new Vector3(worldGrid.GetLength(X), worldGrid.GetLength(Y))/2;
        for (int i = worldGrid.GetLength(X)-1; i >= 0; i--){
            for (int j = worldGrid.GetLength(Y)-1; j >= 0; j--){
                BlockType blockType = worldGrid[i, j];
                if (blockType == BlockType.Air){
                    continue;
                }
                GameObject gridSquare = new($"GridSquare({i}, {j})"){
                    transform = {
                        position = new Vector3(i, j) - center
                    }
                };
                SpriteRenderer spriteRenderer = gridSquare.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = square;
                spriteRenderer.color = blockType switch {
                    BlockType.Rock => Color.gray,
                    BlockType.Dirt => new Color(0.38f, 0.18f, 0.12f),
                    BlockType.Grass => Color.green,
                    BlockType.Sand => new Color(1f, 0.94f, 0.53f),
                    BlockType.Water => Color.blue,
                    BlockType.DirtWall => new Color(0.19f, 0.09f, 0.06f),
                    BlockType.RockWall => new Color(0.4f, 0.4f, 0.4f),
                    _ => spriteRenderer.color
                };
            }
        }
    }
    
}

[Serializable]
public struct WorldSize {
    public int width, height;
    public string name;
}