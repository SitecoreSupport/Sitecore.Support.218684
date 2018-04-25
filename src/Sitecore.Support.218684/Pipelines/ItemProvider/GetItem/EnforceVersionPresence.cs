namespace Sitecore.Support.Pipelines.ItemProvider.GetItem
{
  using Sitecore.Data.Items;
  using Sitecore.Pipelines.ItemProvider.GetItem;
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Web;
  public class EnforceVersionPresence : Sitecore.Pipelines.ItemProvider.GetItem.EnforceVersionPresence
  {
    public override void Process(GetItemArgs args)
    {
      if (!this.IsEnforceVersionPresenceEnabled())
      {
        return;
      }

      var item = this.GetItem(args);

      if (item != null)
        Sitecore.Context.Items["sc_Support_218684_ItemResolved"] = true;

      args.Result = this.IsItemEnforceVersionPresenceEnabled(item) ? null : item;
    }
    private Item GetItem(GetItemArgs args)
    {
      if (args == null)
      {
        return null;
      }

      return args.Result != null || args.Handled ? args.Result : ((object)args.ItemId != null
           ? args.FallbackProvider.GetItem(args.ItemId, args.Language, args.Version, args.Database, args.SecurityCheck)
           : args.FallbackProvider.GetItem(args.ItemPath, args.Language, args.Version, args.Database, args.SecurityCheck));
    }
  }
}