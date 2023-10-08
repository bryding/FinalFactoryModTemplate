using FFCore.Config;

namespace Utils
{
  public static class ConfigUtils
  {

    public static void AddShipToAcceptedShips(ItemConfig itemConfig, int itemId, string shipName)
    {
      var dynamicConfig = itemConfig.DynamicConfig[itemId];
      var acceptedShips = dynamicConfig.AcceptedShips;
      acceptedShips.Add(itemConfig.GetIdForName(shipName));
      dynamicConfig.AcceptedShips = acceptedShips;
      itemConfig.DynamicConfig[itemId] = dynamicConfig;
    }
  }
}