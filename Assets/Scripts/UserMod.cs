using System;
using FFCore.Modding;
using FFCore.Serialization;

public class UserMod : IUserMod
{
  public string ShortName => "RandomMovementExample";
  public string FullName => "Random Fleet Movement";
  public string Description => "A mod that teleports your ships around you as you move.";
  public string Author => "Ben";
  public string EmailContact => "mod@nevergames.com";
  public string Website => "http://nevergames.com";
  public string[] Dependencies => Array.Empty<string>();

  public FFVersion ModVersion => new()
  {
    Major = 1,
    Minor = 0,
    Patch = 0,
    Rc = 0
  };
}