using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SwipeControl : MonoBehaviour
{

    #region Public_Variables
    #endregion

    #region Private_Variables     
    private Vector2 _startTouch, _swipeDelta, _endTouch;

    private bool _isDragging = false;

    [SerializeField]
    #endregion

    #region Events
    #endregion

    #region Unity_CallBacks
    void Awake()
    {

    }

    void OnEnable()
    {

    }

    // Use this for initialization
    void Start()
    {
        // Debug.Log("swipe started");
    }

    // Update is called once per frame
    void Update()
    {
        _swipeDelta = Vector2.zero;                             // RESETTING SWIPE DELTA VALUE TO ZERO CONTINOUSLY IF NO CLICK AND TOUCH

        MobileInput();
        MouseInput();
    }

    void OnDisable()
    {

    }
    #endregion

    #region Private_Methods

    private void MobileInput()
    {
        if (Input.touchCount > 0)
        {
            if (Input.touches[0].phase == TouchPhase.Began)
            {
                _startTouch = Input.touches[0].position;
                _isDragging = true;
            }

            else if (Input.touches[0].phase == TouchPhase.Ended || Input.touches[0].phase == TouchPhase.Canceled)
            {
                _endTouch = Input.touches[0].position;
                Distance();
                MoveObject();
                Reset();
            }
        }
    }

    private void MouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            //Debug.Log("MouseClick");              
            _startTouch = Input.mousePosition;                                  // GET THE POSITION OF MOUSEBUTTON DOWN

            //Debug.Log("touch started :" + Input.mousePosition);
            _isDragging = true;                                                 // FOR RUN THE DISTANCE SCRIPT ONLY AFTER MOUSE BUTTON DOWN ZERO
        }

        else if (Input.GetMouseButtonUp(0))
        {
            //Debug.Log("MouseUp");

            _endTouch = Input.mousePosition;                                    // FOR GETTING THE POSITION OF MOUSE BUTTON UP 

            Distance();                                                         // FOR MEASURE THE DISTANCE BETWEEN START AND END TOUCH

            MoveObject();                                                       // MOVE OBJECT AFTER MESURING DISTANCE

            Reset();                                                            // ASSIGN ZERO VALUE TO START AND END VECTOR

        }
    }

    private void Distance()
    {
        if (_isDragging)
        {
            if (Input.touchCount > 0)
            {
                _swipeDelta = _endTouch - _startTouch;
                // Debug.Log(_swipeDelta.magnitude);
            }

            else if (Input.GetMouseButtonUp(0))
            {
                _swipeDelta = _endTouch - _startTouch;
                //Debug.Log("swipe delta value: " + _swipeDelta.magnitude);
            }
        }
    }

    private void MoveObject()
    {
        if (_swipeDelta.magnitude > 100.0f)                                      // MEASURE DISTANCE BETWEEN START AND END TOUCH
        {
            float x = _swipeDelta.x;
            float y = _swipeDelta.y;

            Debug.Log("value of x :" + x);
            Debug.Log("value of y :" + y);


            if (Mathf.Abs(x) > Mathf.Abs(y))                                     // FOR MEASURE WETHER X IS BIGGER OR Y. IT WILL ONLY CONSIDE VALUE NOT SIGN
            {
                if (x > 0)
                {
                    //_swipeRight = true;
                  //  _playerNew.MoveRight();                                      // METHOD CALL FROM PLAYER SCRIPT

                }
                else if (x < 0)
                {
                    //_swipeLeft = true;
                  //  _playerNew.Moveleft();                                      // METHOD CALL FROM PLAYER SCRIPT
                }
            }
            else
            {
                if (y > 0)
                {
                    //_swipeUp = true;
                   // _playerNew.MoveUp();                                        // METHOD CALL FROM PLAYER SCRIPT
                }
                else
                {
                    //_swipeDown = true;
                    //_playerNew.MoveDown();                                      // METHOD CALL FROM PLAYER SCRIPT
                }
            }
        }
    }

    private void Reset()
    {
        _swipeDelta = _startTouch = _endTouch = Vector2.zero;               // SET START AND END VECTOR AS ZERO THAT SWIPEDELTA BECOME ZERO ELSE IT WILL TAKE AS LAST ASSIGN VALE
        _isDragging = false;
    }

    #endregion

    #region Public_Methods        
    #endregion

    #region Coroutines
    #endregion

    #region Custom_CallBacks
    #endregion
}

