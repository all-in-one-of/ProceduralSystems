using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformSubdividedCube : MonoBehaviour
{
    public float noiseScale = 0.1f;
    private Mesh mesh;
    private List<Vertex> oneSide = new List<Vertex>();

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;

        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].x == -1.0 && vertices[i].y < 0.5f && vertices[i].z == 0.0f)
            {
                var v = new Vertex(vertices[i], i);
                oneSide.Add(v);
            }
        }
    }

    void Update()
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        float displaceAmount = (0.25f - Mathf.PerlinNoise(Time.time, 0.0f)) * noiseScale;

        for (int i = 0; i < oneSide.Count; i++)
        {
            var vertex = oneSide[i];
            vertex.position += new Vector3(displaceAmount, 0, 0);
            vertices[vertex.index] = vertex.position;
            oneSide[i] = vertex;
        }

        mesh.vertices = vertices;
    }
}
