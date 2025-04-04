using System;
using FFCore.Modding;
using FFCore.Version;

public class UserMod : IUserMod
{
  public string ID => "RandomMovementExample";
  public string FullName => "Random Fleet Movement";
  public string Description => "Example Mod for Mod Authors: Mod that randomly teleports your ships around you as you move.";
  public string Author => "Ben";
  public string EmailContact => "mod@nevergames.com";
  public string Website => "http://nevergames.com";
  public string[] Dependencies => Array.Empty<string>();

  public FFVersion ModVersion => new(1, 0, 20, 0);
}