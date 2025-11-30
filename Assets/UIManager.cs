using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

namespace Amar
{
    public class UIManager : MonoBehaviour
    {

        #region Public_Variables
        public Transform _planespos;
        public Button _playButton;
        public Button _getPlane;
        #endregion

        #region Private_Variables
        [SerializeField]
        List<Text> _amount;
        private int[] _planes;
        #endregion

        #region Events
        #endregion

        #region Unity_CallBacks
        void Awake() { }
        void OnEnable() { }
        // Use this for initialization
        void Start()
        {
            _planes = new int[7];

            for (int i = 0; i < 7; i++)
            {
                _planes[i] = i;
            }
        }
        // Update is called once per frame
        void Update()
        {

        }
        void OnDisable() { }
        #endregion

        #region Private_Methods
        #endregion

        #region Public_Methods
        #endregion

        #region UI_Calls
        #endregion

        #region Coroutines
        #endregion

        #region Custom_CallBacks
        #endregion
    }
}