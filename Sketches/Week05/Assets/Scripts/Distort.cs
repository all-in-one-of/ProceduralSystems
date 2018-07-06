using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Distort : MonoBehaviour
{
    private Mesh mesh;

    // Use this for initialization
    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
    }

    void Update()
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] += normals[i] * (Mathf.Sin(Time.time) * 0.001f);
            // float randAmount = Random.Range(-0.01f, 0.01f);
            // vertices[i] = new Vector3(vertices[i].x + randAmount, vertices[i].y + randAmount, vertices[i].z + randAmount);
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
    }

    // private void Update()
    // {
    //     int arraySize = (xSize + 1) * (ySize + 1);
    //     vertices = new Vector3[arraySize];
    //     Vector2[] uv = new Vector2[vertices.Length];
    //     Vector4[] tangents = new Vector4[vertices.Length];
    //     Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

    //     for (int i = 0, y = 0; y <= ySize; y++)
    //     {
    //         for (int x = 0; x <= xSize; x++, i++)
    //         {
    //             vertices[i] = new Vector3(x, y, heightScale * Mathf.PerlinNoise(noiseTime + x, 0f));
    //             uv[i] = new Vector2((float)x / xSize, (float)y / ySize);
    //             tangents[i] = tangent;
    //         }
    //     }

    //     mesh.vertices = vertices;
    //     mesh.uv = uv;
    //     mesh.RecalculateNormals();

    //     if (Time.frameCount > (turnaround * 2))
    //     {
    //         noiseTime = 0;
    //     }

    //     if (Time.frameCount >= turnaround && speed > 0) speed *= -1;

    //     noiseTime += speed;

    // }
}
