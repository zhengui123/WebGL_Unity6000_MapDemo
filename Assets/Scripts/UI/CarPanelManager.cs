using UnityEngine;

public class CarPanelManager : UnitySingle<CarPanelManager>
{
    public GameObject CarPanel;
    public GridLine gridLine;

    public void OpenCarPanel()
    {
        CarPanel.SetActive(true);
        gridLine.gameObject.SetActive(true);
    }

    public void CloseCarPanel()
    {
        CarPanel.SetActive(false);
        gridLine.gameObject.SetActive(false);
    }
}
