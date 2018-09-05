using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MyGrid : MonoBehaviour
{
    public int xSize, ySize;
    public GameObject point;
    private Vector3[] vertices;

    public void Awake()
    {
        StartCoroutine("Generate");
    }

    private IEnumerator Generate()
    {
        WaitForSeconds wait = new WaitForSeconds(0.1f);

        int arraySize = (xSize + 1) * (ySize + 1);
        vertices = new Vector3[arraySize];
        for (int i = 0, y = 0; y <= ySize; y++)
        {
            for (int x = 0; x <= xSize; x++, i++)
            {
                vertices[i] = new Vector3(x, y);
                Instantiate(point, vertices[i], Quaternion.identity);
                yield return wait;
            }
        }
    }
}
