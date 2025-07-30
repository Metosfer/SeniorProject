using System.Collections.Generic;

/// <summary>
/// Save/Load sistemi için objelerin implement etmesi gereken interface
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// Objenin save edilecek verilerini döndürür
    /// </summary>
    /// <returns>Key-Value pairs olarak save data</returns>
    Dictionary<string, object> GetSaveData();
    
    /// <summary>
    /// Save edilmiş verileri objeye yükler
    /// </summary>
    /// <param name="data">Yüklenecek veriler</param>
    void LoadSaveData(Dictionary<string, object> data);
}
