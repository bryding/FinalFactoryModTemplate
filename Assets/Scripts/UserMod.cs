using FFCore.Modding;

public class UserMod : IUserMod
{
  public string Name => "Random Fleet Movement";
  public string Description => "A mod that teleports your ships around you as you move.";
  public string Author => "Ben";

  public ModVersion Version => new()
  {
    Major = 1,
    Minor = 0,
    Patch = 0
  };
}