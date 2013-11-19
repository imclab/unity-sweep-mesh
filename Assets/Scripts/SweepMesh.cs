﻿using UnityEngine;
using System.Collections;

public class SweepMesh : MonoBehaviour
{
    #region Public propertie

    public enum Shape
    {
        Regular,
        Star
    }
    
    public int pathDivision = 100;
    public Shape profileShape = Shape.Regular;
    public int profilePoints = 4;
    public float profileScale = 1.0f;
    public float profileInner = 0.5f;
    public float noiseLevel = 0.0f;
    public float noiseFreq = 0.1f;
    public int noiseFractal = 0;

    #endregion

    #region Private objects

    Mesh mesh;
    Vector3[] vertices;

    #endregion

    #region Profile generator

    static Vector3[] CreateRegularPolygonProfile (int points)
    {
        var profile = new Vector3[points];
        for (int i = 0; i < points; i++)
        {
            var theta = Mathf.PI * 2 / points * i;
            profile [i] = new Vector3 (Mathf.Cos (theta), Mathf.Sin (theta), 0);
        }
        return profile;
    }

    static Vector3[] CreateStarProfile (int points, float innerRadius)
    {
        var profile = new Vector3[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            var theta = Mathf.PI / points * i;
            var radius = ((i & 1) == 0) ? 1.0f : innerRadius;
            profile [i] = new Vector3 (Mathf.Cos (theta), Mathf.Sin (theta), 0) * radius;
        }
        return profile;
    }

    Vector3[] CreateProfile ()
    {
        if (profileShape == Shape.Star)
        {
            return CreateStarProfile (profilePoints, profileInner);
        }
        else
        {
            return CreateRegularPolygonProfile (profilePoints);
        }
    }

    #endregion

    #region Path generator

    AnimationCurve[] CreatePathFromChildren ()
    {
        var path = new AnimationCurve[] {
            new AnimationCurve (),  // position.x
            new AnimationCurve (),  // position.y
            new AnimationCurve (),  // position.z
            new AnimationCurve (),  // up.x
            new AnimationCurve (),  // up.y
            new AnimationCurve ()   // up.z
        };

        var time = 0.0f;
        var delta = 1.0f / (transform.childCount - 1);

        foreach (Transform point in transform)
        {
            var position = point.localPosition;
            var up = point.localRotation * Vector3.up;
            path [0].AddKey (time, position.x);
            path [1].AddKey (time, position.y);
            path [2].AddKey (time, position.z);
            path [3].AddKey (time, up.x);
            path [4].AddKey (time, up.y);
            path [5].AddKey (time, up.z);
            time += delta;
        }

        return path;
    }

    #endregion

    #region Mesh generator

    void UpdateVertices (Vector3[] profile, AnimationCurve[] path)
    {
        var index = 0;
        
        for (var i = 0; i < pathDivision; i++)
        {
            var t1 = (float)(i + 0) / pathDivision;
            var t2 = (float)(i + 1) / pathDivision;
            
            var p1 = new Vector3 (path [0].Evaluate (t1), path [1].Evaluate (t1), path [2].Evaluate (t1));
            var p2 = new Vector3 (path [0].Evaluate (t2), path [1].Evaluate (t2), path [2].Evaluate (t2));
            
            var ny = new Vector3 (path [3].Evaluate (t1), path [4].Evaluate (t1), path [5].Evaluate (t1)).normalized;
            var nz = (p2 - p1).normalized;
            var nx = Vector3.Cross (ny, nz);
            
            foreach (var v in profile)
            {
                var t = profileScale;

                if (noiseLevel > 0.0f)
                {
                    t += Perlin.Fbm (p1 * noiseFreq, noiseFractal) * noiseLevel;
                }

                var p = p1 + nx * (v.x * t) + ny * (v.y * t);

                vertices [index++] = p;
                vertices [index++] = p;
            }
        }
        
        // Head cap.
        vertices [index++] = new Vector3 (path [0].Evaluate (0), path [1].Evaluate (0), path [2].Evaluate (0));

        for (var i = 0; i < profile.Length; i++)
        {
            vertices [index++] = vertices [i * 2];
        }
        
        // Tail cap.
        var et = 1.0f - 1.0f / pathDivision;
        vertices [index++] = new Vector3 (path [0].Evaluate (et), path [1].Evaluate (et), path [2].Evaluate (et));

        var ei = profile.Length * (pathDivision - 1) * 2;
        for (var i = 0; i < profile.Length; i++)
        {
            vertices [index++] = vertices [ei + i * 2];
        }
    }

    int[] CreateIndexArray (int pointsInProfile)
    {
        var array = new int[(pathDivision - 1) * pointsInProfile * 6 + pointsInProfile * 6];
        var index = 0;
        
        for (var i = 0; i < pathDivision - 1; i++)
        {
            for (var i2 = 0; i2 < pointsInProfile; i2++)
            {
                var bi1 = 2 * (pointsInProfile * i + i2) + 1;
                var bi2 = 2 * (pointsInProfile * i + (i2 + 1) % pointsInProfile);
                
                array [index++] = bi1;
                array [index++] = bi2;
                array [index++] = bi2 + pointsInProfile * 2;
                
                array [index++] = bi1;
                array [index++] = bi2 + pointsInProfile * 2;
                array [index++] = bi1 + pointsInProfile * 2;
            }
        }
        
        // Make caps with a trignale fan.
        {
            var bi1 = pointsInProfile * pathDivision * 2;
            var bi2 = bi1 + pointsInProfile + 1;
            
            for (var i = 0; i < pointsInProfile - 1; i++)
            {
                array [index++] = bi1;
                array [index++] = bi1 + i + 2;
                array [index++] = bi1 + i + 1;
                
                array [index++] = bi2;
                array [index++] = bi2 + i + 1;
                array [index++] = bi2 + i + 2;
            }
            
            array [index++] = bi1;
            array [index++] = bi1 + 1;
            array [index++] = bi1 + pointsInProfile;
            
            array [index++] = bi2;
            array [index++] = bi2 + pointsInProfile;
            array [index++] = bi2 + 1;
        }
        
        return array;
    }

    #endregion

    #region MonoBehaviour

    void Start ()
    {
        mesh = new Mesh ();
        mesh.MarkDynamic ();
        GetComponent<MeshFilter> ().sharedMesh = mesh;
    }

    void Update ()
    {
        var profile = CreateProfile ();
        var path = CreatePathFromChildren ();

        var vertexCount = profile.Length * (pathDivision * 2 + 2) + 2;
        var needReallocate = (vertices == null || vertexCount != vertices.Length);

        if (needReallocate)
        {
            vertices = new Vector3[vertexCount];
            mesh.Clear ();
        }

        UpdateVertices (profile, path);
        mesh.vertices = vertices;

        if (needReallocate)
        {
            mesh.SetIndices (CreateIndexArray (profile.Length), MeshTopology.Triangles, 0);
        }

        mesh.RecalculateNormals ();
        mesh.RecalculateBounds ();
    }

    #endregion
}
