using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridLine : MonoBehaviour
{

	public static bool isShowGridLine = true;
	public Material lineMaterial;
    public Transform start3D;
    public Transform endUI;

    private void Awake()
	{
		if (!lineMaterial)
		{
			//lineMaterial = new Material(Shader.Find("Particles/Alpha Blended"));
			//lineMaterial.hideFlags = HideFlags.HideAndDontSave;
			//lineMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
		}
    }
	void OnPostRender()
	{
		if (isShowGridLine)
		{
			GL.PushMatrix();
            lineMaterial.SetPass(0);
            //GL.Color(Color.green);
			//如果报错的话，将这句话取消注释后，再试试
			//            GL.LoadPixelMatrix ();
			GL.LoadOrtho();
			GL.Begin(GL.LINES);

            Vector2 pos = Camera.main.WorldToViewportPoint(start3D.position);
            GL.Vertex(pos);
            GL.Vertex(GetCenterPos(pos, Camera.main.ScreenToViewportPoint(endUI.position)));

            GL.Vertex(GetCenterPos(pos, Camera.main.ScreenToViewportPoint(endUI.position)));
            GL.Vertex(Camera.main.ScreenToViewportPoint(endUI.position));

            GL.End();
			GL.PopMatrix();
		}

	}
	void Update()
	{
		if (Input.GetMouseButtonDown(1))
		{
			isShowGridLine = true;
		}
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			isShowGridLine = false;
		}

	}

    Vector2 centerPos;
    Vector2 GetCenterPos(Vector2 _start, Vector3 _end)
    {
        centerPos.x = _start.x + (_end.x - _start.x) * 0.6f;
        centerPos.y = _end.y;
        return centerPos;
    }

}