using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class WorldGenerator : MonoBehaviour{
    [SerializeField] private Sprite square;
    [SerializeField] private string worldSizeName;
    [SerializeField] private WorldSize[] worldSizes;
    [SerializeField] private GenerationStep[] generationSteps;

    private static int X = 0, Y = 1;
    
    private BlockType[,] worldGrid;
    
    private void Start(){
        GenerateWorld();
    }
    private void GenerateWorld(){
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
                stepProgress = generationStep.Perform(worldGrid);
                generationProgressCompleted += (stepProgress - previousStepProgress)*generationStep.RelativeTimeToPerform;
                print($"Progress: {generationProgressCompleted/generationProgressNeeded}");
            }
        }
        Vector3 center = new Vector3(worldGrid.GetLength(X), worldGrid.GetLength(Y))/2;
        for (int i = worldGrid.GetLength(X)-1; i >= 0; i--){
            for (int j = worldGrid.GetLength(Y)-1; j >= 0; j--){
                BlockType blockType = worldGrid[i, j];
                if (blockType == BlockType.Air){
                    continue;
                }
                GameObject gridSquare = new("gridSquare"){
                    transform = {
                        position = new Vector3(i, j) - center
                    }
                };
                SpriteRenderer spriteRenderer = gridSquare.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = square;
                spriteRenderer.color = blockType switch {
                    BlockType.Rock => Color.gray,
                    BlockType.Dirt => new Color(165, 42, 42),
                    BlockType.Grass => Color.green,
                    BlockType.Sand => new Color(255, 240, 134),
                    BlockType.Water => Color.blue,
                    BlockType.DirtWall => new Color(80, 20, 20),
                    BlockType.RockWall => new Color(100, 100, 100),
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