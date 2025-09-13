using UnityEngine;

// Global modal guard: when any of our panels (Book/Flask/Crusher) is open,
// world interactions should be blocked until closed.
public static class ModalPanelManager
{
    private static int s_openCount = 0;
    public static bool IsAnyOpen => s_openCount > 0;

    public static void Open()
    {
        s_openCount = Mathf.Max(0, s_openCount + 1);
    }

    public static void Close()
    {
        s_openCount = Mathf.Max(0, s_openCount - 1);
    }

    public static void ResetAll()
    {
        s_openCount = 0;
    }

    // Optional helper: query modal without counting Inventory panel if someone uses it
    public static bool IsAnyOpenExceptInventory()
    {
        // Our current system doesn't count Inventory, because we never call Open/Close for it.
        // This method exists for readability and future-proofing.
        return IsAnyOpen;
    }
}
