using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class DoorManager : MonoBehaviour
{
    public TMP_Text doorText;
    public float range = 5f;
    public Transform playerTransform;
    // Start is called before the first frame update
    void Start()
    {
        doorText.fontSize = 0f;
        if (playerTransform == null)
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        }
    }
private void FixedUpdate() {
    CheckTextDistance();
}
    // Update is called once per frame
    void Update()
    {

        
        // T tuşuna basıldığında kontrol et
        if (Input.GetKeyDown(KeyCode.T))
        {
            CheckPlayerDistance();
        }
    }
    void CheckTextDistance()
    {
    float distance = Vector3.Distance(playerTransform.position, transform.position);
    if (distance <= range)
    {
        doorText.fontSize = 36f;
    }
    else
    {
        doorText.fontSize = 0f;
    }
   }
    void CheckPlayerDistance()
    {
        // Oyuncu ile kapı arasındaki mesafeyi hesapla
        float distance = Vector3.Distance(playerTransform.position, transform.position);
        

        if (distance <= range)
        {
            
            Vector3 tempRot = new Vector3(0, -90, 0f);
            
            transform.rotation = Quaternion.Euler(tempRot);
            Debug.Log("Kapı Açıldı");
            UnityEngine.SceneManagement.SceneManager.LoadScene("ShopScene");
        }
        else
        {
            
            Debug.Log("Kapıya çok uzaksın!");
        }
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);   
    }


}