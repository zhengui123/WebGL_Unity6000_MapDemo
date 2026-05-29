using UnityEngine;

public class ChangeSceneDemo : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

   void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ChangeSceneManager.Instance.ChangeScene("EarthModelDemo");
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            ChangeSceneManager.Instance.ChangeScene("3DTilesDemo");
        }           
        else if (Input.GetKeyDown(KeyCode.E))
        {
            ChangeSceneManager.Instance.ChangeScene("CityScenes");
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            ChangeSceneManager.Instance.ChangeScene("Car");
        }
    }

}
