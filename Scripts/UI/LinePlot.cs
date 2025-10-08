// Code adapted from:
// https://github.com/Radishmouse22/UILineRenderer/
// Which is under MIT license.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class Palettes
{
    public static readonly List<Color> OkabeIto = new()
    {
        new Color(0.902f, 0.624f, 0.000f, 1f), // Light orange
        new Color(0.337f, 0.706f, 0.914f, 1f), // Light blue
        new Color(0.000f, 0.620f, 0.451f, 1f), // Green
        new Color(0.941f, 0.894f, 0.259f, 1f), // Yellow
        new Color(0.000f, 0.447f, 0.698f, 1f), // Dark blue
        new Color(0.835f, 0.369f, 0.000f, 1f), // Dark orange
        new Color(0.800f, 0.475f, 0.655f, 1f), // Dark pink
    };
}

[RequireComponent(typeof(CanvasRenderer))]
public class LinePlot : MaskableGraphic
{
    public List<Vector2> points = new();

    public float thickness = 0.01f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points.Count < 2)
            return;

        for (int i = 0; i < points.Count - 1; i++)
        {
            // Create a line segment between the next two points
            CreateLineSegment(points[i], points[i + 1], vh);

            int index = i * 5;

            // Add the line segment to the triangles array
            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index + 3, index + 2, index);

            // These two triangles create the beveled edges
            // between line segments using the end point of
            // the last line segment and the start points of this one
            if (i != 0)
            {
                vh.AddTriangle(index, index - 1, index - 3);
                vh.AddTriangle(index + 1, index - 1, index - 2);
            }
        }
    }

    /// <summary>
    /// Creates a rect from two points that acts as a line segment
    /// </summary>
    /// <param name="point1">The starting point of the segment</param>
    /// <param name="point2">The endint point of the segment</param>
    /// <param name="vh">The vertex helper that the segment is added to</param>
    private void CreateLineSegment(Vector3 point1, Vector3 point2, VertexHelper vh)
    {
        // Create vertex template
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        // Create the start of the segment
        Quaternion point1Rotation = Quaternion.Euler(0, 0, RotatePointTowards(point1, point2) + 90);
        vertex.position = point1Rotation * new Vector3(-thickness / 2, 0);
        vertex.position += point1;
        vh.AddVert(vertex);
        vertex.position = point1Rotation * new Vector3(thickness / 2, 0);
        vertex.position += point1;
        vh.AddVert(vertex);

        // Create the end of the segment
        Quaternion point2Rotation = Quaternion.Euler(0, 0, RotatePointTowards(point2, point1) - 90);
        vertex.position = point2Rotation * new Vector3(-thickness / 2, 0);
        vertex.position += point2;
        vh.AddVert(vertex);
        vertex.position = point2Rotation * new Vector3(thickness / 2, 0);
        vertex.position += point2;
        vh.AddVert(vertex);

        // Also add the end point
        vertex.position = point2;
        vh.AddVert(vertex);
    }

    /// <summary>
    /// Gets the angle that a vertex needs to rotate to face target vertex
    /// </summary>
    /// <param name="vertex">The vertex being rotated</param>
    /// <param name="target">The vertex to rotate towards</param>
    /// <returns>The angle required to rotate vertex towards target</returns>
    private float RotatePointTowards(Vector2 vertex, Vector2 target)
    {
        return (float)(Mathf.Atan2(target.y - vertex.y, target.x - vertex.x) * (180 / Mathf.PI));
    }

    public void SetPlotData(List<float> x, List<float> y)
    {
        for (int i = 0; (i < x.Count) && (i < y.Count); ++i)
        {
            if (i < points.Count)
                points[i] = new Vector2(x[i], y[i]);
            else
                points.Add(new Vector2(x[i], y[i]));
        }

        SetVerticesDirty();
    }
}
