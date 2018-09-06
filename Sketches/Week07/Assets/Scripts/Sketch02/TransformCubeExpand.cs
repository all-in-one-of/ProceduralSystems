using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformCubeExpand : MonoBehaviour
{
    public struct Vertex
    {
        public int index;
        public Vector3 position;

        public Vertex(Vector3 newPosition, int newIndex)
        {
            position = newPosition;
            index = newIndex;
        }
    }

    private Mesh mesh;
    private List<Vertex> oneSide = new List<Vertex>();

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;

        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].x == 0.5)
            {
                var v = new Vertex(vertices[i], i);
                oneSide.Add(v);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        for (int i = 0; i < oneSide.Count; i++)
        {
            var vertex = oneSide[i];
            vertex.position = vertex.position + new Vector3(0.01f, 0, 0);
            vertices[vertex.index] = vertex.position;
            oneSide[i] = vertex;
        }

        mesh.vertices = vertices;
    }
}
