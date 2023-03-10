using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;
using UnityEngine.UI;

public class FireMan : MonoBehaviour
{
    [SerializeField] private Tilemap MovingTileMap; 
    
    // Game manager
    private Transform _t;
    private Rigidbody2D _rb;

    // movement
    [SerializeField] private float movingSpeed;
    [SerializeField] private bool moveByGrid = true;
    [SerializeField] private bool fourDirection = true;
    private Vector2 _moveDirection;
    private Vector2 _lookAtDirection;

    // grid movement
    [SerializeField] private float gridMoveDuration;
    [SerializeField] private int numGridMove = 2;
    private Vector3Int _currentGridPos; // current position of the object in grid cells
    private float _gridMoveTimer; // timer for moving from one grid cell to the next

    // shooting fire
    private float _fireKeyHoldingTime;
    private bool _fireKeyDown;
    private float _cooldownToMolotov;
    private bool _burningBuildingAnimationStarted;
    private Vector2 _throwDirection;

    // controls changing
    private const KeyCode Fire = KeyCode.T;
    private const KeyCode Up = KeyCode.W, Down = KeyCode.S, Left = KeyCode.A, Right = KeyCode.D;

    private RaycastHit2D _hit;

    //**Animation**
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;

    // ** unHide the fireman when he throw a molotov **
    [SerializeField] private bool unHideWhenFire;
    private Hideable _hideable;

    //**don't move to these objects
    [SerializeField] private LayerMask _forbiddenLayers;
    
    // *** fireman pushed
    private Vector3 _waterPushedDirection;
    public int gridToPush = 2;
    private bool _pushedByExtinguisher;
    private bool _doneExtinguisherPush;
    
    // ** cooldown bar
    [SerializeField] private Slider cooldownBar;

    // dumb
    private bool reachedNextPos;
    private bool currentlyJumping;
    private Vector3 nextPos;
    
    // Start is called before the first frame update
    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _t = GetComponent<Transform>();
        _lookAtDirection = Vector2.zero;
        _throwDirection = Vector2.zero;
        _currentGridPos = MovingTileMap.WorldToCell(_t.position);
        // print(MovingTileMap.CellToWorld(_currentGridPos));
        _animator = GetComponent<Animator>();
        // _animator.speed = 0.5f;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        // _spriteRenderer.flipX = !_spriteRenderer.flipX;
        _hideable = GetComponent<Hideable>();
        _waterPushedDirection = Vector3.zero;
        cooldownBar.maxValue = GameManager.MolotovCooldownTime;
        cooldownBar.value = GameManager.MolotovCooldownTime;
    }

    private void Update()
    {
        // don't move when the game is not started yet!!!
        if (!GameManager.IsGameRunning)
            return;

        // *** Movement ***
        var xDirection = Input.GetAxisRaw("Horizontal2");
        var yDirection = Input.GetAxisRaw("Vertical2");
        _moveDirection.x = xDirection;
        _moveDirection.y = yDirection;

        if (!moveByGrid)
            SnappingMovement();
        else
            GridMovement2();

        // *** shooting ability ***
        if (Input.GetKeyDown(Fire))
        {
            _fireKeyDown = true;
        }
        else if (Input.GetKeyUp(Fire))
        {
            if (_fireKeyHoldingTime < 0.2f && _cooldownToMolotov <= 0f)
            {
                // throwing a bomb of fire in the direction the player is looking at. (unless its a building then nothing)
                _cooldownToMolotov = GameManager.MolotovCooldownTime;
                if (unHideWhenFire && !_spriteRenderer.enabled)
                    _hideable.ShowOrHide();
                StartCoroutine(ThrowMolotov());
            }

            _fireKeyHoldingTime = 0f;
            _fireKeyDown = false;
        }

        if (_fireKeyDown)
        {
            _fireKeyHoldingTime += Time.deltaTime;
            if (_fireKeyHoldingTime >= 1f && !_burningBuildingAnimationStarted)
            {
                // start burning building animation 
                _burningBuildingAnimationStarted = true;
            }

            if (_fireKeyHoldingTime >= 5f)
            {
                // the torch is thrown in the -building- and it will start to burn - stop animation also
                print("FIIRREEE BRRRRR");
                _fireKeyDown = false;
            }
        }

        _cooldownToMolotov = Mathf.Max(_cooldownToMolotov - Time.deltaTime, 0f);
        cooldownBar.value = GameManager.MolotovCooldownTime - _cooldownToMolotov;
    }


    private void SnappingMovement()
    {
        var snapping = fourDirection ? 90.0f : 45.0f;
        if (_moveDirection.sqrMagnitude > 0)
        {
            var angle = Mathf.Atan2(_moveDirection.y, _moveDirection.x) * Mathf.Rad2Deg;
            angle = Mathf.Round(angle / snapping) * snapping;
            // _t.rotation = Quaternion.AngleAxis(90 + angle, Vector3.forward);
            _moveDirection = Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.right;
            _lookAtDirection = _moveDirection;
        }
    }

    private void GridMovement()
    {
        if (gridToPush == 0)
        {
            _waterPushedDirection = Vector3.zero;
            gridToPush = 2;
        }
        if (!_waterPushedDirection.Equals(Vector3.zero))
            gridToPush--;
        
        // Check input and move in the corresponding direction
         if ((Input.GetKeyDown(Up) && _moveDirection.x.Equals(0)) || _waterPushedDirection.Equals(Vector3.up))
        {
            _t.position = _currentGridPos;
            _lookAtDirection = Vector3.up;
            _throwDirection = Vector2.up;

            _hit = Physics2D.CircleCast(_t.position, 0.75f, _lookAtDirection, 1.5f, layerMask: _forbiddenLayers);
            if (!_hit)
            {
                _currentGridPos.y += numGridMove;
                StartCoroutine(LerpRigidbody(_t.position, _currentGridPos, Time.time));
            }
        }
        else if ((Input.GetKeyDown(Down) && _moveDirection.x.Equals(0)) || _waterPushedDirection.Equals(Vector3.down))
        {
            _t.position = _currentGridPos;
            _lookAtDirection = Vector3.down;
            _throwDirection = Vector2.down;
            
            _hit = Physics2D.CircleCast(_t.position, 0.75f, _lookAtDirection, 1.5f, layerMask: _forbiddenLayers);
            if (!_hit)
            {
                _currentGridPos.y -= numGridMove;
                StartCoroutine(LerpRigidbody(_t.position, _currentGridPos, Time.time));
            }
        }
        else if ((Input.GetKeyDown(Right) && _moveDirection.y.Equals(0)) || _waterPushedDirection.Equals(Vector3.right))
        {
            _t.position = _currentGridPos;
            _lookAtDirection = Vector3.right;
            _throwDirection = Vector2.right;
            _hit = Physics2D.CircleCast(_t.position, 0.75f, _lookAtDirection, 1.5f, layerMask: _forbiddenLayers);
            if (_spriteRenderer.flipX)
                _spriteRenderer.flipX = false;
            if (!_hit)
            {
                _currentGridPos.x += numGridMove;
                StartCoroutine(LerpRigidbody(_t.position, _currentGridPos, Time.time));
            }
        }
        else if ((Input.GetKeyDown(Left) && _moveDirection.y.Equals(0)) || _waterPushedDirection.Equals(Vector3.left))
        {
            _t.position = _currentGridPos;
            _lookAtDirection = Vector3.left;
            _throwDirection = Vector2.left;
            _hit = Physics2D.CircleCast(_t.position, 0.75f, _lookAtDirection, 1.5f, layerMask: _forbiddenLayers);
            if (!_spriteRenderer.flipX)
                _spriteRenderer.flipX = true;
            if (!_hit)
            {
                _currentGridPos.x -= numGridMove;
                StartCoroutine(LerpRigidbody(_t.position, _currentGridPos, Time.time));
            }
        }
        else 
        {
            _rb.velocity = Vector2.zero;
            _lookAtDirection = Vector3.zero;
            if (_pushedByExtinguisher)
            {
                _currentGridPos = MovingTileMap.WorldToCell(_t.position);
                _pushedByExtinguisher = false;
            }
        }

        if (!_waterPushedDirection.Equals(Vector3.zero)) return;

        _animator.SetInteger("XSpeed", (int)_lookAtDirection.x);
        _animator.SetInteger("YSpeed", (int)_lookAtDirection.y);
    }
    private void GridMovement2()
    {
        // if (!reachedNextPos && nextPos.Equals(MovingTileMap.CellToWorld(_currentGridPos)))
        // {
        //     reachedNextPos = true;
        // }
        
        if (gridToPush == 0)
        {
            _waterPushedDirection = Vector3.zero;
            gridToPush = 2;
        }
        if (!_waterPushedDirection.Equals(Vector3.zero))
            gridToPush--;
        
        // Check input and move in the corresponding direction

        if (Input.GetKeyDown(Up) || _waterPushedDirection.Equals(Vector3.up))
        {
            if (!currentlyJumping)
                CheckMovement(Vector3.up, Vector2.up, false);
        }
        else if (Input.GetKeyDown(Down) || _waterPushedDirection.Equals(Vector3.down))
        {
            if (!currentlyJumping)
                CheckMovement(Vector3.down, Vector2.down, false);
        }
        else if (Input.GetKeyDown(Right) || _waterPushedDirection.Equals(Vector3.right))
        {
            if (!currentlyJumping)
                CheckMovement(Vector3.right, Vector2.right, false);
        }
        else if (Input.GetKeyDown(Left) || _waterPushedDirection.Equals(Vector3.left))
        {
            if (!currentlyJumping)
                CheckMovement(Vector3.left, Vector2.left, true);
        }
        else
        {
            if (!currentlyJumping)
            {
                // var curPos = _t.position;
                // var temp = Vector3Int.CeilToInt(curPos);
                // if ((Math.Abs((int)temp.x) % 2 != 0 || Math.Abs((int)temp.y) % 2 != 0) && _doneExtinguisherPush)
                // {
                //     var newX = Math.Abs((int)temp.x) % 2 != 0 ? Mathf.FloorToInt(curPos.x) : curPos.x;
                //     var newY = Math.Abs((int)temp.y) % 2 != 0 ? Mathf.FloorToInt(curPos.y) : curPos.y;
                //     _t.position = new Vector3(newX, newY, 0);
                // }
                
                var _ourPos = MovingTileMap.WorldToCell(_t.position);
                if (!_ourPos.Equals(_currentGridPos))
                {
                    _currentGridPos = _ourPos;
                }
            }

            _rb.velocity = Vector2.zero;
            _lookAtDirection = Vector3.zero;

        }
        
        if (!_waterPushedDirection.Equals(Vector3.zero)) return;

        _animator.SetInteger("XSpeed", (int)_lookAtDirection.x);
        _animator.SetInteger("YSpeed", (int)_lookAtDirection.y);
    }
    
    void CheckMovement(Vector3 direction, Vector2 throwDirection, bool flipX)
    {
        if (currentlyJumping)
        {
            _currentGridPos = MovingTileMap.WorldToCell(nextPos);
        }
        // _t.position = MovingTileMap.CellToWorld(_currentGridPos);
        
        
        _lookAtDirection = direction;
        _throwDirection = throwDirection;

        var distance = 1.5f;
        var radius = 0.75f;

        _hit = Physics2D.CircleCast(_t.position, radius, _lookAtDirection, distance, layerMask: _forbiddenLayers);

        if (_spriteRenderer.flipX != flipX)
            _spriteRenderer.flipX = flipX;

        if (!_hit)
        {
            // print(_hit.collider);
            _currentGridPos += Vector3Int.RoundToInt(direction);
            // print(_currentGridPos);
            nextPos = MovingTileMap.CellToWorld(_currentGridPos);
            // print(nextPos);
            currentlyJumping = true;
            var curPos = _t.position;
            StartCoroutine(LerpRigidbody(curPos, Vector3Int.RoundToInt(curPos) + 2 * Vector3Int.RoundToInt(direction), Time.time));
        }
        
    }


    IEnumerator LerpRigidbody(Vector3 startPos, Vector3 endPos, float timeStartedLerping)
    {
        while (true)
        {
            var timeSinceStarted = Time.time - timeStartedLerping;
            var percentageComplete = timeSinceStarted / gridMoveDuration;
 
            _rb.MovePosition(Vector3.Lerp(startPos, endPos, percentageComplete));
 
            if (percentageComplete >= 1f)
            {
                currentlyJumping = false;
                break;
            }
 
            yield return new WaitForFixedUpdate();
        }
    }

    private IEnumerator ThrowMolotov()
    {
        var molotov = GameManager.Instance.MolotovPool.Get();
        yield return molotov.Shoot(_t.position, _throwDirection is { x: 0, y: 0 } ? Vector3.right : _throwDirection);
        // when we finish with the bomb, 
        if (!GameManager.Instance.GetMolotovSound().isPlaying)
            GameManager.Instance.GetMolotovSound().Play();
        var molotovDropPos = molotov.GetMolotovDropPos();
        GameManager.Instance.MolotovPool.Release(molotov);
        StartCoroutine(StartFire(molotovDropPos));
    }

    private IEnumerator StartFire(Vector3 molotovDropPos)
    {
        var molotovFire = GameManager.Instance.FireMolotovPool.Get();
        molotovFire.Burn(molotovDropPos);

        // todo this need to be more general (THIS IS WAY WRONG)
        Vector3Int gridPosition = GameManager.Instance.WaterFireTilemap.WorldToCell(molotovDropPos);
        // BoundsInt bounds = new BoundsInt(gridPosition, new Vector3Int(2, 2, 1));
        BoundsInt bounds = new BoundsInt(gridPosition - new Vector3Int(1, 1, 0), new Vector3Int(3, 3, 1));
        var tiles = GameManager.Instance.WaterFireTilemap.GetTilesBlock(bounds);
        int i = 0;
        foreach (var tile in tiles)
        {
            if (!tile.IsUnityNull())
            {
                // then it's water tile, so delete it
                int x = i % bounds.size.x;
                int y = i / bounds.size.x;
                Vector3Int tilePosition = new Vector3Int(x + bounds.x, y + bounds.y, bounds.z);
                GameManager.Instance.WaterFireTilemap.SetTile(tilePosition, null);
            }

            i++;
        }

        yield break;
    }

    public void ChangePushDirection(Vector3 other)
    {
        _waterPushedDirection = other;
    }

    public Hideable GetHideable()
    {
        return _hideable;
    }
    
    // Update is called once per frame
    private void FixedUpdate()
    {
        if (!moveByGrid)
            _rb.MovePosition(_rb.position + _moveDirection * (movingSpeed * Time.fixedDeltaTime));
    }

    public void SetPushed(bool other)
    {
        _pushedByExtinguisher = other;
    }

    public void SetDonePush(bool other)
    {
        _doneExtinguisherPush = other;
    }
}