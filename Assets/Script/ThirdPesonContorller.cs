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
    #region ��ʱ��
    public float fallTimer;
    public float fallTimeoutDelay = 5f;
    #endregion
    #region ��ɫλ�����
    Vector3 playerMovement = Vector3.zero;
    Vector3 averageVel = Vector3.zero;
    public float maxJumpHeight = 1.5f;
    float crouchSpeed = 1.5f;
    float walkSpeed = 2.5f;
    float runSpeed = 5.5f;
    public float gravity = -9.81f;
    float jumpVelocity;
    float groundOffset = 0.5f;
    public float minFallingDistance = 0.5f;//��С�������
    public float fillMultiplier = 2f;
    #endregion
    #region ��ɫ��̬���
    public enum EmptyHandGesture
    {
        Crouch,//�¶�״̬
        Stand,//վ��״̬
        Falling,//����״̬
        Jumping,//��Ծ״̬
        Landing//���״̬
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
    float legGesture = 0f;//��Ծ�Ȳ���̬
    [Range(0f,1f)] public float Distance2Ground;//��������ľ���
    [Range(0f, 0.1f)] public float ShoesHeightOffset;//Ь�Ӹ߶�ƫ����
    #endregion
    #region �������
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
    #region ��ϣֵ����ID
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
    //��ɫǰ3֡�ٶȻ����
    static readonly int CACHE_SIZE = 3;
    Vector3[] velCache = new Vector3[CACHE_SIZE];
    int velCacheIndex;
    private bool isLanding;

    void Start()
    {
        //��ʼ����ɫλ��
        _playerTransform = transform;
        _animator =GetComponent<Animator>();
        character = GetComponent<CharacterController>();
        _cameraTransform = Camera.main.transform;
        isGround = true;
        //�������ָ��
        Cursor.lockState = CursorLockMode.Locked;
        //��ʼ����ɫ��ײ���߶�λ��
        characterCapHeight = character.height;
        characterCapCenter = character.center;
        //charaterTop.y = characterCapHeight;
        characterCapHalfHeight = characterCapHeight / 2f;
        characterCapHalfCenter = characterCapCenter / 2f;
        //��ʼ����ʱ��
        fallTimer = fallTimeoutDelay;
      
    }
    private void Awake()
    {
        //��ȡ����ID��ϣֵ
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
            //��ͨ״̬�½�ɫλ��
            Vector3 deltaPosition = _animator.deltaPosition;
            deltaPosition.y += jumpVelocity *Time.deltaTime;
            character.Move(deltaPosition );
            //�����ɫÿ3֡ƽ������
           averageVel = AverageVel(_animator.velocity);

        }
        else
        {
            //���õ����ٶȣ�ʹ�õ���ǰ3֡���ٶ�
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
            #region IKȨ���������
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot,leftWeight);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot,leftWeight);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot,rightWeight);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot,rightWeight);
            #endregion
            Ray leftRay = new Ray(_animator.GetIKPosition(AvatarIKGoal.LeftFoot) + Vector3.up, Vector3.down);
            //Debug.DrawLine(_animator.GetIKPosition(AvatarIKGoal.LeftFoot) + Vector3.up, _animator.GetIKPosition(AvatarIKGoal.LeftFoot) + Vector3.down * Distance2Ground);
            //���������ڽŵײ�λ�����¼�⣨����Ь�Ӹ߶�Ӱ��Ų�IKλ�ã�
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
    #region �������
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
    /// ��������ǰ��ǰ��֡��ƽ���ٶ�
    /// </summary>
    /// <param name="newVel">ÿ֡�ٶ�</param>
    /// <returns>ǰ��ƽ������</returns>
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
        

   //��ɫ�¶�ʱ��ɫ��ײ���߶Ⱥ����ļ��뷴֮�ָ�
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
    //��ֹ��ɫ�ڶ���ʱ����վ����ͷ�����ϰ��������ʱ���������վ��
    private void PreventStandingInLowHeadroom()
    {   //�������
        Ray ray = new Ray(transform.position + Vector3.up * character.radius * 0.5f, Vector3.up); 
        //������󳤶�
        float crouchRay = characterCapHeight - character.radius * 0.5f;
#if UNITY_EDITOR
        Debug.DrawLine(transform.position + Vector3.up * character.radius * 0.5f, transform.position + (Vector3.up * crouchRay));
#endif
        //Ͷ��һ����������ҽ�ɫվ���߶��Ͽ��Ƿ����ϰ����赲
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
    //����������ת����Hashֵʹ��
    private void GetAnimator2Hash()
    {
        stateHash = Animator.StringToHash("�����̬");
        moveSpeedHash = Animator.StringToHash("�ƶ��ٶ�");
        turnSpeedHash = Animator.StringToHash("ת���ٶ�");
        verticalVelHash = Animator.StringToHash("��ֱ�ٶ�");
        legGestureHash = Animator.StringToHash("���ҽ�");
        leftFootHash = Animator.StringToHash("���IKȨ��");
        rightFootHash = Animator.StringToHash("�ҽ�IKȨ��");
        isHightLanding = Animator.StringToHash("�Ƿ�ߴ����");
        fallHeight = Animator.StringToHash("��ظ߶�");
    }
    //�������״̬
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
            //����ɫ���ڿռ��С����վ��״̬ʱ����ɫĬ��״̬�滻Ϊ�׷�
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
    //��ȡ���ʵ���ƶ�����
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
   
    //���ö���״̬��
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
