using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MyGrid : MonoBehaviour
{
    public int xSize, ySize;
    public GameObject point;
    private Vector3[] vertices;
    private Mesh mesh;

    public void Awake()
    {
        StartCoroutine("Generate");
    }

    private IEnumerator Generate()
    {
        WaitForSeconds wait = new WaitForSeconds(0.1f);
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Procedural Grid";

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

    // private void OnDrawGizmos()
    // {
    //     if (vertices == null) return;

    //     Gizmos.color = Color.black;
    //     foreach (Vector3 vertice in vertices)
    //     {
    //         Gizmos.DrawSphere(vertice, 0.1f);
    //     }
    // }
}
