using UnityEngine;

/// <summary>
/// 大屏轮播类型与 <see cref="GameManager.ControlState"/> 的映射。
/// </summary>
public static class BigScreenCarouselScreenMap
{
  public const int ScreenCount = 4;

  public static string GetDisplayName(BigScreenCarouselType type)
  {
    switch (type)
    {
      case BigScreenCarouselType.Comprehensive:
        return "综合态势";
      case BigScreenCarouselType.Regional:
        return "区域态势";
      case BigScreenCarouselType.Vehicle:
        return "车辆态势";
      case BigScreenCarouselType.Part:
        return "部件态势";
      default:
        return type.ToString();
    }
  }

  public static GameManager.ControlState ToControlState(BigScreenCarouselType type)
  {
    switch (type)
    {
      case BigScreenCarouselType.Comprehensive:
        return GameManager.ControlState.CountryLevel;
      case BigScreenCarouselType.Regional:
        return GameManager.ControlState.ProvinceLevel;
      case BigScreenCarouselType.Vehicle:
        return GameManager.ControlState.VehicleLevel;
      case BigScreenCarouselType.Part:
        return GameManager.ControlState.PartLevel;
      default:
        return GameManager.ControlState.CountryLevel;
    }
  }

  public static BigScreenCarouselType FromControlState(GameManager.ControlState state)
  {
    switch (state)
    {
      case GameManager.ControlState.EarthLevel:
      case GameManager.ControlState.CountryLevel:
        return BigScreenCarouselType.Comprehensive;
      case GameManager.ControlState.ProvinceLevel:
        return BigScreenCarouselType.Regional;
      case GameManager.ControlState.VehicleLevel:
        return BigScreenCarouselType.Vehicle;
      case GameManager.ControlState.PartLevel:
      case GameManager.ControlState.AttackPathLevel:
        return BigScreenCarouselType.Part;
      default:
        return BigScreenCarouselType.Comprehensive;
    }
  }

  public static BigScreenCarouselType GetNext(BigScreenCarouselType current)
  {
    int nextIndex = ((int)current + 1) % ScreenCount;
    return (BigScreenCarouselType)nextIndex;
  }
}
