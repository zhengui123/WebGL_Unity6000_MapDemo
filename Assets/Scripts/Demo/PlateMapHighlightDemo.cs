using UnityEngine;

public class PlateMapHighlightDemo : MonoBehaviour
{

    public string plateName;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.C))
        {
            PlateMapHighlightController.Instance.HighlightModule(plateName);
        }
        if(Input.GetKeyDown(KeyCode.V))
        {
            PlateMapHighlightController.Instance.ClearHighlight();
        }
    }
}
