/**
 * Copyright (c) 2018 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */


using System.Collections.Generic;
using UnityEngine;

static class Utils
{
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        var result = parent.Find(name);
        if (result != null)
        {
            return result;
        }

        foreach (Transform child in parent)
        {
            result = child.FindDeepChild(name);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    public static bool IsGenericList(this System.Type t)
    {
        return t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(List<>));
    }
}
