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
        Debug.Log(ScreenWidth);
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.touchCount > 0)
        {
            if (Input.GetTouch(0).position.x > ScreenWidth / 2 )
            {
                Player.Instance.Arrowright();
            }
            if (Input.GetTouch(0).position.x < ScreenWidth / 2 )
            {
                Player.Instance.Arrowleft();
            }
        }

    }

}

