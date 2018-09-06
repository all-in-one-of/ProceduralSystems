using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubdividedCubeNoise : MonoBehaviour
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
            if (vertices[i].x == -1.0)
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

        // float displaceAmount = (0.25f - Mathf.PerlinNoise(Time.time, 0.0f)) * noiseScale;

        for (int i = 0; i < vertices.Length; i++)
        {
            float noisexyz = Perlin.Noise(vertices[i].y, vertices[i].z, vertices[i].z);
            float displaceAmount = Perlin.Noise(noisexyz, Time.time) * noiseScale;
            vertices[i] += new Vector3(displaceAmount, displaceAmount, displaceAmount);
        }

        mesh.vertices = vertices;
    }
}
