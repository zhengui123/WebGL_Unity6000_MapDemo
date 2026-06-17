using UnityEngine;
[System.Serializable]
public class POIData 
{
    public POIType type;
    public GameObject obj;
    public double x;
    public double y;
    public Vector3 localPosition;

    public POIData(POIType type, GameObject obj, double x, double y, Vector3 localPosition)
    {
        this.type = type;
        this.obj = obj;
        this.x = x;     
        this.y = y;
        this.localPosition = localPosition;
    }
}

public enum POIType
{
    country_Rad,
    provinece_Rad,
    yellow,
}
