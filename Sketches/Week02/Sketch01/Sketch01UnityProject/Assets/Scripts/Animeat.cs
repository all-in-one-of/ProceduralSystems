using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Substance.Game;

public class Animeat : MonoBehaviour
{
    public Substance.Game.SubstanceGraph myMeat;
    public float speed = 0.1f;

    private float t = -1.0f;
    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        float p = Mathf.PingPong(Time.time * speed, 0.5f) + 0.25f;
        myMeat.SetInputFloat("meatiness", p);
        myMeat.QueueForRender();
    }
}
