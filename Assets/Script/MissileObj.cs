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
        if (Player.Instance == null) return;

        int levelIndex = PlayerPrefs.GetInt("LevelIndex", 0);
        if (levelIndex == 0 && Player.Instance.RoateRange.Count > 0 && Player.Instance.SpeedRange.Count > 0)
        {
            rotateSpeed = Random.Range(Player.Instance.RoateRange[0].x, Player.Instance.RoateRange[0].y);
            speed = Random.Range(Player.Instance.SpeedRange[0].x, Player.Instance.SpeedRange[0].y);
        }
        else if (levelIndex == 1 && Player.Instance.RoateRange.Count > 1 && Player.Instance.SpeedRange.Count > 1)
        {
            rotateSpeed = Random.Range(Player.Instance.RoateRange[1].x, Player.Instance.RoateRange[1].y);
            speed = Random.Range(Player.Instance.SpeedRange[1].x, Player.Instance.SpeedRange[1].y);
        }

        // Layer the difficulty ramp on top of the level roll. Tier 0 is
        // the original speed/rotate; tier 9 caps at 1.72x speed and
        // 1.45x turn rate. Baked in at spawn time so mid-flight the
        // missile keeps the stats it was born with -- ramping a live
        // missile mid-chase would feel unfair.
        var diff = DifficultyDirector.Instance;
        if (diff != null)
        {
            speed       *= diff.MissileSpeedMult;
            rotateSpeed *= diff.MissileRotateMult;
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
        if (Player.Instance == null || _rb == null) return;

        if (!stopMove)
        {
            Vector2 _direction = (Vector2)Player.Instance.transform.position - (Vector2)this.transform.position;
            _direction.Normalize();

            float rotateAmount = Vector3.Cross(_direction, this.transform.up).z;
            _rb.angularVelocity = -rotateAmount * rotateSpeed;
            _rb.linearVelocity = this.transform.up * speed;
        }
        if (extraMissile)
        {
            Vector2 _direction = (Vector2)Player.Instance.transform.position - (Vector2)this.transform.position;
            _direction.Normalize();

            float rotateAmount = Vector3.Cross(_direction, this.transform.up).z;
            _rb.angularVelocity = -rotateAmount * rotateSpeed * 0.8f;
            _rb.linearVelocity = this.transform.up * speed * 2;
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
