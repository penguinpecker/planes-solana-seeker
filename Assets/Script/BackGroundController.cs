using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackGroundController : MonoBehaviour
{

    #region Public_Variable
    public List<GameObject> Background;
    public GameObject player ;

    #endregion

    #region Unity_CallBack
    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if(player.transform.position.x > Background[2].transform.position.x && Background[0].transform.position.x < Background[2].transform.position.x)
        {
            Background[0].transform.position  = new Vector3(Background[2].transform.position.x + 18,Background[2].transform.position.y , 0);
            Background[3].transform.position = new Vector3(Background[5].transform.position.x + 18, Background[3].transform.position.y, 0);
            Background[6].transform.position = new Vector3(Background[8].transform.position.x + 18, Background[6].transform.position.y, 0);
           // ExtraObj.Instance.ObjectGenerator();
        }
        if (player.transform.position.x > Background[0].transform.position.x && Background[1].transform.position.x < Background[0].transform.position.x)
        {
            Background[1].transform.position = new Vector3(Background[0].transform.position.x + 18, Background[2].transform.position.y, 0);
            Background[4].transform.position = new Vector3(Background[3].transform.position.x + 18, Background[3].transform.position.y, 0);
            Background[7].transform.position = new Vector3(Background[6].transform.position.x + 18, Background[6].transform.position.y, 0);
           // ExtraObj.Instance.ObjectGenerator();

        }
        if (player.transform.position.x > Background[1].transform.position.x && Background[2].transform.position.x < Background[1].transform.position.x)
        {
            Background[2].transform.position = new Vector3(Background[1].transform.position.x + 18, Background[2].transform.position.y, 0);
            Background[5].transform.position = new Vector3(Background[4].transform.position.x + 18, Background[3].transform.position.y, 0);
            Background[8].transform.position = new Vector3(Background[7].transform.position.x + 18, Background[6].transform.position.y, 0);
           // ExtraObj.Instance.ObjectGenerator();

        }
        if (player.transform.position.x < Background[0].transform.position.x && Background[2].transform.position.x > Background[0].transform.position.x)
        {
            Background[2].transform.position = new Vector3(Background[0].transform.position.x - 18, Background[2].transform.position.y, 0);
            Background[5].transform.position = new Vector3(Background[3].transform.position.x - 18, Background[3].transform.position.y, 0);
            Background[8].transform.position = new Vector3(Background[6].transform.position.x - 18, Background[6].transform.position.y, 0);
           // ExtraObj.Instance.ObjectGenerator();

        }
        if (player.transform.position.x < Background[2].transform.position.x && Background[1].transform.position.x > Background[2].transform.position.x)
        {
            Background[1].transform.position = new Vector3(Background[2].transform.position.x - 18, Background[2].transform.position.y, 0);
            Background[4].transform.position = new Vector3(Background[5].transform.position.x - 18, Background[3].transform.position.y, 0);
            Background[7].transform.position = new Vector3(Background[8].transform.position.x - 18, Background[6].transform.position.y, 0);
           // ExtraObj.Instance.ObjectGenerator();

        }
        if (player.transform.position.x < Background[1].transform.position.x && Background[2].transform.position.x > Background[1].transform.position.x)
        {
            Background[0].transform.position = new Vector3(Background[1].transform.position.x - 18, Background[2].transform.position.y, 0);
            Background[3].transform.position = new Vector3(Background[4].transform.position.x - 18, Background[3].transform.position.y, 0);
            Background[6].transform.position = new Vector3(Background[7].transform.position.x - 18, Background[6].transform.position.y, 0);
           // ExtraObj.Instance.ObjectGenerator();

        }

        // Background Y

        if (player.transform.position.y > Background[2].transform.position.y && Background[6].transform.position.y < Background[2].transform.position.y)
        {
            Background[6].transform.position = new Vector3(Background[0].transform.position.x, Background[0].transform.position.y + 32, 0);
            Background[7].transform.position = new Vector3(Background[1].transform.position.x, Background[1].transform.position.y + 32, 0);
            Background[8].transform.position = new Vector3(Background[2].transform.position.x, Background[2].transform.position.y + 32, 0);
           // ExtraObj.Instance.ObjectGenerator();

        }

        if (player.transform.position.y > Background[8].transform.position.y && Background[5].transform.position.y < Background[8].transform.position.y)
        {
            Background[3].transform.position = new Vector3(Background[0].transform.position.x, Background[6].transform.position.y + 32, 0);
            Background[4].transform.position = new Vector3(Background[1].transform.position.x, Background[7].transform.position.y + 32, 0);
            Background[5].transform.position = new Vector3(Background[2].transform.position.x, Background[8].transform.position.y + 32, 0);
           // ExtraObj.Instance.ObjectGenerator();

        }

        if (player.transform.position.y > Background[5].transform.position.y && Background[2].transform.position.y < Background[5].transform.position.y)
        {
            Background[0].transform.position = new Vector3(Background[0].transform.position.x, Background[3].transform.position.y + 32, 0);
            Background[1].transform.position = new Vector3(Background[1].transform.position.x, Background[4].transform.position.y + 32, 0);
            Background[2].transform.position = new Vector3(Background[2].transform.position.x, Background[5].transform.position.y + 32, 0);
           // ExtraObj.Instance.ObjectGenerator();
        }

        if (player.transform.position.y < Background[8].transform.position.y && Background[2].transform.position.y > Background[5].transform.position.y)
        {

            Background[0].transform.position = new Vector3(Background[0].transform.position.x, Background[6].transform.position.y - 32, 0);
            Background[1].transform.position = new Vector3(Background[1].transform.position.x, Background[7].transform.position.y - 32, 0);
            Background[2].transform.position = new Vector3(Background[2].transform.position.x, Background[8].transform.position.y - 32, 0);
          //  ExtraObj.Instance.ObjectGenerator();
        }

        if (player.transform.position.y < Background[2].transform.position.y && Background[5].transform.position.y > Background[2].transform.position.y)
        {

            Background[3].transform.position = new Vector3(Background[0].transform.position.x, Background[0].transform.position.y - 32, 0);
            Background[4].transform.position = new Vector3(Background[1].transform.position.x, Background[1].transform.position.y - 32, 0);
            Background[5].transform.position = new Vector3(Background[2].transform.position.x, Background[2].transform.position.y - 32, 0);
           // ExtraObj.Instance.ObjectGenerator();
        }

        if (player.transform.position.y < Background[5].transform.position.y && Background[8].transform.position.y > Background[5].transform.position.y)
        {

            Background[6].transform.position = new Vector3(Background[0].transform.position.x, Background[3].transform.position.y - 32, 0);
            Background[7].transform.position = new Vector3(Background[1].transform.position.x, Background[4].transform.position.y - 32, 0);
            Background[8].transform.position = new Vector3(Background[2].transform.position.x, Background[5].transform.position.y - 32, 0);
           // ExtraObj.Instance.ObjectGenerator();
        }
    }
    #endregion
}
