namespace Sitecore.Support.Pipelines.HttpRequest
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Sitecore.Abstractions;
  using Sitecore.Configuration;
  using Sitecore.Data.ItemResolvers;
  using Sitecore.Data.Items;
  using Sitecore.DependencyInjection;
  using Sitecore.Diagnostics;
  using Sitecore.IO;
  using Sitecore.SecurityModel;
  using Sitecore.Sites;
  using Sitecore.StringExtensions;
  using Sitecore.Web;
  using Sitecore.Pipelines.HttpRequest;
  using System.Threading;

  public class ItemResolver : Sitecore.Pipelines.HttpRequest.ItemResolver
  {
    #region Constructors
    [Obsolete("Please use another constructor with parameters")]
    public ItemResolver() : base()
    {
    }

    public ItemResolver(BaseItemManager itemManager, ItemPathResolver pathResolver) : base(itemManager, pathResolver)
    {
    }

    protected ItemResolver([NotNull] BaseItemManager itemManager, [NotNull] ItemPathResolver pathResolver, MixedItemNameResolvingMode itemNameResolvingMode) :
      base (itemManager, pathResolver, itemNameResolvingMode)
    {
    }
    #endregion

    protected override bool TryResolveItem([NotNull] string itemPath, [NotNull] HttpRequestArgs args, out Item item, out bool permissionDenied)
    {
      permissionDenied = false;

      using (new EnforceVersionPresenceDisabler())
      {
        item = this.ItemManager.GetItem(itemPath, Sitecore.Context.Language, Data.Version.Latest, Sitecore.Context.Database, SecurityCheck.Disable);
      }

      if (item == null)
      {
        return false;
      }

      if (Sitecore.Context.Site != null && Sitecore.Context.Site.SiteInfo.EnforceVersionPresence)
      {
        bool IsItemEnforceVersionPresenceEnabled;
        using (new EnforceVersionPresenceDisabler())
        {
          IsItemEnforceVersionPresenceEnabled = item != null && item.RuntimeSettings.TemporaryVersion && !item.Name.StartsWith("__") && ((BaseItem)item)[FieldIDs.EnforceVersionPresence] == "1";
        }

        if (IsItemEnforceVersionPresenceEnabled)
        {
          item = null; // was resolved without version.  
          args.CustomData.Add("sc_Support_218684_versionRestriction", true);
          return false;
        }       
      }

      if (item.Access.CanRead())
      {
        return true;
      }
      else
      {
        permissionDenied = true;
        item = null; // was resolved without security, and user cannot read it.                        
        return false;
      }
    }

    public override void Process([NotNull] HttpRequestArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      if (this.SkipItemResolving(args))
      {
        return;
      }

      bool permissionDenied = false;
      Item resolvedItem = null;

      string path = string.Empty;

      try
      {
        this.StartProfilingOperation("Resolve current item.", args);

        var uniquePaths = new HashSet<string>();
        foreach (var candidatePath in this.GetCandidatePaths(args))
        {
          if (!uniquePaths.Add(candidatePath))
          {
            continue; // Already checked this URL
          }

          if (this.TryResolveItem(candidatePath, args, out resolvedItem, out permissionDenied))
          {
            path = candidatePath; // found matching item by path, will stop search
            break;
          }

          if (permissionDenied)
          {
            return; // found item exists, but we cannot touch it due to lack of permissions.
          }

          var versionRestriction = (bool) (args.CustomData["sc_Support_218684_versionRestriction"] ?? false);

          if (versionRestriction)
          {
            return; // found item exists, but there is no version and EnforceVersionPresence is enabled.
          }
        }

        var site = Sitecore.Context.Site;

        if (resolvedItem == null || resolvedItem.Name.Equals("*"))
        {
          var displayNameItem = this.ResolveByMixedDisplayName(args, out permissionDenied);
          if (displayNameItem != null)
          {
            resolvedItem = displayNameItem;
          }
        }

        if (resolvedItem == null && site != null && !permissionDenied && this.UseSiteStartPath(args))
        {
          if (this.TryResolveItem(site.StartPath, args, out resolvedItem, out permissionDenied))
          {
            path = site.StartPath;
          }
        }
      }
      finally
      {
        if (resolvedItem != null)
        {
          this.TraceInfo("Current item is {0}.".FormatWith(path));
        }

        args.PermissionDenied = permissionDenied;
        Sitecore.Context.Item = resolvedItem;

        this.EndProfilingOperation(status: null, args: args);
      }
    }
  }
}