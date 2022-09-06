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
            Debug.Log("低位通过" + firstHit.normal);
            if(Vector3.Angle (playerTransform.forward,-climbHitNormal) > 45f)
             {
                return PlayerEnvironmentalMonitoring.Jump;
            }//低位再次检测
            Debug.DrawLine(playerTransform.position + Vector3.up * k_Half, playerTransform.position + Vector3.up * k_Half + -climbHitNormal,Color.red);
            if(Physics.Raycast(playerTransform.position +Vector3.up *  k_Half,-climbHitNormal,out RaycastHit lowHit, cheekDistance))
            {   //往上继续检测一个身位
                Debug.Log("腿部身位检测通过");
                if (Physics.Raycast(playerTransform.position + Vector3.up * (bodySize + k_Half), -climbHitNormal, out RaycastHit firstBodyHit, cheekDistance))
                {
                    Debug.Log("第一身位通过");
                    //往上检测两个身位 通过后进入攀爬状态
                    if (Physics.Raycast(playerTransform.position + Vector3.up * (bodySize * 2 + k_Half), -climbHitNormal, out RaycastHit climbHit, cheekDistance))
                    {
                        Debug.Log("第二身位通过");
                        return PlayerEnvironmentalMonitoring.Climbing;
                    }
                    else 
                    {
                        Debug.Log("低位攀爬状态");
                        return PlayerEnvironmentalMonitoring.LowThrow;
                    }
                }else if(Physics.Raycast(lowHit.point + Vector3.up * bodySize + Vector3.forward * wallWide,Vector3.down,out RaycastHit throwHit, cheekDistance))
                {
                    Debug.Log("低位翻越通过");
                    return PlayerEnvironmentalMonitoring.ThrowOver;
                }
                else
                {
                    Debug.Log("低位翻越失败，返回低位攀爬");
                    return PlayerEnvironmentalMonitoring.LowThrow;
                }

               
            }
        }
        return PlayerEnvironmentalMonitoring.Jump;
    }
}
