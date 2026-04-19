using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    #region Public_VAriable
    public static Player Instance { get; set; }
    public GameObject right;
    public GameObject left;
    public float RotationSpeed ;
    public float speed ;
    public GameObject GameOver;
    public GameObject ExtraObj;
    public GameObject GamePanel;
    public Text levelString;
    public List<Vector2> RoateRange;
    public List<Vector2> SpeedRange;
    #endregion

    #region Private_Variable
    [SerializeField]
    GameObject _animationComp;
    private float _rotationX;
    Rigidbody2D _rb;
    private bool _protect;
    private bool _isDead = false;
    private int LevelIndex = 0;
    [SerializeField]
    private GameObject VariousPlane;
    // Consumed on first missile hit when the active plane has a shield
    // perk. Reset every OnEnable via ApplyPlanePerks.
    private bool _shieldAvailable;
    #endregion

    #region Unity_Callback

    public void OnEnable()
    {
        _isDead = false;  // Reset death state when player is enabled
        VariousPlane.SetActive(true);

        _animationComp.SetActive(false);
        if (PlayerPrefs.HasKey("LevelIndex"))
        {
            LevelIndex = PlayerPrefs.GetInt("LevelIndex");
        }
        else
        {
            PlayerPrefs.SetInt("LevelIndex", LevelIndex);
        }
        //this.transform.position = Vector3.zero;
        //this.transform.localRotation = Quaternion.EulerAngles(Vector3.zero);
        LevelSpeed();
        ApplyPlanePerks();
    }

    // Pull the current plane's perks (turn multiplier, magnet radius,
    // shield flag) from PlaneStats and apply them on top of the level's
    // base stats. Called every OnEnable so switching planes between runs
    // takes effect immediately.
    private void ApplyPlanePerks()
    {
        int planeId = PlayerPrefs.GetInt("PlaneID", 0);
        var perks = PlaneStats.ForId(planeId);

        // Scale the rotation speed the level just set. Multiplicative so
        // Normal and High levels both benefit proportionally.
        RotationSpeed *= perks.TurnMult;

        // Shield: one free hit. Consumed in OnCollisionEnter2D below.
        _shieldAvailable = perks.HasShield;

        // Magnet: ensure we have a CoinMagnet on the player, sized to
        // this plane's radius. Radius 0 disables it (remove component).
        var magnet = GetComponent<CoinMagnet>();
        if (perks.MagnetRadius > 0f)
        {
            if (magnet == null) magnet = gameObject.AddComponent<CoinMagnet>();
            magnet.Radius = perks.MagnetRadius;
            magnet.enabled = true;
        }
        else if (magnet != null)
        {
            magnet.enabled = false;
        }
    }
    void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
    }

    // Use this for initialization
    void Start()
    {
        // Disable plane sound - will be edited later
        AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
        foreach (AudioSource audio in audioSources)
        {
            audio.enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {

       // LevelSpeed();

       // StickMovement();
    }

    public void ResetPos()
    {
        
    }

    public void FixedUpdate()
    {
        //  _rb.velocity = transform.up * speed;

        transform.Translate(Vector3.up * speed * Time.deltaTime, Space.Self);
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            transform.RotateAround(left.transform.position, Vector3.forward, RotationSpeed );
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            transform.RotateAround(right.transform.position, Vector3.back, RotationSpeed );
        }
    }

    public void OnDisable()
    {
        GameManager.Instance.OnplayerDie();
    }
    #endregion


    #region Private_Method
    private void StickMovement()
    {
        _rotationX = CrossPlatformInputManager.GetAxis("Horizontal");
        transform.Rotate(0, 0, _rotationX * RotationSpeed);
    }

    private void LevelSpeed()
    {
        if(LevelIndex == 0)
        {
            Debug.Log("normal level");
            levelString.text = "Normal";
            speed = 6;
            RotationSpeed = 7;
        }
        if(LevelIndex == 1)
        {
            Debug.Log("High Level");
            levelString.text = "High";
            speed = 8;
            RotationSpeed = 7;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("ExtraObj"))
        {
            if (!_protect && !_isDead)
            {
                // B52 shield: absorb the first missile hit per run.
                // Destroy the missile (so it doesn't keep pushing us
                // and re-trigger) and consume the shield. Player keeps
                // flying; next hit is lethal.
                if (_shieldAvailable)
                {
                    _shieldAvailable = false;
                    Destroy(collision.gameObject);
                    return;
                }
                StartCoroutine(OnPlayerDestroy());
            }
        }
        else if (collision.gameObject.CompareTag("ExtraObj"))
        {
            // Don't collect coins if player is dead
            if (_isDead) return;

            collision.gameObject.SetActive(false);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ExtraIns();
            }
        }
    }
    #endregion

    #region Public_MEthod
    public void Arrowleft()
    {
        transform.RotateAround(left.transform.position, Vector3.forward, RotationSpeed);
    }
    public void Arrowright()
    {
        transform.RotateAround(right.transform.position, Vector3.back, RotationSpeed);
    }

    public void LevelValue()
    {
        Debug.Log("Level Value");
        if (LevelIndex < 1)
        {
            Debug.Log("Level Value One");
            LevelIndex++;
            PlayerPrefs.SetInt("LevelIndex", LevelIndex);
            LevelSpeed();
        }
        else
        {
            Debug.Log("Level Value Zero");
            LevelIndex = 0;
            PlayerPrefs.SetInt("LevelIndex", LevelIndex);
            LevelSpeed();
        }
    }
    #endregion

    #region Coroutine
    IEnumerator OnPlayerDestroy()
    {
        _isDead = true;  // Mark player as dead immediately
        VariousPlane.SetActive(false);
        _animationComp.SetActive(true);
        yield return new WaitForSeconds(2.0f);
        gameObject.SetActive(false);
    }
    #endregion


}
