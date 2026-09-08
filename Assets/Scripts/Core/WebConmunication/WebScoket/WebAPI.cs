using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WebAPI : UnitySingle<WebAPI>
{


    public void GoNext()
    {
        LogManager.LogHost("GoNext");
    }
    
    public void GoLeft()
    {
        LogManager.LogHost("GoLeft");
    }
}
