using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnvironmentalMonitoring : MonoBehaviour
{
    public float k_Half = 0.5f;
    public float cheekDistance = 1f;
    public Vector3 climbHitNormal;
    public float bodySize = 1f;
    const float wallWide = 0.2f;
    public enum PlayerEnvironmentalMonitoring
    {
        Jump,
        ThrowOver,
        LowThrow,
        Climbing
    }
    private void Update()
    {
        EnvironmentCheek(transform);
    }
    public PlayerEnvironmentalMonitoring EnvironmentCheek(Transform playerTransform)
    {
        if(Physics.Raycast(playerTransform.position + Vector3.up * k_Half,playerTransform.forward,out RaycastHit firstHit, cheekDistance))
        {
            Debug.DrawLine(playerTransform.position + Vector3.up * k_Half, playerTransform.position + Vector3.up * k_Half + playerTransform.forward);
            climbHitNormal = firstHit.normal;
            Debug.Log("��λͨ��" + firstHit.normal);
            if(Vector3.Angle (playerTransform.forward,-climbHitNormal) > 45f)
             {
                return PlayerEnvironmentalMonitoring.Jump;
            }//��λ�ٴμ��
            Debug.DrawLine(playerTransform.position + Vector3.up * k_Half, playerTransform.position + Vector3.up * k_Half + -climbHitNormal,Color.red);
            if(Physics.Raycast(playerTransform.position +Vector3.up *  k_Half,-climbHitNormal,out RaycastHit lowHit, cheekDistance))
            {   //���ϼ������һ����λ
                Debug.Log("�Ȳ���λ���ͨ��");
                if (Physics.Raycast(playerTransform.position + Vector3.up * (bodySize + k_Half), -climbHitNormal, out RaycastHit firstBodyHit, cheekDistance))
                {
                    Debug.Log("��һ��λͨ��");
                    //���ϼ��������λ ͨ�����������״̬
                    if (Physics.Raycast(playerTransform.position + Vector3.up * (bodySize * 2 + k_Half), -climbHitNormal, out RaycastHit climbHit, cheekDistance))
                    {
                        Debug.Log("�ڶ���λͨ��");
                        return PlayerEnvironmentalMonitoring.Climbing;
                    }
                    else 
                    {
                        Debug.Log("��λ����״̬");
                        return PlayerEnvironmentalMonitoring.LowThrow;
                    }
                }else if(Physics.Raycast(lowHit.point + Vector3.up * bodySize + Vector3.forward * wallWide,Vector3.down,out RaycastHit throwHit, cheekDistance))
                {
                    Debug.Log("��λ��Խͨ��");
                    return PlayerEnvironmentalMonitoring.ThrowOver;
                }
                else
                {
                    Debug.Log("��λ��Խʧ�ܣ����ص�λ����");
                    return PlayerEnvironmentalMonitoring.LowThrow;
                }

               
            }
        }
        return PlayerEnvironmentalMonitoring.Jump;
    }
}
