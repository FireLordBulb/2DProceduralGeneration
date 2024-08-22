using UnityEngine;

[CreateAssetMenu(menuName = "GenerationSteps/Oceans", fileName = "Oceans")]
public class Oceans : GenerationStep{
    [SerializeField] private int waterWidth, waterDepth;
    [SerializeField] private int seaBedThickness;
    [SerializeField] private int beachWidth, beachDepth;
    public override float Perform(BlockType[,] worldGrid, WorldSize worldSize, Seed seed){
        return 1;
    }
}
