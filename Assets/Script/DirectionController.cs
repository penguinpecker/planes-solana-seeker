using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DirectionController : MonoBehaviour
{
    #region Public_Variable
    public GameObject _player;
    #endregion

    #region Private_Variable
    #endregion
    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
#if UNITY_EDITOR
        Vector2 mousePOS = Input.mousePosition;
        mousePOS = Camera.main.ScreenToWorldPoint(mousePOS);
        // _player.transform.up = (Input.GetTouch(0).position) - (Vector2)(_player.transform.up);
        if (Input.GetMouseButton(0))
        {
            Vector2 direction = new Vector2(mousePOS.x - _player.transform.position.x, mousePOS.y - _player.transform.position.y);
            _player.transform.up = direction;
        }
#endif
#if UNITY_ANDROID
        if (Input.touchCount <= 0)
            return;

        Touch touch = Input.GetTouch(0);

        Vector3 pointerPos = touch.position;
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(pointerPos);

        Vector2 directionnew = new Vector2(mousePos.x - _player.transform.position.x, mousePos.y - _player.transform.position.y);
        _player.transform.up = directionnew;
#endif
    }
}