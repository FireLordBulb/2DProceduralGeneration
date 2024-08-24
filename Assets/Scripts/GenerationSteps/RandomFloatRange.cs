using System;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public struct RandomFloatRange {
    [SerializeField] private float min, max;

    public float Value => min+(max-min)*Random.value;
}