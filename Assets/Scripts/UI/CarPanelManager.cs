using UnityEngine;

public class CarPanelManager : UnitySingle<CarPanelManager>
{

    public GameObject CarPanel;

    public GridLine gridLine;



    public void Awake()
    {
        CarPanel.SetActive(false);
        gridLine.enabled = false;
    }



    public void Update()
    {

        if (Input.GetKeyDown(KeyCode.Z))
        {
            OpenCarPanel();
        }



        if (Input.GetKeyDown(KeyCode.X))
        {
            CloseCarPanel();
        }
    }



    public void OpenCarPanel()
    {

        CarPanel.SetActive(true);
        gridLine.enabled = true;
    }



    public void CloseCarPanel()
    {

        if (gridLine == null || !gridLine.enabled)
        {
            SetPanelInactive();
            return;
        }


        gridLine.PlayReverseAnimation(gridLine.m_EndUI, OnGridLineReverseCompleted);
    }



    private void OnGridLineReverseCompleted()
    {
        SetPanelInactive();
    }



    private void SetPanelInactive()
    {

        CarPanel.SetActive(false);
        if (gridLine != null)
        {
            gridLine.enabled = false;
        }
    }
}

