using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlantManager : MonoBehaviour
{
    private float playerDistance;
    public float range = 10f;
    public Transform playerPos;

    public string plantName;

    [Header("Ground Stick Settings")]
    [Tooltip("Yere temas ettirilecek referans nokta (alt uç).")] 
    public Transform groundCheck;


    public TMPro.TextMeshProUGUI text;

    void Start()
    {
      text.text = plantName;

        if (playerDistance <= range)
        {
            text.enabled = false;
        }
        else
        {
            text.enabled = true;
        }


        if (playerPos == null)
        {
            playerPos = GameObject.FindGameObjectWithTag("Player").transform;
        }

    }

    // Update is called once per frame
private void LateUpdate() 
{
    playerDistance = Vector3.Distance(transform.position, playerPos.position);
    if (playerDistance <= range)
    {
        text.enabled = true;
        
        // Text'i oyuncuya doğru çevir (ters durmaması için yön tersine çevrildi)
        Vector3 directionToPlayer = (text.transform.position - playerPos.position).normalized;
        directionToPlayer.y = 0; // Y ekseninde dönmeyi engelle (sadece yatay düzlemde)
        
        if (directionToPlayer != Vector3.zero)
        {
            text.transform.rotation = Quaternion.LookRotation(directionToPlayer);
        }
    }
    else
    {
        text.enabled = false;
    }
}
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }

}
