using System;
using UnityEngine;

[CreateAssetMenu(menuName = "GenerationSteps/TerrainGeneration", fileName = "TerrainGeneration")]
public class TerrainGeneration : GenerationStep {
    [SerializeField] private float heightAboveSeaScaledNoiseRoughness;
    [Range(0, 1)] // 0 means nothing exists above the underground and 1 means the max height touches the top of the world grid.
    [SerializeField] private float maxHeightFraction;
    [SerializeField] private TerrainModificationCurve[] terrainModificationCurves;
    [SerializeField] private int dirtThickness;
    
    public override float Perform(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed){
        int worldHeightAboveSea = worldSize.height-worldSize.seaLevel;
        float maxHeight = worldHeightAboveSea*maxHeightFraction;
        for (int x = elevations.Length-1; x >= 0; x--){
            float noise = (1+OpenSimplex2S.Noise2(seed, x*heightAboveSeaScaledNoiseRoughness/worldHeightAboveSea, 0))/2;
            elevations[x] = worldSize.seaLevel+(int)(maxHeight*noise*noise);
        }
        foreach (TerrainModificationCurve curve in terrainModificationCurves){
            seed.Increment();
            for (int x = elevations.Length-1; x >= 0; x--){
                float noise = OpenSimplex2S.Noise2(seed, x*curve.noiseRoughness, 0);
                elevations[x] += (int)(curve.maxBlockDifference*noise);
            } 
        }
        for (int x = elevations.Length-1; x >= 0; x--){
            int y = worldSize.height-1;
            elevations[x] = Math.Clamp(elevations[x], 0, y);
            FillColumn(worldGrid, x, y,
                new BlockSection(elevations[x], BlockType.Air),
                new BlockSection(Math.Max(elevations[x]-dirtThickness, 0), BlockType.Dirt),
                new BlockSection(0, BlockType.Rock)
            );
        }
        return 1;
    }
}

[Serializable]
public struct TerrainModificationCurve {
    public float noiseRoughness;
    public float maxBlockDifference;
}