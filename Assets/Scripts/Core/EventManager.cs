using UnityEngine;
using System;


public class EventManager : UnitySingle<EventManager>
{
    public event Action<string> OnPlateMapDisplayFocus;
    public void TriggerPlateMapDisplayFocus(string plateMapName)
    {
        OnPlateMapDisplayFocus?.Invoke(plateMapName);
    }
   
   
}
