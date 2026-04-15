using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenTouch : MonoBehaviour
{
    private float ScreenWidth;

    // Use this for initialization
    void Start()
    {
        ScreenWidth = Screen.width;
    }

    // Update is called once per frame
    void Update()
    {
        if (Player.Instance == null) return;

        if (Input.touchCount > 0)
        {
            if (Input.GetTouch(0).position.x > ScreenWidth / 2)
            {
                Player.Instance.Arrowright();
            }
            else if (Input.GetTouch(0).position.x < ScreenWidth / 2)
            {
                Player.Instance.Arrowleft();
            }
        }
    }

}

