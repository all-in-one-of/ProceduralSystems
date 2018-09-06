using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Vertex
{
    public int index;
    public Vector3 position;

    public Vertex(Vector3 newPosition, int newIndex)
    {
        position = newPosition;
        index = newIndex;
    }
}