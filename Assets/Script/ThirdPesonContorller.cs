using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPesonContorller : MonoBehaviour
{
    Transform _playerTransform;
    Animator _animator;
    Transform _cameraTransform;
    CharacterController character;
    public LayerMask layerMask;
    #region 计时器
    public float fallTimer;
    public float fallTimeoutDelay = 5f;
    #endregion
    #region 角色位移相关
    Vector3 playerMovement = Vector3.zero;
    Vector3 averageVel = Vector3.zero;
    public float maxJumpHeight = 1.5f;
    float crouchSpeed = 1.5f;
    float walkSpeed = 2.5f;
    float runSpeed = 5.5f;
    public float gravity = -9.81f;
    float jumpVelocity;
    float groundOffset = 0.5f;
    public float minFallingDistance = 0.5f;//最小下落距离
    public float fillMultiplier = 2f;
    #endregion
    #region 角色姿态相关
    public enum EmptyHandGesture
    {
        Crouch,//下蹲状态
        Stand,//站立状态
        Falling,//下落状态
        Jumping,//跳跃状态
        Landing//落地状态
        //Clamp
    }
    [HideInInspector]
    public EmptyHandGesture emptyHandGesture = EmptyHandGesture.Stand;
    float crouchThreshould = -0.1f;
    float standThreshould = 1f;
    float durationThreshould = 2.1f;
    float landingThreshould;
    float characterCapHeight;
    float characterCapHalfHeight;
    public float JumpCD = 1.5f;
    Vector3 characterCapCenter;
    Vector3 characterCapHalfCenter;
    //Vector3 charaterTop;
    bool isNot2Stand;

    public enum LocomotionState
    {
        Idle,
        Wlak,
        Run,
        //Clamping
    }
    [HideInInspector]
    public LocomotionState locomotion = LocomotionState.Idle;
    public enum ArmState
    {
        Normal,
        Arm
    }
    [HideInInspector]
    public ArmState arm = ArmState.Normal;
    float legGesture = 0f;//跳跃腿部姿态
    [Range(0f,1f)] public float Distance2Ground;//往地上射的距离
    [Range(0f, 0.1f)] public float ShoesHeightOffset;//鞋子高度偏移量
    #endregion
    #region 输入相关
    Vector2 movementInput;
    Vector3 InputDir;
    bool isRunning;
    bool isCrouch;
    bool isAiming;
    bool isJumping;
    bool isGround;
    bool couldFalling;
    bool m_isCrouching;
    bool isClamp;
    bool m_isClamping;
    #endregion
    #region 哈希值动画ID
    int stateHash;
    int moveSpeedHash;
    int turnSpeedHash;
    int verticalVelHash;
    int legGestureHash;
    int leftFootHash;
    int rightFootHash;
    int isHightLanding;
    int fallHeight;
    #endregion
    //角色前3帧速度缓存池
    static readonly int CACHE_SIZE = 3;
    Vector3[] velCache = new Vector3[CACHE_SIZE];
    int velCacheIndex;
    private bool isLanding;

    void Start()
    {
        //初始化角色位置
        _playerTransform = transform;
        _animator =GetComponent<Animator>();
        character = GetComponent<CharacterController>();
        _cameraTransform = Camera.main.transform;
        isGround = true;
        //隐藏鼠标指针
        Cursor.lockState = CursorLockMode.Locked;
        //初始化角色碰撞器高度位置
        characterCapHeight = character.height;
        characterCapCenter = character.center;
        //charaterTop.y = characterCapHeight;
        characterCapHalfHeight = characterCapHeight / 2f;
        characterCapHalfCenter = characterCapCenter / 2f;
        //初始化计时器
        fallTimer = fallTimeoutDelay;
      
    }
    private void Awake()
    {
        //获取动画ID哈希值
        GetAnimator2Hash();
    }
    void Update()
    {
        CheekIsGround();
        if (m_isCrouching && isGround) {PreventStandingInLowHeadroom();}
        Jump();
        SwitchPlayerState();
        PlayerDirection();
        SetAnimator();
        ScaleCapsuleForCrouching();
    }
    private void OnAnimatorMove()
    {
        if (emptyHandGesture != EmptyHandGesture.Jumping && emptyHandGesture != EmptyHandGesture.Falling)
        {
            //普通状态下角色位移
            Vector3 deltaPosition = _animator.deltaPosition;
            deltaPosition.y += jumpVelocity *Time.deltaTime;
            character.Move(deltaPosition );
            //计算角色每3帧平均速率
           averageVel = AverageVel(_animator.velocity);

        }
        else
        {
            //沿用地面速度，使用地面前3帧的速度
            averageVel.y = jumpVelocity;
            Vector3 playerMovement = averageVel * Time.deltaTime;
            character.Move(playerMovement);
        }

    }
    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator)
        {
            RaycastHit hit;
            float footStep = Mathf.Repeat(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
            if(locomotion == LocomotionState.Run || locomotion == LocomotionState.Wlak)
            {
                /*if (footStep < 0.6f)
                {
                    _animator.SetFloat(leftFootHash, 0f,0.1f,Time.deltaTime);
                    _animator.SetFloat(rightFootHash, 1f, 0.1f, Time.deltaTime);

                }
                else
                {
                    _animator.SetFloat(leftFootHash, 1f, 0.1f, Time.deltaTime);
                    _animator.SetFloat(rightFootHash, 0f, 0.1f, Time.deltaTime);
                }*/
                _animator.SetFloat(leftFootHash, 0.01f);
                _animator.SetFloat(rightFootHash, 0.01f);
            }
            else
            {
                _animator.SetFloat(leftFootHash, 1f );
                _animator.SetFloat(rightFootHash, 1f);
            }
            float leftWeight = _animator.GetFloat(leftFootHash);
            float rightWeight = _animator.GetFloat(rightFootHash);
            #region IK权重设置相关
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot,leftWeight);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot,leftWeight);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot,rightWeight);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot,rightWeight);
            #endregion
            Ray leftRay = new Ray(_animator.GetIKPosition(AvatarIKGoal.LeftFoot) + Vector3.up, Vector3.down);
            //Debug.DrawLine(_animator.GetIKPosition(AvatarIKGoal.LeftFoot) + Vector3.up, _animator.GetIKPosition(AvatarIKGoal.LeftFoot) + Vector3.down * Distance2Ground);
            //保持射线在脚底部位置往下检测（不受鞋子高度影响脚部IK位置）
            if(Physics.Raycast(leftRay,out hit,Distance2Ground + 1f + ShoesHeightOffset,layerMask))
            {
                //LeftFoot
                if(hit.transform.tag == "walkable")
                {
                    Vector3 lfootPosition = hit.point + Vector3.up * ShoesHeightOffset;
                    
                    _animator.SetIKPosition(AvatarIKGoal.LeftFoot, lfootPosition);
                    _animator.SetIKRotation(AvatarIKGoal.LeftFoot, Quaternion.LookRotation(transform.forward, hit.normal));
                }
            }
            Ray rightRay = new Ray(_animator.GetIKPosition(AvatarIKGoal.RightFoot) + Vector3.up, Vector3.down);           
            if (Physics.Raycast(rightRay, out hit, (Distance2Ground + 1f + ShoesHeightOffset), layerMask))
            {
               // Debug.DrawLine(_animator.GetIKPosition(AvatarIKGoal.RightFoot) + Vector3.up, _animator.GetIKPosition(AvatarIKGoal.RightFoot) + Vector3.down * Distance2Ground);
                //RightFoot
                if (hit.transform.tag == "walkable")
                {
                    Vector3 rfootPosition = hit.point + Vector3.up * ShoesHeightOffset;
                    _animator.SetIKPosition(AvatarIKGoal.RightFoot, rfootPosition);
                    _animator.SetIKRotation(AvatarIKGoal.RightFoot, Quaternion.LookRotation(transform.forward, hit.normal));
                }
            }
        }
    }
    #region 输入相关
    public void GetMoveInput(InputAction.CallbackContext ctx)
    {
        movementInput = ctx.ReadValue<Vector2>();
    }
    public void GetRunInput(InputAction.CallbackContext ctx)
    {
        isRunning = ctx.ReadValueAsButton();
    }
    public void GetCrouchInput(InputAction.CallbackContext ctx)
    {
        isCrouch = ctx.ReadValueAsButton();
    }
    public void GetAimInput(InputAction.CallbackContext ctx)
    {
        isAiming = ctx.ReadValueAsButton();
    }
    public void GetJumpInput(InputAction.CallbackContext ctx)
    {
        isJumping = ctx.ReadValueAsButton();
    }
    public void GetClamping(InputAction.CallbackContext ctx)
    {
        isClamp = ctx.ReadValueAsButton();
    }
    #endregion
    /// <summary>
    /// 计算起跳前的前三帧的平均速度
    /// </summary>
    /// <param name="newVel">每帧速度</param>
    /// <returns>前三平均速率</returns>
    Vector3 AverageVel(Vector3 newVel)
    {
        velCache[velCacheIndex] = newVel;
        velCacheIndex++;
        velCacheIndex %= CACHE_SIZE;
        Vector3 average = Vector3.zero;
        foreach (Vector3 vel in velCache)
        {
            average += vel;
        }
        
        return average /= CACHE_SIZE;
    }
        

   //角色下蹲时角色碰撞器高度和轴心减半反之恢复
    private void ScaleCapsuleForCrouching() 
    {
        
        if (isCrouch && isGround || isNot2Stand)
        {
          
                m_isCrouching = true;
                character.height = characterCapHalfHeight;
                character.center = characterCapHalfCenter;
                     
        }
        else
        {
            m_isCrouching = false;
            character.height = characterCapHeight;
            character.center = characterCapCenter;
        }

    }
    //阻止角色在蹲下时不能站立（头顶有障碍物或其他时）的情况下站立
    private void PreventStandingInLowHeadroom()
    {   //射线起点
        Ray ray = new Ray(transform.position + Vector3.up * character.radius * 0.5f, Vector3.up); 
        //射线最大长度
        float crouchRay = characterCapHeight - character.radius * 0.5f;
#if UNITY_EDITOR
        Debug.DrawLine(transform.position + Vector3.up * character.radius * 0.5f, transform.position + (Vector3.up * crouchRay));
#endif
        //投射一个球体检测玩家角色站立高度上空是否有障碍物阻挡
        if (Physics.SphereCast(ray, character.radius * 0.5f, crouchRay, layerMask, QueryTriggerInteraction.Ignore))
            {
                m_isCrouching = true;
                isNot2Stand = true;
            }
            else
            {
                isNot2Stand = false;
            }
        
    }
    //将动画变量转化成Hash值使用
    private void GetAnimator2Hash()
    {
        stateHash = Animator.StringToHash("玩家姿态");
        moveSpeedHash = Animator.StringToHash("移动速度");
        turnSpeedHash = Animator.StringToHash("转弯速度");
        verticalVelHash = Animator.StringToHash("垂直速度");
        legGestureHash = Animator.StringToHash("左右脚");
        leftFootHash = Animator.StringToHash("左脚IK权重");
        rightFootHash = Animator.StringToHash("右脚IK权重");
        isHightLanding = Animator.StringToHash("是否高处落地");
        fallHeight = Animator.StringToHash("落地高度");
    }
    //控制玩家状态
    void SwitchPlayerState()
    {
        if (!isGround)
        {
            if(jumpVelocity > 0)
            {
                emptyHandGesture = EmptyHandGesture.Jumping;
            }
            else if(emptyHandGesture != EmptyHandGesture.Jumping)
            {
                if (couldFalling)
                {
                    emptyHandGesture = EmptyHandGesture.Falling;               
                }
            }
        }else if (isLanding )
        {
            emptyHandGesture = EmptyHandGesture.Landing;
        }
        else
        {
            //!isNot2Stand:
            //当角色处于空间较小不能站立状态时将角色默认状态替换为蹲伏
            if (m_isCrouching || isNot2Stand)
            {
                emptyHandGesture = EmptyHandGesture.Crouch;
            }
            else 
            {
                if (!isNot2Stand)
                {
                    emptyHandGesture = EmptyHandGesture.Stand;
                }
            }
            if (movementInput.magnitude == 0f)
            {
                locomotion = LocomotionState.Idle;
            }
            else if (!isRunning)
            {
                locomotion = LocomotionState.Wlak;
            }
            else
            {
                locomotion = LocomotionState.Run;
            }
            if (isAiming)
            {
                arm = ArmState.Arm;
            }
            else
            {
                arm = ArmState.Normal;
            }
        }       
    }
    //获取玩家实际移动方向
    void PlayerDirection()
    {
        if (!m_isClamping)
        {
            Vector3 camForwardProjection = new Vector3(_cameraTransform.forward.x, 0f, _cameraTransform.forward.z).normalized;
            playerMovement = camForwardProjection * movementInput.y + _cameraTransform.right * movementInput.x;
            playerMovement = _playerTransform.InverseTransformVector(playerMovement);
        }else
        {

        }
    }
    void CheekIsGround() 
    {
        if (Physics.SphereCast(transform.position + (Vector3.up * groundOffset), character.radius, Vector3.down,out RaycastHit hit,
            groundOffset - character.radius + 2 * character.skinWidth))
        {
            isGround = true;
            jumpVelocity = gravity * Time.deltaTime;
            
            if(fallTimer < -0.6f)
            {
                landingThreshould = -4f;
                isLanding = true;
            }else if (fallTimer < -0.4f)
            {
                landingThreshould = -2f;
                isLanding = true;
            }else
            {
                isLanding = false;
            }
            fallTimer = fallTimeoutDelay;
        }
        else
        {
            isGround = false;
            couldFalling = !Physics.Raycast(transform.position, Vector3.down, minFallingDistance);

            if(jumpVelocity <= 0f )
            {
                jumpVelocity += gravity * fillMultiplier * Time.deltaTime;
                fallTimer -= Time.deltaTime;
               
            }
            else
            {
                jumpVelocity += gravity * Time.deltaTime;
            }
        }
    }
    void Jump()
    {
        if(isGround && isJumping && !isNot2Stand)
        {
            jumpVelocity = Mathf.Sqrt(-2 * gravity * maxJumpHeight);
            legGesture = Mathf.Repeat(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
            legGesture = legGesture < 0.5f ? 1f : -1f;
            if(locomotion == LocomotionState.Run)
            {
                legGesture *= 3;
            }else if(locomotion == LocomotionState.Wlak)
            {
                legGesture *= 2;
            }
            else
            {
                legGesture = 0f;
            }
        }       
    }
  /*  void Clamping()
    {
        if (Physics.Raycast(charaterTop, Vector3.forward, out RaycastHit hit, 0.01f))
        {
            if (hit.collider.tag == "Clamping" && isClamp)
            {
                m_isClamping = true;
            }
        }
    }*/
   
    //设置动画状态机
    void SetAnimator()
    {
        if(emptyHandGesture == EmptyHandGesture.Stand)
        {
            _animator.SetFloat(stateHash, standThreshould,0.1f,Time.deltaTime);
            switch (locomotion)
            {
                case LocomotionState.Idle:
                    _animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                    break;
                case LocomotionState.Wlak:
                    _animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkSpeed, 0.1f, Time.deltaTime);
                    break;
                case LocomotionState.Run:
                    _animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                    break;
            }
        }else if(emptyHandGesture == EmptyHandGesture.Crouch)
        {
            _animator.SetFloat(stateHash, crouchThreshould, 0.1f, Time.deltaTime);
            switch (locomotion)
            {
                case LocomotionState.Idle:
                    _animator.SetFloat(moveSpeedHash, 0f, 0.1f, Time.deltaTime);
                    break;
                default:
                    _animator.SetFloat(moveSpeedHash, crouchSpeed, 0.1f, Time.deltaTime);
                    break;

            }
        }else if(emptyHandGesture == EmptyHandGesture.Jumping || emptyHandGesture == EmptyHandGesture.Falling)
        {   
            _animator.SetFloat(stateHash, durationThreshould);
            _animator.SetFloat(verticalVelHash, jumpVelocity,0.1f,Time.deltaTime);
            _animator.SetFloat(legGestureHash, legGesture, 0.03f, Time.deltaTime);
        }
        else if(emptyHandGesture == EmptyHandGesture.Landing)
        {
            _animator.SetTrigger(isHightLanding);
            _animator.SetFloat(fallHeight,landingThreshould);
        }
        if(arm == ArmState.Normal)
        {
            float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);
            _animator.SetFloat(turnSpeedHash, rad, 0.1f, Time.deltaTime);
            if (emptyHandGesture != EmptyHandGesture.Jumping && emptyHandGesture != EmptyHandGesture.Falling)
            { 
                _playerTransform.Rotate(0f, rad * 180 * Time.deltaTime, 0f);
            }
            else
            {
                _playerTransform.Rotate(0f, rad * 60 * Time.deltaTime, 0f);
            }
        }

    } 
}
