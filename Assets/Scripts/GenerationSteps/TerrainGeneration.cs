using System;
using UnityEngine;

[CreateAssetMenu(menuName = "GenerationSteps/TerrainGeneration", fileName = "TerrainGeneration")]
public class TerrainGeneration : GenerationStep {
    [SerializeField] private float noiseSmoothness;
    [Range(0, 1)] // 0 means nothing exists above the underground and 1 means the max height touches the top of the world grid.
    [SerializeField] private float maxHeightFraction;
    [SerializeField] private TerrainModificationCurve[] terrainModificationCurves;
    [SerializeField] private int dirtThickness;
    private int[] elevations;
    public override float Perform(BlockType[,] worldGrid, WorldSize worldSize, Seed seed){
        elevations = new int[worldSize.width];
        int worldHeightAboveUnderground = worldSize.height-worldSize.seaLevel;
        float maxHeight = worldHeightAboveUnderground*maxHeightFraction;
        for (int x = elevations.Length-1; x >= 0; x--){
            float noise = (1+OpenSimplex2S.Noise2(seed, x /noiseSmoothness /worldHeightAboveUnderground, 0))/2;
            // Square the magnitude of noise, making more of it be closer to 0.
            noise *= Math.Abs(noise);
            elevations[x] = worldSize.seaLevel+(int)(maxHeight*noise);
        }
        foreach (TerrainModificationCurve curve in terrainModificationCurves){
            seed.Increment();
            for (int x = elevations.Length-1; x >= 0; x--){
                float noise = OpenSimplex2S.Noise2(seed, x /curve.noiseSmoothness, 0);
                elevations[x] += (int)(curve.maxBlockDifference*noise);
            } 
        }
        for (int x = elevations.Length - 1; x >= 0; x--){
            int elevation = Math.Max(elevations[x], 0);
            {
                int y = worldSize.height-1;
                for (; y > elevation; y--){
                    worldGrid[x, y] = BlockType.Air;
                }
                worldGrid[x, y] = BlockType.Grass;
                y--;
                int lowestDirtY = elevation-dirtThickness;
                lowestDirtY = Math.Max(lowestDirtY, 0);
                for (; y >= lowestDirtY; y--){
                    worldGrid[x, y] = BlockType.Dirt;
                }
                for (; y >= 0; y--){
                    worldGrid[x, y] = BlockType.Rock;
                }
            }
        }
        return 1;
    }
}

[Serializable]
public struct TerrainModificationCurve {
    public float noiseSmoothness;
    public float maxBlockDifference;
}