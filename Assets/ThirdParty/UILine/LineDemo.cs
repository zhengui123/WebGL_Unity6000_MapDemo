using UnityEngine;

public class LineDemo : MonoBehaviour
{
    public GridLine gridLine;

    public Transform endUI;
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
        if (Input.GetKeyDown(KeyCode.Z))
        {
            gridLine.PlayDrawAnimation(gridLine.m_EndUI);
        }



        if (Input.GetKeyDown(KeyCode.X))
        {
           gridLine.PlayReverseAnimation(gridLine.m_EndUI);

        }
    }
}
