using FFCore.Extensions;
using FFCore.Modding;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Examples.RepairBeacon
{
  /// <summary>
  ///   Example of a mod UI panel, built in code so it needs no prefab: while a building with a
  ///   <see cref="RepairBeacon" /> is selected, a button at the bottom left switches it on or off.
  ///   <para>
  ///     UI code only READS the world. A click sends a player action (<see cref="RepairBeaconActions" />);
  ///     the button then shows the beacon's state as it changes on the next heartbeat, so another
  ///     player's click shows here too. Never write simulation components from a MonoBehaviour: in
  ///     multiplayer that change would exist on your machine only.
  ///   </para>
  /// </summary>
  public class RepairBeaconPanel : MonoBehaviour
  {
    private GameObject panel;
    private Text label;
    private Entity shown = Entity.Null;

    /// <summary>Builds the panel under the game's in-game canvas (called from OnGameStart).</summary>
    public static RepairBeaconPanel Create(Canvas canvas)
    {
      var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

      var root = new GameObject("RepairBeaconPanel", typeof(RectTransform), typeof(Image));
      root.transform.SetParent(canvas.transform, false);
      var rect = (RectTransform)root.transform;
      rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
      rect.anchoredPosition = new Vector2(20f, 160f);
      rect.sizeDelta = new Vector2(240f, 44f);
      root.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.9f);

      var buttonObject = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
      buttonObject.transform.SetParent(root.transform, false);
      var buttonRect = (RectTransform)buttonObject.transform;
      buttonRect.anchorMin = Vector2.zero;
      buttonRect.anchorMax = Vector2.one;
      buttonRect.offsetMin = new Vector2(4f, 4f);
      buttonRect.offsetMax = new Vector2(-4f, -4f);
      buttonObject.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.5f, 1f);

      var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
      textObject.transform.SetParent(buttonObject.transform, false);
      var textRect = (RectTransform)textObject.transform;
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = textRect.offsetMax = Vector2.zero;
      var text = textObject.GetComponent<Text>();
      text.font = font;
      text.alignment = TextAnchor.MiddleCenter;
      text.color = Color.white;

      var controller = root.AddComponent<RepairBeaconPanel>();
      controller.panel = root;
      controller.label = text;
      buttonObject.GetComponent<Button>().onClick.AddListener(controller.OnClick);
      root.SetActive(false);
      return controller;
    }

    // The panel object itself is hidden while nothing is selected, so its own Update would stop; a small
    // always-active driver object runs this instead.
    public void Refresh()
    {
      var selected = SpaghettiApi.Instance != null ? SpaghettiApi.Instance.SelectedEntity : Entity.Null;
      if (selected == Entity.Null || !Ecs.TryGetComponent(selected, out RepairBeacon beacon))
      {
        shown = Entity.Null;
        panel.SetActive(false);
        return;
      }

      shown = selected;
      label.text = beacon.Enabled ? "Repair beacon: ON" : "Repair beacon: OFF";
      panel.SetActive(true);
    }

    private void OnClick()
    {
      if (shown != Entity.Null && Ecs.TryGetComponent(shown, out RepairBeacon beacon))
      {
        RepairBeaconActions.RequestEnabled(shown, !beacon.Enabled);
      }
    }
  }

  /// <summary>Calls <see cref="RepairBeaconPanel.Refresh" /> every frame, from an object that is never hidden.</summary>
  public class RepairBeaconPanelDriver : MonoBehaviour
  {
    public RepairBeaconPanel Panel;

    private void Update()
    {
      if (Panel != null)
      {
        Panel.Refresh();
      }
    }
  }
}
