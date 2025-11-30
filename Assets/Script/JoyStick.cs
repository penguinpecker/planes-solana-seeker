using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


namespace Amar
{
    public class JoyStick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
    {
        [SerializeField]
        Image _bgImage, _joystickImage;
        [SerializeField]
        GameObject _player;

        private Vector3 posVector;
        public virtual void OnPointerDown(PointerEventData ped)
        {
            OnDrag(ped);
        }

        public virtual void OnPointerUp(PointerEventData ped)
        {
            posVector = Vector3.zero;
            _joystickImage.rectTransform.anchoredPosition = Vector3.zero;
        }

        public virtual void OnDrag(PointerEventData ped)
        {
            Vector2 pos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_bgImage.rectTransform, ped.position, ped.pressEventCamera, out pos))
            {
                pos.x = (pos.x / _bgImage.rectTransform.sizeDelta.x);
                pos.y = (pos.y / _bgImage.rectTransform.sizeDelta.y);

                // Debug.Log("Pos X" + pos.x);
                // Debug.Log("Pos y" + pos.y);
                posVector = new Vector3(pos.x * 2, 0, pos.y * 2);
                posVector = (posVector.magnitude > 1.0f) ? posVector.normalized : posVector;

                _joystickImage.rectTransform.anchoredPosition = new Vector3(posVector.x * (_bgImage.rectTransform.sizeDelta.x / 3), posVector.z * (_bgImage.rectTransform.sizeDelta.y / 3));

            }

            Vector3 mousePos = _joystickImage.transform.localPosition ;
            Debug.Log("Player pos" + _player.transform.position.x);
            Debug.Log("mouse pod " + mousePos.x +"   "+ mousePos.y);
            Debug.Log("subtraction" + (_player.transform.position.x - mousePos.x));

             _player.transform.up = new Vector2(mousePos.x- _player.transform.position.x, mousePos.y- _player.transform.position.y).normalized;
            //Debug.Log("Player pos" + mousePos);

            /*  if (Mathf.Abs(posVector.z) > Mathf.Abs(posVector.x))
              {
                  if (posVector.z > 0)
                  {
                      //PlayerNew.Instance.MoveUp();
                  }
                  else if (posVector.z < 0)
                  {
                      //PlayerNew.Instance.MoveDown();
                  }
              } */
        }
    }
}

