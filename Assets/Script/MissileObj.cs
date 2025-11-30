using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissileObj : MonoBehaviour
{
    #region Private_Variable
    private Rigidbody2D _rb;
    private float rotateSpeed;
    private float speed;
    private bool stopMove;
    [SerializeField]
    GameObject _animator;
    [SerializeField]
    AudioClip _audioClip;
    #endregion

    #region Public_Variable
    public GameObject _Player;
    public bool extraMissile;
    #endregion

    #region unity_CAll

    void OnEnable()
    {
        Debug.Log("Missile generate");
        if (PlayerPrefs.GetInt("LevelIndex") == 0)
        {
            rotateSpeed = Random.Range(Player.Instance.RoateRange[0].x, Player.Instance.RoateRange[0].y);
            speed = Random.Range(Player.Instance.SpeedRange[0].x, Player.Instance.SpeedRange[0].y);
        }

        if (PlayerPrefs.GetInt("LevelIndex") == 1)
        {
            rotateSpeed = Random.Range(Player.Instance.RoateRange[1].x, Player.Instance.RoateRange[1].y);
            speed = Random.Range(Player.Instance.SpeedRange[1].x, Player.Instance.SpeedRange[1].y);
        }

    }
    // Use this for initialization
    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        StartCoroutine(DestroyMissile());
    }

    // Update is called once per frame
    void Update()
    {
    }

    void LateUpdate()
    {
        //CreatObj();

    }

    void FixedUpdate()
    {
        CreatObj();
    }

    public void OnDisable()
    {
        Destroy(gameObject);
    }

    private void CreatObj()
    {
        if (!stopMove)
        {
            Vector2 _direction = (Vector2)Player.Instance.transform.position - (Vector2)this.transform.position;

            _direction.Normalize();

            float rotateAmount = Vector3.Cross(_direction, this.transform.up).z;

            _rb.angularVelocity = -rotateAmount * rotateSpeed;

            _rb.velocity = this.transform.up * speed;
        }
        if (extraMissile)
        {
            Vector2 _direction = (Vector2)Player.Instance.transform.position - (Vector2)this.transform.position;

            _direction.Normalize();

            float rotateAmount = Vector3.Cross(_direction, this.transform.up).z;

            _rb.angularVelocity = -rotateAmount * rotateSpeed * 0.8f;

            _rb.velocity = this.transform.up * speed * 2;
        }

    }

    #endregion

    #region Private_Method

    IEnumerator DestroyMissile()
    {
        yield return new WaitForSeconds(15.0f);
        StartCoroutine(AfterDisable());
    }

    IEnumerator AfterDisable()
    {
        this.gameObject.GetComponent<AudioSource>().PlayOneShot(_audioClip);
        stopMove = true;
        this.gameObject.GetComponent<SpriteRenderer>().enabled = false;
        _animator.SetActive(true);
        yield return new WaitForSeconds(1.0f);
        Destroy(gameObject);
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        StartCoroutine(AfterDisable());
    }

    #endregion
}
