using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformCube : MonoBehaviour
{
    private Mesh mesh;

    // Use this for initialization
    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        vertices[0] += new Vector3(0, 0.01f, 0);
        vertices[1] += new Vector3(0, 0.01f, 0);

        mesh.vertices = vertices;
    }
}
