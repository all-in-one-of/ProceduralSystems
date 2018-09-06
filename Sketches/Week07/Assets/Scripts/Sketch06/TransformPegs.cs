using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformPegs : MonoBehaviour
{
    private float speed = 0.01f;
    private Mesh mesh;
    private List<Vertex> oneSide = new List<Vertex>();
    private List<Vertex> peg = new List<Vertex>();

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
                if (v.position.z <= 0.8f && v.position.z >= 0.4f && v.position.y <= 0.8f && v.position.y >= 0.4f)
                {
                    peg.Add(v);
                }
            }
        }
    }

    void Update()
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        for (int i = 0; i < peg.Count; i++)
        {
            var vertex = peg[i];
            vertex.position += new Vector3(speed, 0, 0);
            vertices[vertex.index] = vertex.position;
            if (vertex.position.x < -3.0f || vertex.position.x > -1.0f)
            {
                speed *= -1;
            }
            peg[i] = vertex;
        }

        mesh.vertices = vertices;
    }
}
