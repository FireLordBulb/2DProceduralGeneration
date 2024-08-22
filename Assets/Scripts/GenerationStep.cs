using UnityEngine;

public abstract class GenerationStep : ScriptableObject{
    [SerializeField] private float relativeTimeToPerform = 1;

    public float RelativeTimeToPerform => relativeTimeToPerform;

    public abstract float Perform(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed);

    protected static void FillColumn(BlockType[,] worldGrid, int x, int y, params BlockSection[] sections){
        foreach (BlockSection section in sections){
            for (; y > section.LowerBound; y--){
                worldGrid[x, y] = section.Type;
            }
        }
    }
    
    protected struct BlockSection {
        // Exclusive lower bound.
        public readonly int LowerBound;
        public readonly BlockType Type;
        
        public BlockSection(int lowerBound, BlockType type){
            LowerBound = lowerBound;
            Type = type;
        }
    }
}