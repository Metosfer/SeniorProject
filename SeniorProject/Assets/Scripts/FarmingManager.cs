using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FarmingManager : MonoBehaviour
{
    [Header("References")]
    public FarmingAreaManager farmingArea;

    // Example hooks for integrating with pickup system
    public void OnRakePickedUp()
    {
        HarrowManager.SetEquipped(true);
    }

    public void OnRakeDropped()
    {
        HarrowManager.SetEquipped(false);
    }

    // Optional utility to prepare all empty plots (debug/testing)
    [ContextMenu("Prepare All Empty Plots")]
    public void PrepareAll()
    {
        if (farmingArea == null) return;
        var field = typeof(FarmingAreaManager).GetMethod("PreparePlot", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var pointsField = typeof(FarmingAreaManager).GetField("plotPoints", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var points = pointsField?.GetValue(farmingArea) as System.Collections.IList;
        if (points == null) return;
        for (int i = 0; i < points.Count; i++)
        {
            field?.Invoke(farmingArea, new object[] { i });
        }
    }
}
