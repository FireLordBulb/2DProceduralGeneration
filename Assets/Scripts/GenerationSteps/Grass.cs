using UnityEngine;

[CreateAssetMenu(menuName = "GenerationSteps/Grass", fileName = "Grass")]
public class Grass : GenerationStep {
    public override float Perform(BlockType[,] worldGrid, int[] elevations, WorldSize worldSize, Seed seed){
        // The edge cases at the ends of the array can be ignored since ocean will overwrite that anyway.
        for (int x = elevations.Length-2; x > 0; x--){
            BlockType left, right;
            int y = elevations[x];
            do {
                worldGrid[x, y] = BlockType.Grass;
                left = worldGrid[x-1, y];
                right = worldGrid[x+1, y];
                y--;
            } while (right == BlockType.Air || left == BlockType.Air);
        }
        return 1;
    }
}
