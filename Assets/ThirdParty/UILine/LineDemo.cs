using UnityEngine;

public class LineDemo : MonoBehaviour
{
    public GridLine gridLine;

    [SerializeField] private string[] _start3DObjectNames;
    [SerializeField] private int _currentIndex;

    private void Update()
    {
        if (_start3DObjectNames == null || _start3DObjectNames.Length == 0)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            string targetName = _start3DObjectNames[_currentIndex % _start3DObjectNames.Length];
            gridLine.PlayDrawAnimation(targetName);
            _currentIndex = (_currentIndex + 1) % _start3DObjectNames.Length;
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            string activeName = gridLine.ActiveStart3DName;
            if (!string.IsNullOrEmpty(activeName))
            {
                gridLine.PlayReverseAnimation(activeName);
            }
        }
    }
}
