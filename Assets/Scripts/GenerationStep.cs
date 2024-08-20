using UnityEngine;

public abstract class GenerationStep : ScriptableObject{
    [SerializeField] private float relativeTimeToPerform = 1;

    public float RelativeTimeToPerform => relativeTimeToPerform;

    public abstract float Perform(BlockType[,] worldGrid, long seed);
}