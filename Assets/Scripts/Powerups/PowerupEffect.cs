using UnityEngine;

public abstract class PowerupEffect : ScriptableObject
{
    [SerializeField] private Sprite sprite;

    public Sprite Sprite => sprite;

    public abstract void Apply(GameObject player);
}