using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct AnimLookup
{
    public int Key;
    public string Value;

    public AnimLookup(int Key, string Value)
    {
        this.Key = Key;
        this.Value = Value;
    }
}
