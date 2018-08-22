/**
 * Copyright (c) 2018 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */


﻿using UnityEditor;
using UnityEngine;
using static Map.Autoware.VectorMapUtility;

[CustomEditor(typeof(MapLaneSegmentBuilder)), CanEditMultipleObjects]
public class MapLaneSegmentBuilderEditor : MapSegmentBuilderEditor
{
    private Color rayColor = Color.cyan;
    private Color segmentPointColor = Color.cyan;
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
    }

    protected override void OnSceneGUI()
    {
        base.OnSceneGUI();

        MapSegmentBuilder vmSegBuilder = (MapSegmentBuilder)target;
        Undo.RecordObject(vmSegBuilder, "Segment points change");

        var localPositions = vmSegBuilder.segment.targetLocalPositions;

        var pointCount = localPositions.Count;

        if (pointCount < 2)
        {
            return;
        }

        Transform mainTrans = vmSegBuilder.transform;

        for (int i = 0; i < pointCount - 1; i++)
        {
            Handles.color = segmentPointColor;
            Handles.DrawWireDisc(mainTrans.TransformPoint(localPositions[i]), Vector3.up, VectorMapTool.PROXIMITY * 0.5f);
            Handles.color = rayColor;
            Handles.DrawLine(mainTrans.TransformPoint(localPositions[i]), mainTrans.TransformPoint(localPositions[i + 1]));
            Map.Draw.DrawArrowForDebug(mainTrans.TransformPoint(localPositions[i]), mainTrans.TransformPoint(localPositions[i + 1]), rayColor, VectorMapTool.ARROWSIZE);
        }

        Handles.color = segmentPointColor;
        Handles.DrawWireDisc(mainTrans.TransformPoint(localPositions[pointCount - 1]), Vector3.up, VectorMapTool.PROXIMITY * 0.5f);
    }
}